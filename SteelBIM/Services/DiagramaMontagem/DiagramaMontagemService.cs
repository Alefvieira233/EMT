using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models.DiagramaMontagem;

namespace SteelBIM.Services.DiagramaMontagem
{
    public class DiagramaMontagemService
    {
        // ============================================
        // 2A. METODO PUBLICO PRINCIPAL — ORQUESTRADOR
        // ============================================
        public DiagramaMontagemResultado Executar(
            UIDocument uidoc,
            IList<ElementId> idsSelecionados,
            DiagramaMontagemConfig config)
        {
            Logger.Info("[DiagramaMontagem] Iniciando — {Count} elementos", idsSelecionados.Count);

            var resultado = new DiagramaMontagemResultado();
            Document doc = uidoc.Document;

            try
            {
                // 1) Coletar elementos validos
                List<Element> elementos = new List<Element>();
                foreach (ElementId eid in idsSelecionados)
                {
                    Element e = doc.GetElement(eid);
                    if (e != null)
                        elementos.Add(e);
                }

                if (elementos.Count == 0)
                {
                    resultado.Mensagem = "Nenhum elemento valido na selecao.";
                    return resultado;
                }

                // 2) Detectar plano e bbox da selecao
                Transform sectionTransform;
                BoundingBoxXYZ sectionBbox;
                DetectarPlanoSelecao(elementos, config, out sectionTransform, out sectionBbox);

                // 3) Criar Section View (em transaction propria — Revit exige)
                // v2.6.9: nome contextual quando vista superior (planta) — facilita
                // identificacao no Project Browser.
                // v2.8.0 F12 (Wave 3): logica de naming extraida pra DiagramaMontagemViewNamer (testado).
                string nomeBase = DiagramaMontagemViewNamer.BuildContextualName(config.NomeVista, config.Orientacao);

                ViewSection vista;
                using (Transaction tx1 = new Transaction(doc, "Criar vista do Diagrama de Montagem"))
                {
                    tx1.Start();
                    vista = CriarSectionView(doc, sectionBbox, nomeBase);
                    if (vista == null)
                    {
                        tx1.RollBack();
                        resultado.Mensagem = "Nao foi possivel criar a Section View. Verifique se existe ViewFamilyType de Section no projeto.";
                        return resultado;
                    }
                    tx1.Commit();
                }

                resultado.VistaCriadaId = vista.Id;
                resultado.NomeVistaCriada = vista.Name;

                // 4) Cropar para os elementos selecionados (transaction separada)
                using (Transaction tx2 = new Transaction(doc, "Cropar vista para selecao"))
                {
                    tx2.Start();
                    AplicarCropToElementos(doc, vista, elementos, config.MargemMm);
                    tx2.Commit();
                }

                // 5) Eixos visiveis (se config pediu)
                if (config.MostrarEixos)
                {
                    using (Transaction tx3 = new Transaction(doc, "Ajustar eixos na vista"))
                    {
                        tx3.Start();
                        resultado.EixosVisiveis = AjustarVisibilidadeEixos(doc, vista);
                        tx3.Commit();
                    }
                }

                // 6) Cotas entre eixos consecutivos
                if (config.AdicionarCotasEntreEixos && resultado.EixosVisiveis >= 2)
                {
                    using (Transaction tx4 = new Transaction(doc, "Adicionar cotas entre eixos"))
                    {
                        tx4.Start();
                        resultado.CotasCriadas = CriarCotasEntreEixos(doc, vista);
                        tx4.Commit();
                    }
                }

                // 7) Tags com Mark em cada elemento
                if (config.AdicionarTagsMarca)
                {
                    using (Transaction tx5 = new Transaction(doc, "Adicionar tags com marcas"))
                    {
                        tx5.Start();
                        int comMark, semMark;
                        AdicionarTagsMarca(doc, vista, elementos, out comMark, out semMark);
                        resultado.TagsCriadas = comMark;
                        resultado.TagsSemMark = semMark;
                        tx5.Commit();
                    }
                }

                // 8) Cotas verticais (SpotElevation em niveis clusterizados)
                // v2.6.9: skip em vista superior (planta) — SpotElevation mostra
                // altura Z, conceito que nao faz sentido em planta XY.
                if (config.AdicionarCotasVerticais && config.Orientacao != OrientacaoDiagrama.Superior)
                {
                    using (Transaction tx6 = new Transaction(doc, "Cotas verticais"))
                    {
                        tx6.Start();
                        resultado.CotasVerticais = CriarCotasVerticais(
                            doc, vista, elementos, config.ToleranciaClusterizacaoMm);
                        tx6.Commit();
                    }
                }
                else if (config.AdicionarCotasVerticais && config.Orientacao == OrientacaoDiagrama.Superior)
                {
                    Logger.Debug("[DiagramaMontagem] SpotElevation skip — vista superior (planta) nao suporta cotas verticais.");
                }

                // 9) Cota total do conjunto
                if (config.AdicionarCotaTotalConjunto && resultado.EixosVisiveis >= 2)
                {
                    using (Transaction tx7 = new Transaction(doc, "Cota total conjunto"))
                    {
                        tx7.Start();
                        resultado.CotaTotalConjunto = CriarCotaTotalConjunto(doc, vista);
                        tx7.Commit();
                    }
                }

                // 10) Simbolo de nivel (Levels do projeto)
                if (config.MostrarSimboloDeNivel)
                {
                    using (Transaction tx8 = new Transaction(doc, "Mostrar simbolo de nivel"))
                    {
                        tx8.Start();
                        resultado.NiveisVisiveis = AjustarVisibilidadeNiveis(doc, vista, elementos);
                        tx8.Commit();
                    }
                }

                // 11) Comprimentos individuais (v2.6.6: offset adaptativo + clearance configuravel)
                if (config.AdicionarComprimentosIndividuais)
                {
                    using (Transaction tx9 = new Transaction(doc, "Comprimentos individuais"))
                    {
                        tx9.Start();
                        resultado.ComprimentosCriados = CriarComprimentosIndividuais(doc, vista, elementos, config);
                        tx9.Commit();
                    }
                }

                // 12) Insercao em folha (POR ULTIMO — depende da vista pronta)
                if (config.ColocarEmFolha)
                {
                    using (Transaction tx10 = new Transaction(doc, "Inserir em folha"))
                    {
                        tx10.Start();
                        ElementId folhaId = ColocarVistaEmFolha(doc, vista, config, out string nomeFolha);
                        if (folhaId != ElementId.InvalidElementId)
                        {
                            resultado.FolhaCriada = true;
                            resultado.FolhaCriadaId = folhaId;
                            resultado.NomeFolhaCriada = nomeFolha;
                        }
                        else
                        {
                            resultado.Avisos.Add("Nao foi possivel criar folha. Verifique se ha TitleBlock disponivel no projeto.");
                        }
                        tx10.Commit();
                    }
                }

                // 13) Abrir vista para o usuario
                uidoc.ActiveView = vista;

                resultado.Sucesso = true;
                resultado.Mensagem =
                    $"Diagrama criado: '{vista.Name}'.\n" +
                    $"Eixos visiveis: {resultado.EixosVisiveis}\n" +
                    $"Niveis visiveis: {resultado.NiveisVisiveis}\n" +
                    $"Cotas entre eixos: {resultado.CotasCriadas}\n" +
                    $"Cotas verticais: {resultado.CotasVerticais}\n" +
                    (resultado.CotaTotalConjunto ? "Cota total do conjunto: SIM\n" : "Cota total do conjunto: NAO\n") +
                    $"Tags com marca: {resultado.TagsCriadas} (sem Mark: {resultado.TagsSemMark})\n" +
                    (config.AdicionarComprimentosIndividuais ? $"Comprimentos individuais: {resultado.ComprimentosCriados}\n" : "") +
                    (resultado.FolhaCriada ? $"Folha: '{resultado.NomeFolhaCriada}' criada\n" : "");

                if (resultado.TagsSemMark > 0)
                {
                    resultado.Avisos.Add(
                        $"{resultado.TagsSemMark} elemento(s) sem parametro Mark. " +
                        "Use 'Marcar Pecas' antes para atribuir marcas automaticamente.");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[DiagramaMontagem] Falha geral");
                resultado.Mensagem = ex.Message;
                return resultado;
            }
        }

        // ============================================
        // 2B. DETECTAR PLANO E BBOX DA SELECAO
        // ============================================
        private void DetectarPlanoSelecao(
            List<Element> elementos,
            DiagramaMontagemConfig config,
            out Transform sectionTransform,
            out BoundingBoxXYZ sectionBbox)
        {
            // Coletar pontos de todos os elementos
            List<XYZ> pontos = new List<XYZ>();
            foreach (Element e in elementos)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    pontos.Add(bb.Min);
                    pontos.Add(bb.Max);
                }
                else if (e.Location is LocationCurve lc && lc.Curve != null)
                {
                    pontos.Add(lc.Curve.GetEndPoint(0));
                    pontos.Add(lc.Curve.GetEndPoint(1));
                }
                else if (e.Location is LocationPoint lp)
                {
                    pontos.Add(lp.Point);
                }
            }

            if (pontos.Count == 0)
            {
                sectionTransform = Transform.Identity;
                sectionBbox = new BoundingBoxXYZ();
                return;
            }

            double minX = pontos.Min(p => p.X);
            double maxX = pontos.Max(p => p.X);
            double minY = pontos.Min(p => p.Y);
            double maxY = pontos.Max(p => p.Y);
            double minZ = pontos.Min(p => p.Z);
            double maxZ = pontos.Max(p => p.Z);

            double extX = maxX - minX;
            double extY = maxY - minY;

            // v2.6.9: calculo do Transform + bbox local delegado ao helper puro
            // SectionBoxBuilder (testavel sem Revit). Mantem 2 branches:
            //  - Superior: vista de planta (-Z observador)
            //  - Elevacao: comportamento v2.3.0+ (Paralelo X ou Y)
            double margemFt = UnitUtils.ConvertToInternalUnits(config.MargemMm, UnitTypeId.Millimeters);
            Vec3 bbMin = new Vec3(minX, minY, minZ);
            Vec3 bbMax = new Vec3(maxX, maxY, maxZ);
            SectionBoxData boxData;

            if (config.Orientacao == OrientacaoDiagrama.Superior)
            {
                boxData = SectionBoxBuilder.CalcularPlanta(bbMin, bbMax, margemFt);
            }
            else
            {
                // Decidir entre Paralelo X / Paralelo Y (Auto = baseia na geometria).
                bool paraleloAoX;
                if (config.Orientacao == OrientacaoDiagrama.Auto)
                    paraleloAoX = extX >= extY;
                else
                    paraleloAoX = (config.Orientacao == OrientacaoDiagrama.ParaleloEixoX);

                boxData = SectionBoxBuilder.CalcularElevacao(bbMin, bbMax, margemFt, paraleloAoX);
            }

            // Materializa SectionBoxData (puro) em Transform + BoundingBoxXYZ do Revit
            sectionTransform = Transform.Identity;
            sectionTransform.Origin = new XYZ(boxData.OrigemTransform.X, boxData.OrigemTransform.Y, boxData.OrigemTransform.Z);
            sectionTransform.BasisX = new XYZ(boxData.BasisX.X, boxData.BasisX.Y, boxData.BasisX.Z);
            sectionTransform.BasisY = new XYZ(boxData.BasisY.X, boxData.BasisY.Y, boxData.BasisY.Z);
            sectionTransform.BasisZ = new XYZ(boxData.BasisZ.X, boxData.BasisZ.Y, boxData.BasisZ.Z);

            sectionBbox = new BoundingBoxXYZ();
            sectionBbox.Transform = sectionTransform;
            sectionBbox.Min = new XYZ(boxData.BboxMin.X, boxData.BboxMin.Y, boxData.BboxMin.Z);
            sectionBbox.Max = new XYZ(boxData.BboxMax.X, boxData.BboxMax.Y, boxData.BboxMax.Z);
        }

        // ============================================
        // 2C. CRIAR SECTION VIEW
        // ============================================
        private ViewSection CriarSectionView(Document doc, BoundingBoxXYZ sectionBbox, string nomeBase)
        {
            ViewFamilyType vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

            if (vft == null)
            {
                Logger.Error("[DiagramaMontagem] Nenhum ViewFamilyType de Section disponivel");
                return null;
            }

            ViewSection section = ViewSection.CreateSection(doc, vft.Id, sectionBbox);
            if (section == null)
                return null;

            // v2.6.9: Revit as vezes recalcula BasisY apos CreateSection quando o
            // Transform tem orientacao incomum (ex: vista superior com BasisZ=-Z).
            // Verificamos se UpDirection bate com o BasisY do Transform que
            // pedimos; se nao bater, tentamos forcar. Setter publico pode nao
            // estar disponivel — try/catch silencioso, smoke visual confirma.
            try
            {
                XYZ expectedUp = sectionBbox.Transform?.BasisY;
                if (expectedUp != null && section.UpDirection != null)
                {
                    XYZ actualUp = section.UpDirection;
                    if (Math.Abs(actualUp.X - expectedUp.X) > 1e-6 ||
                        Math.Abs(actualUp.Y - expectedUp.Y) > 1e-6 ||
                        Math.Abs(actualUp.Z - expectedUp.Z) > 1e-6)
                    {
                        Logger.Debug(
                            "[DiagramaMontagem] UpDirection da vista divergiu do BasisY pedido " +
                            "({Actual} vs {Expected}). Smoke visual valida orientacao.",
                            actualUp, expectedUp);
                    }
                }
            }
            catch (Exception exUp)
            {
                Logger.Debug("[DiagramaMontagem] Falha ao verificar UpDirection: {Msg} — ignorando", exUp.Message);
            }

            // Renomear (Revit pode rejeitar nome duplicado — fallback com sufixo)
            string nomeFinal = nomeBase;
            int sufixo = 1;
            while (NomeJaUsado(doc, nomeFinal))
            {
                nomeFinal = $"{nomeBase} ({sufixo++})";
                if (sufixo > 99)
                    break; // sanity
            }
            try
            { section.Name = nomeFinal; }
            catch { /* Revit pode rejeitar caracteres invalidos — manter nome auto */ }

            // Escala 1:75 (padrao das pranchas BR)
            try
            { section.Scale = 75; }
            catch (Exception exScale)
            {
                // v2.8.7 (auditoria arquitetura): vista pode ter scale travada por
                // ViewTemplate ou ser categoria que nao aceita scale livre — fica
                // com default do Revit, nao e' bloqueante.
                SteelBIM.Infrastructure.Logger.Debug("[DiagramaMontagem] nao foi possivel setar Scale=75 em {Nome}: {Msg}", section.Name, exScale.Message);
            }

            return section;
        }

        private bool NomeJaUsado(Document doc, string nome)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => string.Equals(v.Name, nome, StringComparison.OrdinalIgnoreCase));
        }

        // ============================================
        // 2D. APLICAR CROP PARA OS ELEMENTOS
        // ============================================
        private void AplicarCropToElementos(Document doc, ViewSection vista, List<Element> elementos, double margemMm)
        {
            vista.CropBoxActive = true;
            vista.CropBoxVisible = false; // esconder a moldura do crop

            // Crop ja foi definido pela CreateSection, mas podemos refinar se necessario
            // Para MVP, deixamos o crop como saiu da CreateSection (ja com a margem aplicada
            // no BBox passado).
        }

        // ============================================
        // 2E. AJUSTAR VISIBILIDADE DOS EIXOS
        // ============================================
        private int AjustarVisibilidadeEixos(Document doc, ViewSection vista)
        {
            IList<Grid> grids = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            int visiveis = 0;
            foreach (Grid g in grids)
            {
                try
                {
                    // Mostrar bubble nos dois lados da vista
                    g.ShowBubbleInView(DatumEnds.End0, vista);
                    g.ShowBubbleInView(DatumEnds.End1, vista);
                    visiveis++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha ao ajustar eixo {Name}", g.Name);
                }
            }

            return visiveis;
        }

        // ============================================
        // 2F. CRIAR COTAS ENTRE EIXOS CONSECUTIVOS
        // ============================================
        private int CriarCotasEntreEixos(Document doc, ViewSection vista)
        {
            // Coletar grids visiveis na vista, ordenados pela posicao na direcao "right" da vista
            XYZ rightDir = vista.RightDirection;
            XYZ origin = vista.Origin;

            var gridsComOrdem = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(g => new
                {
                    Grid = g,
                    Ordem = ProjetarPontoNaDirecao(GridPosicaoBase(g), origin, rightDir)
                })
                .Where(x => x.Grid.Curve != null)
                .OrderBy(x => x.Ordem)
                .ToList();

            if (gridsComOrdem.Count < 2)
                return 0;

            int cotasOk = 0;

            // Pegar um ponto Y (altura) acima do conjunto para colocar a linha de cota
            BoundingBoxXYZ cropBox = vista.CropBox;
            XYZ topo = vista.Origin + vista.UpDirection * (cropBox.Max.Y + UnitUtils.ConvertToInternalUnits(500.0, UnitTypeId.Millimeters));

            for (int i = 0; i < gridsComOrdem.Count - 1; i++)
            {
                Grid g1 = gridsComOrdem[i].Grid;
                Grid g2 = gridsComOrdem[i + 1].Grid;

                try
                {
                    ReferenceArray refs = new ReferenceArray();
                    refs.Append(new Reference(g1));
                    refs.Append(new Reference(g2));

                    // Linha de cota: na altura "topo", do ponto do g1 ao do g2
                    XYZ p1 = ProjetarParaTopo(GridPosicaoBase(g1), topo, vista.UpDirection);
                    XYZ p2 = ProjetarParaTopo(GridPosicaoBase(g2), topo, vista.UpDirection);
                    Line linhaCota = Line.CreateBound(p1, p2);

                    Dimension dim = doc.Create.NewDimension(vista, linhaCota, refs);
                    if (dim != null)
                        cotasOk++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha ao cotar entre {G1} e {G2}", g1.Name, g2.Name);
                }
            }

            return cotasOk;
        }

        private XYZ GridPosicaoBase(Grid g)
        {
            if (g.Curve is Line lin)
                return lin.GetEndPoint(0);
            return g.Curve.Evaluate(0.5, true);
        }

        private double ProjetarPontoNaDirecao(XYZ ponto, XYZ origem, XYZ direcao)
        {
            return (ponto - origem).DotProduct(direcao);
        }

        private XYZ ProjetarParaTopo(XYZ ponto, XYZ topo, XYZ upDir)
        {
            // Preserva o X do ponto original mas usa Y do topo
            double yTopo = topo.DotProduct(upDir);
            XYZ pontoNoUp = upDir * yTopo;
            XYZ horizontal = ponto - upDir * ponto.DotProduct(upDir);
            return horizontal + pontoNoUp;
        }

        // ============================================
        // 2G. ADICIONAR TAGS COM MARK
        // ============================================
        private void AdicionarTagsMarca(
            Document doc,
            ViewSection vista,
            List<Element> elementos,
            out int comMark,
            out int semMark)
        {
            comMark = 0;
            semMark = 0;

            foreach (Element e in elementos)
            {
                try
                {
                    Parameter mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    string markValue = mark?.AsString();
                    if (string.IsNullOrWhiteSpace(markValue))
                        semMark++;
                    else
                        comMark++;

                    // Ponto de tag: centro do bbox em coords da vista
                    BoundingBoxXYZ bb = e.get_BoundingBox(vista);
                    if (bb == null)
                        continue;
                    XYZ centro = (bb.Min + bb.Max) * 0.5;

                    IndependentTag.Create(
                        doc,
                        vista.Id,
                        new Reference(e),
                        false, // sem leader
                        TagMode.TM_ADDBY_CATEGORY,
                        TagOrientation.Horizontal,
                        centro);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha ao tag elemento {Id}", e.Id);
                }
            }
        }

        // ============================================
        // 2H. COTAS VERTICAIS (SpotElevation clusterizado) — v2.4.0
        // ============================================
        /// <summary>
        /// Cria SpotElevation em niveis chave dos elementos selecionados:
        /// pontos onde ha viga, mudanca de elevacao, base/topo pilar.
        /// Clusteriza pontos com elevacao Z proxima (tolerancia em mm).
        /// Limita entre 3 e 15 cotas para nao poluir.
        /// </summary>
        private int CriarCotasVerticais(Document doc, ViewSection vista, List<Element> elementos, double tolMm)
        {
            // Coletar TODOS os pontos Z relevantes
            var pontosZ = new List<double>();
            foreach (Element e in elementos)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    pontosZ.Add(bb.Min.Z);
                    pontosZ.Add(bb.Max.Z);
                }
                else if (e.Location is LocationCurve lc && lc.Curve != null)
                {
                    pontosZ.Add(lc.Curve.GetEndPoint(0).Z);
                    pontosZ.Add(lc.Curve.GetEndPoint(1).Z);
                }
            }
            if (pontosZ.Count == 0)
                return 0;

            // Clusterizar
            double tolFt = UnitUtils.ConvertToInternalUnits(tolMm, UnitTypeId.Millimeters);
            pontosZ = pontosZ.OrderBy(z => z).ToList();
            var clusters = new List<double>();
            double zClusterAtual = pontosZ[0];
            clusters.Add(zClusterAtual);
            foreach (double z in pontosZ.Skip(1))
            {
                if (z - zClusterAtual > tolFt)
                {
                    clusters.Add(z);
                    zClusterAtual = z;
                }
            }

            // Limitar entre 3 e 15 cotas (regra do plano — nao poluir)
            if (clusters.Count > 15)
            {
                // Reamostrar uniformemente para 15
                var reduzido = new List<double>();
                double passo = (clusters.Count - 1) / 14.0;
                for (int i = 0; i < 15; i++)
                    reduzido.Add(clusters[(int)Math.Round(i * passo)]);
                clusters = reduzido.Distinct().ToList();
            }

            // Posicionar SpotElevations a direita da extensao da vista
            BoundingBoxXYZ bbVista = vista.get_BoundingBox(null);
            if (bbVista == null)
                return 0;

            double xDireita = bbVista.Max.X + UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters); // 80cm a direita

            int cotasOk = 0;
            foreach (double zCluster in clusters)
            {
                try
                {
                    // Encontrar um elemento que tem face nesse nivel Z
                    Element refElem = null;
                    foreach (Element e in elementos)
                    {
                        BoundingBoxXYZ bb = e.get_BoundingBox(null);
                        if (bb == null)
                            continue;
                        if (Math.Abs(bb.Min.Z - zCluster) < tolFt || Math.Abs(bb.Max.Z - zCluster) < tolFt)
                        {
                            refElem = e;
                            break;
                        }
                    }
                    if (refElem == null)
                        continue;

                    Reference refE = new Reference(refElem);
                    XYZ pontoElbow = new XYZ(xDireita, 0, zCluster);
                    XYZ pontoTexto = new XYZ(xDireita + UnitUtils.ConvertToInternalUnits(200, UnitTypeId.Millimeters), 0, zCluster);
                    XYZ pontoCota = new XYZ(xDireita, 0, zCluster);

                    SpotDimension sd = doc.Create.NewSpotElevation(
                        vista, refE,
                        new XYZ(xDireita, 0, zCluster),
                        pontoElbow,
                        pontoTexto,
                        pontoCota,
                        true /* hasLeader */);

                    if (sd != null)
                        cotasOk++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha SpotElevation Z={Z}", zCluster);
                }
            }

            return cotasOk;
        }

        // ============================================
        // 2I. COTA TOTAL DO CONJUNTO — v2.4.0
        // ============================================
        /// <summary>
        /// Cria uma cota linear total entre o eixo mais a esquerda e o mais a direita visiveis na vista.
        /// Linha de cota fica ACIMA da linha de cotas entre eixos consecutivos.
        /// </summary>
        private bool CriarCotaTotalConjunto(Document doc, ViewSection vista)
        {
            try
            {
                XYZ rightDir = vista.RightDirection;
                XYZ origin = vista.Origin;

                var gridsComOrdem = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .Where(g => g.Curve != null)
                    .Select(g => new
                    {
                        Grid = g,
                        Ordem = ProjetarPontoNaDirecao(GridPosicaoBase(g), origin, rightDir)
                    })
                    .OrderBy(x => x.Ordem)
                    .ToList();

                if (gridsComOrdem.Count < 2)
                    return false;

                Grid primeiro = gridsComOrdem.First().Grid;
                Grid ultimo = gridsComOrdem.Last().Grid;

                ReferenceArray refs = new ReferenceArray();
                refs.Append(new Reference(primeiro));
                refs.Append(new Reference(ultimo));

                // Linha de cota 1m mais alta que a das cotas entre eixos
                BoundingBoxXYZ cropBox = vista.CropBox;
                double yTopoFt = cropBox.Max.Y + UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
                XYZ p1 = ProjetarParaTopo(GridPosicaoBase(primeiro), vista.Origin + vista.UpDirection * yTopoFt, vista.UpDirection);
                XYZ p2 = ProjetarParaTopo(GridPosicaoBase(ultimo), vista.Origin + vista.UpDirection * yTopoFt, vista.UpDirection);
                Line linhaCota = Line.CreateBound(p1, p2);

                Dimension dim = doc.Create.NewDimension(vista, linhaCota, refs);
                return dim != null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[DiagramaMontagem] Falha cota total conjunto");
                return false;
            }
        }

        // ============================================
        // 2J. SIMBOLO DE NIVEL (Levels) — v2.4.0
        // ============================================
        /// <summary>
        /// Mostra os Levels do projeto que cruzam o range Z da selecao,
        /// trazendo seu bubble visivel na vista (igual aos Grids).
        /// </summary>
        private int AjustarVisibilidadeNiveis(Document doc, ViewSection vista, List<Element> elementos)
        {
            // Determinar range Z da selecao
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (Element e in elementos)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb == null)
                    continue;
                if (bb.Min.Z < minZ)
                    minZ = bb.Min.Z;
                if (bb.Max.Z > maxZ)
                    maxZ = bb.Max.Z;
            }
            if (minZ == double.MaxValue)
                return 0;

            double tolFt = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters); // 50cm fora do range tb mostra

            IList<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Where(l => l.Elevation >= minZ - tolFt && l.Elevation <= maxZ + tolFt)
                .ToList();

            int visiveis = 0;
            foreach (Level l in levels)
            {
                try
                {
                    l.ShowBubbleInView(DatumEnds.End0, vista);
                    l.ShowBubbleInView(DatumEnds.End1, vista);
                    visiveis++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha Level bubble {Name}", l.Name);
                }
            }

            return visiveis;
        }

        // ============================================
        // 2K. COMPRIMENTOS INDIVIDUAIS
        //   v2.4.0: TextNote experimental
        //   v2.6.5: Dimension real + ValueOverride 5mm threshold
        //   v2.6.6: offset ADAPTATIVO (halfSectionPerp + clearance configuravel)
        // ============================================
        /// <summary>
        /// Cria uma <see cref="Dimension"/> REAL ao lado de cada peca estrutural,
        /// usando <see cref="FamilyInstance.GetReferences"/>(Left/Right) — mesmo
        /// padrao do <c>CotarPecaFabricacaoService.CriarCotaViaFamilyRefs</c>.
        ///
        /// v2.6.6: a linha da cota fica sempre a <c>config.ClearanceCotaIndividualMm</c>
        /// (default 35mm) da face externa do perfil — independente do tamanho da
        /// seccao. Le <c>STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH</c> do FamilySymbol
        /// e delega o calculo do offset ao helper puro <see cref="DimensionPlanCalculator"/>.
        ///
        /// Quando o comprimento medido divergir mais de 5mm do comprimento de
        /// fabricacao (STRUCTURAL_FRAME_CUT_LENGTH), aplica <c>ValueOverride</c>
        /// para mostrar o valor de fabricacao — evitando que cotas mostrem
        /// 1224mm quando a peca sera cortada em 1215mm.
        /// </summary>
        private int CriarComprimentosIndividuais(Document doc, ViewSection vista, List<Element> elementos, DiagramaMontagemConfig config)
        {
            int criados = 0;

            // clearance da config -> ft (Revit internal units)
            double clearanceFt = UnitUtils.ConvertToInternalUnits(
                config.ClearanceCotaIndividualMm, UnitTypeId.Millimeters);

            // viewNormal = direcao perpendicular ao plano da vista (ViewDirection
            // aponta para fora do plano da Section View).
            XYZ vd = vista.ViewDirection;
            Vec3 viewNormal = new Vec3(vd.X, vd.Y, vd.Z);

            foreach (Element e in elementos)
            {
                try
                {
                    if (!(e is FamilyInstance elem))
                        continue;
                    if (!(e.Location is LocationCurve lc) || lc.Curve == null)
                        continue;

                    // Endpoints da peca (em coords do modelo)
                    XYZ pa = lc.Curve.GetEndPoint(0);
                    XYZ pb = lc.Curve.GetEndPoint(1);
                    Vec3 p1 = new Vec3(pa.X, pa.Y, pa.Z);
                    Vec3 p2 = new Vec3(pb.X, pb.Y, pb.Z);

                    // v2.6.6: ler seccao real do perfil (STRUCTURAL_SECTION_COMMON_*)
                    // -> offset adaptativo. Caller passa 0/0 se nao encontrar params
                    // standard; helper aplica fallback de 100mm.
                    double depthFt = LerSectionCommonParam(elem, BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
                    double widthFt = LerSectionCommonParam(elem, BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
                    if (depthFt <= 0 && widthFt <= 0)
                    {
                        Logger.Debug(
                            "[DiagramaMontagem] Peca {Id} sem parametros standard de secao (STRUCTURAL_SECTION_COMMON_*). " +
                            "Usando fallback 100mm. Considerar usar familia estrutural padrao.",
                            e.Id);
                    }

                    // Plano da Line de cota (origem + direcao) — calculo puro adaptativo
                    PlanoCotaResult plano;
                    try
                    {
                        plano = DimensionPlanCalculator.CalcularPlanoCota(
                            p1, p2, viewNormal,
                            sectionDepthFt: depthFt,
                            sectionWidthFt: widthFt,
                            clearanceFt: clearanceFt);
                    }
                    catch (Exception exGeom)
                    {
                        Logger.Warn(exGeom, "[DiagramaMontagem] Peca {Id} sem geometria valida para cota — pulando", e.Id);
                        continue;
                    }

                    XYZ origem = new XYZ(plano.Origem.X, plano.Origem.Y, plano.Origem.Z);
                    XYZ direcao = new XYZ(plano.Direcao.X, plano.Direcao.Y, plano.Direcao.Z);
                    Line dimLine = Line.CreateUnbound(origem, direcao);

                    // FamilyInstance.GetReferences(Left/Right) — refs canonicas
                    Dimension dim = CriarCotaViaFamilyRefs(doc, vista, elem, dimLine);
                    if (dim == null)
                    {
                        Logger.Warn("[DiagramaMontagem] Peca {Id} sem refs Left/Right — pulando", e.Id);
                        continue;
                    }

                    // Override se geometrico diverge da fabricacao > 5mm
                    double lengthFabFt = LerComprimentoFabricacao(e);
                    if (lengthFabFt > 0)
                    {
                        double lengthGeomFt = lc.Curve.Length;
                        if (DimensionPlanCalculator.DeveAplicarOverride(lengthGeomFt, lengthFabFt))
                        {
                            double lengthFabMm = UnitUtils.ConvertFromInternalUnits(lengthFabFt, UnitTypeId.Millimeters);
                            try
                            { dim.ValueOverride = $"{lengthFabMm:F0}"; }
                            catch (Exception exOv)
                            {
                                Logger.Warn(exOv, "[DiagramaMontagem] Falha ao aplicar ValueOverride na peca {Id}", e.Id);
                            }
                        }
                    }

                    criados++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[DiagramaMontagem] Falha comprimento {Id}", e.Id);
                }
            }
            return criados;
        }

        /// <summary>
        /// Le um parametro STRUCTURAL_SECTION_COMMON_* do FamilySymbol da peca.
        /// Retorna 0.0 se a familia nao expoe esse parametro (helper trata como
        /// "sem params" e usa fallback).
        /// </summary>
        private double LerSectionCommonParam(FamilyInstance elem, BuiltInParameter bip)
        {
            try
            {
                FamilySymbol sym = elem.Symbol;
                if (sym == null)
                    return 0.0;
                Parameter p = sym.get_Parameter(bip);
                if (p == null || p.StorageType != StorageType.Double)
                    return 0.0;
                double v = p.AsDouble();
                return v > 0 ? v : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Clone local do padrao <c>CotarPecaFabricacaoService.CriarCotaViaFamilyRefs</c>
        /// — cota usando FamilyInstance.GetReferences(Left) + .GetReferences(Right).
        /// Retorna null se a peca nao tiver os tipos de Reference solicitados
        /// (caller pula silenciosamente + loga warn).
        /// </summary>
        private Dimension CriarCotaViaFamilyRefs(Document doc, View view, FamilyInstance elem, Line dimLine)
        {
            try
            {
                IList<Reference> refsLeft = elem.GetReferences(FamilyInstanceReferenceType.Left);
                IList<Reference> refsRight = elem.GetReferences(FamilyInstanceReferenceType.Right);
                if (refsLeft == null || refsLeft.Count == 0)
                    return null;
                if (refsRight == null || refsRight.Count == 0)
                    return null;

                ReferenceArray arr = new ReferenceArray();
                arr.Append(refsLeft[0]);
                arr.Append(refsRight[0]);

                return doc.Create.NewDimension(view, dimLine, arr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Le o comprimento de fabricacao da peca: STRUCTURAL_FRAME_CUT_LENGTH
        /// (primario, gerado pelo Revit para Cut Length) -> INSTANCE_LENGTH_PARAM
        /// (fallback). Retorna 0 se nenhum dos dois tem valor — caller usa
        /// length geometrico nesse caso (sem override).
        /// </summary>
        private double LerComprimentoFabricacao(Element e)
        {
            Parameter pCut = e.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            if (pCut != null && pCut.StorageType == StorageType.Double && pCut.AsDouble() > 0)
                return pCut.AsDouble();
            Parameter pLen = e.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
            if (pLen != null && pLen.StorageType == StorageType.Double && pLen.AsDouble() > 0)
                return pLen.AsDouble();
            return 0;
        }

        // ============================================
        // 2L. INSERCAO EM FOLHA — v2.4.0
        // ============================================
        /// <summary>
        /// Cria ViewSheet com TitleBlock disponivel no projeto e adiciona
        /// a vista como Viewport. Retorna ElementId.InvalidElementId se nao
        /// houver TitleBlock no projeto.
        /// </summary>
        private ElementId ColocarVistaEmFolha(Document doc, ViewSection vista, DiagramaMontagemConfig config, out string nomeFolha)
        {
            nomeFolha = string.Empty;

            FamilySymbol titleBlock = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.IsActive);

            // Se nenhum ativo, tenta ativar o primeiro
            if (titleBlock == null)
            {
                FamilySymbol qualquer = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();
                if (qualquer == null)
                    return ElementId.InvalidElementId;

                qualquer.Activate();
                titleBlock = qualquer;
            }

            ViewSheet sheet = ViewSheet.Create(doc, titleBlock.Id);
            if (sheet == null)
                return ElementId.InvalidElementId;

            // Setar nome e numero
            try
            {
                string numero = !string.IsNullOrWhiteSpace(config.NumeroFolha) ? config.NumeroFolha : "EM-XX";
                string nome = !string.IsNullOrWhiteSpace(config.NomeFolha) ? config.NomeFolha : vista.Name;

                // Garantir unicidade do numero (Revit rejeita duplicate sheet number)
                int sufixo = 1;
                string numeroFinal = numero;
                while (NumeroFolhaJaUsado(doc, numeroFinal))
                {
                    numeroFinal = $"{numero}-{sufixo++}";
                    if (sufixo > 99)
                        break;
                }

                sheet.SheetNumber = numeroFinal;
                sheet.Name = nome;
                nomeFolha = nome;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[DiagramaMontagem] Falha ao nomear folha");
            }

            // Adicionar viewport
            try
            {
                BoundingBoxUV bbSheet = sheet.Outline;
                XYZ pontoCentral = new XYZ(
                    (bbSheet.Min.U + bbSheet.Max.U) / 2,
                    (bbSheet.Min.V + bbSheet.Max.V) / 2,
                    0);
                Viewport.Create(doc, sheet.Id, vista.Id, pontoCentral);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[DiagramaMontagem] Falha ao criar viewport na folha");
            }

            return sheet.Id;
        }

        private bool NumeroFolhaJaUsado(Document doc, string numero)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Any(s => string.Equals(s.SheetNumber, numero, StringComparison.OrdinalIgnoreCase));
        }
    }
}
