#nullable enable
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Services.PF;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfLancarFundacoes : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Lançar Fundação";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            bool hasSelection = uidoc.Selection.GetElementIds()
                .Select(doc.GetElement)
                .Any(PfElementService.IsStructuralColumn);

            PfFoundationPlacementWindow window = new PfFoundationPlacementWindow(doc, hasSelection);
            if (!window.HasSymbols)
                return NothingToDo("Nenhum tipo de fundação estrutural compatível foi encontrado no modelo.");

            if (window.ShowDialog() != true)
                return Result.Cancelled;

            var config = window.BuildConfig();
            if (config == null)
                return Result.Cancelled;

            var service = new PfFoundationPlacementService();
            var summary = service.Execute(uidoc, config);

            if (summary.FundacoesCriadas == 0)
            {
                ShowWarning(summary.BuildMessage(), "Nenhuma fundação lançada");
                return Result.Cancelled;
            }

            if (summary.Falhas > 0 || summary.PilaresSemNivel > 0)
            {
                ShowWarning(summary.BuildMessage(), "Lançamento concluído com avisos");
            }
            else
            {
                ShowSuccess(summary.BuildMessage(), "Lançamento concluído");
            }

            return Result.Succeeded;
        }
    }
}
