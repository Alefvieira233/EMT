#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SteelBIM.Core;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Services.DiagramaMontagem;
using SteelBIM.Utils;

namespace SteelBIM.Services
{
    /// <summary>
    /// Gera vistas de detalhe (longitudinal e transversal) para pecas estruturais,
    /// voltadas para shop drawings de fabricacao metalica.
    ///
    /// v2.7.9 (ADR-003 template): primeiro service do projeto a injetar
    /// <see cref="IUIDecisionService"/> via construtor — desacopla das
    /// chamadas diretas a <c>AppDialogService</c> static, permitindo testes
    /// com mock. Constructor default preserva backward-compat: callers
    /// existentes (3 commands) continuam fazendo <c>new AutoVistaService()</c>
    /// e o service auto-instancia <see cref="AppDialogUIDecisionService"/>
    /// (adapter de producao).
    ///
    /// **Padrao replicavel:** outros services seguindo ADR-003 devem copiar
    /// este modelo de constructor.
    /// </summary>
    public class AutoVistaService
    {
        private const string Titulo = "Auto-Vista de Peça";

        // v2.7.9 (ADR-003): UI injetada pra eliminar coupling com AppDialogService
        // static. Default = adapter de producao (AppDialogUIDecisionService) pra
        // backward-compat com callers que fazem 'new AutoVistaService()'.
        private readonly IUIDecisionService _ui;

        public AutoVistaService(IUIDecisionService? ui = null)
        {
            _ui = ui ?? new AppDialogUIDecisionService();
        }

        // ================================================================
        //  Ponto de entrada
        // ================================================================

        public void Executar(UIDocument uidoc, GerarVistaPecaConfig config)
        {
            Document doc = uidoc.Document;

            List<FamilyInstance> elementos = ObterElementos(uidoc, doc, config);
            if (elementos.Count == 0)
            {
                _ui.Warn(Titulo,
                    "Nenhum elemento estrutural válido encontrado na seleção.",
                    "Seleção vazia");
                return;
            }

            if (!config.TemVistasSelecionadas())
            {
                _ui.Warn(Titulo,
                    "Selecione ao menos um tipo de vista para gerar.",
                    "Nenhuma vista selecionada");
                return;
            }

            // Obter ViewFamilyType para cortes
            ViewFamilyType? vftSection = ObterViewFamilyType(doc, ViewFamily.Section);
            if (vftSection == null)
            {
                _ui.Error(Titulo,
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
                    _ui.Warn(Titulo,
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
            // v2.7.2: contadores das anotacoes automaticas
            int cotasCriadas = 0;
            int tagsCriadas = 0;
            int tagsSemMark = 0;
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
                        // P1.1 (2026-04-28): operacao em lote sobre N elementos — suprimir
                        // warnings comuns (vista cortando geometria fora dos limites, etc.)
                        // que de outro modo abrem dialogo modal a cada elemento.
                        SteelBIM.Utils.FailureHandlingHelper.SwallowWarnings(t);

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

                                // v2.7.2: anotacoes automaticas (opt-in via config)
                                if (config.AdicionarCotagemLongitudinal)
                                    cotasCriadas += CotarLongitudinal(doc, vistaLong, elem);

                                if (config.AdicionarTagComMarca)
                                {
                                    (int tagCreated, int semMark) = CriarTagComMarca(doc, vistaLong, elem);
                                    tagsCriadas += tagCreated;
                                    tagsSemMark += semMark;
                                }
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

            // v2.7.2: contadores das anotacoes automaticas (so mostra se ativadas)
            if (config.AdicionarCotagemLongitudinal)
                resumo += $"\nCotas longitudinais: {cotasCriadas}";
            if (config.AdicionarTagComMarca)
            {
                resumo += $"\nTags com marca: {tagsCriadas}";
                if (tagsSemMark > 0)
                    resumo += $" (peças sem Mark puladas: {tagsSemMark})";
            }

            if (falhas.Count > 0)
                resumo += "\n\nObservações:\n• " + string.Join("\n• ", falhas);

            _ui.Info(Titulo, resumo, "Vistas geradas com sucesso");
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

                // v2.7.2: cotagem + tag agora sao opt-in via config, chamadas no
                // caller `Executar` apos esta funcao retornar a vista criada.
                // Antes a cotagem era chamada incondicionalmente aqui.

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
        ///
        /// v2.7.2: reformulada para usar offset adaptativo (35mm da face externa,
        /// reusa <see cref="DimensionPlanCalculator"/> da v2.6.6) e
        /// <c>ValueOverride</c> com Cut Length quando geom diverge do fab > 5mm
        /// (padrao v2.6.5). Antes usava offset fixo de 500mm do eixo e nao
        /// aplicava Override — pecas cortadas com cope/notch mostravam o
        /// comprimento geometrico (ex: 1224mm) em vez do Cut Length real
        /// (ex: 1215mm).
        ///
        /// Retorna 1 se Dimension criada, 0 caso contrario (peca sem refs
        /// Left/Right, geometria degenerada, etc).
        /// </summary>
        private int CotarLongitudinal(Document doc, ViewSection view, FamilyInstance elem)
        {
            try
            {
                IList<Reference>? refsLeft = elem.GetReferences(FamilyInstanceReferenceType.Left);
                IList<Reference>? refsRight = elem.GetReferences(FamilyInstanceReferenceType.Right);
                if (refsLeft == null || refsLeft.Count == 0)
                    return 0;
                if (refsRight == null || refsRight.Count == 0)
                    return 0;

                // Endpoints da LocationCurve — necessario pro calculo do plano
                if (elem.Location is not LocationCurve lc || lc.Curve == null)
                    return 0;
                XYZ pa = lc.Curve.GetEndPoint(0);
                XYZ pb = lc.Curve.GetEndPoint(1);
                if (pa.DistanceTo(pb) < 1e-6)
                {
                    Logger.Debug("[AutoVista] Peca {Id} com LocationCurve degenerada — cota pulada", elem.Id);
                    return 0;
                }

                // v2.7.2: ler seccao do Symbol para offset adaptativo (v2.6.6)
                double depthFt = LerSecParam(elem, BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
                double widthFt = LerSecParam(elem, BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
                double clearanceFt = UnitUtils.ConvertToInternalUnits(35.0, UnitTypeId.Millimeters);

                // Calcular plano via helper puro v2.6.6
                Vec3 p1 = new Vec3(pa.X, pa.Y, pa.Z);
                Vec3 p2 = new Vec3(pb.X, pb.Y, pb.Z);
                XYZ vd = view.ViewDirection;
                Vec3 viewNormal = new Vec3(vd.X, vd.Y, vd.Z);

                PlanoCotaResult plano;
                try
                {
                    plano = DimensionPlanCalculator.CalcularPlanoCota(
                        p1, p2, viewNormal, depthFt, widthFt, clearanceFt);
                }
                catch (Exception exGeom)
                {
                    Logger.Debug(
                        "[AutoVista] Peca {Id} sem plano de cota valido ({Msg}) — pulando",
                        elem.Id, exGeom.Message);
                    return 0;
                }

                XYZ origem = new XYZ(plano.Origem.X, plano.Origem.Y, plano.Origem.Z);
                XYZ direcao = new XYZ(plano.Direcao.X, plano.Direcao.Y, plano.Direcao.Z);
                Line dimLine = Line.CreateUnbound(origem, direcao);

                var refArr = new ReferenceArray();
                refArr.Append(refsLeft[0]);
                refArr.Append(refsRight[0]);

                Dimension dim = doc.Create.NewDimension(view, dimLine, refArr);
                if (dim == null)
                    return 0;

                // v2.7.2: ValueOverride com Cut Length quando geom diverge > 5mm
                double lengthFabFt = LerCutLength(elem);
                if (lengthFabFt > 0)
                {
                    double lengthGeomFt = lc.Curve.Length;
                    if (DimensionPlanCalculator.DeveAplicarOverride(lengthGeomFt, lengthFabFt))
                    {
                        double mm = UnitUtils.ConvertFromInternalUnits(lengthFabFt, UnitTypeId.Millimeters);
                        try
                        { dim.ValueOverride = $"{mm:F0}"; }
                        catch (Exception exOv)
                        {
                            // NOTA 3 v2.7.2: log Debug minimo com Element ID + razao,
                            // sem catch vazio. Revit pode bloquear quando
                            // associatividade da Dimension nao permite override.
                            Logger.Debug(
                                "[AutoVista] ValueOverride rejeitado para peca {Id}: {Msg} — " +
                                "cota mantida com valor geometrico",
                                elem.Id, exOv.Message);
                        }
                    }
                }

                return 1;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[AutoVista] CotarLongitudinal: falha silenciada Id {Id}", elem.Id);
                return 0;
            }
        }

        /// <summary>
        /// v2.7.2: cria <see cref="IndependentTag"/> com o parametro Mark da peca.
        /// Pecas sem Mark sao puladas silenciosamente (Logger.Debug). Fallback
        /// <see cref="TextNote"/> quando nenhum FamilySymbol de
        /// <c>OST_StructuralFramingTags</c>/<c>OST_StructuralColumnTags</c>
        /// estiver carregado no projeto.
        /// </summary>
        /// <returns>Tupla (criadas, semMark):
        /// criadas = 1 se tag/textnote criada, 0 caso contrario.
        /// semMark = 1 se peca nao tem Mark preenchido, 0 caso contrario.
        /// </returns>
        private (int criadas, int semMark) CriarTagComMarca(Document doc, ViewSection view, FamilyInstance elem)
        {
            try
            {
                Parameter pMark = elem.LookupParameter("Mark");
                string? mark = pMark?.AsString();
                if (string.IsNullOrWhiteSpace(mark))
                {
                    Logger.Debug("[AutoVista] Peca {Id} sem Mark — tag pulada", elem.Id);
                    return (0, 1);
                }

                if (elem.Location is not LocationCurve lc || lc.Curve == null)
                    return (0, 0);

                // Posicao da tag: midpoint da peca + offset vertical (oposto a linha
                // de cota — esta fica em uma direcao do perpendicular, tag fica na
                // outra). Offset 120mm pra nao colidir com a cota nem com o perfil.
                XYZ centro = lc.Curve.Evaluate(0.5, true);
                double offsetFt = UnitUtils.ConvertToInternalUnits(120, UnitTypeId.Millimeters);
                XYZ pos = centro - view.UpDirection * offsetFt;

                // Tentativa 1: FamilySymbol de Structural Framing Tag carregado
                FamilySymbol? tagSym = AcharTagSymbol(doc, elem);
                if (tagSym != null)
                {
                    if (!tagSym.IsActive)
                    {
                        tagSym.Activate();
                        doc.Regenerate();
                    }
                    IndependentTag.Create(
                        doc, tagSym.Id, view.Id,
                        new Reference(elem),
                        addLeader: false,
                        TagOrientation.Horizontal,
                        pos);
                    return (1, 0);
                }

                // Fallback: TextNote com o texto do Mark
                TextNoteType? ttype = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();
                if (ttype != null)
                {
                    TextNote.Create(doc, view.Id, pos, mark, ttype.Id);
                    Logger.Debug(
                        "[AutoVista] Peca {Id}: tag estrutural ausente no projeto, fallback TextNote com Mark '{Mark}'",
                        elem.Id, mark);
                    return (1, 0);
                }

                Logger.Warn("[AutoVista] Peca {Id}: nem FamilySymbol de tag nem TextNoteType disponivel", elem.Id);
                return (0, 0);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[AutoVista] CriarTagComMarca: falha silenciada Id {Id}", elem.Id);
                return (0, 0);
            }
        }

        /// <summary>
        /// v2.7.2: acha o FamilySymbol de tag estrutural mais apropriado para a
        /// peca. Preferencia: tag da categoria especifica (Framing vs Columns);
        /// fallback para qualquer tag estrutural; ultimo recurso null (caller
        /// usa TextNote).
        /// </summary>
        private FamilySymbol? AcharTagSymbol(Document doc, FamilyInstance elem)
        {
            long catId = elem.Category?.Id?.Value ?? 0;
            BuiltInCategory tagCat = catId == (long)BuiltInCategory.OST_StructuralColumns
                ? BuiltInCategory.OST_StructuralColumnTags
                : BuiltInCategory.OST_StructuralFramingTags;

            FamilySymbol? preferido = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(tagCat)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
            if (preferido != null)
                return preferido;

            // Fallback: qualquer outra tag estrutural carregada
            BuiltInCategory outra = tagCat == BuiltInCategory.OST_StructuralFramingTags
                ? BuiltInCategory.OST_StructuralColumnTags
                : BuiltInCategory.OST_StructuralFramingTags;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(outra)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        /// <summary>
        /// v2.7.2: le parametro de seccao do FamilySymbol (Height ou Width).
        /// Adapter trivial — retorna 0 em qualquer falha (Symbol null, parametro
        /// ausente, storage type errado). Caller pro CalcularPlanoCota cobre
        /// fallback de 100mm internamente.
        /// </summary>
        private double LerSecParam(FamilyInstance elem, BuiltInParameter bip)
        {
            try
            {
                FamilySymbol? sym = elem.Symbol;
                if (sym == null)
                    return 0.0;
                Parameter? p = sym.get_Parameter(bip);
                if (p == null || p.StorageType != StorageType.Double)
                    return 0.0;
                double v = p.AsDouble();
                return v > 0 ? v : 0.0;
            }
            catch { return 0.0; }
        }

        /// <summary>
        /// v2.7.2: le Cut Length de fabricacao da peca (instance parameter).
        /// Fallback INSTANCE_LENGTH_PARAM se Cut Length ausente. 0 se nada
        /// disponivel — caller pula Override.
        /// </summary>
        private double LerCutLength(FamilyInstance elem)
        {
            Parameter? pCut = elem.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            if (pCut != null && pCut.StorageType == StorageType.Double && pCut.AsDouble() > 0)
                return pCut.AsDouble();
            Parameter? pLen = elem.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
            if (pLen != null && pLen.StorageType == StorageType.Double && pLen.AsDouble() > 0)
                return pLen.AsDouble();
            return 0;
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
                {
                    titleBlock.Activate();
                    doc.Regenerate(); // title block recem-ativado precisa regenerar antes de ViewSheet.Create
                }

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
                if (exact != null)
                    return exact;
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
