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
    public class CmdPfInserirEstribosPilar : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Estribos Pilar";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsStructuralColumn,
                "Selecione os pilares estruturais para configurar e lancar os estribos.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfColumnStirrupsWindow window = new PfColumnStirrupsWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfColumnStirrupsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteColumnStirrups(uidoc, config);
        }
    }
}
