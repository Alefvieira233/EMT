using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirEstribosViga : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Estribos Viga";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            PfBeamStirrupsWindow window = new PfBeamStirrupsWindow(doc);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfBeamStirrupsConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteBeamStirrups(uidoc, config);
        }
    }
}
