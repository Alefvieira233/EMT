#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services.Trelica
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
        /// M4: encapsula a TRANSACAO (antes aberta no Command) — abre, SwallowWarnings,
        /// chama <see cref="Executar"/> (pipeline), commita; em erro faz rollback e relança.
        /// Mantem o Command fino e uniformiza a camada (transacao no service).
        /// </summary>
        public CotarTrelicaReport ExecutarComTransacao(
            UIDocument uidoc,
            Document doc,
            View vista,
            IReadOnlyList<FamilyInstance> barras,
            CotarTrelicaConfig config)
        {
            using (Transaction t = new Transaction(doc, "EMT - Cotar Treliça"))
            {
                t.Start();
                // Pipeline cria muitas Dimensions/Tags/TextNotes; warnings comuns ("dimension
                // outside view", "joined geometry...") nao podem bloquear o commit. Erros reais
                // (Severity != Warning) seguem normais.
                FailureHandlingHelper.SwallowWarnings(t);
                try
                {
                    CotarTrelicaReport report = Executar(uidoc, doc, vista, barras, config);
                    t.Commit();
                    return report;
                }
                catch
                {
                    if (t.HasStarted() && !t.HasEnded())
                        t.RollBack();
                    throw;
                }
            }
        }

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
                // v2.8.9 FIX: reutiliza a classificacao do passo 3 (que desambigua banzo
                // superior/inferior por altura). Antes o passo 4 re-classificava com
                // ClassificarPorInclinacao e a deteccao SEMPRE falhava (ver ExtrairNosBanzo).
                Logger.Info("[CotarTrelica] passo 4 — extraindo nos dos banzos");
                var nosSuperior = ExtrairNosBanzo(vista, barras, barrasClassificadas,
                    TrelicaClassificador.TipoMembro.BanzoSuperior);
                var nosInferior = ExtrairNosBanzo(vista, barras, barrasClassificadas,
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
            if (lista.Count == 0)
                return (0, 0, 0, 0, 0, 0);

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
            IReadOnlyDictionary<FamilyInstance, TrelicaClassificador.TipoMembro> classificacao,
            TrelicaClassificador.TipoMembro tipoBanzo)
        {
            // v2.8.9 FIX (P0 — funcao 100% inoperante): este metodo re-classificava cada
            // barra com ClassificarPorInclinacao, que SO devolve BanzoIndefinido/Montante/
            // Diagonal — NUNCA BanzoSuperior/Inferior. Logo `tipo == tipoBanzo` era SEMPRE
            // falso, nosSuperior/nosInferior saiam VAZIOS e o pipeline abortava em "banzos
            // nao detectados" para TODA trelica. Agora reutiliza a classificacao do passo 3
            // (que ja desambigua por altura via ClassificarBanzoPorAltura) e delega a coleta
            // ao helper puro TrelicaGeometria.ColetarNosDoBanzo (testavel sem Revit).
            XYZ u = vista.RightDirection;
            XYZ v = vista.UpDirection;
            XYZ origem = vista.Origin;

            var entrada =
                new List<(TrelicaClassificador.TipoMembro Tipo, (double X, double Z) P0, (double X, double Z) P1)>();

            foreach (var fi in barras)
            {
                try
                {
                    if (!classificacao.TryGetValue(fi, out var tipo) || tipo != tipoBanzo)
                        continue;

                    if (fi.Location is LocationCurve locCurve && locCurve.Curve is Curve curve)
                    {
                        XYZ a = curve.GetEndPoint(0);
                        XYZ b = curve.GetEndPoint(1);
                        (double, double) p0 = (u.DotProduct(a - origem), v.DotProduct(a - origem));
                        (double, double) p1 = (u.DotProduct(b - origem), v.DotProduct(b - origem));
                        entrada.Add((tipo, p0, p1));
                    }
                }
                catch
                {
                    // Ignorar erros de barra individual
                }
            }

            return TrelicaGeometria.ColetarNosDoBanzo(entrada, tipoBanzo);
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

                if (config.CotarVaosEntreApoios && xInferior.Count >= 2)
                {
                    // v2.8.9 FIX: apoios = extremos do banzo INFERIOR (a trelica apoia
                    // embaixo). Antes usava os extremos do banzo superior, que pode ter
                    // balanco/beiral em treliça de duas aguas — vao entre apoios errado.
                    var (xEsq, xDir) = TrelicaGeometria.ExtremosApoio(xInferior);
                    faixas.Add(CotaFaixaBuilder.FaixaVaosEntreApoios(
                        new[] { xEsq, xDir }.ToList(), offsetPes * 2.0));
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
                        var xsSup = nosSuperior.Select(p => p.X).ToList();
                        var xsInf = nosInferior.Select(p => p.X).ToList();
                        const double tolEstacaoPes = 0.05; // ~15 mm — casa estacao do montante ao no do banzo

                        foreach (var seg in faixa.Segmentos)
                        {
                            try
                            {
                                // Estacao X do montante (segmento degenerado: XInicio == XFim).
                                double xEstacao = seg.XInicio;

                                // v2.8.9 FIX: localizar nos por indice (nao FirstOrDefault +
                                // sentinela ".X==0", que descartava nos legitimamente em X≈0 e
                                // aceitava "nao encontrado" — tuple default — como valido com Z=0).
                                int iSup = TrelicaGeometria.IndiceNoMaisProximo(xsSup, xEstacao, tolEstacaoPes);
                                int iInf = TrelicaGeometria.IndiceNoMaisProximo(xsInf, xEstacao, tolEstacaoPes);
                                if (iSup < 0 || iInf < 0)
                                    continue;

                                var noSup = nosSuperior[iSup];
                                var noInf = nosInferior[iInf];

                                // v2.8.9 FIX: altura = separacao vertical 2D entre os banzos na
                                // estacao (a coordenada Z 2D ja e' a vertical da elevacao). Antes
                                // reconstruia 3D via DesprojetarPonto e lia .Z MUNDIAL — so coincidia
                                // quando UpDirection == +Z mundial (quebrava em corte rotacionado).
                                double alturaFt = Math.Abs(noSup.Z - noInf.Z);
                                double alturaMm = UnitUtils.ConvertFromInternalUnits(alturaFt, UnitTypeId.Millimeters);
                                if (alturaMm < 1.0)
                                    continue;

                                // Texto no meio do montante, posicionado no plano da vista.
                                ElementId textTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                                double xMeio = (noSup.X + noInf.X) / 2.0;
                                double zMeio = (noSup.Z + noInf.Z) / 2.0;
                                XYZ posText = TrelicaRevitHelper.DesprojetarPonto(xMeio, zMeio, vista);
                                string textoAltura = $"{alturaMm:F0}";
                                var tn = TrelicaRevitHelper.CriarTextoNota(doc, vista, posText, textoAltura, textTypeId);
                                if (tn != null)
                                    textosCriados++;
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
                            if (barraInicio == null)
                                continue;

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

                    if (refs.Size == 0)
                        continue;

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
                        // v2.8.9 FIX: 1 Dimension encadeada por faixa. Antes somava
                        // faixa.Segmentos.Count, inflando "cotas criadas" no relatorio.
                        cotasCriadas++;
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
                    if (tn != null)
                        textosCriados++;
                }

                // Posicionar texto "BANZO INFERIOR <perfil>" abaixo do banzo
                if (!string.IsNullOrEmpty(perfilInferior) && nosInferior.Count >= 2)
                {
                    double xMeio = (nosInferior.First().X + nosInferior.Last().X) / 2.0;
                    double zInferior = nosInferior.Min(p => p.Z) - offsetTextoPes;
                    XYZ pos3D = TrelicaRevitHelper.DesprojetarPonto(xMeio, zInferior, vista);

                    string texto = $"BANZO INFERIOR {perfilInferior}";
                    var tn = TrelicaRevitHelper.CriarTextoNota(doc, vista, pos3D, texto, textTypeId);
                    if (tn != null)
                        textosCriados++;
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
                if (fi.Symbol == null)
                    return "-";
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
            if (!config.CantoneiraDupla)
                return 1;

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

            if (barrasBanzo.Count == 0)
                return "";

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
