#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    /// <summary>
    /// Gera vistas de detalhe (longitudinal e transversal) para pecas estruturais,
    /// voltadas para shop drawings de fabricacao metalica.
    /// </summary>
    public class AutoVistaService
    {
        private const string Titulo = "Auto-Vista de Peça";

        // ================================================================
        //  Ponto de entrada
        // ================================================================

        public void Executar(UIDocument uidoc, GerarVistaPecaConfig config)
        {
            Document doc = uidoc.Document;

            List<FamilyInstance> elementos = ObterElementos(uidoc, doc, config);
            if (elementos.Count == 0)
            {
                AppDialogService.ShowWarning(Titulo,
                    "Nenhum elemento estrutural válido encontrado na seleção.",
                    "Seleção vazia");
                return;
            }

            if (!config.TemVistasSelecionadas())
            {
                AppDialogService.ShowWarning(Titulo,
                    "Selecione ao menos um tipo de vista para gerar.",
                    "Nenhuma vista selecionada");
                return;
            }

            // Obter ViewFamilyType para cortes
            ViewFamilyType? vftSection = ObterViewFamilyType(doc, ViewFamily.Section);
            if (vftSection == null)
            {
                AppDialogService.ShowError(Titulo,
                    "Não foi encontrado um ViewFamilyType para Section no projeto.",
                    "Tipo de vista ausente");
                return;
            }

            // Obter TitleBlock para folhas (se necessario)
            FamilySymbol? titleBlock = null;
            if (config.CriarFolha)
            {
                titleBlock = ObterTitleBlock(doc, config.FamiliaFolhaTitulo, config.TipoFolhaTitulo);
                if (titleBlock == null)
                {
                    AppDialogService.ShowWarning(Titulo,
                        "Nenhuma família de folha de título encontrada. As vistas serão criadas sem folha.",
                        "Folha de título ausente");
                    config.CriarFolha = false;
                }
            }

            double margemFt = UnitUtils.ConvertToInternalUnits(config.MargemMm, UnitTypeId.Millimeters);
            double profCorteMetade = UnitUtils.ConvertToInternalUnits(
                config.ProfundidadeCorteTransversalMm / 2.0, UnitTypeId.Millimeters);

            int vistasCriadas = 0;
            int folhasCriadas = 0;
            List<string> falhas = new();

            foreach (FamilyInstance elem in elementos)
            {
                try
                {
                    DadosGeometriaPeca? dados = ExtrairDadosGeometria(elem);
                    if (dados == null)
                    {
                        falhas.Add($"Id {elem.Id.Value}: sem geometria de curva válida.");
                        continue;
                    }

                    string nomePeca = MontarNomePeca(elem, config.PrefixoNome);
                    List<ViewSection> vistasGeradas = new();

                    using (Transaction t = new Transaction(doc, "Gerar Vista de Peça"))
                    {
                        t.Start();

                        // Vista longitudinal (elevacao lateral)
                        if (config.CriarVistaLongitudinal)
                        {
                            ViewSection? vistaLong = CriarVistaLongitudinal(
                                doc, vftSection, elem, dados, margemFt,
                                $"{nomePeca} - Longitudinal", config.EscalaVista);

                            if (vistaLong != null)
                            {
                                vistasGeradas.Add(vistaLong);
                                vistasCriadas++;
                            }
                            else
                            {
                                falhas.Add($"{nomePeca}: falha na vista longitudinal.");
                            }
                        }

                        // Corte transversal
                        if (config.CriarCorteTransversal)
                        {
                            ViewSection? vistaTransv = CriarCorteTransversal(
                                doc, vftSection, elem, dados, margemFt, profCorteMetade,
                                $"{nomePeca} - Transversal", config.EscalaVista);

                            if (vistaTransv != null)
                            {
                                vistasGeradas.Add(vistaTransv);
                                vistasCriadas++;
                            }
                            else
                            {
                                falhas.Add($"{nomePeca}: falha no corte transversal.");
                            }
                        }

                        // Criar folha e posicionar vistas
                        if (config.CriarFolha && titleBlock != null && vistasGeradas.Count > 0)
                        {
                            ViewSheet? folha = CriarFolhaComVistas(
                                doc, titleBlock, vistasGeradas, nomePeca);
                            if (folha != null)
                                folhasCriadas++;
                        }

                        t.Commit();
                    }
                }
                catch (Exception ex)
                {
                    falhas.Add($"Id {elem.Id.Value}: {ex.Message}");
                }
            }

            // Resumo final
            string resumo = $"Processo concluído!\n\n" +
                            $"Elementos processados: {elementos.Count}\n" +
                            $"Vistas criadas: {vistasCriadas}";

            if (config.CriarFolha)
                resumo += $"\nFolhas criadas: {folhasCriadas}";

            if (falhas.Count > 0)
                resumo += "\n\nObservações:\n• " + string.Join("\n• ", falhas);

            AppDialogService.ShowInfo(Titulo, resumo, "Vistas geradas com sucesso");
        }

        // ================================================================
        //  Selecao de elementos
        // ================================================================

        private List<FamilyInstance> ObterElementos(
            UIDocument uidoc, Document doc, GerarVistaPecaConfig config)
        {
            if (config.Escopo == EscopoSelecaoPeca.VistaAtiva)
                return ColetarElementosDaVista(doc, uidoc.Document.ActiveView, config.FiltroCategoria);

            // Selecao manual
            List<FamilyInstance> selecionados = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Where(EhElementoEstrutural)
                .Where(fi => AtendeFiltroCategoria(fi, config.FiltroCategoria))
                .ToList();

            if (selecionados.Count > 0)
                return selecionados;

            // Pedir selecao
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroElementoEstrutural(),
                    "Selecione as peças estruturais e pressione Enter");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<FamilyInstance>()
                    .Where(EhElementoEstrutural)
                    .Where(fi => AtendeFiltroCategoria(fi, config.FiltroCategoria))
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<FamilyInstance>();
            }
        }

        private List<FamilyInstance> ColetarElementosDaVista(Document doc)
        {
            return ColetarElementosDaVista(doc, doc.ActiveView, VistaPecaCategoriaFiltro.Todos);
        }

        private List<FamilyInstance> ColetarElementosDaVista(
            Document doc,
            View vista,
            VistaPecaCategoriaFiltro filtroCategoria)
        {
            var result = new List<FamilyInstance>();

            var cats = new[]
            {
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns
            };

            foreach (var cat in cats)
            {
                var elems = new FilteredElementCollector(doc, vista.Id)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(EhElementoEstrutural)
                    .Where(fi => AtendeFiltroCategoria(fi, filtroCategoria));

                result.AddRange(elems);
            }

            return result;
        }

        private static bool EhElementoEstrutural(FamilyInstance fi)
        {
            var cat = fi.Category?.BuiltInCategory;
            return cat == BuiltInCategory.OST_StructuralFraming
                || cat == BuiltInCategory.OST_StructuralColumns;
        }

        private static bool AtendeFiltroCategoria(FamilyInstance fi, VistaPecaCategoriaFiltro filtro)
        {
            BuiltInCategory? categoria = fi?.Category?.BuiltInCategory;

            return filtro switch
            {
                VistaPecaCategoriaFiltro.Pilares => categoria == BuiltInCategory.OST_StructuralColumns,
                VistaPecaCategoriaFiltro.Vigas => categoria == BuiltInCategory.OST_StructuralFraming,
                _ => true
            };
        }

        // ================================================================
        //  Geometria da peca
        // ================================================================

        private sealed class DadosGeometriaPeca
        {
            public XYZ PontoInicio { get; set; } = XYZ.Zero;
            public XYZ PontoFim { get; set; } = XYZ.Zero;
            public XYZ Direcao { get; set; } = XYZ.BasisX;
            public double Comprimento { get; set; }
            public BoundingBoxXYZ BoundingBox { get; set; } = new BoundingBoxXYZ();
            public bool EhPilar { get; set; }
        }

        private DadosGeometriaPeca? ExtrairDadosGeometria(FamilyInstance elem)
        {
            var dados = new DadosGeometriaPeca();
            dados.BoundingBox = elem.get_BoundingBox(null);
            if (dados.BoundingBox == null)
                return null;

            dados.EhPilar = elem.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralColumns;

            if (elem.Location is LocationCurve locCurve && locCurve.Curve is Line line)
            {
                dados.PontoInicio = line.GetEndPoint(0);
                dados.PontoFim = line.GetEndPoint(1);
                dados.Comprimento = line.Length;
                dados.Direcao = (dados.PontoFim - dados.PontoInicio).Normalize();
            }
            else if (elem.Location is LocationPoint locPt)
            {
                // Pilares com LocationPoint
                XYZ basePt = locPt.Point;
                double height = dados.BoundingBox.Max.Z - dados.BoundingBox.Min.Z;
                dados.PontoInicio = basePt;
                dados.PontoFim = basePt + XYZ.BasisZ * height;
                dados.Comprimento = height;
                dados.Direcao = XYZ.BasisZ;
            }
            else
            {
                return null;
            }

            return dados;
        }

        // ================================================================
        //  Criacao de vistas
        // ================================================================

        /// <summary>
        /// Vista longitudinal: corte paralelo ao eixo da peca, olhando de frente.
        /// A direcao de visualizacao e perpendicular ao eixo no plano horizontal.
        /// </summary>
        private ViewSection? CriarVistaLongitudinal(
            Document doc, ViewFamilyType vft, FamilyInstance elem,
            DadosGeometriaPeca dados, double margem, string nome, int escala)
        {
            try
            {
                XYZ dir = dados.Direcao;
                XYZ centro = (dados.PontoInicio + dados.PontoFim) / 2.0;

                // Direcao de visualizacao: perpendicular ao eixo da peca
                XYZ viewDir;
                if (dados.EhPilar)
                {
                    // Para pilares verticais, olhar de frente (eixo Y negativo ou X)
                    viewDir = XYZ.BasisY.Negate();
                }
                else
                {
                    // Para vigas, perpendicular ao eixo no plano horizontal
                    viewDir = new XYZ(-dir.Y, dir.X, 0);
                    if (viewDir.GetLength() < 1e-6)
                        viewDir = XYZ.BasisY;
                    viewDir = viewDir.Normalize();
                }

                XYZ upDir = XYZ.BasisZ;
                XYZ rightDir = upDir.CrossProduct(viewDir);
                if (rightDir.GetLength() < 1e-6)
                {
                    // Fallback se viewDir e upDir sao paralelos
                    upDir = XYZ.BasisX;
                    rightDir = upDir.CrossProduct(viewDir);
                }
                rightDir = rightDir.Normalize();
                upDir = viewDir.CrossProduct(rightDir).Normalize();

                // Dimensoes do bounding box
                BoundingBoxXYZ bb = dados.BoundingBox;
                double halfLength = dados.Comprimento / 2.0 + margem;

                // Projetar bounding box para calcular altura e profundidade
                double alturaMax = Math.Max(
                    Math.Abs((bb.Max - bb.Min).DotProduct(upDir)),
                    UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters));
                double profMax = Math.Max(
                    Math.Abs((bb.Max - bb.Min).DotProduct(viewDir)),
                    UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters));

                double halfHeight = alturaMax / 2.0 + margem;
                double depth = profMax + margem * 2;

                // Montar BoundingBoxXYZ para o corte
                BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                Transform transform = Transform.Identity;
                transform.Origin = centro;
                transform.BasisX = rightDir;
                transform.BasisY = upDir;
                transform.BasisZ = viewDir;

                sectionBox.Transform = transform;
                sectionBox.Min = new XYZ(-halfLength, -halfHeight, 0);
                sectionBox.Max = new XYZ(halfLength, halfHeight, depth);

                ViewSection view = ViewSection.CreateSection(doc, vft.Id, sectionBox);
                view.Name = GerarNomeUnico(doc, nome);
                view.Scale = escala;

                // Aplicar crop para mostrar apenas a peca
                view.CropBoxActive = true;
                view.CropBoxVisible = false;

                // Regenerar para que a view enxergue os elementos antes de isolar/cotar
                doc.Regenerate();

                // Isolar apenas a peca selecionada
                IsolarElementoNaVista(view, elem);

                // Cotar automaticamente (comprimento)
                CotarLongitudinal(doc, view, elem);

                return view;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Corte transversal: corte perpendicular ao eixo da peca, no ponto medio.
        /// Mostra a secao transversal do perfil.
        /// </summary>
        private ViewSection? CriarCorteTransversal(
            Document doc, ViewFamilyType vft, FamilyInstance elem,
            DadosGeometriaPeca dados, double margem, double profundidade,
            string nome, int escala)
        {
            try
            {
                XYZ dir = dados.Direcao;
                XYZ centro = (dados.PontoInicio + dados.PontoFim) / 2.0;

                // A direcao de visualizacao eh ao longo do eixo da peca
                XYZ viewDir = dir;

                // Up: vertical para vigas, perpendicular para pilares
                XYZ upDir;
                if (dados.EhPilar)
                {
                    upDir = XYZ.BasisY;
                    if (Math.Abs(viewDir.DotProduct(upDir)) > 0.99)
                        upDir = XYZ.BasisX;
                }
                else
                {
                    upDir = XYZ.BasisZ;
                }

                XYZ rightDir = upDir.CrossProduct(viewDir);
                if (rightDir.GetLength() < 1e-6)
                {
                    upDir = XYZ.BasisX;
                    rightDir = upDir.CrossProduct(viewDir);
                }
                rightDir = rightDir.Normalize();
                upDir = viewDir.CrossProduct(rightDir).Normalize();

                // Dimensoes da secao
                BoundingBoxXYZ bb = dados.BoundingBox;
                double largura = Math.Abs((bb.Max - bb.Min).DotProduct(rightDir));
                double altura = Math.Abs((bb.Max - bb.Min).DotProduct(upDir));

                double halfWidth = Math.Max(largura, UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters)) / 2.0 + margem;
                double halfHeight = Math.Max(altura, UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters)) / 2.0 + margem;

                BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                Transform transform = Transform.Identity;
                transform.Origin = centro;
                transform.BasisX = rightDir;
                transform.BasisY = upDir;
                transform.BasisZ = viewDir;

                sectionBox.Transform = transform;
                sectionBox.Min = new XYZ(-halfWidth, -halfHeight, 0);
                sectionBox.Max = new XYZ(halfWidth, halfHeight, profundidade * 2);

                ViewSection view = ViewSection.CreateSection(doc, vft.Id, sectionBox);
                view.Name = GerarNomeUnico(doc, nome);
                view.Scale = escala;

                view.CropBoxActive = true;
                view.CropBoxVisible = false;

                // Regenerar antes de isolar/cotar
                doc.Regenerate();

                // Isolar apenas a peca selecionada
                IsolarElementoNaVista(view, elem);

                // Cotar automaticamente (altura + largura do perfil)
                CotarTransversal(doc, view, elem);

                return view;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ================================================================
        //  Pos-processamento: isolar e cotar
        // ================================================================

        /// <summary>
        /// Isola o elemento na vista de forma permanente (para nao desaparecer apos salvar).
        /// </summary>
        private void IsolarElementoNaVista(View view, Element elem)
        {
            try
            {
                var ids = new List<ElementId> { elem.Id };
                view.IsolateElementsTemporary(ids);
                view.ConvertTemporaryHideIsolateToPermanent();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"AutoVistaService.IsolarElementoNaVista: nao foi possivel isolar Id {elem.Id.Value}");
            }
        }

        /// <summary>
        /// Cota de comprimento na vista longitudinal usando FamilyInstanceReferenceType.Left/Right.
        /// Essas refs apontam para FACES da peca, nao para pontos isolados.
        /// </summary>
        private void CotarLongitudinal(Document doc, ViewSection view, FamilyInstance elem)
        {
            try
            {
                IList<Reference>? refsLeft = elem.GetReferences(FamilyInstanceReferenceType.Left);
                IList<Reference>? refsRight = elem.GetReferences(FamilyInstanceReferenceType.Right);
                if (refsLeft == null || refsLeft.Count == 0) return;
                if (refsRight == null || refsRight.Count == 0) return;

                var refArr = new ReferenceArray();
                refArr.Append(refsLeft[0]);
                refArr.Append(refsRight[0]);

                double offset = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters);
                XYZ linhaPoint = view.Origin - view.UpDirection * offset;
                Line dimLine = Line.CreateUnbound(linhaPoint, view.RightDirection);

                doc.Create.NewDimension(view, dimLine, refArr);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "AutoVistaService.CotarLongitudinal: falha silenciada");
            }
        }

        /// <summary>
        /// Cotas de altura (Top/Bottom) e largura (Front/Back) na vista transversal.
        /// </summary>
        private void CotarTransversal(Document doc, ViewSection view, FamilyInstance elem)
        {
            double offset = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters);

            // Altura
            try
            {
                IList<Reference>? refsTop = elem.GetReferences(FamilyInstanceReferenceType.Top);
                IList<Reference>? refsBottom = elem.GetReferences(FamilyInstanceReferenceType.Bottom);
                if (refsTop != null && refsTop.Count > 0 && refsBottom != null && refsBottom.Count > 0)
                {
                    var refArr = new ReferenceArray();
                    refArr.Append(refsTop[0]);
                    refArr.Append(refsBottom[0]);
                    XYZ linhaPoint = view.Origin - view.RightDirection * offset;
                    Line dimLine = Line.CreateUnbound(linhaPoint, view.UpDirection);
                    doc.Create.NewDimension(view, dimLine, refArr);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "AutoVistaService.CotarTransversal altura: falha silenciada");
            }

            // Largura
            try
            {
                IList<Reference>? refsFront = elem.GetReferences(FamilyInstanceReferenceType.Front);
                IList<Reference>? refsBack = elem.GetReferences(FamilyInstanceReferenceType.Back);
                if (refsFront != null && refsFront.Count > 0 && refsBack != null && refsBack.Count > 0)
                {
                    var refArr = new ReferenceArray();
                    refArr.Append(refsFront[0]);
                    refArr.Append(refsBack[0]);
                    XYZ linhaPoint = view.Origin - view.UpDirection * offset;
                    Line dimLine = Line.CreateUnbound(linhaPoint, view.RightDirection);
                    doc.Create.NewDimension(view, dimLine, refArr);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "AutoVistaService.CotarTransversal largura: falha silenciada");
            }
        }

        // ================================================================
        //  Folha (ViewSheet)
        // ================================================================

        private ViewSheet? CriarFolhaComVistas(
            Document doc, FamilySymbol titleBlock, List<ViewSection> vistas, string nomePeca)
        {
            try
            {
                if (!titleBlock.IsActive)
                    titleBlock.Activate();

                ViewSheet sheet = ViewSheet.Create(doc, titleBlock.Id);
                sheet.Name = nomePeca;
                sheet.SheetNumber = $"SD-{DateTime.Now:yyMMdd-HHmmss}";

                // Posicionar vistas lado a lado no centro da folha
                BoundingBoxUV outline = sheet.Outline;
                double sheetWidth = outline.Max.U - outline.Min.U;
                double sheetHeight = outline.Max.V - outline.Min.V;
                double centerX = outline.Min.U + sheetWidth / 2.0;
                double centerY = outline.Min.V + sheetHeight / 2.0;

                double spacing = sheetWidth / (vistas.Count + 1);

                for (int i = 0; i < vistas.Count; i++)
                {
                    XYZ location = new XYZ(
                        centerX + (i - (vistas.Count - 1) / 2.0) * spacing * 0.5,
                        centerY,
                        0);

                    if (Viewport.CanAddViewToSheet(doc, sheet.Id, vistas[i].Id))
                    {
                        Viewport.Create(doc, sheet.Id, vistas[i].Id, location);
                    }
                }

                return sheet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ================================================================
        //  Helpers
        // ================================================================

        private ViewFamilyType? ObterViewFamilyType(Document doc, ViewFamily family)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == family);
        }

        private FamilySymbol? ObterTitleBlock(Document doc, string familyName, string typeName)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            // Tentar correspondencia exata
            if (!string.IsNullOrWhiteSpace(familyName) && !string.IsNullOrWhiteSpace(typeName))
            {
                var exact = collector.FirstOrDefault(fs =>
                    fs.Family.Name == familyName && fs.Name == typeName);
                if (exact != null) return exact;
            }

            // Primeiro disponivel
            return collector.FirstOrDefault();
        }

        private string MontarNomePeca(FamilyInstance elem, string prefixo)
        {
            string familyName = elem.Symbol?.Family?.Name ?? "Elem";
            string typeName = elem.Symbol?.Name ?? "";
            string mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";

            if (!string.IsNullOrWhiteSpace(mark))
                return $"{prefixo}-{mark}";

            return $"{prefixo}-{familyName}-{typeName}-{elem.Id.Value}";
        }

        private string GerarNomeUnico(Document doc, string nomeBase)
        {
            // Verificar se ja existe uma vista com esse nome
            var existentes = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.Name)
                .ToHashSet();

            if (!existentes.Contains(nomeBase))
                return nomeBase;

            int contador = 2;
            while (existentes.Contains($"{nomeBase} ({contador})"))
                contador++;

            return $"{nomeBase} ({contador})";
        }

        // ================================================================
        //  Filtro de selecao
        // ================================================================

        private class FiltroElementoEstrutural : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                var cat = elem.Category?.BuiltInCategory;
                return cat == BuiltInCategory.OST_StructuralFraming
                    || cat == BuiltInCategory.OST_StructuralColumns;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
