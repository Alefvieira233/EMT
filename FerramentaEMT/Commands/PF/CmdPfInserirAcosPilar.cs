using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosPilar : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Pilar";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            PfColumnBarsWindow window = new PfColumnBarsWindow(doc);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfColumnBarsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteColumnBars(uidoc, config);
        }
    }
}
