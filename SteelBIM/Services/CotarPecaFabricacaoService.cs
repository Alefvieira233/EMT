#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    /// <summary>
    /// Cotagem inteligente para fabricacao metalica.
    /// Adiciona cotas automaticas de comprimento, secao, furos e distancias de borda
    /// em pecas estruturais visiveis na vista ativa.
    ///
    /// IMPORTANTE — Padrão de referências para NewDimension:
    ///   • face.Reference é REFERENCE_TYPE_SURFACE → rejeitado pela API.
    ///   • edge.Reference é REFERENCE_TYPE_LINEAR  → aceito pela API.
    ///   • GetInstanceGeometry() retorna cópias que podem falhar no NewDimension.
    ///   • GetSymbolGeometry() preserva as referências originais válidas;
    ///     a transformação é acumulada manualmente.
    /// </summary>
    public class CotarPecaFabricacaoService
    {
        private const string Titulo = "Cotagem de Fabricação";

        // ================================================================
        //  Ponto de entrada
        // ================================================================

        public void Executar(UIDocument uidoc, CotarPecaFabricacaoConfig config)
        {
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!VistaSuportada(view))
            {
                AppDialogService.ShowWarning(Titulo,
                    "Este comando funciona em plantas, cortes e elevações.",
                    "Vista não suportada");
                return;
            }

            List<FamilyInstance> elementos = ObterElementos(uidoc, doc, config.Escopo);
            if (elementos.Count == 0)
            {
                AppDialogService.ShowWarning(Titulo,
                    "Nenhum elemento estrutural válido encontrado.",
                    "Seleção vazia");
                return;
            }

            double offsetFt = UnitUtils.ConvertToInternalUnits(config.OffsetCotaMm, UnitTypeId.Millimeters);

            int cotasCriadas = 0;
            List<string> falhas = new();

            using (Transaction t = new Transaction(doc, "Cotagem de Fabricação"))
            {
                t.Start();

                // Suprimir warnings para operações em lote (P1.1 — helper central)
                FerramentaEMT.Utils.FailureHandlingHelper.SwallowWarnings(t);

                foreach (FamilyInstance elem in elementos)
                {
                    try
                    {
                        int criadas = CotarElemento(doc, view, elem, config, offsetFt);
                        cotasCriadas += criadas;
                    }
                    catch (Exception ex)
                    {
                        falhas.Add($"Id {elem.Id.Value}: {ex.Message}");
                    }
                }

                if (cotasCriadas > 0)
                    t.Commit();
                else
                    t.RollBack();
            }

            // Resumo
            string resumo = cotasCriadas > 0
                ? $"Cotagem concluída!\n\nElementos processados: {elementos.Count}\nCotas criadas: {cotasCriadas}"
                : "Não foi possível criar cotas para os elementos selecionados.\n\n" +
                  "Dicas:\n• Verifique se os elementos estão visíveis na vista ativa.\n" +
                  "• Tente em uma vista de corte ou elevação onde a peça apareça inteira.";

            if (falhas.Count > 0)
                resumo += "\n\nObservações:\n• " + string.Join("\n• ", falhas);

            if (cotasCriadas > 0)
                AppDialogService.ShowInfo(Titulo, resumo, "Cotagem concluída");
            else
                AppDialogService.ShowWarning(Titulo, resumo, "Nenhuma cota criada");
        }

        // ================================================================
        //  Cotagem por elemento
        // ================================================================

        private int CotarElemento(
            Document doc, View view, FamilyInstance elem,
            CotarPecaFabricacaoConfig config, double offsetFt)
        {
            int count = 0;

            DadosPeca? dados = ExtrairDadosPeca(elem, view);
            if (dados == null) return 0;

            // Eixos da vista para determinar direções válidas
            XYZ eixoH = ObterEixoHorizontalDaVista(view);
            XYZ eixoV = ObterEixoVerticalDaVista(view);
            XYZ normalVista = view.ViewDirection;

            // 1. Cota de comprimento total
            if (config.CotarComprimento)
            {
                Dimension? dim = CriarCotaEntreExtremos(
                    doc, view, elem, dados, offsetFt, eixoH, eixoV, normalVista);
                if (dim != null) count++;
            }

            // 2. Cota de altura do perfil (d)
            if (config.CotarAlturaPerfil)
            {
                Dimension? dim = CriarCotaAlturaPerfil(
                    doc, view, elem, dados, offsetFt, eixoH, eixoV, normalVista);
                if (dim != null) count++;
            }

            // 3. Cota de largura da mesa (bf)
            if (config.CotarLarguraMesa)
            {
                Dimension? dim = CriarCotaLarguraMesa(
                    doc, view, elem, dados, offsetFt, eixoH, eixoV, normalVista);
                if (dim != null) count++;
            }

            return count;
        }

        // ================================================================
        //  Dados da peca
        // ================================================================

        private sealed class DadosPeca
        {
            public XYZ PontoInicio { get; set; } = XYZ.Zero;
            public XYZ PontoFim { get; set; } = XYZ.Zero;
            public XYZ Direcao { get; set; } = XYZ.BasisX;
            public double Comprimento { get; set; }
            public bool EhPilar { get; set; }
            public BoundingBoxXYZ? BoundingBox { get; set; }
        }

        private DadosPeca? ExtrairDadosPeca(FamilyInstance elem, View view)
        {
            var dados = new DadosPeca();
            dados.EhPilar = elem.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralColumns;
            dados.BoundingBox = elem.get_BoundingBox(view);

            if (elem.Location is LocationCurve locCurve && locCurve.Curve is Line line)
            {
                dados.PontoInicio = line.GetEndPoint(0);
                dados.PontoFim = line.GetEndPoint(1);
                dados.Comprimento = line.Length;
                dados.Direcao = (dados.PontoFim - dados.PontoInicio).Normalize();
            }
            else if (elem.Location is LocationPoint locPt && dados.BoundingBox != null)
            {
                XYZ basePt = locPt.Point;
                double height = dados.BoundingBox.Max.Z - dados.BoundingBox.Min.Z;
                dados.PontoInicio = new XYZ(basePt.X, basePt.Y, dados.BoundingBox.Min.Z);
                dados.PontoFim = new XYZ(basePt.X, basePt.Y, dados.BoundingBox.Max.Z);
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
        //  Coleta de referências com Edge.Reference + GetSymbolGeometry
        //  (seguindo o padrão do CotasService do Victor)
        // ================================================================

        /// <summary>
        /// Referência de aresta qualificada para cotagem, com posição projetada.
        /// </summary>
        private sealed class RefCota
        {
            public Reference Referencia { get; set; } = null!;
            public double Posicao { get; set; }
            public string Chave { get; set; } = "";
        }

        /// <summary>
        /// Coleta referências lineares (Edge.Reference) ao longo de um eixo de medição.
        /// Usa GetSymbolGeometry() com transform acumulado (padrão que funciona com NewDimension).
        /// </summary>
        private List<RefCota> ColetarReferenciasNoEixo(
            Document doc, View view, FamilyInstance elem,
            XYZ eixoMedicao, XYZ normalVista)
        {
            Options opcoes = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = view
            };

            GeometryElement? geo = elem.get_Geometry(opcoes);
            if (geo == null) return new List<RefCota>();

            var referencias = new List<RefCota>();
            ColetarEdgesRecursivo(doc, geo, Transform.Identity, eixoMedicao, normalVista, referencias);

            // Fallback: tentar FamilyInstanceReferenceType se não achou edges
            if (referencias.Count < 2)
            {
                TentarAdicionarReferenciasPorTipo(doc, view, elem, eixoMedicao, referencias);
            }

            return referencias;
        }

        /// <summary>
        /// Percorre a geometria recursivamente usando GetSymbolGeometry()
        /// e coleta Edge.Reference das arestas que servem como referência de cota.
        /// </summary>
        private void ColetarEdgesRecursivo(
            Document doc,
            IEnumerable<GeometryObject> objetos,
            Transform transformAcumulada,
            XYZ eixoMedicao,
            XYZ normalVista,
            List<RefCota> referencias)
        {
            foreach (GeometryObject obj in objetos)
            {
                if (obj is GeometryInstance instancia)
                {
                    // GetSymbolGeometry() mantém as referências originais válidas.
                    // Acumulamos a transformação manualmente para projetar posições.
                    Transform proximoTransform = transformAcumulada.Multiply(instancia.Transform);
                    ColetarEdgesRecursivo(
                        doc,
                        instancia.GetSymbolGeometry(),
                        proximoTransform,
                        eixoMedicao, normalVista, referencias);
                    continue;
                }

                if (obj is Solid solid && !solid.Faces.IsEmpty)
                {
                    ProcessarSolidParaCota(doc, solid, transformAcumulada, eixoMedicao, normalVista, referencias);
                }
            }
        }

        /// <summary>
        /// Para cada face do sólido cujo normal é (anti)paralelo ao eixo de medição,
        /// coleta as arestas lineares que são paralelas à normal da vista
        /// (ou seja, aparecem como linhas/pontos na vista — referências lineares válidas).
        /// </summary>
        private void ProcessarSolidParaCota(
            Document doc,
            Solid solid,
            Transform transform,
            XYZ eixoMedicao,
            XYZ normalVista,
            List<RefCota> referencias)
        {
            foreach (Face face in solid.Faces)
            {
                if (face is not PlanarFace pf) continue;

                // Normal da face no espaço do modelo
                XYZ normalModelo = transform.OfVector(pf.FaceNormal);

                // A face deve ser perpendicular ao eixo de medição
                // (normal da face paralela ao eixo ⇒ a face "olha" na direção da medição)
                double dotEixo = Math.Abs(normalModelo.DotProduct(eixoMedicao));
                if (dotEixo < 0.85) continue; // tolerância ~30°

                // Coleta arestas desta face
                foreach (EdgeArray loop in pf.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        if (edge.Reference == null) continue;

                        Curve curva = edge.AsCurve();
                        if (curva is not Line linhaAresta) continue;

                        // A aresta deve ser paralela à normal da vista
                        // (assim ela aparece como um ponto/linha na vista e gera ref linear válida)
                        XYZ direcaoAresta = transform.OfVector(linhaAresta.Direction).Normalize();
                        double dotNormalVista = Math.Abs(direcaoAresta.DotProduct(normalVista));
                        if (dotNormalVista < 0.7) continue;

                        // Posição da aresta projetada no eixo de medição
                        XYZ pontoMedio = transform.OfPoint(curva.Evaluate(0.5, true));
                        double posicao = pontoMedio.DotProduct(eixoMedicao);

                        string chave = edge.Reference.ConvertToStableRepresentation(doc);

                        // Evitar duplicatas (mesma posição)
                        bool duplicata = referencias.Any(r =>
                            Math.Abs(r.Posicao - posicao) < UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Millimeters));

                        if (!duplicata)
                            referencias.Add(new RefCota { Referencia = edge.Reference, Posicao = posicao, Chave = chave });
                    }
                }
            }
        }

        /// <summary>
        /// Fallback: usa FamilyInstance.GetReferences() para obter referências
        /// dos planos de referência internos da família.
        /// </summary>
        private void TentarAdicionarReferenciasPorTipo(
            Document doc, View view, FamilyInstance elem,
            XYZ eixoMedicao, List<RefCota> referencias)
        {
            // Mapear eixo de medição para tipo de referência
            var tiposParaTentar = new List<FamilyInstanceReferenceType>();

            XYZ eixoH = ObterEixoHorizontalDaVista(view);
            XYZ eixoV = ObterEixoVerticalDaVista(view);
            double alignH = Math.Abs(eixoMedicao.DotProduct(eixoH));
            double alignV = Math.Abs(eixoMedicao.DotProduct(eixoV));

            if (alignH >= alignV)
            {
                tiposParaTentar.Add(FamilyInstanceReferenceType.Left);
                tiposParaTentar.Add(FamilyInstanceReferenceType.Right);
                tiposParaTentar.Add(FamilyInstanceReferenceType.CenterLeftRight);
            }
            else
            {
                tiposParaTentar.Add(FamilyInstanceReferenceType.Top);
                tiposParaTentar.Add(FamilyInstanceReferenceType.Bottom);
                tiposParaTentar.Add(FamilyInstanceReferenceType.CenterElevation);
            }

            foreach (var tipo in tiposParaTentar)
            {
                try
                {
                    IList<Reference> refs = elem.GetReferences(tipo);
                    if (refs != null && refs.Count > 0)
                    {
                        XYZ? centro = ObterCentroDoElemento(elem, view);
                        if (centro == null) continue;

                        double posicao = centro.DotProduct(eixoMedicao);
                        string chave = refs[0].ConvertToStableRepresentation(doc);

                        if (!referencias.Any(r => r.Chave == chave))
                            referencias.Add(new RefCota { Referencia = refs[0], Posicao = posicao, Chave = chave });
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Tipo de referencia nao suportado pela familia"); }
            }
        }

        private XYZ? ObterCentroDoElemento(Element elem, View view)
        {
            BoundingBoxXYZ? bb = elem.get_BoundingBox(view);
            if (bb == null) return null;
            return (bb.Min + bb.Max) / 2.0;
        }

        // ================================================================
        //  Cotas específicas
        // ================================================================

        /// <summary>
        /// Cota de comprimento total: entre as faces de extremidade da peça.
        /// </summary>
        private Dimension? CriarCotaEntreExtremos(
            Document doc, View view, FamilyInstance elem, DadosPeca dados,
            double offset, XYZ eixoH, XYZ eixoV, XYZ normalVista)
        {
            try
            {
                // Eixo de medição = direção da peça projetada no plano da vista
                XYZ eixoMedicao = ProjetarNoPlanoDaVista(dados.Direcao, normalVista);
                if (eixoMedicao.GetLength() < 1e-6) return null;
                eixoMedicao = eixoMedicao.Normalize();

                // Linha de cota: paralela ao eixo de medição, com offset perpendicular
                XYZ centro = (dados.PontoInicio + dados.PontoFim) / 2.0;
                XYZ offsetDir = DeterminarDirecaoOffset(view, eixoMedicao, eixoH, eixoV);
                XYZ pontoLinha = centro + offsetDir * offset;
                Line dimLine = Line.CreateUnbound(pontoLinha, eixoMedicao);

                // 1a tentativa: Family refs canônicos (Left/Right) — apontam para FACES
                Dimension? dim = CriarCotaViaFamilyRefs(
                    doc, view, elem, dimLine,
                    FamilyInstanceReferenceType.Left,
                    FamilyInstanceReferenceType.Right);
                if (dim != null) return dim;

                // 2a tentativa: coletor por edges (fallback)
                List<RefCota> refs = ColetarReferenciasNoEixo(doc, view, elem, eixoMedicao, normalVista);
                if (refs.Count < 2) return null;

                refs.Sort((a, b) => a.Posicao.CompareTo(b.Posicao));

                ReferenceArray refArray = new ReferenceArray();
                refArray.Append(refs.First().Referencia);
                refArray.Append(refs.Last().Referencia);

                return doc.Create.NewDimension(view, dimLine, refArray);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao criar cota de fabricacao"); return null; }
        }

        /// <summary>
        /// Tenta criar cota usando FamilyInstance.GetReferences(tipo) —
        /// essas referências apontam para planos de face da família (Left/Right/Top/Bottom/Front/Back)
        /// e produzem cotas canônicas que se estendem ao longo de faces inteiras.
        /// Retorna null se a família não tiver os tipos solicitados.
        /// </summary>
        private Dimension? CriarCotaViaFamilyRefs(
            Document doc, View view, FamilyInstance elem, Line dimLine,
            FamilyInstanceReferenceType tipoA, FamilyInstanceReferenceType tipoB)
        {
            try
            {
                IList<Reference> refsA = elem.GetReferences(tipoA);
                IList<Reference> refsB = elem.GetReferences(tipoB);
                if (refsA == null || refsA.Count == 0) return null;
                if (refsB == null || refsB.Count == 0) return null;

                ReferenceArray arr = new ReferenceArray();
                arr.Append(refsA[0]);
                arr.Append(refsB[0]);

                return doc.Create.NewDimension(view, dimLine, arr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cota de altura do perfil (d): mede a altura da seção.
        /// </summary>
        private Dimension? CriarCotaAlturaPerfil(
            Document doc, View view, FamilyInstance elem, DadosPeca dados,
            double offset, XYZ eixoH, XYZ eixoV, XYZ normalVista)
        {
            try
            {
                // Direção da altura: vertical para vigas, depende da orientação para pilares
                XYZ dirAltura;
                if (dados.EhPilar)
                    dirAltura = ProjetarNoPlanoDaVista(XYZ.BasisZ, normalVista);
                else
                    dirAltura = ProjetarNoPlanoDaVista(XYZ.BasisZ, normalVista);

                if (dirAltura.GetLength() < 1e-6) return null;
                dirAltura = dirAltura.Normalize();

                // Posicionar cota no início da peça, offset lateral
                XYZ eixoMedicaoPeca = ProjetarNoPlanoDaVista(dados.Direcao, normalVista);
                XYZ pontoBase;
                if (eixoMedicaoPeca.GetLength() > 1e-6)
                    pontoBase = dados.PontoInicio - eixoMedicaoPeca.Normalize() * offset;
                else
                    pontoBase = dados.PontoInicio;

                Line dimLine = Line.CreateUnbound(pontoBase, dirAltura);

                // 1a tentativa: Family refs canônicos (Top/Bottom) — apontam para FACES
                Dimension? dim = CriarCotaViaFamilyRefs(
                    doc, view, elem, dimLine,
                    FamilyInstanceReferenceType.Top,
                    FamilyInstanceReferenceType.Bottom);
                if (dim != null) return dim;

                // 2a tentativa: edges (fallback)
                List<RefCota> refs = ColetarReferenciasNoEixo(doc, view, elem, dirAltura, normalVista);
                if (refs.Count < 2) return null;

                refs.Sort((a, b) => a.Posicao.CompareTo(b.Posicao));

                ReferenceArray refArray = new ReferenceArray();
                refArray.Append(refs.First().Referencia);
                refArray.Append(refs.Last().Referencia);

                return doc.Create.NewDimension(view, dimLine, refArray);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao criar cota de fabricacao"); return null; }
        }

        /// <summary>
        /// Cota de largura da mesa (bf): mede a largura na direção perpendicular horizontal.
        /// </summary>
        private Dimension? CriarCotaLarguraMesa(
            Document doc, View view, FamilyInstance elem, DadosPeca dados,
            double offset, XYZ eixoH, XYZ eixoV, XYZ normalVista)
        {
            try
            {
                // Direção da largura: perpendicular ao eixo da peça e à vertical
                XYZ dirLargura;
                if (dados.EhPilar)
                {
                    dirLargura = ProjetarNoPlanoDaVista(XYZ.BasisX, normalVista);
                    if (dirLargura.GetLength() < 1e-6)
                        dirLargura = ProjetarNoPlanoDaVista(XYZ.BasisY, normalVista);
                }
                else
                {
                    // Perpendicular horizontal ao eixo da viga
                    XYZ perp = new XYZ(-dados.Direcao.Y, dados.Direcao.X, 0);
                    dirLargura = ProjetarNoPlanoDaVista(perp, normalVista);
                }

                if (dirLargura.GetLength() < 1e-6) return null;
                dirLargura = dirLargura.Normalize();

                // Posicionar abaixo da peça
                XYZ pontoBase;
                if (dados.BoundingBox != null)
                {
                    pontoBase = new XYZ(
                        (dados.BoundingBox.Min.X + dados.BoundingBox.Max.X) / 2.0,
                        (dados.BoundingBox.Min.Y + dados.BoundingBox.Max.Y) / 2.0,
                        dados.BoundingBox.Min.Z) - XYZ.BasisZ * offset;
                }
                else
                {
                    pontoBase = dados.PontoInicio - XYZ.BasisZ * offset;
                }

                Line dimLine = Line.CreateUnbound(pontoBase, dirLargura);

                // 1a tentativa: Family refs canônicos (Front/Back) — apontam para FACES
                Dimension? dim = CriarCotaViaFamilyRefs(
                    doc, view, elem, dimLine,
                    FamilyInstanceReferenceType.Front,
                    FamilyInstanceReferenceType.Back);
                if (dim != null) return dim;

                // 2a tentativa: edges (fallback)
                List<RefCota> refs = ColetarReferenciasNoEixo(doc, view, elem, dirLargura, normalVista);
                if (refs.Count < 2) return null;

                refs.Sort((a, b) => a.Posicao.CompareTo(b.Posicao));

                ReferenceArray refArray = new ReferenceArray();
                refArray.Append(refs.First().Referencia);
                refArray.Append(refs.Last().Referencia);

                return doc.Create.NewDimension(view, dimLine, refArray);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao criar cota de fabricacao"); return null; }
        }

        // ================================================================
        //  Helpers de vista
        // ================================================================

        private XYZ ProjetarNoPlanoDaVista(XYZ vetor, XYZ normalVista)
        {
            // Projeta um vetor no plano da vista (remove componente na direção normal)
            return vetor - normalVista * vetor.DotProduct(normalVista);
        }

        private XYZ DeterminarDirecaoOffset(View view, XYZ eixoMedicao, XYZ eixoH, XYZ eixoV)
        {
            // O offset é perpendicular ao eixo de medição no plano da vista
            double dotH = Math.Abs(eixoMedicao.DotProduct(eixoH));
            if (dotH > 0.7)
                return eixoV.Negate(); // mede na horizontal → offset para baixo
            else
                return eixoH.Negate(); // mede na vertical → offset para a esquerda
        }

        private XYZ ObterEixoHorizontalDaVista(View view)
        {
            if (view is ViewPlan) return XYZ.BasisX;
            if (view is ViewSection vs)
                return vs.RightDirection;
            return view.RightDirection;
        }

        private XYZ ObterEixoVerticalDaVista(View view)
        {
            if (view is ViewPlan) return XYZ.BasisY;
            if (view is ViewSection vs)
                return vs.UpDirection;
            return view.UpDirection;
        }

        // ================================================================
        //  Selecao de elementos
        // ================================================================

        private List<FamilyInstance> ObterElementos(
            UIDocument uidoc, Document doc, EscopoCotagem escopo)
        {
            if (escopo == EscopoCotagem.VistaAtiva)
                return ColetarElementosDaVista(doc);

            // Seleção manual (já selecionados)
            List<FamilyInstance> selecionados = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Where(EhElementoEstrutural)
                .ToList();

            if (selecionados.Count > 0)
                return selecionados;

            // Pedir seleção
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroEstruturalCotagem(),
                    "Selecione as peças a cotar e pressione Enter");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<FamilyInstance>()
                    .Where(EhElementoEstrutural)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<FamilyInstance>();
            }
        }

        private List<FamilyInstance> ColetarElementosDaVista(Document doc)
        {
            View vista = doc.ActiveView;
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
                    .Where(EhElementoEstrutural);

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

        private bool VistaSuportada(View view)
        {
            return view is ViewPlan
                || view is ViewSection
                || (view.ViewType == ViewType.Elevation)
                || (view.ViewType == ViewType.Detail);
        }

        // ================================================================
        //  Filtro de seleção e Failure handler
        // ================================================================

        private class FiltroEstruturalCotagem : ISelectionFilter
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

        // WarningSwallower extraido para FerramentaEMT.Utils.FailureHandlingHelper (P1.1, 2026-04-28).
    }
}
