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
    /// Verifica elementos estruturais com perfil ausente ou dimensoes zero (height/width).
    /// </summary>
    public class MissingProfileRule : IModelCheckRule
    {
        public string Name => "Perfil Ausente";
        public string Description => "Localiza elementos estruturais com simbolo ausente ou dimensoes zero (altura/largura).";

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
                    // Verificar simbolo
                    if (elem.Symbol == null || elem.Symbol.Family == null)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' nao tem simbolo/tipo de familia associado.",
                            Suggestion = "Atribua um tipo de familia valido com dimensoes definidas."
                        });
                        continue;
                    }

                    // Verificar dimensoes (altura e largura sao comuns em perfis)
                    double height = 0, width = 0;

                    Parameter hParam = elem.Symbol.LookupParameter("HEIGHT") ??
                                      elem.Symbol.LookupParameter("h") ??
                                      elem.Symbol.LookupParameter("Altura");

                    Parameter wParam = elem.Symbol.LookupParameter("WIDTH") ??
                                      elem.Symbol.LookupParameter("w") ??
                                      elem.Symbol.LookupParameter("Largura");

                    if (hParam != null && hParam.StorageType == StorageType.Double)
                        height = hParam.AsDouble();

                    if (wParam != null && wParam.StorageType == StorageType.Double)
                        width = wParam.AsDouble();

                    // Se ambas sao zero, flagar como problema
                    if (Math.Abs(height) < 0.001 && Math.Abs(width) < 0.001)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = $"Elemento '{elem.Name}' ({elem.Symbol.Name}) tem dimensoes zero (altura e largura).",
                            Suggestion = "Verifique o simbolo e as dimensoes do perfil — podem estar incorretos ou nao definidos."
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
