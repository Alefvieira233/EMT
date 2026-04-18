using System;
using System.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Core;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Services.ModelCheck;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdVerificarModelo : FerramentaCommandBase
    {
        protected override string CommandName => "Verificacao de Modelo";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            VerificarModeloWindow janela = new VerificarModeloWindow(uidoc);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            ModelCheckConfig config = janela.BuildConfig();

            // TODO(ADR-003): quando tivermos progress bar na UI de verificacao,
            // passar IProgress<ProgressReport> aqui para feedback em tempo real.
            // CancellationToken ligaria num botao Cancelar — infraestrutura pronta.
            ModelCheckService service = new ModelCheckService();

            FerramentaEMT.Core.Result<ModelCheckReport> outcome;
            try
            {
                outcome = service.Executar(uidoc, config, progress: null, ct: CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Futuro: quando CT estiver amarrado a um botao Cancelar,
                // este bloco trata a interrupcao como cancelamento limpo.
                return Result.Cancelled;
            }

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel verificar");
                return Result.Failed;
            }

            ModelCheckReport report = outcome.Value;

            // Feedback separado da exportacao Excel: analise sempre e apresentada
            // mesmo quando o export falha (ADR-003 — falha parcial nao invalida resultado).
            if (!string.IsNullOrEmpty(report.ExportError))
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "A analise foi concluida, mas a exportacao do Excel falhou:\n\n" + report.ExportError,
                    "Exportacao falhou");
            }
            else if (!string.IsNullOrEmpty(report.ExportedToPath))
            {
                AppDialogService.ShowInfo(
                    CommandName,
                    "Relatorio exportado com sucesso:\n" + report.ExportedToPath,
                    "Exportacao Concluida");
            }

            // Apresentacao do relatorio
            if (report.TotalIssues > 0 || report.Results.Count > 0)
            {
                VerificarModeloReportWindow reportWindow = new VerificarModeloReportWindow(uidoc, report);
                reportWindow.ShowDialog();
            }
            else
            {
                AppDialogService.ShowInfo(
                    CommandName,
                    "Nenhum problema encontrado no modelo.",
                    "Verificacao Concluida");
            }

            return Result.Succeeded;
        }
    }
}
