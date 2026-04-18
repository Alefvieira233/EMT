using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.PF
{
    internal static class PfIsolationService
    {
        public static Result IsolateElements(
            UIDocument uidoc,
            IEnumerable<ElementId> elementIds,
            string commandName,
            string emptyMessage)
        {
            List<ElementId> ids = elementIds?
                .Where(x => x != null && x != ElementId.InvalidElementId)
                .Distinct()
                .ToList() ?? new List<ElementId>();

            if (ids.Count == 0)
            {
                AppDialogService.ShowWarning(commandName, emptyMessage, "Nada para isolar");
                return Result.Cancelled;
            }

            using (Transaction transaction = new Transaction(uidoc.Document, commandName))
            {
                transaction.Start();
                uidoc.Document.ActiveView.IsolateElementsTemporary(ids);
                transaction.Commit();
            }

            uidoc.Selection.SetElementIds(ids);
            return Result.Succeeded;
        }
    }
}
