using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Core;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Services
{
    /// <summary>
    /// Quarta adocao do ADR-003 + ADR-004 (Onda 2).
    ///
    /// Contrato novo: retorna <see cref="Core.Result{ResultadoAgrupamento}"/> /
    /// <see cref="Core.Result{ResultadoLimpeza}"/>, aceita <see cref="IProgress{ProgressReport}"/>
    /// opcional e <see cref="CancellationToken"/> opcional. Servico "mudo" por ADR-003
    /// (so <see cref="Logger"/>; dialogos sao decididos pelo comando).
    ///
    /// ADR-004: progresso granular e checagem de cancelamento no loop de assinaturas
    /// +overrides. A transacao Revit e nao-interrompivel (cancelar no meio deixa grupos
    /// parcialmente criados — e feio mas nao corrompe o modelo, entao o pattern
    /// e: checar CT antes de comecar a aplicar, depois deixar a transacao terminar).
    ///
    /// Classes permanecem estaticas porque nao tem estado e nao precisam de mock em
    /// testes (consomem FilteredElementCollector direto da Revit API).
    /// </summary>
    internal static class AgrupamentoVisualService
    {
        private const string PrefixoGruposPilares = "EMT_COL_";
        private const string PrefixoGruposVigas = "EMT_VIG_";

        /// <summary>Resultado de uma operacao de agrupamento (pilares ou vigas).</summary>
        public sealed class ResultadoAgrupamento
        {
            public string TituloOperacao { get; set; } = string.Empty;
            public int ElementosNaVista { get; set; }
            public int ConjuntosIdentificados { get; set; }
            public int ConjuntosColoridos { get; set; }
            public int ElementosComOverride { get; set; }
            public int GruposEmtCriados { get; set; }
            public int GruposEmtDesfeitosAntes { get; set; }
            public int ConjuntosSomenteVisuais { get; set; }
            public bool CriouGruposNativos { get; set; }
            public List<string> Falhas { get; } = new List<string>();
            public TimeSpan Duracao { get; set; }
        }

        /// <summary>Resultado de uma operacao de limpeza de overrides + grupos EMT.</summary>
        public sealed class ResultadoLimpeza
        {
            public int OverridesRemovidos { get; set; }
            public int GruposDesfeitos { get; set; }
            public TimeSpan Duracao { get; set; }
        }

        private static readonly Color[] PaletaCores =
        {
            new Color(220, 20, 60),
            new Color(30, 144, 255),
            new Color(34, 139, 34),
            new Color(255, 140, 0),
            new Color(148, 0, 211),
            new Color(0, 139, 139),
            new Color(205, 92, 92),
            new Color(70, 130, 180)
        };

        public static Core.Result<ResultadoAgrupamento> AgruparPilares(
            UIDocument uidoc,
            IProgress<ProgressReport> progress = null,
            CancellationToken ct = default)
        {
            return AgruparPorTipo(
                uidoc,
                BuiltInCategory.OST_StructuralColumns,
                "Agrupar Pilares",
                PrefixoGruposPilares,
                progress,
                ct);
        }

        public static Core.Result<ResultadoAgrupamento> AgruparVigas(
            UIDocument uidoc,
            IProgress<ProgressReport> progress = null,
            CancellationToken ct = default)
        {
            return AgruparPorTipo(
                uidoc,
                BuiltInCategory.OST_StructuralFraming,
                "Agrupar Vigas",
                PrefixoGruposVigas,
                progress,
                ct);
        }

        public static Core.Result<ResultadoLimpeza> LimparAgrupamentos(
            UIDocument uidoc,
            IProgress<ProgressReport> progress = null,
            CancellationToken ct = default)
        {
            if (uidoc is null)
                return Core.Result<ResultadoLimpeza>.Fail("UIDocument nulo.");

            Stopwatch sw = Stopwatch.StartNew();
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (view == null)
                return Core.Result<ResultadoLimpeza>.Fail("Nao ha vista ativa.");

            ProgressReporter reporter = new ProgressReporter(progress, throttleMs: 100, ct);

            // Fase 1 (interrompivel): coleta de IDs.
            reporter.Report(0, 0, "Coletando elementos da vista...");
            try
            {
                reporter.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Limpar Agrupamentos cancelado antes da coleta.");
                throw;
            }

            List<ElementId> idsPilares = ColetarElementosDaVista(doc, view, BuiltInCategory.OST_StructuralColumns, incluirElementosEmGruposDoUsuario: true)
                .Select(x => x.Id)
                .ToList();

            List<ElementId> idsVigas = ColetarElementosDaVista(doc, view, BuiltInCategory.OST_StructuralFraming, incluirElementosEmGruposDoUsuario: true)
                .Select(x => x.Id)
                .ToList();

            List<ElementId> idsAfetados = idsPilares.Concat(idsVigas).Distinct().ToList();

            // Ultimo check antes da transacao — durante o commit, cancelar nao e seguro.
            try
            {
                reporter.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Limpar Agrupamentos cancelado antes da transacao.");
                throw;
            }

            // Fase 2 (nao interrompivel): aplicar overrides + desfazer grupos.
            int gruposDesfeitos;
            try
            {
                using (Transaction t = new Transaction(doc, "Limpar Cores e Grupos EMT"))
                {
                    t.Start();

                    OverrideGraphicSettings limpar = new OverrideGraphicSettings();
                    foreach (ElementId id in idsAfetados)
                        view.SetElementOverrides(id, limpar);

                    gruposDesfeitos = DesfazerGruposCriados(doc, PrefixoGruposPilares)
                                    + DesfazerGruposCriados(doc, PrefixoGruposVigas);

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Falha ao limpar agrupamentos.");
                return Core.Result<ResultadoLimpeza>.Fail("Falha ao limpar agrupamentos: " + ex.Message);
            }

            uidoc.Selection.SetElementIds(new List<ElementId>());

            sw.Stop();

            ResultadoLimpeza resultado = new ResultadoLimpeza
            {
                OverridesRemovidos = idsAfetados.Count,
                GruposDesfeitos = gruposDesfeitos,
                Duracao = sw.Elapsed
            };

            reporter.ReportFinal(idsAfetados.Count, idsAfetados.Count, "Limpeza concluida");
            Logger.Info(
                "Agrupamentos limpos: {Overrides} overrides, {Grupos} grupos EMT desfeitos em {ElapsedMs} ms",
                resultado.OverridesRemovidos,
                resultado.GruposDesfeitos,
                (long)resultado.Duracao.TotalMilliseconds);

            return Core.Result<ResultadoLimpeza>.Ok(resultado);
        }

        private static Core.Result<ResultadoAgrupamento> AgruparPorTipo(
            UIDocument uidoc,
            BuiltInCategory categoria,
            string titulo,
            string prefixoGrupo,
            IProgress<ProgressReport> progress,
            CancellationToken ct)
        {
            if (uidoc is null)
                return Core.Result<ResultadoAgrupamento>.Fail("UIDocument nulo.");

            Stopwatch sw = Stopwatch.StartNew();
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (view == null)
                return Core.Result<ResultadoAgrupamento>.Fail("Nao ha vista ativa.");

            ProgressReporter reporter = new ProgressReporter(progress, throttleMs: 100, ct);

            // ===== FASE 1 (interrompivel): coleta + assinaturas de equivalencia =====
            reporter.Report(0, 0, "Coletando elementos da vista...");
            List<FamilyInstance> elementos = ColetarElementosDaVista(
                doc, view, categoria, incluirElementosEmGruposDoUsuario: false);

            if (elementos.Count == 0)
                return Core.Result<ResultadoAgrupamento>.Fail(
                    "Nenhum elemento estrutural valido foi encontrado na vista ativa.");

            reporter.Report(0, elementos.Count, $"Gerando assinaturas de {elementos.Count} elementos...");

            // Pre-computar assinaturas com check de CT a cada N elementos. Em modelos com
            // alguns milhares de elementos, CriarAssinaturaEquivalencia nao e trivial
            // (le varios parametros e geometria da curve) — vale a pena o progresso.
            List<KeyValuePair<string, FamilyInstance>> assinaturas = new List<KeyValuePair<string, FamilyInstance>>(elementos.Count);
            for (int i = 0; i < elementos.Count; i++)
            {
                if ((i & 31) == 0) // a cada 32 elementos (barato)
                    reporter.ThrowIfCancellationRequested();

                FamilyInstance el = elementos[i];
                assinaturas.Add(new KeyValuePair<string, FamilyInstance>(CriarAssinaturaEquivalencia(el), el));

                if ((i & 63) == 0)
                    reporter.Report(i + 1, elementos.Count, $"Gerando assinaturas {i + 1}/{elementos.Count}");
            }

            List<IGrouping<string, FamilyInstance>> gruposPorTipo = assinaturas
                .GroupBy(kv => kv.Key, kv => kv.Value)
                .OrderBy(x => DescreverElemento(x.FirstOrDefault(), doc))
                .ToList();

            // Ultimo check antes da transacao.
            reporter.ThrowIfCancellationRequested();

            bool criarGruposNativos = categoria != BuiltInCategory.OST_StructuralColumns;
            int gruposCriados = 0;
            int conjuntosColoridos = 0;
            int conjuntosSomenteVisuais = 0;
            int elementosColoridos = 0;
            int gruposDesfeitosAntes = 0;
            List<string> falhas = new List<string>();

            // ===== FASE 2 (NAO interrompivel): transacao Revit =====
            // Uma vez dentro da transacao, nao checamos CT. Abortar no meio criaria grupos
            // orfaos e overrides parciais na vista. Pattern consistente com o resto da
            // base (DSTV, LDM, ModelCheck).
            reporter.Report(0, gruposPorTipo.Count, "Aplicando cores e criando grupos...");

            try
            {
                using (Transaction t = new Transaction(doc, titulo))
                {
                    t.Start();

                    gruposDesfeitosAntes += DesfazerGruposCriados(doc, prefixoGrupo);

                    int indiceCor = 0;
                    int indiceConjunto = 0;
                    foreach (IGrouping<string, FamilyInstance> grupoPorTipo in gruposPorTipo)
                    {
                        indiceConjunto++;

                        List<ElementId> ids = grupoPorTipo
                            .Select(x => x.Id)
                            .Distinct()
                            .ToList();

                        if (ids.Count == 0)
                            continue;

                        OverrideGraphicSettings overrideGrafico = CriarOverride(PaletaCores[indiceCor % PaletaCores.Length]);
                        foreach (ElementId id in ids)
                            view.SetElementOverrides(id, overrideGrafico);

                        conjuntosColoridos++;
                        elementosColoridos += ids.Count;
                        indiceCor++;

                        reporter.Report(indiceConjunto, gruposPorTipo.Count,
                            $"Colorindo conjunto {indiceConjunto}/{gruposPorTipo.Count} ({ids.Count} elementos)");

                        if (ids.Count < 2)
                            continue;

                        if (!criarGruposNativos)
                        {
                            conjuntosSomenteVisuais++;
                            continue;
                        }

                        try
                        {
                            Group group = doc.Create.NewGroup(ids);
                            doc.Regenerate();
                            string descricaoGrupo = DescreverElemento(grupoPorTipo.FirstOrDefault(), doc);
                            RenomearGrupo(doc, group, prefixoGrupo + SanitizarNome(descricaoGrupo));
                            gruposCriados++;
                        }
                        catch (Exception ex)
                        {
                            string descricao = DescreverElemento(grupoPorTipo.FirstOrDefault(), doc);
                            falhas.Add($"{descricao}: {ex.Message}");
                            Logger.Warn(ex, "Falha ao criar grupo EMT para conjunto {Descricao}", descricao);
                        }
                    }

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Falha na transacao de {Titulo}.", titulo);
                return Core.Result<ResultadoAgrupamento>.Fail($"Falha ao processar {titulo}: {ex.Message}");
            }

            uidoc.Selection.SetElementIds(elementos.Select(x => x.Id).ToList());

            sw.Stop();

            ResultadoAgrupamento resultado = new ResultadoAgrupamento
            {
                TituloOperacao = titulo,
                ElementosNaVista = elementos.Count,
                ConjuntosIdentificados = gruposPorTipo.Count,
                ConjuntosColoridos = conjuntosColoridos,
                ElementosComOverride = elementosColoridos,
                GruposEmtCriados = gruposCriados,
                GruposEmtDesfeitosAntes = gruposDesfeitosAntes,
                ConjuntosSomenteVisuais = conjuntosSomenteVisuais,
                CriouGruposNativos = criarGruposNativos,
                Duracao = sw.Elapsed
            };
            resultado.Falhas.AddRange(falhas);

            reporter.ReportFinal(gruposPorTipo.Count, gruposPorTipo.Count, "Processamento concluido");

            Logger.Info(
                "{Titulo} concluido: {Elementos} elementos, {Conjuntos} conjuntos, {Grupos} grupos EMT em {ElapsedMs} ms (falhas={Falhas})",
                titulo,
                resultado.ElementosNaVista,
                resultado.ConjuntosIdentificados,
                resultado.GruposEmtCriados,
                (long)resultado.Duracao.TotalMilliseconds,
                resultado.Falhas.Count);

            return Core.Result<ResultadoAgrupamento>.Ok(resultado);
        }

        /// <summary>
        /// Monta a mensagem amigavel de sucesso consumida pelo comando via
        /// <c>AppDialogService.ShowInfo</c>. Fica no servico pra garantir que o texto
        /// se alinha com o que o servico realmente fez.
        /// </summary>
        public static string BuildResumoText(ResultadoAgrupamento r)
        {
            if (r == null) return "Processamento concluido.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Processamento concluido.");
            sb.AppendLine();
            sb.AppendLine($"Elementos na vista: {r.ElementosNaVista}");
            sb.AppendLine($"Conjuntos exatamente identicos: {r.ConjuntosIdentificados}");
            sb.AppendLine($"Conjuntos coloridos: {r.ConjuntosColoridos}");
            sb.AppendLine($"Elementos com override: {r.ElementosComOverride}");
            if (r.CriouGruposNativos)
                sb.AppendLine($"Grupos EMT criados: {r.GruposEmtCriados}");
            else
                sb.AppendLine($"Conjuntos mantidos apenas no agrupamento visual: {r.ConjuntosSomenteVisuais}");
            sb.AppendLine($"Grupos EMT antigos desfeitos antes da recriacao: {r.GruposEmtDesfeitosAntes}");

            if (!r.CriouGruposNativos)
            {
                sb.AppendLine();
                sb.AppendLine("Observacao:");
                sb.AppendLine("Para pilares, o comando evita criar grupos nativos do Revit para nao desanexar membros dos eixos.");
            }

            if (r.Falhas.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Falhas ao criar alguns grupos:");
                foreach (string falha in r.Falhas.Take(6))
                    sb.AppendLine("• " + falha);
                if (r.Falhas.Count > 6)
                    sb.AppendLine($"• ... e mais {r.Falhas.Count - 6}.");
            }

            return sb.ToString();
        }

        /// <summary>Mensagem amigavel pra operacao de limpeza.</summary>
        public static string BuildResumoText(ResultadoLimpeza r)
        {
            if (r == null) return "Limpeza concluida.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Limpeza concluida.");
            sb.AppendLine();
            sb.AppendLine($"Overrides removidos na vista: {r.OverridesRemovidos}");
            sb.AppendLine($"Grupos EMT desfeitos: {r.GruposDesfeitos}");
            return sb.ToString();
        }

        private static List<FamilyInstance> ColetarElementosDaVista(
            Document doc,
            View view,
            BuiltInCategory categoria,
            bool incluirElementosEmGruposDoUsuario)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(categoria)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(x => x.Category != null)
                .Where(x => incluirElementosEmGruposDoUsuario || PodeParticiparDoAgrupamento(doc, x))
                .ToList();
        }

        private static bool PodeParticiparDoAgrupamento(Document doc, Element elemento)
        {
            if (elemento == null)
                return false;

            if (elemento.GroupId == ElementId.InvalidElementId)
                return true;

            Group grupo = doc.GetElement(elemento.GroupId) as Group;
            string nomeGrupo = grupo?.GroupType?.Name ?? string.Empty;

            return nomeGrupo.StartsWith(PrefixoGruposPilares, StringComparison.OrdinalIgnoreCase) ||
                   nomeGrupo.StartsWith(PrefixoGruposVigas, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly string[] TokensPeso =
        {
            "weight",
            "peso"
        };

        private static string CriarAssinaturaEquivalencia(FamilyInstance instancia)
        {
            if (instancia?.Category == null)
                return "SEM_CATEGORIA";

            BuiltInCategory categoria = (BuiltInCategory)instancia.Category.Id.Value;
            return categoria switch
            {
                BuiltInCategory.OST_StructuralColumns => CriarAssinaturaPilar(instancia),
                BuiltInCategory.OST_StructuralFraming => CriarAssinaturaViga(instancia),
                _ => CriarAssinaturaGenerica(instancia)
            };
        }

        private static string CriarAssinaturaPilar(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(512);
            AppendCabecalhoComum(sb, instancia, "COLUMN");

            AppendParametro(sb, "SLANT_TYPE", instancia, BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM);
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static string CriarAssinaturaViga(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(1024);
            AppendCabecalhoComum(sb, instancia, "BEAM");

            AppendParametro(sb, "CUT_LENGTH", instancia, BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametro(sb, "Y_JUST", instancia, BuiltInParameter.Y_JUSTIFICATION);
            AppendParametro(sb, "Z_JUST", instancia, BuiltInParameter.Z_JUSTIFICATION);
            AppendParametro(sb, "YZ_JUST", instancia, BuiltInParameter.YZ_JUSTIFICATION);
            AppendParametro(sb, "BEAM_H_JUST", instancia, BuiltInParameter.BEAM_H_JUSTIFICATION);
            AppendParametro(sb, "BEAM_V_JUST", instancia, BuiltInParameter.BEAM_V_JUSTIFICATION);
            AppendParametro(sb, "SY_JUST", instancia, BuiltInParameter.START_Y_JUSTIFICATION);
            AppendParametro(sb, "EY_JUST", instancia, BuiltInParameter.END_Y_JUSTIFICATION);
            AppendParametro(sb, "SZ_JUST", instancia, BuiltInParameter.START_Z_JUSTIFICATION);
            AppendParametro(sb, "EZ_JUST", instancia, BuiltInParameter.END_Z_JUSTIFICATION);
            AppendParametro(sb, "Y_OFF", instancia, BuiltInParameter.Y_OFFSET_VALUE);
            AppendParametro(sb, "Z_OFF", instancia, BuiltInParameter.Z_OFFSET_VALUE);
            AppendParametro(sb, "SY_OFF", instancia, BuiltInParameter.START_Y_OFFSET_VALUE);
            AppendParametro(sb, "EY_OFF", instancia, BuiltInParameter.END_Y_OFFSET_VALUE);
            AppendParametro(sb, "SZ_OFF", instancia, BuiltInParameter.START_Z_OFFSET_VALUE);
            AppendParametro(sb, "EZ_OFF", instancia, BuiltInParameter.END_Z_OFFSET_VALUE);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static string CriarAssinaturaGenerica(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(2048);
            AppendCabecalhoComum(sb, instancia, "GENERIC");
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static void AppendCabecalhoComum(StringBuilder sb, FamilyInstance instancia, string marcador)
        {
            sb.Append("KIND=").Append(marcador).Append('|');
            sb.Append("CAT=").Append(instancia.Category?.Id.Value ?? 0).Append('|');
            sb.Append("TYPE=").Append(instancia.GetTypeId().Value).Append('|');
            sb.Append("STRUCTTYPE=").Append((int)instancia.StructuralType).Append('|');
        }

        private static void AppendComprimentoDaInstancia(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "LEN", elemento, BuiltInParameter.INSTANCE_LENGTH_PARAM);
        }

        private static void AppendRotacaoSecao(StringBuilder sb, FamilyInstance instancia)
        {
            Parameter parametroRotacao = instancia.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE);
            if (parametroRotacao != null && parametroRotacao.HasValue && parametroRotacao.StorageType == StorageType.Double)
            {
                sb.Append("ROT=").Append(FormatoExato(parametroRotacao.AsDouble())).Append('|');
                return;
            }

            if (instancia.Location is LocationPoint locationPoint)
                sb.Append("ROT=").Append(FormatoExato(locationPoint.Rotation)).Append('|');
        }

        private static void AppendVolume(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "VOL", elemento, BuiltInParameter.HOST_VOLUME_COMPUTED);
        }

        private static void AppendMaterialEstrutural(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "MAT", elemento, BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            AppendParametro(sb, "MAT_TYPE", elemento, BuiltInParameter.STRUCTURAL_MATERIAL_TYPE);
        }

        private static void AppendParametro(StringBuilder sb, string chave, Element elemento, BuiltInParameter builtInParameter)
        {
            Parameter parametro = elemento?.get_Parameter(builtInParameter);
            if (parametro == null || !parametro.HasValue)
                return;

            switch (parametro.StorageType)
            {
                case StorageType.Double:
                    sb.Append(chave).Append('=').Append(FormatoExato(parametro.AsDouble())).Append('|');
                    break;
                case StorageType.Integer:
                    sb.Append(chave).Append('=').Append(parametro.AsInteger()).Append('|');
                    break;
                case StorageType.ElementId:
                    sb.Append(chave).Append('=').Append(parametro.AsElementId().Value).Append('|');
                    break;
            }
        }

        private static void AppendParametrosPorNome(StringBuilder sb, Element elemento, string prefixo, IEnumerable<string> tokens)
        {
            List<string> encontrados = new List<string>();
            foreach (Parameter parametro in elemento.Parameters)
            {
                if (parametro == null || !parametro.HasValue || parametro.Definition == null)
                    continue;
                if (parametro.StorageType == StorageType.String || parametro.StorageType == StorageType.None)
                    continue;

                string nome = parametro.Definition.Name ?? string.Empty;
                string nomeNormalizado = nome.ToLowerInvariant();
                if (!tokens.Any(x => nomeNormalizado.Contains(x)))
                    continue;

                long id = parametro.Id?.Value ?? 0;
                switch (parametro.StorageType)
                {
                    case StorageType.Double:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{FormatoExato(parametro.AsDouble())}");
                        break;
                    case StorageType.Integer:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{parametro.AsInteger()}");
                        break;
                    case StorageType.ElementId:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{parametro.AsElementId().Value}");
                        break;
                }
            }

            foreach (string item in encontrados.OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(item).Append('|');
        }

        private static void AppendCurveInvariante(StringBuilder sb, Curve curve)
        {
            if (curve == null)
            {
                sb.Append("CURVE=NULL|");
                return;
            }

            sb.Append("CTYPE=").Append(curve.GetType().Name).Append('|');
            sb.Append("CLEN=").Append(FormatoExato(curve.Length)).Append('|');

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            XYZ delta = p1 - p0;
            double dx = Math.Abs(delta.X);
            double dy = Math.Abs(delta.Y);
            double dz = Math.Abs(delta.Z);
            double horizontal = Math.Sqrt((dx * dx) + (dy * dy));

            sb.Append("DX=").Append(FormatoExato(dx)).Append('|');
            sb.Append("DY=").Append(FormatoExato(dy)).Append('|');
            sb.Append("DZ=").Append(FormatoExato(dz)).Append('|');
            sb.Append("DH=").Append(FormatoExato(horizontal)).Append('|');

            if (curve is Arc arc)
            {
                sb.Append("R=").Append(FormatoExato(arc.Radius)).Append('|');
                sb.Append("ADIRX=").Append(FormatoExato(Math.Abs(arc.Normal.X))).Append('|');
                sb.Append("ADIRY=").Append(FormatoExato(Math.Abs(arc.Normal.Y))).Append('|');
                sb.Append("ADIRZ=").Append(FormatoExato(Math.Abs(arc.Normal.Z))).Append('|');
            }
        }

        private static string FormatoExato(double valor)
        {
            return valor.ToString("R", CultureInfo.InvariantCulture);
        }

        private static OverrideGraphicSettings CriarOverride(Color cor)
        {
            OverrideGraphicSettings settings = new OverrideGraphicSettings();
            settings.SetProjectionLineColor(cor);
            settings.SetCutLineColor(cor);
            settings.SetSurfaceTransparency(30);
            settings.SetCutForegroundPatternColor(cor);
            settings.SetSurfaceForegroundPatternColor(cor);
            return settings;
        }

        private static int DesfazerGruposCriados(Document doc, string prefixoGrupo)
        {
            List<Group> grupos = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(x => x.GroupType != null &&
                            x.GroupType.Name != null &&
                            x.GroupType.Name.StartsWith(prefixoGrupo, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = 0;
            foreach (Group grupo in grupos)
            {
                try
                {
                    grupo.UngroupMembers();
                    total++;
                }
                // TODO M12+: contabilizar falhas e reportar ao usuario ao final
                //          ("Desagrupados: X. Falhas: Y") para melhor UX
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[AgrupamentoVisual] Falha ao desagrupar {Nome}", grupo.GroupType?.Name);
                }
            }

            return total;
        }

        private static void RenomearGrupo(Document doc, Group group, string nomeBase)
        {
            if (group?.GroupType == null)
                return;

            HashSet<string> nomesExistentes = new FilteredElementCollector(doc)
                .OfClass(typeof(GroupType))
                .Cast<GroupType>()
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            nomesExistentes.Remove(group.GroupType.Name);

            string nome = nomeBase;
            int indice = 2;
            while (nomesExistentes.Contains(nome))
            {
                nome = $"{nomeBase}_{indice:00}";
                indice++;
            }

            group.GroupType.Name = nome;
        }

        private static string DescreverTipo(Document doc, ElementId typeId)
        {
            ElementType tipo = doc.GetElement(typeId) as ElementType;
            if (tipo == null)
                return "Tipo sem nome";

            string familia = string.Empty;
            if (tipo is FamilySymbol simbolo && simbolo.FamilyName != null)
                familia = simbolo.FamilyName.Trim();

            string nomeTipo = tipo.Name?.Trim() ?? "Sem nome";
            return string.IsNullOrWhiteSpace(familia) ? nomeTipo : $"{familia} - {nomeTipo}";
        }

        private static string DescreverElemento(FamilyInstance instancia, Document doc)
        {
            if (instancia == null)
                return "Elemento sem descricao";

            return DescreverTipo(doc, instancia.GetTypeId());
        }

        private static string SanitizarNome(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "Grupo";

            StringBuilder sb = new StringBuilder(texto.Length);
            foreach (char c in texto)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == ' ' || c == '-' || c == '_')
                {
                    sb.Append('_');
                }
            }

            string resultado = sb.ToString().Trim('_');
            while (resultado.Contains("__"))
                resultado = resultado.Replace("__", "_");

            return string.IsNullOrWhiteSpace(resultado) ? "Grupo" : resultado;
        }
    }
}
