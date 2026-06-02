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
    public class CmdPfInserirAcosViga : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Viga";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            SteelBIM.Utils.DisclaimerService.MostrarUmaVezPorSessao(
                "pf-armadura", CommandName, SteelBIM.Utils.DisclaimerTexts.Armadura);

            List<Element> hosts = PfElementService.PrepararHosts(
                uidoc,
                PfElementService.IsStructuralBeam,
                "Selecione as vigas estruturais para configurar e lancar as barras.");

            if (hosts.Count == 0)
                return Result.Cancelled;


            PfBeamBarsWindow window = new PfBeamBarsWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfBeamBarsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteBeamBars(uidoc, config, hosts);
        }
    }
}
