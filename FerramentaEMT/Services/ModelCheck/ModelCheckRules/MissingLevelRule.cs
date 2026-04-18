#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Verifica elementos estruturais sem nivel (LevelId == InvalidElementId).
    /// </summary>
    public class MissingLevelRule : IModelCheckRule
    {
        public string Name => "Nivel Ausente";
        public string Description => "Localiza elementos estruturais sem nivel atribuido.";

        public IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId>? scopeIds)
        {
            var issues = new List<ModelCheckIssue>();
            if (doc == null)
                return issues;

            FilteredElementCollector collector = (scopeIds != null && scopeIds.Count > 0)
                ? new FilteredElementCollector(doc, scopeIds)
                : new FilteredElementCollector(doc);
            collector = collector
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming);

            foreach (FamilyInstance elem in collector)
            {
                if (elem == null || elem.Symbol == null)
                    continue;

                try
                {
                    if (elem.LevelId == null || elem.LevelId == ElementId.InvalidElementId)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' ({elem.Symbol.Name}) nao tem nivel atribuido.",
                            Suggestion = "Atribua o elemento a um nivel existente no projeto."
                        });
                    }
                }
                catch (Exception)
                {
                    // Ignorar
                }
            }

            return issues;
        }
    }
}
