using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosPilar : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Pilar";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsStructuralColumn,
                "Selecione os pilares estruturais para configurar e lancar as barras longitudinais.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfColumnBarsWindow window = new PfColumnBarsWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfColumnBarsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteColumnBars(uidoc, config);
        }
    }
}
