using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdLimparAgrupamentosVisuais : FerramentaCommandBase
    {
        protected override string CommandName => "Limpar Cor + Grupos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Limpeza e rapida (so overrides + UngroupMembers). Sem progress bar.
            FerramentaEMT.Core.Result<AgrupamentoVisualService.ResultadoLimpeza> outcome =
                AgrupamentoVisualService.LimparAgrupamentos(uidoc);

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel limpar");
                return Result.Cancelled;
            }

            string resumo = AgrupamentoVisualService.BuildResumoText(outcome.Value);
            AppDialogService.ShowInfo(CommandName, resumo, "Limpeza concluida");
            return Result.Succeeded;
        }
    }
}
