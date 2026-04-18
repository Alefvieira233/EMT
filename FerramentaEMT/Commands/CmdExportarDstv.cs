using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

            if (string.IsNullOrWhiteSpace(config.PastaDestino))
            {
                AppDialogService.ShowWarning(CommandName,
                    "Selecione uma pasta de destino para os arquivos .nc1.",
                    "Pasta nao informada");
                return Result.Failed;
            }

            DstvExportService service = new DstvExportService();
            DstvExportService.ResultadoExport resultado = service.Executar(uidoc, config);

            return resultado.ArquivosGerados > 0
                ? Result.Succeeded
                : Result.Failed;
        }
    }
}
