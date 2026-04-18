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
    public class CmdMarcarPecas : FerramentaCommandBase
    {
        protected override string CommandName => "Marcação de Peças";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Confirmar antes de executar (operacao que altera parametros)
            if (!AppDialogService.ShowConfirmation(
                CommandName,
                "Este comando irá analisar os elementos estruturais, agrupar peças idênticas " +
                "e atribuir marcas de fabricação automaticamente.\n\n" +
                "Deseja configurar e executar a marcação?",
                "Marcação Inteligente de Peças",
                "Configurar",
                "Cancelar"))
            {
                return Result.Cancelled;
            }

            MarcarPecasWindow janela = new MarcarPecasWindow(uidoc);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            MarcarPecasConfig config = janela.BuildConfig();
            if (config == null || !config.TemCategoriaSelecionada())
            {
                AppDialogService.ShowWarning(CommandName,
                    "Selecione ao menos uma categoria para marcar.",
                    "Configuração incompleta");
                return Result.Failed;
            }

            MarcarPecasService service = new MarcarPecasService();
            var resultadoMarcacao = service.Executar(uidoc, config);

            return resultadoMarcacao.MarcasAtribuidas > 0
                ? Result.Succeeded
                : Result.Failed;
        }
    }
}
