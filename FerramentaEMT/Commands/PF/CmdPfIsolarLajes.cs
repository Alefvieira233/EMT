using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services.PF;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfIsolarLajes : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Isolar Lajes";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;
            List<ElementId> ids = PfElementService.CollectIdsInView(
                doc,
                view,
                fi => PfElementService.IsPfLaje(fi));

            return PfIsolationService.IsolateElements(
                uidoc,
                ids,
                CommandName,
                "Nenhuma laje PF foi encontrada na vista ativa.");
        }
    }
}
