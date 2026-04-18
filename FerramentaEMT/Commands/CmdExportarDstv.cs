using System.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Core;
using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Services.CncExport;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdExportarDstv : FerramentaCommandBase
    {
        protected override string CommandName => "Exportar DSTV/NC1";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            ExportarDstvWindow janela = new ExportarDstvWindow(uidoc);
            bool? ok = janela.ShowDialog();
            if (ok != true)
                return Result.Cancelled;

            ExportarDstvConfig config = janela.BuildConfig();

            DstvExportService service = new DstvExportService();

            // TODO(ADR-003): quando tivermos um status bar widget no Revit host,
            // passar IProgress<ProgressReport> aqui para feedback em tempo real.
            // Por enquanto a exportacao corre sincrona e o summary e exibido
            // via AppDialogService.ShowInfo no final.
            FerramentaEMT.Core.Result<DstvExportService.ResultadoExport> outcome =
                service.Executar(uidoc, config, progress: null, ct: CancellationToken.None);

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel exportar");
                return Result.Failed;
            }

            DstvExportService.ResultadoExport resultado = outcome.Value;

            // Cancelamento explicito via PickObjects e modelado como "Ok com Cancelado=true"
            // para distinguir de "selecao vazia" (que e Fail). Nao reportamos erro ao usuario.
            if (resultado.Cancelado)
                return Result.Cancelled;

            return resultado.ArquivosGerados > 0
                ? Result.Succeeded
                : Result.Failed;
        }
    }
}
