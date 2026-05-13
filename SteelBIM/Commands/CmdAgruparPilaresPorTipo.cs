using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAgruparPilaresPorTipo : FerramentaCommandBase
    {
        protected override string CommandName => "Agrupar Pilares";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // ADR-003: servico e "mudo" e retorna Result<T>. O comando decide a UX
            // (progresso + dialog de sucesso/falha). Pilares sao rapidos: usamos
            // CancellationToken.None e sem progress — tipicamente <1s mesmo em modelos
            // grandes (sem NewGroup pra pilares, so overrides).
            FerramentaEMT.Core.Result<AgrupamentoVisualService.ResultadoAgrupamento> outcome =
                AgrupamentoVisualService.AgruparPilares(uidoc);

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel agrupar");
                return Result.Cancelled;
            }

            string resumo = AgrupamentoVisualService.BuildResumoText(outcome.Value);
            AppDialogService.ShowInfo(CommandName, resumo, "Processamento concluido");
            return Result.Succeeded;
        }
    }
}
