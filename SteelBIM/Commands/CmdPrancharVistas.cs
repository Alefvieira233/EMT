#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models;
using SteelBIM.Services.Layout;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// v2.8.13 (Onda 3): "Pranchar Vistas" — selecione as vistas no Navegador de Projeto,
    /// clique, escolha o carimbo/formato e a função cria a folha e distribui as vistas em grade.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdPrancharVistas : FerramentaCommandBase
    {
        protected override string CommandName => "Pranchar Vistas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<View> vistas = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id) as View)
                .Where(v => v != null && v is not ViewSheet && v is not ViewSchedule && !v.IsTemplate)
                .Cast<View>()
                .ToList();

            if (vistas.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Selecione as VISTAS no Navegador de Projeto (plantas/cortes/elevações/3D) e rode de novo.",
                    "Nenhuma vista selecionada");
                return Result.Cancelled;
            }

            List<(string Familia, string Tipo)> titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Select(s => (s.Family?.Name ?? string.Empty, s.Name))
                .Distinct()
                .OrderBy(t => t.Item1)
                .ThenBy(t => t.Item2)
                .ToList();

            if (titleBlocks.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Nenhuma família de carimbo (title block) carregada no projeto. Carregue um formato (A1, etc.) e tente de novo.",
                    "Carimbo ausente");
                return Result.Cancelled;
            }

            PrancharVistasWindow window = new PrancharVistasWindow(titleBlocks, vistas.Count);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PrancharVistasConfig? config = window.BuildConfig();
            if (config == null)
                return Result.Cancelled;

            new PrancharVistasService().Executar(uidoc, vistas, config);
            return Result.Succeeded;
        }
    }
}
