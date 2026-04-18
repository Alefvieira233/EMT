#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Verifica elementos estruturais (vigas, pilares) sem material atribuido.
    /// Procura pelo parametro STRUCTURAL_MATERIAL_PARAM.
    /// </summary>
    public class MissingMaterialRule : IModelCheckRule
    {
        public string Name => "Material Ausente";
        public string Description => "Localiza elementos estruturais sem material atribuido no parametro STRUCTURAL_MATERIAL_PARAM.";

        public IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId>? scopeIds)
        {
            var issues = new List<ModelCheckIssue>();
            if (doc == null)
                return issues;

            // Coletor de elementos estruturais
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
                    // Tenta acessar o parametro STRUCTURAL_MATERIAL_PARAM
                    Parameter matParam = elem.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);

                    bool isMissing = matParam == null ||
                                    string.IsNullOrWhiteSpace(matParam.AsString());

                    if (isMissing)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Warning,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' ({elem.Symbol.Name}) sem material atribuido.",
                            Suggestion = "Atribua um material estrutural ao elemento (aco, aluminio, etc.)."
                        });
                    }
                }
                catch (Exception)
                {
                    // Ignorar elementos com erro na leitura de parametros
                    // Continuar com proximo
                }
            }

            return issues;
        }
    }
}
