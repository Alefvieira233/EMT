#nullable enable
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Services.IdentificacaoPerfil;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando para identificacao em massa de perfis metalicos.
    /// Usuario seleciona elementos em qualquer vista (planta, elevacao, 3D),
    /// clica botao, plugin coloca tag de perfil em todos automaticamente.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdIdentificarPerfil : FerramentaCommandBase
    {
        protected override string CommandName => "Identificar Perfil";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Validar pre-selecao (>= 1 elemento)
            var selecaoIds = uidoc.Selection.GetElementIds();
            if (selecaoIds.Count == 0)
            {
                return NothingToDo(
                    "Selecione ao menos um elemento estrutural " +
                    "(viga, pilar ou contraventamento) antes de executar este comando.");
            }

            // Abrir janela de configuracao
            IdentificarPerfilWindow janela = new IdentificarPerfilWindow();
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            IdentificarPerfilConfig config = janela.BuildConfig();
            if (config == null || !config.TemCategoriaSelecionada())
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Selecione ao menos uma categoria de elementos para identificar " +
                    "(vigas, pilares ou contraventos).",
                    "Configuração incompleta");
                return Result.Failed;
            }

            // Executar servico
            IdentificarPerfilService service = new IdentificarPerfilService();
            var relatorio = service.Executar(uidoc, config);

            // Feedback ao usuario
            if (relatorio.TotalTagsCriadas > 0)
            {
                Logger.Info(
                    "[{Cmd}] sucesso: {Tags} tags criadas, {Pulados} elementos ja com tag",
                    CommandName, relatorio.TotalTagsCriadas, relatorio.ElementosPuladosTagExistente);

                ShowSuccess(
                    relatorio.ObterResumo(),
                    "Identificação concluída");

                return Result.Succeeded;
            }
            else if (relatorio.ElementosPuladosTagExistente > 0)
            {
                ShowWarning(
                    $"Todos os {relatorio.ElementosPuladosTagExistente} elementos " +
                    "já possuem tags. Ative a opção \"Substituir tags existentes\" " +
                    "para sobrescrever.",
                    "Nenhuma tag criada");

                return Result.Cancelled;
            }
            else
            {
                string msgErro = relatorio.Erros.Count > 0
                    ? string.Join("\n", relatorio.Erros)
                    : "Nenhuma tag foi criada.";

                ShowWarning(msgErro, "Falha na identificação");
                return Result.Failed;
            }
        }
    }
}
