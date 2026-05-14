using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfNomearElementos : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Nomear Elementos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            AppSettings settings = AppSettings.Load();
            PfNamingWindow janela = new PfNamingWindow(uidoc, settings);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            PfNamingConfig config = janela.BuildConfig();
            if (config == null)
                return Result.Failed;

            return new PfNamingService().Execute(uidoc, CommandName, config);
        }
    }
}
