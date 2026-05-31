#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Utils;

namespace SteelBIM.Services.PF
{
    internal static class PfIsolationService
    {
        public static Result IsolateElements(
            UIDocument uidoc,
            IEnumerable<ElementId>? elementIds,
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

            View view = uidoc.Document.ActiveView;
            if (view == null)
            {
                Logger.Warn("[PfIsolationService] ActiveView nulo, abortando");
                AppDialogService.ShowWarning(
                    commandName,
                    "Este comando precisa de uma vista ativa. Abra uma vista no Revit antes de executar.",
                    "Sem vista ativa");
                return Result.Cancelled;
            }

            using (Transaction transaction = new Transaction(uidoc.Document, commandName))
            {
                transaction.Start();
                view.IsolateElementsTemporary(ids);
                transaction.Commit();
            }

            uidoc.Selection.SetElementIds(ids);
            return Result.Succeeded;
        }
    }
}
