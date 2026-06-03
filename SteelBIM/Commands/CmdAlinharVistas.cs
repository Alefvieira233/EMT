#nullable enable
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Services.Layout;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// v2.8.13 (Onda 2): "Alinhar Vistas" — alinha/distribui (estilo PowerPoint) os viewports
    /// selecionados na prancha ativa. Selecione 2+ vistas, clique, escolha a ação.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAlinharVistas : FerramentaCommandBase
    {
        protected override string CommandName => "Alinhar Vistas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            if (doc.ActiveView is not ViewSheet)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Abra uma PRANCHA (folha) e selecione as vistas que deseja alinhar.",
                    "Abra uma prancha");
                return Result.Cancelled;
            }

            AlinharVistasWindow window = new AlinharVistasWindow();
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            new AlinharVistasService().Executar(uidoc, window.Acao);
            return Result.Succeeded;
        }
    }
}
