#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Verifica instancias de familias com simbolo ou familia nulos.
    /// </summary>
    public class StructuralWithoutTypeRule : IModelCheckRule
    {
        public string Name => "Sem Tipo";
        public string Description => "Localiza instancias de familias estruturais sem simbolo ou familia.";

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
                if (elem == null)
                    continue;

                try
                {
                    if (elem.Symbol == null)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento estrutural '{elem.Name}' nao tem tipo/simbolo associado.",
                            Suggestion = "Atribua um tipo (simbolo) de familia valido ao elemento."
                        });
                        continue;
                    }

                    if (elem.Symbol.Family == null)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Simbolo '{elem.Symbol.Name}' nao tem familia associada.",
                            Suggestion = "Verifique a integridade do simbolo e recarregue a familia se necessario."
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
