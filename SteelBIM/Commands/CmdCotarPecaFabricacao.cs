using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdCotarPecaFabricacao : FerramentaCommandBase
    {
        protected override string CommandName => "Cotagem de Fabricação";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            CotarPecaFabricacaoWindow janela = new CotarPecaFabricacaoWindow();
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            CotarPecaFabricacaoConfig config = janela.BuildConfig();
            if (config == null || !config.TemCotaSelecionada())
            {
                AppDialogService.ShowWarning(CommandName,
                    "Selecione ao menos um tipo de cota para gerar.",
                    "Configuração incompleta");
                return Result.Failed;
            }

            CotarPecaFabricacaoService service = new CotarPecaFabricacaoService();
            service.Executar(uidoc, config);

            return Result.Succeeded;
        }
    }
}
