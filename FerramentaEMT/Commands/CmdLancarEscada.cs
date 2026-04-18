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
    public class CmdLancarEscada : FerramentaCommandBase
    {
        protected override string CommandName => "Escada";

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

            List<Level> niveis = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .ToList();

            if (niveis.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhum nível foi encontrado no projeto.", "Níveis não encontrados");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();

            EscadaWindow wnd = new EscadaWindow(listaPerfis, niveis, settings);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            EscadaConfig config = wnd.BuildConfig();
            if (config == null || config.SymbolLongarina == null || config.NivelReferencia == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            if (config.CriarDegraus && config.TipoDegrau == EscadaTipoDegrau.PerfilLinear && config.SymbolDegrau == null)
            {
                AppDialogService.ShowWarning(CommandName, "Selecione o perfil do degrau ou desmarque a opção de criar degraus.", "Perfil do degrau ausente");
                return Result.Failed;
            }

            EscadaService service = new EscadaService();
            service.Executar(uidoc, doc, config);

            return Result.Succeeded;
        }
    }
}
