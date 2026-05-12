#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Conexoes;

namespace FerramentaEMT.Services.Conexoes
{
    public sealed class PlacaBaseLancamentoService
    {
        private static readonly string[] SteelHints = { "steel", "aço", "aco", "metal" };
        private static readonly string[] ConcreteHints = { "concrete", "concreto" };

        public static IList<FamilySymbol> CollectCompatibleSymbols(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(IsSupportedBasePlateSymbol)
                .OrderBy(s => s.Family?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public PlacaBaseLancamentoResultado Lancar(Document doc, PlacaBaseConfig config)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            FamilySymbol symbol = ResolveSymbol(doc, config)
                ?? throw new InvalidOperationException("Família/tipo de placa de base não encontrado no modelo.");

            if (!IsSupportedBasePlateSymbol(symbol))
                throw new InvalidOperationException("O tipo selecionado precisa ser uma família face-based/work plane-based.");

            IList<FamilyInstance> pilaresMetalicos = CollectSteelColumns(doc);
            IList<Element> apoiosConcreto = CollectConcreteSupports(doc);

            var resultado = new PlacaBaseLancamentoResultado
            {
                PilaresAnalisados = pilaresMetalicos.Count
            };

            using (Transaction tx = new Transaction(doc, "Lançar placas de base"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                foreach (FamilyInstance pilar in pilaresMetalicos)
                {
                    XYZ pontoBase = GetColumnBasePoint(pilar);
                    Element? apoio = FindConcreteBelow(pontoBase, apoiosConcreto);

                    if (apoio == null)
                    {
                        resultado.PilaresSemApoio++;
                        continue;
                    }

                    PlanarFace? faceSuperior = GetTopFace(apoio);
                    if (faceSuperior == null)
                    {
                        resultado.PilaresSemFaceSuperior++;
                        continue;
                    }

                    IntersectionResult? projecao = faceSuperior.Project(pontoBase);
                    if (projecao == null)
                    {
                        resultado.PilaresSemFaceSuperior++;
                        continue;
                    }

                    XYZ pontoInsercao = projecao.XYZPoint;
                    XYZ orientacao = GetReferenceDirection(pilar, faceSuperior.FaceNormal);

                    try
                    {
                        FamilyInstance placa = doc.Create.NewFamilyInstance(
                            faceSuperior,
                            pontoInsercao,
                            orientacao,
                            symbol);

                        if (config.SobrescreverParametros)
                            ApplyParameters(placa, config);

                        TrySetElementId(placa, "EMT_Pilar_Base", pilar.Id);
                        TrySetElementId(placa, "EMT_Apoio_Base", apoio.Id);
                        TrySetString(placa, "Comments", $"EMT_PLACA_BASE|Pilar:{pilar.Id.Value}|Apoio:{apoio.Id.Value}");

                        resultado.PlacasInseridas++;
                    }
                    catch (Exception ex)
                    {
                        resultado.FalhasInsercao++;
                        Logger.Error(ex,
                            "[PlacaBaseLancamento] Falha ao inserir placa no pilar {PilarId} sobre apoio {ApoioId}",
                            pilar.Id.Value,
                            apoio.Id.Value);
                    }
                }

                tx.Commit();
            }

            Logger.Info("[PlacaBaseLancamento] {Resumo}", resultado.Resumo().Replace('\n', ' '));
            return resultado;
        }

        private static FamilySymbol? ResolveSymbol(Document doc, PlacaBaseConfig config)
        {
            if (config.FamilySymbolId != ElementId.InvalidElementId &&
                doc.GetElement(config.FamilySymbolId) is FamilySymbol direct)
            {
                return direct;
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                    string.Equals(fs.Family?.Name, config.FamilyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fs.Name, config.TypeName, StringComparison.OrdinalIgnoreCase));
        }

        private static IList<FamilyInstance> CollectSteelColumns(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(IsSteelColumn)
                .ToList();
        }

        private static IList<Element> CollectConcreteSupports(Document doc)
        {
            var supportedCategories = new[]
            {
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Walls
            };

            var items = new List<Element>();
            foreach (BuiltInCategory category in supportedCategories)
            {
                items.AddRange(new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .Where(e => IsConcreteElement(e)));
            }

            return items
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();
        }

        private static bool IsSteelColumn(FamilyInstance instance)
        {
            return HasMaterialHint(instance, SteelHints);
        }

        private static bool IsConcreteElement(Element element)
        {
            return HasMaterialHint(element, ConcreteHints);
        }

        private static bool HasMaterialHint(Element element, string[] hints)
        {
            foreach (Material material in GetElementMaterials(element))
            {
                string name = (material?.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (hints.Any(h => name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }

            return false;
        }

        private static IEnumerable<Material> GetElementMaterials(Element element)
        {
            var doc = element.Document;
            var seen = new HashSet<long>();

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter?.StorageType != StorageType.ElementId)
                    continue;

                ElementId id = parameter.AsElementId();
                if (id == null || id == ElementId.InvalidElementId || !seen.Add(id.Value))
                    continue;

                if (doc.GetElement(id) is Material material)
                    yield return material;
            }

            if (element is FamilyInstance fi && fi.Symbol != null)
            {
                foreach (Parameter parameter in fi.Symbol.Parameters)
                {
                    if (parameter?.StorageType != StorageType.ElementId)
                        continue;

                    ElementId id = parameter.AsElementId();
                    if (id == null || id == ElementId.InvalidElementId || !seen.Add(id.Value))
                        continue;

                    if (doc.GetElement(id) is Material material)
                        yield return material;
                }
            }
        }

        private static XYZ GetColumnBasePoint(FamilyInstance pilar)
        {
            if (pilar.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ? bb = pilar.get_BoundingBox(null);
            if (bb == null)
                return XYZ.Zero;

            return new XYZ(
                (bb.Min.X + bb.Max.X) * 0.5,
                (bb.Min.Y + bb.Max.Y) * 0.5,
                bb.Min.Z);
        }

        private static Element? FindConcreteBelow(XYZ point, IEnumerable<Element> supports)
        {
            double toleranceZ = ToFeet(150);
            Element? best = null;
            double bestDistance = double.MaxValue;

            foreach (Element support in supports)
            {
                BoundingBoxXYZ? bb = support.get_BoundingBox(null);
                if (bb == null)
                    continue;

                bool insideXY =
                    point.X >= bb.Min.X &&
                    point.X <= bb.Max.X &&
                    point.Y >= bb.Min.Y &&
                    point.Y <= bb.Max.Y;

                if (!insideXY)
                    continue;

                double dz = point.Z - bb.Max.Z;
                if (dz < -toleranceZ)
                    continue;

                double abs = Math.Abs(dz);
                if (abs < bestDistance)
                {
                    bestDistance = abs;
                    best = support;
                }
            }

            return best;
        }

        private static PlanarFace? GetTopFace(Element element)
        {
            Options options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            PlanarFace? bestFace = null;
            double highestZ = double.MinValue;

            foreach (Solid solid in EnumerateSolids(element.get_Geometry(options)))
            {
                if (solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace planarFace)
                        continue;

                    XYZ normal = planarFace.FaceNormal.Normalize();
                    if (normal.Z < 0.9)
                        continue;

                    BoundingBoxUV bb = planarFace.GetBoundingBox();
                    UV centerUv = new UV(
                        (bb.Min.U + bb.Max.U) * 0.5,
                        (bb.Min.V + bb.Max.V) * 0.5);
                    XYZ center = planarFace.Evaluate(centerUv);

                    if (center.Z > highestZ)
                    {
                        highestZ = center.Z;
                        bestFace = planarFace;
                    }
                }
            }

            return bestFace;
        }

        private static IEnumerable<Solid> EnumerateSolids(GeometryElement? geometry)
        {
            if (geometry == null)
                yield break;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 1e-9)
                {
                    yield return solid;
                    continue;
                }

                if (obj is GeometryInstance instance)
                {
                    foreach (Solid nested in EnumerateSolids(instance.GetInstanceGeometry()))
                        yield return nested;
                }
            }
        }

        private static XYZ GetReferenceDirection(FamilyInstance pilar, XYZ faceNormal)
        {
            Transform trf = pilar.GetTransform();
            XYZ candidate = trf.BasisX;

            if (candidate == null || candidate.IsZeroLength())
                candidate = XYZ.BasisX;

            XYZ planeDirection = candidate - faceNormal.Multiply(candidate.DotProduct(faceNormal));
            if (planeDirection.IsZeroLength())
            {
                planeDirection = XYZ.BasisY - faceNormal.Multiply(XYZ.BasisY.DotProduct(faceNormal));
            }

            return planeDirection.IsZeroLength() ? XYZ.BasisX : planeDirection.Normalize();
        }

        private static void ApplyParameters(FamilyInstance instance, PlacaBaseConfig config)
        {
            TrySetLength(instance, "Comprimento", config.ComprimentoMm);
            TrySetLength(instance, "Largura", config.LarguraMm);
            TrySetLength(instance, "Espessura", config.EspessuraMm);
            TrySetLength(instance, "Furo_Diametro", config.FuroDiametroMm);
            TrySetLength(instance, "Furo_Offset_X", config.FuroOffsetXmm);
            TrySetLength(instance, "Furo_Offset_Y", config.FuroOffsetYmm);
            TrySetLength(instance, "Chumbador_Diametro", config.ChumbadorDiametroMm);
            TrySetLength(instance, "Graute_Espessura", config.GrauteEspessuraMm);
            TrySetString(instance, "Solda", config.Solda);
        }

        private static bool IsSupportedBasePlateSymbol(FamilySymbol symbol)
        {
            if (symbol?.Family == null)
                return false;

            return symbol.Family.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased;
        }

        private static void TrySetLength(Element element, string parameterName, double valueMm)
        {
            Parameter? parameter = element.LookupParameter(parameterName);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
                return;

            parameter.Set(ToFeet(valueMm));
        }

        private static void TrySetString(Element element, string parameterName, string value)
        {
            Parameter? parameter = element.LookupParameter(parameterName);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
                return;

            parameter.Set(value ?? string.Empty);
        }

        private static void TrySetElementId(Element element, string parameterName, ElementId value)
        {
            Parameter? parameter = element.LookupParameter(parameterName);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                return;

            parameter.Set(value);
        }

        private static double ToFeet(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }
    }
}
