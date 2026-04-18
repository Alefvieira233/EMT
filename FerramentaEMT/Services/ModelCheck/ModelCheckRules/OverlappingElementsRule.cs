#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Detecta elementos estruturais que se sobrepoe (usando bounding box + solid boolean intersection).
    /// Usa pre-filtro de bounding box por performance.
    /// </summary>
    public class OverlappingElementsRule : IModelCheckRule
    {
        public string Name => "Sobreposicao de Elementos";
        public string Description => "Localiza elementos estruturais que se sobrepoe (interseccao de geometria solida).";

        private const double MinVolumeThreshold = 0.0001; // metro cubico

        private int _skippedOnError;

        public IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId>? scopeIds)
        {
            var issues = new List<ModelCheckIssue>();
            if (doc == null)
                return issues;

            _skippedOnError = 0;

            FilteredElementCollector collector = (scopeIds != null && scopeIds.Count > 0)
                ? new FilteredElementCollector(doc, scopeIds)
                : new FilteredElementCollector(doc);
            collector = collector
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming);

            var elementos = collector.Cast<FamilyInstance>()
                .Where(e => e != null && e.Symbol != null)
                .ToList();

            if (elementos.Count < 2)
                return issues;

            // Verificar cada par de elementos
            for (int i = 0; i < elementos.Count - 1; i++)
            {
                for (int j = i + 1; j < elementos.Count; j++)
                {
                    var elem1 = elementos[i];
                    var elem2 = elementos[j];

                    if (CheckOverlap(elem1, elem2))
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Warning,
                            ElementId = elem1.Id.Value,
                            Description = $"Elemento '{elem1.Name}' ({elem1.Symbol.Name}) se sobrepoe com " +
                                        $"'{elem2.Name}' ({elem2.Symbol.Name}).",
                            Suggestion = "Revise o posicionamento e dimensoes dos elementos para evitar sobreposicoes."
                        });
                    }
                }
            }

            if (_skippedOnError > 0)
                Logger.Warn("[{Rule}] {Count} par(es) de elementos pulado(s) por erro na analise geometrica.", Name, _skippedOnError);

            return issues;
        }

        private bool CheckOverlap(FamilyInstance elem1, FamilyInstance elem2)
        {
            try
            {
                // Pre-filtro: verificar bounding boxes
                BoundingBoxXYZ bb1 = elem1.get_BoundingBox(null);
                BoundingBoxXYZ bb2 = elem2.get_BoundingBox(null);

                if (bb1 == null || bb2 == null)
                    return false;

                // Se bounding boxes nao se tocam, nao podem se sobrepor
                if (!BoundingBoxesIntersect(bb1, bb2))
                    return false;

                // Bounding boxes se sobrepoe — fazer teste mais preciso com geometria solida
                GeometryElement geom1 = elem1.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
                GeometryElement geom2 = elem2.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });

                if (geom1 == null || geom2 == null)
                    return false;

                var solids1 = ExtractSolids(geom1).ToList();
                var solids2 = ExtractSolids(geom2).ToList();

                if (solids1.Count == 0 || solids2.Count == 0)
                    return false;

                // Testar interseccao entre solidos
                foreach (var solid1 in solids1)
                {
                    foreach (var solid2 in solids2)
                    {
                        try
                        {
                            Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                                solid1, solid2, BooleanOperationsType.Intersect);

                            if (intersection != null && intersection.Volume > MinVolumeThreshold)
                                return true;
                        }
                        catch
                        {
                            // Operacao booleana nao suportada — ignorar
                        }
                    }
                }

                return false;
            }
            catch
            {
                // Erro na verificacao — asumir sem sobreposicao
                _skippedOnError++;
                return false;
            }
        }

        private bool BoundingBoxesIntersect(BoundingBoxXYZ bb1, BoundingBoxXYZ bb2)
        {
            if (bb1.Min.X > bb2.Max.X || bb2.Min.X > bb1.Max.X) return false;
            if (bb1.Min.Y > bb2.Max.Y || bb2.Min.Y > bb1.Max.Y) return false;
            if (bb1.Min.Z > bb2.Max.Z || bb2.Min.Z > bb1.Max.Z) return false;

            return true;
        }

        private IEnumerable<Solid> ExtractSolids(GeometryElement geom)
        {
            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                    yield return solid;
                else if (obj is GeometryInstance gi)
                {
                    var instanceGeom = gi.GetInstanceGeometry();
                    if (instanceGeom != null)
                    {
                        foreach (var subSolid in ExtractSolids(instanceGeom))
                            yield return subSolid;
                    }
                }
            }
        }
    }
}
