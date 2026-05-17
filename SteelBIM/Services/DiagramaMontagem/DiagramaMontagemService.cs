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
                ViewSection vista;
                using (Transaction tx1 = new Transaction(doc, "Criar vista do Diagrama de Montagem"))
                {
                    tx1.Start();
                    vista = CriarSectionView(doc, sectionBbox, config.NomeVista);
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

                // 8) Abrir vista para o usuario
                uidoc.ActiveView = vista;

                resultado.Sucesso = true;
                resultado.Mensagem =
                    $"Diagrama criado: '{vista.Name}'.\n" +
                    $"Eixos visiveis: {resultado.EixosVisiveis}\n" +
                    $"Cotas: {resultado.CotasCriadas}\n" +
                    $"Tags: {resultado.TagsCriadas} (sem Mark: {resultado.TagsSemMark})";

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
            double extZ = maxZ - minZ;

            // Decidir orientacao do plano de secao
            // Plano de secao mostra a vista de elevacao — direcao "para frente"
            // do observador eh normal a vista
            bool paraleloAoX;
            if (config.Orientacao == OrientacaoDiagrama.Auto)
            {
                // Se elementos extendem mais em X, observador olha de Y (secao paralela a X)
                paraleloAoX = extX >= extY;
            }
            else
            {
                paraleloAoX = (config.Orientacao == OrientacaoDiagrama.ParaleloEixoX);
            }

            XYZ origem = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            XYZ rightDir, viewDir;

            if (paraleloAoX)
            {
                // Vista "frontal" — observador em Y- olhando para Y+
                rightDir = XYZ.BasisX;
                viewDir = -XYZ.BasisY; // direcao de visualizacao
            }
            else
            {
                rightDir = XYZ.BasisY;
                viewDir = XYZ.BasisX;
            }

            XYZ upDir = XYZ.BasisZ;

            sectionTransform = Transform.Identity;
            sectionTransform.Origin = origem;
            sectionTransform.BasisX = rightDir;
            sectionTransform.BasisY = upDir;
            sectionTransform.BasisZ = viewDir;

            // BBox da section em coords locais (rightDir, upDir, viewDir)
            double margemFt = UnitUtils.ConvertToInternalUnits(config.MargemMm, UnitTypeId.Millimeters);
            double largura = (paraleloAoX ? extX : extY) + 2 * margemFt;
            double altura = extZ + 2 * margemFt;
            double profundidade = (paraleloAoX ? extY : extX) + 2 * margemFt;

            sectionBbox = new BoundingBoxXYZ();
            sectionBbox.Transform = sectionTransform;
            sectionBbox.Min = new XYZ(-largura / 2, -altura / 2, -profundidade / 2);
            sectionBbox.Max = new XYZ(largura / 2, altura / 2, profundidade / 2);
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
            catch { }

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
    }
}
