using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Core;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    /// <summary>
    /// Detecta interferencias entre hosts (pisos, quadros estruturais) e cortadores
    /// (colunas/pilares estruturais) e aplica corte automatico usando
    /// <see cref="JoinGeometryUtils"/> ou <see cref="SolidSolidCutUtils"/>,
    /// escolhendo a estrategia que Revit aceitar para cada par.
    /// </summary>
    /// <remarks>
    /// Origem: trabalho do Victor (snapshot 2026-04-14).
    /// Adaptado para ADR-003: servico "mudo" (zero UI), retorna <see cref="Result{T}"/>,
    /// chamador gerencia transacao externa e monta UX. Ver PLAYBOOK-adr-003-migration.md.
    /// </remarks>
    internal sealed class CortarElementosService
    {
        private const double MinOverlapFt = 5.0 * RevitUtils.FT_PER_MM;
        private const double MinIntersectionVolumeFt3 = 1e-8;
        private const double SearchToleranceFt = 50.0 * RevitUtils.FT_PER_MM;

        private static readonly IReadOnlyList<BuiltInCategory> HostCategories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFraming
        };

        private static readonly IReadOnlyList<BuiltInCategory> CutterCategories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Columns
        };

        private enum StatusTentativa
        {
            NaoAplicavel,
            Falhou,
            JaConforme,
            Aplicado
        }

        private sealed class ResultadoTentativa
        {
            internal ResultadoTentativa(StatusTentativa status, List<string> diagnostico = null)
            {
                Status = status;
                Diagnostico = diagnostico ?? new List<string>();
            }

            internal StatusTentativa Status { get; }
            internal List<string> Diagnostico { get; }
        }

        private sealed class ResultadoPar
        {
            internal ResultadoPar(bool aplicado, bool jaConforme, string estrategia, List<string> diagnostico)
            {
                Aplicado = aplicado;
                JaConforme = jaConforme;
                Estrategia = estrategia;
                Diagnostico = diagnostico ?? new List<string>();
            }

            internal bool Aplicado { get; }
            internal bool JaConforme { get; }
            internal bool Sucesso => Aplicado || JaConforme;
            internal string Estrategia { get; }
            internal List<string> Diagnostico { get; }
        }

        private sealed class ParCorte
        {
            internal ParCorte(Element host, Element cutter)
            {
                Host = host;
                Cutter = cutter;
            }

            internal Element Host { get; }
            internal Element Cutter { get; }
        }

        /// <summary>
        /// Executa a deteccao e corte automatico sobre o escopo fornecido.
        /// DEVE ser chamado dentro de uma <see cref="Transaction"/> ativa — o servico
        /// usa <see cref="SubTransaction"/> internamente para isolar cada tentativa.
        /// </summary>
        /// <param name="doc">Documento Revit ativo.</param>
        /// <param name="elementosEscopo">
        /// Elementos ja validados (apenas host/cutter categories). O servico filtra novamente
        /// por defesa em profundidade.
        /// </param>
        /// <returns>
        /// <see cref="Result{T}.Ok"/> sempre que haja um desfecho processavel, mesmo que
        /// zero cortes sejam aplicados (ver <see cref="CortarElementosResultado.HouveAlteracao"/>).
        /// <see cref="Result{T}.Fail"/> apenas em falhas de entrada (doc nulo, escopo vazio).
        /// </returns>
        internal Result<CortarElementosResultado> Executar(Document doc, IReadOnlyList<Element> elementosEscopo)
        {
            if (doc == null)
            {
                Logger.Warn("[CortarElementos] Documento nulo recebido pelo servico");
                return Result<CortarElementosResultado>.Fail("Documento Revit indisponivel.");
            }

            List<string> diagnostico = new List<string>();

            List<Element> selecionados = (elementosEscopo ?? new List<Element>())
                .Where(EhElementoValidoParaEscopo)
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();

            if (selecionados.Count == 0)
            {
                Logger.Warn("[CortarElementos] Nenhum elemento valido no escopo fornecido");
                return Result<CortarElementosResultado>.Fail(
                    "Nenhum piso, elemento de quadro estrutural, coluna ou pilar estrutural foi fornecido.");
            }

            List<Element> hostsSelecionados = selecionados.Where(EhHostValido).ToList();
            List<Element> cuttersSelecionados = selecionados.Where(EhCortadorValido).ToList();

            if (hostsSelecionados.Count == 0 && cuttersSelecionados.Count == 0)
            {
                diagnostico.Add("Selecao nao contem nenhum host (piso/quadro) nem cortador (coluna/pilar).");
                return Result<CortarElementosResultado>.Ok(new CortarElementosResultado(
                    selecionados.Count, 0, 0, 0, 0, 0, 0, new List<long>(), diagnostico));
            }

            List<ParCorte> pares = DescobrirPares(doc, hostsSelecionados, cuttersSelecionados, diagnostico);
            List<long> elementosRelacionados = pares
                .SelectMany(p => new[] { p.Host.Id.Value, p.Cutter.Id.Value })
                .Distinct()
                .ToList();

            if (pares.Count == 0)
            {
                diagnostico.Add("Nenhuma interferencia valida encontrada entre pisos/quadro estrutural e colunas/pilares.");
                Logger.Info("[CortarElementos] Nenhum par interferente — {Total} elementos analisados", selecionados.Count);
                return Result<CortarElementosResultado>.Ok(new CortarElementosResultado(
                    selecionados.Count,
                    hostsSelecionados.Count,
                    cuttersSelecionados.Count,
                    0, 0, 0, 0,
                    elementosRelacionados,
                    diagnostico));
            }

            int alteracoesAplicadas = 0;
            int jaConformes = 0;
            int falhas = 0;

            foreach (ParCorte par in pares.OrderBy(p => p.Host.Id.Value).ThenBy(p => p.Cutter.Id.Value))
            {
                ResultadoPar resultadoPar = ProcessarPar(doc, par.Host, par.Cutter);
                string descricaoPar = $"{DescreverElemento(par.Host)} <- {DescreverElemento(par.Cutter)}";

                if (resultadoPar.Sucesso)
                {
                    if (resultadoPar.Aplicado) alteracoesAplicadas++;
                    else if (resultadoPar.JaConforme) jaConformes++;

                    string prefixo = resultadoPar.Aplicado ? "aplicado" : "ja conforme";
                    diagnostico.Add($"{descricaoPar} -> {prefixo} por {resultadoPar.Estrategia}.");
                    diagnostico.AddRange(
                        resultadoPar.Diagnostico
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Take(4)
                            .Select(x => $"  {x}"));
                    continue;
                }

                falhas++;
                diagnostico.Add($"{descricaoPar} -> falha ao cortar.");
                diagnostico.AddRange(
                    resultadoPar.Diagnostico
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Take(6)
                        .Select(x => $"  {x}"));
            }

            Logger.Info(
                "[CortarElementos] {Total} selecionados, {Pares} pares interferentes, {Aplic} aplicados, {Conf} ja conformes, {Fal} falhas",
                selecionados.Count, pares.Count, alteracoesAplicadas, jaConformes, falhas);

            return Result<CortarElementosResultado>.Ok(new CortarElementosResultado(
                selecionados.Count,
                pares.Select(p => p.Host.Id.Value).Distinct().Count(),
                pares.Select(p => p.Cutter.Id.Value).Distinct().Count(),
                pares.Count,
                alteracoesAplicadas,
                jaConformes,
                falhas,
                elementosRelacionados,
                diagnostico));
        }

        private List<ParCorte> DescobrirPares(
            Document doc,
            List<Element> hostsSelecionados,
            List<Element> cuttersSelecionados,
            List<string> diagnostico)
        {
            List<ParCorte> pares = new List<ParCorte>();
            HashSet<string> chaves = new HashSet<string>();

            if (hostsSelecionados.Count > 0 && cuttersSelecionados.Count > 0)
            {
                foreach (Element host in hostsSelecionados)
                    foreach (Element cortador in cuttersSelecionados)
                        TentarAdicionarPar(host, cortador, pares, chaves);

                diagnostico.Add("Modo: hosts e cortadores ambos selecionados; todos os pares foram enviados para tentativa.");
                return pares;
            }

            if (hostsSelecionados.Count > 0)
            {
                foreach (Element host in hostsSelecionados)
                {
                    List<Element> cortadores = EncontrarElementosIntersectando(doc, host, CutterCategories);
                    foreach (Element cortador in cortadores)
                        TentarAdicionarPar(host, cortador, pares, chaves);
                }

                diagnostico.Add("Modo: hosts selecionados, colunas/pilares buscados automaticamente.");
                return pares;
            }

            foreach (Element cortador in cuttersSelecionados)
            {
                List<Element> hosts = EncontrarElementosIntersectando(doc, cortador, HostCategories);
                foreach (Element host in hosts)
                    TentarAdicionarPar(host, cortador, pares, chaves);
            }

            diagnostico.Add("Modo: colunas/pilares selecionados, hosts buscados automaticamente.");
            return pares;
        }

        private void TentarAdicionarPar(Element host, Element cutter, List<ParCorte> pares, HashSet<string> chaves)
        {
            if (host == null || cutter == null || host.Id == cutter.Id)
                return;

            string chave = $"{host.Id.Value}:{cutter.Id.Value}";
            if (!chaves.Add(chave))
                return;

            pares.Add(new ParCorte(host, cutter));
        }

        private List<Element> EncontrarElementosIntersectando(
            Document doc,
            Element origem,
            IReadOnlyList<BuiltInCategory> categorias)
        {
            List<Element> encontrados = new List<Element>();

            BoundingBoxXYZ bbox = null;
            try { bbox = origem?.get_BoundingBox(null); } catch { }

            if (origem == null || bbox == null || categorias == null || categorias.Count == 0)
                return encontrados;

            XYZ min = new XYZ(
                bbox.Min.X - SearchToleranceFt,
                bbox.Min.Y - SearchToleranceFt,
                bbox.Min.Z - SearchToleranceFt);
            XYZ max = new XYZ(
                bbox.Max.X + SearchToleranceFt,
                bbox.Max.Y + SearchToleranceFt,
                bbox.Max.Z + SearchToleranceFt);

            Outline outline = new Outline(min, max);
            BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);
            ElementMulticategoryFilter categoriasFilter = new ElementMulticategoryFilter(categorias.ToList());

            IEnumerable<Element> candidatos = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(categoriasFilter)
                .WherePasses(bboxFilter)
                .Where(e => e != null && e.Id != origem.Id && EhElementoValidoParaEscopo(e));

            foreach (Element candidato in candidatos)
            {
                if (TemInterferenciaRelevante(doc, origem, candidato))
                    encontrados.Add(candidato);
            }

            return encontrados
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();
        }

        private bool TemInterferenciaRelevante(Document doc, Element primeiro, Element segundo)
        {
            BoundingBoxXYZ bb1 = null;
            BoundingBoxXYZ bb2 = null;

            try { bb1 = primeiro?.get_BoundingBox(null); } catch { }
            try { bb2 = segundo?.get_BoundingBox(null); } catch { }

            if (bb1 == null || bb2 == null || !BoundingBoxesOverlapSignificantly(bb1, bb2))
                return false;

            try
            {
                bool? intersecaoSolida = TemIntersecaoDeSolidos(primeiro, segundo);
                if (intersecaoSolida.HasValue)
                    return intersecaoSolida.Value;
            }
            catch (Exception ex)
            {
                Logger.Debug("[CortarElementos] TemIntersecaoDeSolidos falhou: {Msg}", ex.Message);
            }

            try
            {
                if (ElementIntersectsFilter.IsElementSupported(primeiro) &&
                    ElementIntersectsFilter.IsElementSupported(segundo))
                {
                    ElementIntersectsElementFilter filter = new ElementIntersectsElementFilter(primeiro);
                    return filter.PassesFilter(doc, segundo.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("[CortarElementos] ElementIntersectsFilter falhou: {Msg}", ex.Message);
            }

            return true;
        }

        private bool? TemIntersecaoDeSolidos(Element primeiro, Element segundo)
        {
            List<Solid> solidsPrimeiro = ExtrairSolidos(primeiro).ToList();
            List<Solid> solidsSegundo = ExtrairSolidos(segundo).ToList();

            if (solidsPrimeiro.Count == 0 || solidsSegundo.Count == 0)
                return null;

            foreach (Solid solidPrimeiro in solidsPrimeiro)
            {
                foreach (Solid solidSegundo in solidsSegundo)
                {
                    try
                    {
                        Solid intersecao = BooleanOperationsUtils.ExecuteBooleanOperation(
                            solidPrimeiro,
                            solidSegundo,
                            BooleanOperationsType.Intersect);

                        if (intersecao != null && intersecao.Volume > MinIntersectionVolumeFt3)
                            return true;
                    }
                    catch
                    {
                        // Alguns solids quebram em booleanas — nao e erro fatal, so passa adiante
                    }
                }
            }

            return false;
        }

        private IEnumerable<Solid> ExtrairSolidos(Element elemento)
        {
            if (elemento == null)
                yield break;

            GeometryElement geometria = null;
            try
            {
                geometria = elemento.get_Geometry(new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    IncludeNonVisibleObjects = true
                });
            }
            catch { }

            if (geometria == null)
                yield break;

            foreach (Solid solid in ExtrairSolidos(geometria))
                yield return solid;
        }

        private IEnumerable<Solid> ExtrairSolidos(GeometryElement geometria)
        {
            if (geometria == null)
                yield break;

            foreach (GeometryObject obj in geometria)
            {
                if (obj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                {
                    yield return solid;
                    continue;
                }

                if (obj is GeometryInstance instancia)
                {
                    GeometryElement geometriaInstancia = null;
                    try { geometriaInstancia = instancia.GetInstanceGeometry(); }
                    catch { }

                    if (geometriaInstancia == null)
                        continue;

                    foreach (Solid solidInstancia in ExtrairSolidos(geometriaInstancia))
                        yield return solidInstancia;
                }
            }
        }

        private bool BoundingBoxesOverlapSignificantly(BoundingBoxXYZ bb1, BoundingBoxXYZ bb2)
        {
            double ox = OverlapOnAxis(bb1.Min.X, bb1.Max.X, bb2.Min.X, bb2.Max.X);
            if (ox < MinOverlapFt) return false;

            double oy = OverlapOnAxis(bb1.Min.Y, bb1.Max.Y, bb2.Min.Y, bb2.Max.Y);
            if (oy < MinOverlapFt) return false;

            double oz = OverlapOnAxis(bb1.Min.Z, bb1.Max.Z, bb2.Min.Z, bb2.Max.Z);
            if (oz < MinOverlapFt) return false;

            return true;
        }

        private double OverlapOnAxis(double min1, double max1, double min2, double max2)
        {
            double overlapStart = min1 > min2 ? min1 : min2;
            double overlapEnd = max1 < max2 ? max1 : max2;
            return overlapEnd - overlapStart;
        }

        private ResultadoPar ProcessarPar(Document doc, Element host, Element cutter)
        {
            List<string> acumulado = new List<string>();

            ResultadoPar resultado;
            if (ExecutarTentativaEmSubTransaction(
                doc,
                "join geometry",
                d => TentarJoinGeometryComoCorte(doc, host, cutter, d),
                acumulado,
                out resultado))
                return resultado;

            if (ExecutarTentativaEmSubTransaction(
                doc,
                "solid-solid cut",
                d => TentarSolidSolidCut(doc, host, cutter, d),
                acumulado,
                out resultado))
                return resultado;

            return new ResultadoPar(false, false, "nenhum", acumulado);
        }

        private bool ExecutarTentativaEmSubTransaction(
            Document doc,
            string nomeEstrategia,
            Func<List<string>, ResultadoTentativa> tentativa,
            List<string> acumulado,
            out ResultadoPar resultado)
        {
            resultado = null;

            using (SubTransaction st = new SubTransaction(doc))
            {
                st.Start();

                List<string> diagnosticoLocal = new List<string>();
                ResultadoTentativa tentativaResultado;

                try
                {
                    tentativaResultado = tentativa(diagnosticoLocal) ??
                        new ResultadoTentativa(StatusTentativa.Falhou, new List<string> { "Tentativa sem retorno." });
                }
                catch (Exception ex)
                {
                    tentativaResultado = new ResultadoTentativa(StatusTentativa.Falhou, new List<string> { ex.Message });
                }

                if (tentativaResultado.Status == StatusTentativa.Aplicado)
                {
                    st.Commit();
                    resultado = new ResultadoPar(true, false, nomeEstrategia, tentativaResultado.Diagnostico);
                    return true;
                }

                st.RollBack();

                if (tentativaResultado.Status == StatusTentativa.JaConforme)
                {
                    resultado = new ResultadoPar(false, true, nomeEstrategia, tentativaResultado.Diagnostico);
                    return true;
                }

                if (tentativaResultado.Status == StatusTentativa.Falhou)
                {
                    acumulado.AddRange(
                        tentativaResultado.Diagnostico
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => $"{nomeEstrategia}: {x}"));
                }

                return false;
            }
        }

        private ResultadoTentativa TentarJoinGeometryComoCorte(
            Document doc,
            Element host,
            Element cutter,
            List<string> diagnostico)
        {
            bool jaUnidos;
            try
            {
                jaUnidos = JoinGeometryUtils.AreElementsJoined(doc, host, cutter);
            }
            catch (Exception ex)
            {
                return new ResultadoTentativa(StatusTentativa.Falhou, new List<string> { $"Nao foi possivel consultar a uniao: {ex.Message}" });
            }

            if (jaUnidos)
            {
                try
                {
                    if (JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, host))
                    {
                        diagnostico.Add("A coluna/pilar ja estava cortando o host.");
                        return new ResultadoTentativa(StatusTentativa.JaConforme, diagnostico);
                    }
                }
                catch (Exception ex)
                {
                    diagnostico.Add($"Nao foi possivel verificar a ordem do corte: {ex.Message}");
                }

                try
                {
                    JoinGeometryUtils.SwitchJoinOrder(doc, host, cutter);
                    diagnostico.Add("Ordem de corte invertida para priorizar a coluna/pilar.");

                    if (JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, host))
                        return new ResultadoTentativa(StatusTentativa.Aplicado, diagnostico);
                }
                catch (Exception ex)
                {
                    diagnostico.Add($"SwitchJoinOrder falhou: {ex.Message}");
                }

                return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
            }

            try
            {
                JoinGeometryUtils.JoinGeometry(doc, host, cutter);
                diagnostico.Add("JoinGeometry aplicado.");
            }
            catch (Exception ex)
            {
                diagnostico.Add($"JoinGeometry falhou: {ex.Message}");
                return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
            }

            try
            {
                if (JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, host))
                {
                    diagnostico.Add("A coluna/pilar assumiu o papel de corte.");
                    return new ResultadoTentativa(StatusTentativa.Aplicado, diagnostico);
                }

                JoinGeometryUtils.SwitchJoinOrder(doc, host, cutter);
                diagnostico.Add("SwitchJoinOrder aplicado apos a uniao.");

                if (JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, host))
                    return new ResultadoTentativa(StatusTentativa.Aplicado, diagnostico);

                diagnostico.Add("A uniao foi criada, mas a coluna/pilar nao ficou como elemento cortador.");
                return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
            }
            catch (Exception ex)
            {
                diagnostico.Add($"Nao foi possivel confirmar o corte apos a uniao: {ex.Message}");
                return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
            }
        }

        private ResultadoTentativa TentarSolidSolidCut(
            Document doc,
            Element host,
            Element cutter,
            List<string> diagnostico)
        {
            if (!SolidSolidCutUtils.IsAllowedForSolidCut(host) ||
                !SolidSolidCutUtils.IsAllowedForSolidCut(cutter))
            {
                return new ResultadoTentativa(StatusTentativa.NaoAplicavel);
            }

            try
            {
                bool existeCorte = SolidSolidCutUtils.CutExistsBetweenElements(host, cutter, out bool hostCortaCutter);
                if (existeCorte)
                {
                    if (!hostCortaCutter)
                    {
                        diagnostico.Add("O solid-solid cut ja existia com a coluna/pilar cortando o host.");
                        return new ResultadoTentativa(StatusTentativa.JaConforme, diagnostico);
                    }

                    SolidSolidCutUtils.RemoveCutBetweenSolids(doc, host, cutter);
                    diagnostico.Add("Corte solido existente removido para inverter o sentido.");
                }
            }
            catch (Exception ex)
            {
                diagnostico.Add($"Nao foi possivel consultar/remover o corte solido existente: {ex.Message}");
            }

            try
            {
                if (!SolidSolidCutUtils.CanElementCutElement(cutter, host, out CutFailureReason motivo))
                {
                    diagnostico.Add($"A coluna/pilar nao pode cortar o host por solid-solid cut: {motivo}.");
                    return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
                }

                SolidSolidCutUtils.AddCutBetweenSolids(doc, host, cutter);
                diagnostico.Add("Solid-solid cut aplicado.");
                return new ResultadoTentativa(StatusTentativa.Aplicado, diagnostico);
            }
            catch (Exception ex)
            {
                diagnostico.Add($"AddCutBetweenSolids falhou: {ex.Message}");
                return new ResultadoTentativa(StatusTentativa.Falhou, diagnostico);
            }
        }

        // =====================================================================
        // Helpers publicos de validacao de escopo — usados pelo comando tambem
        // =====================================================================

        /// <summary>
        /// True se o elemento e piso, quadro estrutural, coluna ou pilar (hosts ou cortadores).
        /// </summary>
        internal static bool EhElementoValidoParaEscopo(Element elemento)
        {
            if (elemento == null || elemento.Category == null || elemento.ViewSpecific)
                return false;

            return EhHostValido(elemento) || EhCortadorValido(elemento);
        }

        /// <summary>True se o elemento e piso ou quadro estrutural (pode receber corte).</summary>
        internal static bool EhHostValido(Element elemento)
        {
            if (elemento?.Category == null)
                return false;

            long categoryId = elemento.Category.Id.Value;
            return categoryId == (long)BuiltInCategory.OST_Floors ||
                   categoryId == (long)BuiltInCategory.OST_StructuralFraming;
        }

        /// <summary>True se o elemento e coluna ou pilar estrutural (pode cortar).</summary>
        internal static bool EhCortadorValido(Element elemento)
        {
            if (elemento?.Category == null)
                return false;

            long categoryId = elemento.Category.Id.Value;
            return categoryId == (long)BuiltInCategory.OST_StructuralColumns ||
                   categoryId == (long)BuiltInCategory.OST_Columns;
        }

        private string DescreverElemento(Element elemento)
        {
            if (elemento == null) return "<nulo>";

            string categoria = elemento.Category?.Name ?? "Sem categoria";
            string nome = elemento.Name ?? elemento.GetType().Name;
            return $"{categoria} | {nome} (Id {elemento.Id.Value})";
        }
    }
}
