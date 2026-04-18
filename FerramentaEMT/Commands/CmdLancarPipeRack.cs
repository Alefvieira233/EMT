using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdLancarPipeRack : FerramentaCommandBase
    {
        protected override string CommandName => "Pipe Rack";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<FamilySymbol> listaPerfis = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.Category is not null &&
                            (x.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming ||
                             x.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns))
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .ToList();

            List<Level> niveis = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .ToList();

            if (listaPerfis.Count == 0 || niveis.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Perfis estruturais ou níveis não foram encontrados no projeto.", "Dados do projeto indisponíveis");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();
            PipeRackWindow wnd = new PipeRackWindow(listaPerfis, niveis, settings);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            PipeRackConfig config = wnd.BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            PipeRackService service = new PipeRackService();
            service.Executar(uidoc, doc, config);
            return Result.Succeeded;
        }
    }
}
