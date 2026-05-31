#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models.PF;

namespace SteelBIM.Services.PF
{
    internal static class PfRebarShapeCatalog
    {
        public static IReadOnlyList<PfRebarShapeOption> LoadStirrupTieShapes(Document? doc)
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

        /// <summary>
        /// Seleciona no ComboBox o item cujo Name bate (case-insensitive) com o
        /// preferredName. Retorna true se encontrou e selecionou, false caso contrário.
        /// Usado pela PfEstacaRebarWindow pra pré-selecionar a shape salva nas
        /// configurações do usuário.
        /// </summary>
        public static bool TrySelect(ComboBox? combo, string? preferredName)
        {
            if (combo == null || string.IsNullOrWhiteSpace(preferredName))
                return false;

            foreach (object item in combo.Items)
            {
                if (item is PfRebarShapeOption option &&
                    string.Equals(option.Name, preferredName, StringComparison.CurrentCultureIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }

        private static string BuildDisplayName(string? name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? "(sem nome)"
                : name.Trim();
        }

        private static decimal GetNumericOrder(string? name)
        {
            if (decimal.TryParse((name ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                return value;

            return decimal.MaxValue;
        }
    }
}
