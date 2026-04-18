using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAgruparVigasPorTipo : FerramentaCommandBase
    {
        protected override string CommandName => "Agrupar Vigas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // ADR-003 + ADR-004: vigas podem ser muito mais numerosas que pilares
            // (milhares em galpoes grandes), entao vale envolver em RevitProgressHost
            // quando migrarmos para o host completo. Por enquanto, apenas Result<T>
            // com progress=null — o cancelamento ainda funciona via ct caso um caller
            // externo decida passar um token (testes, por exemplo).
            FerramentaEMT.Core.Result<AgrupamentoVisualService.ResultadoAgrupamento> outcome =
                AgrupamentoVisualService.AgruparVigas(uidoc);

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
