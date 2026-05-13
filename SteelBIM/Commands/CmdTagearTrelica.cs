#nullable enable
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Services.Trelica;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando para identificacao leve de treliça (tagging sem cotas).
    /// Seleciona barras em uma elevacao, coloca tags de perfil em cada barra.
    /// Util para projetistas que ja tem cotas mas falta identificar.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdTagearTrelica : FerramentaCommandBase
    {
        protected override string CommandName => "Tagear Treliça";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Validar pre-selecao (>= 1 elemento)
            var selecaoIds = uidoc.Selection.GetElementIds();
            if (selecaoIds.Count == 0)
            {
                return NothingToDo(
                    "Selecione ao menos uma barra de treliça " +
                    "antes de executar este comando.");
            }

            // Abrir janela de configuracao
            TagearTrelicaWindow janela = new TagearTrelicaWindow();
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            TagearTrelicaConfig config = janela.BuildConfig();
            if (config == null || !config.TemTipoSelecionado())
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Selecione ao menos um tipo de membro para tagear " +
                    "(banzo superior, banzo inferior, montantes ou diagonais).",
                    "Configuração incompleta");
                return Result.Failed;
            }

            // Executar servico
            TagearTrelicaService service = new TagearTrelicaService();
            var relatorio = service.Executar(uidoc, config);

            // Feedback ao usuario
            if (relatorio.TotalTagsCriadas > 0)
            {
                Logger.Info(
                    "[{Cmd}] sucesso: {Tags} tags criadas",
                    CommandName, relatorio.TotalTagsCriadas);

                ShowSuccess(
                    relatorio.ObterResumo(),
                    "Tagging concluído");

                return Result.Succeeded;
            }
            else
            {
                string msgErro = relatorio.Erros.Count > 0
                    ? string.Join("\n", relatorio.Erros)
                    : "Nenhuma tag foi criada.";

                ShowWarning(msgErro, "Falha no tagging");
                return Result.Failed;
            }
        }
    }
}
