using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Commands;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosEstaca : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Estaca";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            System.Collections.Generic.List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsStructuralPile,
                "Selecione as estacas para lançar a armadura longitudinal...");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfEstacaRebarWindow window = new PfEstacaRebarWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfEstacaRebarConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteEstacaBars(uidoc, config);
        }
    }
}
