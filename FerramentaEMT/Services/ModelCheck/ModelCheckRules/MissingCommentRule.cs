#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Verifica elementos estruturais sem comentario.
    /// Esta e uma verificacao INFORMATIVA apenas (INFO severity).
    /// </summary>
    public class MissingCommentRule : IModelCheckRule
    {
        public string Name => "Sem Comentario";
        public string Description => "Localiza elementos estruturais sem comentario (informativa).";

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
                    Parameter commentParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

                    bool hasComment = commentParam != null &&
                                    !string.IsNullOrWhiteSpace(commentParam.AsString());

                    if (!hasComment)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Info,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' ({elem.Symbol.Name}) nao tem comentario.",
                            Suggestion = "Considere adicionar um comentario para documentacao e rastreabilidade."
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
