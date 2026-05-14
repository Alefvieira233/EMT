using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models;
using SteelBIM.Services;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarVistaPeca : FerramentaCommandBase
    {
        protected override string CommandName => "Auto-Vista de Peça";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            GerarVistaPecaWindow janela = new GerarVistaPecaWindow(uidoc);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            GerarVistaPecaConfig config = janela.BuildConfig();
            if (config == null || !config.TemVistasSelecionadas())
            {
                AppDialogService.ShowWarning(CommandName,
                    "Selecione ao menos um tipo de vista para gerar.",
                    "Configuração incompleta");
                return Result.Failed;
            }

            AutoVistaService service = new AutoVistaService();
            service.Executar(uidoc, config);

            return Result.Succeeded;
        }
    }
}
