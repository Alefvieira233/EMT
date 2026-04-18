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
    public class CmdNumerarItens : FerramentaCommandBase
    {
        protected override string CommandName => "Numerar Itens";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            AppSettings settings = AppSettings.Load();
            NumeracaoItensWindow janela = new NumeracaoItensWindow(uidoc, settings);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            NumeracaoItensConfig config = janela.BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            NumeracaoItensService service = new NumeracaoItensService();
            return service.IniciarSessao(uidoc.Application, uidoc, config);
        }
    }
}
