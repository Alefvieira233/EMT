#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Verifica elementos estruturais (vigas/pilares) com comprimento zero ou menor que 1mm.
    /// Usa STRUCTURAL_FRAME_CUT_LENGTH se disponivel, caso contrario location curve length.
    /// </summary>
    public class ZeroLengthRule : IModelCheckRule
    {
        public string Name => "Comprimento Zero";
        public string Description => "Localiza elementos estruturais com comprimento zero ou menor que 1mm.";

        private const double MinLengthMm = 1.0;
        private const double MmToInch = 0.00328084; // 1mm = 0.00328084 inches (Revit internal)

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
                    double lengthInches = 0;

                    // Tentar parametro STRUCTURAL_FRAME_CUT_LENGTH primeiro
                    Parameter cutLengthParam = elem.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                    if (cutLengthParam != null && cutLengthParam.StorageType == StorageType.Double)
                    {
                        lengthInches = cutLengthParam.AsDouble();
                    }
                    else if (elem.Location is LocationCurve locCurve && locCurve.Curve != null)
                    {
                        lengthInches = locCurve.Curve.Length;
                    }

                    // Converter para mm e verificar
                    double lengthMm = lengthInches / MmToInch;

                    if (lengthMm < MinLengthMm)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Error,
                            ElementId = elem.Id.Value,
                            Description = string.Format(CultureInfo.InvariantCulture,
                                        "Elemento '{0}' ({1}) tem comprimento de {2:F2}mm (minimo esperado: {3}mm).",
                                        elem.Name, elem.Symbol.Name, lengthMm, MinLengthMm),
                            Suggestion = "Verifique o posicionamento do elemento — pode estar muito curto ou com uma localizacao invalida."
                        });
                    }
                }
                catch (Exception ex)
                {
                    Infrastructure.Logger.Warn(ex, "Falha ao verificar comprimento do elemento");
                }
            }

            return issues;
        }
    }
}
