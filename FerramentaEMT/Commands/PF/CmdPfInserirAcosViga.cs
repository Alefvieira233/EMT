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
    public class CmdPfInserirAcosViga : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Viga";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsStructuralBeam,
                "Selecione as vigas estruturais para configurar e lancar as barras.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfBeamBarsWindow window = new PfBeamBarsWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfBeamBarsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteBeamBars(uidoc, config);
        }
    }
}
