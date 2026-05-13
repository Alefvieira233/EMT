using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace FerramentaEMT.Services.PF
{
    internal sealed class PfRebarBarTypeOption
    {
        public PfRebarBarTypeOption(ElementId id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public ElementId Id { get; }
        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    internal static class PfRebarTypeCatalog
    {
        public static List<PfRebarBarTypeOption> Load(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new PfRebarBarTypeOption(x.Id, x.Name))
                .ToList();
        }

        public static bool TrySelect(ComboBox combo, string preferredName)
        {
            if (combo == null)
                return false;

            foreach (object item in combo.Items)
            {
                if (item is PfRebarBarTypeOption option &&
                    string.Equals(option.Name, preferredName, StringComparison.CurrentCultureIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }
    }
}
