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
    public class CmdGerarTravamentos : FerramentaCommandBase
    {
        protected override string CommandName => "Travamentos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<FamilySymbol> listaPerfis = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .ToList();

            if (listaPerfis.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma família estrutural foi encontrada.", "Perfis não encontrados");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();

            TravamentoConfig config = null;

            var wnd = new TravamentoWindow(listaPerfis, settings);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;
            config = wnd.BuildConfig();

            if (config == null || (!config.LancarTirante && !config.LancarFrechal))
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            TravamentoService service = new TravamentoService();
            service.Executar(uidoc, doc, config);

            return Result.Succeeded;
        }
    }
}
