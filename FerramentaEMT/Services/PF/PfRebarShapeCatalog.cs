using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.PF;

namespace FerramentaEMT.Services.PF
{
    internal static class PfRebarShapeCatalog
    {
        public static IReadOnlyList<PfRebarShapeOption> LoadStirrupTieShapes(Document doc)
        {
            List<PfRebarShapeOption> items = new List<PfRebarShapeOption>
            {
                new PfRebarShapeOption
                {
                    Name = string.Empty,
                    DisplayName = "Automatico",
                    IsAutomatic = true
                }
            };

            if (doc == null)
                return items;

            items.AddRange(new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .Where(x => x.RebarStyle == RebarStyle.StirrupTie)
                .Select(x => new PfRebarShapeOption
                {
                    ElementIdValue = x.Id?.Value ?? 0,
                    Name = x.Name ?? string.Empty,
                    DisplayName = BuildDisplayName(x.Name)
                })
                .OrderBy(x => GetNumericOrder(x.Name))
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase));

            return items;
        }

        private static string BuildDisplayName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? "(sem nome)"
                : name.Trim();
        }

        private static decimal GetNumericOrder(string name)
        {
            if (decimal.TryParse((name ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                return value;

            return decimal.MaxValue;
        }
    }
}
