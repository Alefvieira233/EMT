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

            // ADR-003: servico e "mudo" — retorna Result<T> com o estado do kickoff.
            // Comando decide a UX (warning, info, etc). O ShowInfo final de sucesso
            // fica dentro da sessao porque pertence ao lifecycle da janela persistente.
            NumeracaoItensService service = new NumeracaoItensService();
            FerramentaEMT.Core.Result<NumeracaoItensService.InicioResultado> outcome =
                service.IniciarSessao(uidoc.Application, uidoc, config);

            if (outcome.IsFailure)
            {
                AppDialogService.ShowError(CommandName, outcome.Error, "Não foi possível iniciar");
                return Result.Failed;
            }

            NumeracaoItensService.InicioResultado info = outcome.Value;

            if (info.JaHaviaSessaoAtiva)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Já existe uma sessão de numeração em andamento. A janela foi trazida para frente.",
                    "Sessão ativa");
                return Result.Cancelled;
            }

            if (!info.SessaoIniciada && info.TotalElegiveis == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    $"Nenhum elemento elegível foi encontrado com os filtros escolhidos.\n\n" +
                    $"Candidatos examinados: {info.TotalCandidatos}",
                    "Nenhum item encontrado");
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}
