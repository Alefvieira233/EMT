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
            PfBeamBarsWindow window = new PfBeamBarsWindow(doc);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfBeamBarsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteBeamBars(uidoc, config);
        }
    }
}
