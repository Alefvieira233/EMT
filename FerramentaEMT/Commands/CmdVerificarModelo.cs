using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            // Abrir janela de configuracao
            VerificarModeloWindow janela = new VerificarModeloWindow(uidoc);
            bool? resultado = janela.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            ModelCheckConfig config = janela.BuildConfig();
            if (config == null || config.GetEnabledRulesCount() == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Selecione ao menos uma regra para executar.",
                    "Configuracao incompleta");
                return Result.Failed;
            }

            // Executar verificacao
            ModelCheckService service = new ModelCheckService();
            ModelCheckReport report = service.Executar(uidoc, config);

            // Mostrar relatorio
            if (report != null && (report.TotalIssues > 0 || report.Results.Count > 0))
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
