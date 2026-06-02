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
    public class CmdPfInserirEstribosViga : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Estribos Viga";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            SteelBIM.Utils.DisclaimerService.MostrarUmaVezPorSessao(
                "pf-armadura", CommandName, SteelBIM.Utils.DisclaimerTexts.Armadura);

            List<Element> hosts = PfElementService.PrepararHosts(
                uidoc,
                PfElementService.IsStructuralBeam,
                "Selecione as vigas estruturais para configurar e lancar os estribos.");

            if (hosts.Count == 0)
                return Result.Cancelled;


            PfBeamStirrupsWindow window = new PfBeamStirrupsWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfBeamStirrupsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteBeamStirrups(uidoc, config, hosts);
        }
    }
}
