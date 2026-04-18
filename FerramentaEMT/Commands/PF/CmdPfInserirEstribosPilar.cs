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
            PfColumnStirrupsWindow window = new PfColumnStirrupsWindow(doc);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfColumnStirrupsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteColumnStirrups(uidoc, config);
        }
    }
}
