using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarCotasPorAlinhamento : FerramentaCommandBase
    {
        protected override string CommandName => "Cotas por Alinhamento";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // ADR-003 (P1.4 2026-04-29): CotasService devolve Result<CotagemResumo> e
            // nunca exibe dialog. Toda a UX (warning, info de sucesso, cancel silencioso)
            // mora aqui no command.
            var service = new CotasService();
            FerramentaEMT.Core.Result<CotagemResumo> outcome = service.Executar(uidoc);

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel criar cotas");
                return Result.Cancelled;
            }

            CotagemResumo resumo = outcome.Value;
            if (resumo.Cancelado)
                return Result.Cancelled;

            if (!string.IsNullOrEmpty(resumo.MensagemSucessoFormatada))
                AppDialogService.ShowInfo(CommandName, resumo.MensagemSucessoFormatada, "Cotas criadas com sucesso");

            return Result.Succeeded;
        }
    }
}
