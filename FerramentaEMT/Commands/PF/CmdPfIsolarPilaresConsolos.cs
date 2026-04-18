using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services.PF;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfIsolarPilaresConsolos : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Isolar Pilares + Consolos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;
            List<ElementId> ids = PfElementService.CollectIdsInView(
                doc,
                view,
                fi => PfElementService.IsStructuralColumn(fi) || PfElementService.IsPfConsolo(fi));

            return PfIsolationService.IsolateElements(
                uidoc,
                ids,
                CommandName,
                "Nenhum pilar ou consolo PF foi encontrado na vista ativa.");
        }
    }
}
