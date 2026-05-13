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
    /// Verifica elementos estruturais sem marca de fabricacao (ALL_MODEL_MARK vazio).
    /// </summary>
    public class MissingMarkRule : IModelCheckRule
    {
        public string Name => "Marca Ausente";
        public string Description => "Localiza elementos estruturais sem marca de fabricacao no parametro ALL_MODEL_MARK.";

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

            int skippedOnError = 0;
            foreach (FamilyInstance elem in collector)
            {
                if (elem == null || elem.Symbol == null)
                    continue;

                try
                {
                    Parameter markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);

                    bool isMissing = markParam == null ||
                                    string.IsNullOrWhiteSpace(markParam.AsString());

                    if (isMissing)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' ({elem.Symbol.Name}) sem marca de fabricacao.",
                            Suggestion = "Use o comando 'Marcar Pecas' para atribuir marcas automaticamente."
                        });
                    }
                }
                catch (Exception)
                {
                    skippedOnError++;
                }
            }

            if (skippedOnError > 0)
                Logger.Warn("[{Rule}] {Count} elemento(s) pulado(s) por erro na leitura de parametros.", Name, skippedOnError);

            return issues;
        }
    }
}
