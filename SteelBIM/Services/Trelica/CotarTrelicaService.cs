#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Orquestrador principal da funcao "Cotar Treliça". Implementa o pipeline
    /// de 10 passos para gerar cotas em 5 faixas + identificacao de perfis em uma
    /// elevacao de trelica (Revit Elevation/Section).
    /// </summary>
    /// <remarks>
    /// Pipeline (ver docs/PLANO-LAPIDACAO-V2.md secao 4.1):
    ///   1. Projetar barras em 2D usando orientacao da vista.
    ///   2. Calcular bounding box 2D.
    ///   3. Classificar cada barra (Banzo Superior/Inferior, Montante, Diagonal, etc.).
    ///   4. Extrair nos dos banzos.
    ///   5. Detectar topologia (Plana, DuasAguas, Shed, Desconhecida).
    ///   6. Construir as 5 faixas de cotas.
    ///   7. Criar Dimensions no Revit.
    ///   8. Criar Tags de perfil.
    ///   9. Criar TextNotes "BANZO SUPERIOR/INFERIOR".
    ///   10. Retornar relatorio.
    ///
    /// Toda operacao roda dentro de UMA unica transacao gerenciada pelo Command.
    /// </remarks>
    public sealed class CotarTrelicaService
    {
        /// <summary>
        /// Executa o pipeline completo de cotagem de trelica.
        /// </summary>
        /// <param name="uidoc">UIDocument ativo.</param>
        /// <param name="doc">Document ativo.</param>
        /// <param name="vista">View de elevacao/corte onde serao criadas as cotas.</param>
        /// <param name="barras">Barras pre-selecionadas (FamilyInstance StructuralFraming).</param>
        /// <param name="config">Configuracao de cotagem (quais faixas, offset, tags, etc.).</param>
        /// <returns>CotarTrelicaReport com contagem de elementos criados e avisos.</returns>
        /// <remarks>
        /// A TRANSACAO JA DEVE ESTAR INICIADA antes de chamar este metodo.
        /// O service NAO cria nem fecha transacao — isso e responsabilidade do Command.
        /// </remarks>
        public CotarTrelicaReport Executar(
            UIDocument uidoc,
            Document doc,
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            CotarTrelicaConfig config)
        {
            var sw = Stopwatch.StartNew();
            int cotasCriadas = 0;
            int tagsCriadas = 0;
            int textosCriados = 0;
            var avisos = new List<string>();

            try
            {
                Logger.Info("[CotarTrelica] iniciando pipeline (config={@Cfg})", config);

                // ===== 1. Projetar barras em 2D =====
                Logger.Info("[CotarTrelica] passo 1 — projetando {N} barras em 2D", barras.Count);
                var barrasProjetadas = ProjetarBarrasEm2D(vista, barras);
                if (barrasProjetadas.Count == 0)
                {
                    avisos.Add("Nao foi possivel projetar nenhuma barra.");
                    Logger.Warn("[CotarTrelica] nenhuma barra projetada");
                    sw.Stop();
                    return new CotarTrelicaReport(0, 0, 0, avisos.Count, avisos, sw.ElapsedMilliseconds);
                }

                // ===== 2. Calcular bounding box 2D =====
                Logger.Info("[CotarTrelica] passo 2 — calculando bounding box 2D");
                var boundingBox2D = CalcularBoundingBox2D(barrasProjetadas.Values);
                if (boundingBox2D.Width <= 0 || boundingBox2D.Height <= 0)
                {
                    avisos.Add("Bounding box invalido (treliça muito pequena ou degenerada).");
                    Logger.Warn("[CotarTrelica] bounding box invalido");
                    sw.Stop();
                    return new CotarTrelicaReport(0, 0, 0, avisos.Count, avisos, sw.ElapsedMilliseconds);
                }

                // ===== 3. Classificar cada barra =====
                Logger.Info("[CotarTrelica] passo 3 — classificando {N} barras", barras.Count);
                var barrasClassificadas = ClassificarBarras(vista, barras, boundingBox2D);

                // ===== 4. Extrair nos dos banzos =====
                Logger.Info("[CotarTrelica] passo 4 — extraindo nos dos banzos");
                var nosSuperior = ExtrairNosBanzo(vista, barras,
                    TrelicaClassificador.TipoMembro.BanzoSuperior);
                var nosInferior = ExtrairNosBanzo(vista, barras,
                    TrelicaClassificador.TipoMembro.BanzoInferior);

                if (nosSuperior.Count == 0 || nosInferior.Count == 0)
                {
                    avisos.Add("Nao foi possivel detectar banzos validos na trelica.");
                    Logger.Warn("[CotarTrelica] nos do banzo nao detectados adequadamente");
                    sw.Stop();
                    return new CotarTrelicaReport(0, 0, 0, avisos.Count, avisos, sw.ElapsedMilliseconds);
                }

                // ===== 5. Detectar topologia =====
                Logger.Info("[CotarTrelica] passo 5 — detectando topologia");
                var topologia = TrelicaTopologia.Detectar(nosSuperior);
                Logger.Info("[CotarTrelica] topologia detectada: {Topo}", topologia);

                // ===== 6. Construir as 5 faixas de cotas =====
                Logger.Info("[CotarTrelica] passo 6 — construindo faixas de cotas");
                var faixas = ConstruirFaixasCotas(nosSuperior, nosInferior, config);

                // ===== 7. Criar Dimensions no Revit =====
                Logger.Info("[CotarTrelica] passo 7 — criando Dimensions");
                cotasCriadas = CriarDimensionsNoRevit(doc, vista, faixas, barras,
                    nosSuperior, nosInferior, boundingBox2D, config, ref avisos, ref textosCriados);

                // ===== 8. Criar Tags de perfil =====
                if (config.IdentificarPerfis)
                {
                    Logger.Info("[CotarTrelica] passo 8 — criando tags de perfil");
                    tagsCriadas = CriarTagsDeBarra(doc, vista, barras, config, ref avisos);
                }
                else
                {
                    Logger.Info("[CotarTrelica] passo 8 — tags desabilitadas");
                }

                // ===== 9. Criar TextNotes de rotulo de banzo =====
                Logger.Info("[CotarTrelica] passo 9 — criando textos de banzo");
                textosCriados += CriarTextosRotuloBanzos(doc, vista, barras, barrasClassificadas,
                    nosSuperior, nosInferior, config);

                // ===== 10. Retornar relatorio =====
                sw.Stop();
                Logger.Info("[CotarTrelica] pipeline concluido em {Elapsed}ms — " +
                    "{Cotas} cotas, {Tags} tags, {Textos} textos, {Warnings} avisos",
                    sw.ElapsedMilliseconds, cotasCriadas, tagsCriadas, textosCriados, avisos.Count);

                return new CotarTrelicaReport(
                    cotasCriadas, tagsCriadas, textosCriados, avisos.Count, avisos, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error(ex, "[CotarTrelica] falha no pipeline apos {Elapsed}ms",
                    sw.ElapsedMilliseconds);
                avisos.Add($"Erro critico: {ex.Message}");
                throw;
            }
        }

        // =====================================================================
        // Passo 1: Projetar barras em 2D
        // =====================================================================

        private Dictionary<FamilyInstance, (double X, double Z)> ProjetarBarrasEm2D(
            View vista, IReadOnlyList<FamilyInstance> barras)
        {
            var resultado = new Dictionary<FamilyInstance, (double, double)>();

            XYZ u = vista.RightDirection;
            XYZ v = vista.UpDirection;
            XYZ origem = vista.Origin;

            foreach (var fi in barras)
            {
                if (fi.Location is LocationCurve locCurve && locCurve.Curve is Curve curve)
                {
                    try
                    {
                        XYZ ptMeio = curve.Evaluate(0.5, true);
                        double x2D = u.DotProduct(ptMeio - origem);
                        double z2D = v.DotProduct(ptMeio - origem);
                        resultado[fi] = (x2D, z2D);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "[CotarTrelica.Projetar] falha projetando barra {Id}", fi.Id.Value);
                    }
                }
            }

            return resultado;
        }

        // =====================================================================
        // Passo 2: Bounding box 2D
        // =====================================================================

        private (double XMin, double XMax, double ZMin, double ZMax, double Width, double Height)
            CalcularBoundingBox2D(IEnumerable<(double X, double Z)> pontos2D)
        {
            var lista = pontos2D.ToList();
            if (lista.Count == 0) return (0, 0, 0, 0, 0, 0);

            double xMin = lista.Min(p => p.X);
            double xMax = lista.Max(p => p.X);
            double zMin = lista.Min(p => p.Z);
            double zMax = lista.Max(p => p.Z);

            return (xMin, xMax, zMin, zMax, xMax - xMin, zMax - zMin);
        }

        // =====================================================================
        // Passo 3: Classificar barras
        // =====================================================================

        private Dictionary<FamilyInstance, TrelicaClassificador.TipoMembro> ClassificarBarras(
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            (double XMin, double XMax, double ZMin, double ZMax, double Width, double Height) bbox)
        {
            var resultado = new Dictionary<FamilyInstance, TrelicaClassificador.TipoMembro>();
            double zMedioBBox = (bbox.ZMin + bbox.ZMax) / 2.0;

            foreach (var fi in barras)
            {
                try
                {
                    if (fi.Location is LocationCurve locCurve && locCurve.Curve is Curve curve)
                    {
                        XYZ p0 = curve.GetEndPoint(0);
                        XYZ p1 = curve.GetEndPoint(1);
                        XYZ dir = (p1 - p0).Normalize();

                        // Inclinacao absoluta em relacao ao plano XY
                        double inclinacaoAbs = Math.Asin(Math.Abs(dir.Z));

                        var tipoInclinacao = TrelicaClassificador.ClassificarPorInclinacao(inclinacaoAbs);

                        // Se e banzo (indefinido), desambigua por altura
                        if (tipoInclinacao == TrelicaClassificador.TipoMembro.BanzoIndefinido)
                        {
                            double zMedioBarra = (p0.Z + p1.Z) / 2.0;
                            tipoInclinacao = TrelicaClassificador.ClassificarBanzoPorAltura(
                                zMedioBarra, zMedioBBox);
                        }

                        resultado[fi] = tipoInclinacao;
                    }
                    else
                    {
                        resultado[fi] = TrelicaClassificador.TipoMembro.Indefinido;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[CotarTrelica.Classificar] falha classificando barra {Id}",
                        fi.Id.Value);
                    resultado[fi] = TrelicaClassificador.TipoMembro.Indefinido;
                }
            }

            return resultado;
        }

        // =====================================================================
        // Passo 4: Extrair nos dos banzos
        // =====================================================================

        private IReadOnlyList<(double X, double Z)> ExtrairNosBanzo(
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            TrelicaClassificador.TipoMembro tipoBanzo)
        {
            var nos = new HashSet<(double, double)>();
            XYZ u = vista.RightDirection;
            XYZ v = vista.UpDirection;
            XYZ origem = vista.Origin;

            foreach (var fi in barras)
            {
                try
                {
                    if (fi.Location is LocationCurve locCurve && locCurve.Curve is Curve curve)
                    {
                        // Classificar a barra
                        double inclinacao = Math.Asin(Math.Abs(
                            (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize().Z));
                        var tipo = TrelicaClassificador.ClassificarPorInclinacao(inclinacao);

                        if (tipo == tipoBanzo)
                        {
                            // Adicionar endpoints projetados
                            foreach (var pt in new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) })
                            {
                                double x2D = u.DotProduct(pt - origem);
                                double z2D = v.DotProduct(pt - origem);
                                nos.Add((x2D, z2D));
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorar erros de barra individual
                }
            }

            // Retornar ordenado por X
            return nos.OrderBy(p => p.Item1).ToList();
        }

        // =====================================================================
        // Passo 6: Construir faixas
        // =====================================================================

        private List<CotaFaixaBuilder.FaixaCotas> ConstruirFaixasCotas(
            IReadOnlyList<(double X, double Z)> nosSuperior,
            IReadOnlyList<(double X, double Z)> nosInferior,
            CotarTrelicaConfig config)
        {
            var faixas = new List<CotaFaixaBuilder.FaixaCotas>();

            // Extrair apenas coordenadas X
            var xSuperior = nosSuperior.Select(p => p.X).ToList();
            var xInferior = nosInferior.Select(p => p.X).ToList();

            // Converter offset de mm para pes
            double offsetPes = UnitUtils.ConvertToInternalUnits(
                config.OffsetFaixaMm, UnitTypeId.Millimeters);

            try
            {
                if (config.CotarPaineisBanzoSuperior)
                    faixas.Add(CotaFaixaBuilder.FaixaPaineisBanzoSuperior(xSuperior, offsetPes));

                if (config.CotarVaosEntreApoios && xSuperior.Count >= 2)
                {
                    var xApoios = new[] { xSuperior.First(), xSuperior.Last() }.ToList();
                    faixas.Add(CotaFaixaBuilder.FaixaVaosEntreApoios(xApoios, offsetPes * 2.0));
                }

                if (config.CotarPaineisBanzoInferior && xInferior.Count >= 2)
                    faixas.Add(CotaFaixaBuilder.FaixaPaineisBanzoInferior(xInferior, offsetPes));

                if (config.CotarVaoTotal && xSuperior.Count >= 2)
                    faixas.Add(CotaFaixaBuilder.FaixaVaoTotal(
                        xSuperior.First(), xSuperior.Last(), offsetPes * 2.0));

                if (config.CotarAlturaMontantes)
                {
                    // Usar as coordenadas X do banzo superior como estacoes de montante
                    faixas.Add(CotaFaixaBuilder.FaixaAlturasMontantes(xSuperior));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[CotarTrelica.ConstruirFaixas] erro ao construir faixas");
            }

            return faixas;
        }

        // =====================================================================
        // Passo 7: Criar Dimensions
        // =====================================================================

        private int CriarDimensionsNoRevit(
            Document doc,
            View vista,
            List<CotaFaixaBuilder.FaixaCotas> faixas,
            IReadOnlyList<FamilyInstance> barras,
            IReadOnlyList<(double X, double Z)> nosSuperior,
            IReadOnlyList<(double X, double Z)> nosInferior,
            (double XMin, double XMax, double ZMin, double ZMax, double Width, double Height) bbox,
            CotarTrelicaConfig config,
            ref List<string> avisos,
            ref int textosCriados)
        {
            int cotasCriadas = 0;

            foreach (var faixa in faixas)
            {
                try
                {
                    Logger.Info("[CotarTrelica.CriarDimensions] processando faixa {Tipo} com {N} segmentos",
                        faixa.Tipo, faixa.Segmentos.Count);

                    // Para faixa 5 (alturas), criar TextNotes verticais
                    if (faixa.Tipo == CotaFaixaBuilder.Faixa.AlturasMontantes)
                    {
                        foreach (var seg in faixa.Segmentos)
                        {
                            try
                            {
                                // Encontrar a barra no no superior (Y = X do segmento)
                                double xEstacao = seg.XInicio;
                                var barraSuperior = TrelicaRevitHelper.EncontrarBarraNoNo(
                                    barras, xEstacao, vista);
                                if (barraSuperior == null) continue;

                                var barraInferior = TrelicaRevitHelper.EncontrarBarraNoNo(
                                    barras, xEstacao, vista);
                                if (barraInferior == null) continue;

                                // Obter coordenadas Z (elevacao) dos nos
                                var noSup = nosSuperior.FirstOrDefault(p => Math.Abs(p.X - xEstacao) < 0.01);
                                var noInf = nosInferior.FirstOrDefault(p => Math.Abs(p.X - xEstacao) < 0.01);

                                if (noSup.X == 0 || noInf.X == 0) continue;

                                // Desprojetar para 3D
                                XYZ pos3DSup = TrelicaRevitHelper.DesprojetarPonto(noSup.X, noSup.Z, vista);
                                XYZ pos3DInf = TrelicaRevitHelper.DesprojetarPonto(noInf.X, noInf.Z, vista);

                                // Calcular altura em milimetros
                                double altura = pos3DSup.Z - pos3DInf.Z; // diferenca em pés
                                double alturaMm = UnitUtils.ConvertFromInternalUnits(altura, UnitTypeId.Millimeters);

                                // Criar TextNote com valor de altura
                                ElementId textTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                                XYZ posText = new XYZ((pos3DSup.X + pos3DInf.X) / 2.0, (pos3DSup.Y + pos3DInf.Y) / 2.0, pos3DSup.Z + 0.5);
                                string textoAltura = $"{alturaMm:F0}";
                                var tn = TrelicaRevitHelper.CriarTextoNota(doc, vista, posText, textoAltura, textTypeId);
                                if (tn != null) textosCriados++;
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "[CotarTrelica.CriarDimensions] falha criando altura em {X:F2}", seg.XInicio);
                            }
                        }
                        continue;
                    }

                    // Para outras faixas, criar Dimensions com References
                    var refs = new ReferenceArray();
                    foreach (var seg in faixa.Segmentos)
                    {
                        try
                        {
                            // Encontrar barra no inicio do segmento
                            var barraInicio = TrelicaRevitHelper.EncontrarBarraNoNo(
                                barras, seg.XInicio, vista);
                            if (barraInicio == null) continue;

                            var refInicio = TrelicaRevitHelper.ObterReferenciaExtremo(
                                barraInicio.Value.Barra, barraInicio.Value.Endpoint, vista);
                            if (refInicio != null)
                                refs.Append(refInicio);

                            // Para ultimo segmento, tambem obter referencia do fim
                            if (seg == faixa.Segmentos.Last())
                            {
                                var barraFim = TrelicaRevitHelper.EncontrarBarraNoNo(
                                    barras, seg.XFim, vista);
                                if (barraFim != null)
                                {
                                    var refFim = TrelicaRevitHelper.ObterReferenciaExtremo(
                                        barraFim.Value.Barra, barraFim.Value.Endpoint, vista);
                                    if (refFim != null)
                                        refs.Append(refFim);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "[CotarTrelica.CriarDimensions] falha coletando referencia em {X:F2}", seg.XInicio);
                        }
                    }

                    if (refs.Size == 0) continue;

                    // Calcular ponto e direcao da linha de cota
                    double zCota = bbox.ZMax + faixa.OffsetZPes; // default para faixas acima
                    if (faixa.OffsetZPes < 0)
                        zCota = bbox.ZMin + faixa.OffsetZPes; // para faixas abaixo

                    double xMeioBBox = (bbox.XMin + bbox.XMax) / 2.0;
                    XYZ dimLinePoint = TrelicaRevitHelper.DesprojetarPonto(xMeioBBox, zCota, vista);
                    XYZ dimLineDir = vista.RightDirection; // horizontal

                    var dim = TrelicaRevitHelper.CriarRunningDimension(doc, vista, refs, dimLinePoint, dimLineDir);
                    if (dim != null)
                    {
                        cotasCriadas += faixa.Segmentos.Count;
                        Logger.Debug("[CotarTrelica.CriarDimensions] faixa {Tipo} criada com sucesso",
                            faixa.Tipo);
                    }
                    else
                    {
                        avisos.Add($"Nao foi possivel criar dimension para faixa {faixa.Tipo}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[CotarTrelica.CriarDimensions] falha em faixa {Tipo}",
                        faixa.Tipo);
                    avisos.Add($"Falha criando cota de faixa {faixa.Tipo}: {ex.Message}");
                }
            }

            return cotasCriadas;
        }

        // =====================================================================
        // Passo 8: Criar Tags de perfil
        // =====================================================================

        private int CriarTagsDeBarra(
            Document doc,
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            CotarTrelicaConfig config,
            ref List<string> avisos)
        {
            int tagsCriadas = 0;

            try
            {
                foreach (var fi in barras)
                {
                    try
                    {
                        // Detectar perfil e multiplicador
                        string nomePerfil = LerNomeTipoPerfil(fi);
                        int multiplicador = DetectarMultiplicadorComposto(fi, config);

                        // Formatar nome
                        string perfilFormatado = TrelicaPerfilFormatter.Formatar(
                            nomePerfil, multiplicador);

                        // Tentar criar tag
                        if (TentarCriarTag(doc, vista, fi, perfilFormatado))
                        {
                            tagsCriadas++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "[CotarTrelica.CriarTags] falha criando tag para barra {Id}",
                            fi.Id.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[CotarTrelica.CriarTags] erro geral ao criar tags");
                avisos.Add($"Erro criando tags: {ex.Message}");
            }

            if (tagsCriadas < barras.Count)
            {
                avisos.Add($"Nem todas as tags foram criadas ({tagsCriadas}/{barras.Count}).");
            }

            return tagsCriadas;
        }

        // =====================================================================
        // Passo 9: Criar TextNotes de banzo
        // =====================================================================

        private int CriarTextosRotuloBanzos(
            Document doc,
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            Dictionary<FamilyInstance, TrelicaClassificador.TipoMembro> classificacao,
            IReadOnlyList<(double X, double Z)> nosSuperior,
            IReadOnlyList<(double X, double Z)> nosInferior,
            CotarTrelicaConfig config)
        {
            int textosCriados = 0;
            ElementId textTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            double offsetTextoPes = UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters);

            try
            {
                // Detectar perfil do banzo superior
                string perfilSuperior = DetectarPerfilBanzo(barras, classificacao,
                    TrelicaClassificador.TipoMembro.BanzoSuperior, config);

                // Detectar perfil do banzo inferior
                string perfilInferior = DetectarPerfilBanzo(barras, classificacao,
                    TrelicaClassificador.TipoMembro.BanzoInferior, config);

                // Posicionar texto "BANZO SUPERIOR <perfil>" acima do banzo
                if (!string.IsNullOrEmpty(perfilSuperior) && nosSuperior.Count >= 2)
                {
                    double xMeio = (nosSuperior.First().X + nosSuperior.Last().X) / 2.0;
                    double zSuperior = nosSuperior.Max(p => p.Z) + offsetTextoPes;
                    XYZ pos3D = TrelicaRevitHelper.DesprojetarPonto(xMeio, zSuperior, vista);

                    string texto = $"BANZO SUPERIOR {perfilSuperior}";
                    var tn = TrelicaRevitHelper.CriarTextoNota(doc, vista, pos3D, texto, textTypeId);
                    if (tn != null) textosCriados++;
                }

                // Posicionar texto "BANZO INFERIOR <perfil>" abaixo do banzo
                if (!string.IsNullOrEmpty(perfilInferior) && nosInferior.Count >= 2)
                {
                    double xMeio = (nosInferior.First().X + nosInferior.Last().X) / 2.0;
                    double zInferior = nosInferior.Min(p => p.Z) - offsetTextoPes;
                    XYZ pos3D = TrelicaRevitHelper.DesprojetarPonto(xMeio, zInferior, vista);

                    string texto = $"BANZO INFERIOR {perfilInferior}";
                    var tn = TrelicaRevitHelper.CriarTextoNota(doc, vista, pos3D, texto, textTypeId);
                    if (tn != null) textosCriados++;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[CotarTrelica.CriarTextos] erro ao criar textos de banzo");
            }

            return textosCriados;
        }

        // =====================================================================
        // Helpers privados
        // =====================================================================

        private string LerNomeTipoPerfil(FamilyInstance fi)
        {
            try
            {
                if (fi.Symbol == null) return "-";
                string family = fi.Symbol.Family?.Name ?? "";
                string type = fi.Symbol.Name ?? "";
                return $"{family} {type}".Trim();
            }
            catch
            {
                return "-";
            }
        }

        private int DetectarMultiplicadorComposto(FamilyInstance fi, CotarTrelicaConfig config)
        {
            if (!config.CantoneiraDupla) return 1;

            try
            {
                // Tentar ler shared parameter EMT_PerfilComposto
                Parameter param = fi.LookupParameter("EMT_PerfilComposto");
                if (param != null && param.AsInteger() > 0)
                    return 2;

                // Fallback: heuristica por nome
                string nomePerfil = LerNomeTipoPerfil(fi);
                if (TrelicaPerfilFormatter.EhCantoneira(nomePerfil))
                    return 2;
            }
            catch
            {
                // Ignorar erro, retornar 1
            }

            return 1;
        }

        private string DetectarPerfilBanzo(
            IReadOnlyList<FamilyInstance> barras,
            Dictionary<FamilyInstance, TrelicaClassificador.TipoMembro> classificacao,
            TrelicaClassificador.TipoMembro tipoBanzo,
            CotarTrelicaConfig config)
        {
            var barrasBanzo = barras.Where(b =>
                classificacao.TryGetValue(b, out var tipo) && tipo == tipoBanzo).ToList();

            if (barrasBanzo.Count == 0) return "";

            // Usar primeira barra como representativa (todas as barras do banzo devem ter mesmo perfil)
            var rep = barrasBanzo[0];
            string nomePerfil = LerNomeTipoPerfil(rep);
            int multiplicador = DetectarMultiplicadorComposto(rep, config);
            return TrelicaPerfilFormatter.Formatar(nomePerfil, multiplicador);
        }

        private bool TentarCriarTag(Document doc, View vista, FamilyInstance fi, string perfilText)
        {
            try
            {
                // Calcular posicao da tag: midpoint da barra com offset perpendicular
                double offsetTagPes = UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters);
                XYZ posicao = TrelicaRevitHelper.CalcularPosicaoTag(fi, vista, offsetTagPes);

                // Verificar se barra e curta (< 400mm) para usar leader
                bool barrasCurta = false;
                if (fi.Location is LocationCurve lc)
                {
                    double comprimentoMm = UnitUtils.ConvertFromInternalUnits(lc.Curve.Length, UnitTypeId.Millimeters);
                    barrasCurta = comprimentoMm < 400;
                }

                var tag = TrelicaRevitHelper.CriarTag(doc, vista, fi, posicao, comLeader: barrasCurta);
                if (tag != null)
                {
                    Logger.Debug("[CotarTrelica.CriarTag] tag criada para barra {Id}: {Perfil}",
                        fi.Id.Value, perfilText);
                    return true;
                }

                Logger.Warn("[CotarTrelica.CriarTag] tag nao criada para barra {Id}", fi.Id.Value);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[CotarTrelica.CriarTag] falha em barra {Id}", fi.Id.Value);
                return false;
            }
        }
    }
}
