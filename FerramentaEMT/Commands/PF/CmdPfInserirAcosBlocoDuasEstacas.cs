using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosBlocoDuasEstacas : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Bloco 2 Estacas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsTwoPileCap,
                "Selecione os blocos de duas estacas para configurar e lancar a armadura.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfTwoPileCapRebarWindow window = new PfTwoPileCapRebarWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfTwoPileCapRebarConfig config = window.BuildConfig();
            return new PfTwoPileCapRebarService().Execute(uidoc, config);
        }
    }
}
