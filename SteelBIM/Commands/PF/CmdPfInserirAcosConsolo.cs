using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosConsolo : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Consolo";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            PfConsoloRebarWindow window = new PfConsoloRebarWindow(doc);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfConsoloRebarConfig config = window.BuildConfig();
            return new PfRebarService().ExecuteConsoloBars(uidoc, config);
        }
    }
}
