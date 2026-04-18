using System;
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
    public class CmdGerarTercasPlano : FerramentaCommandBase
    {
        protected override string CommandName => "Gerar Terças por Plano";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            Plane plane = ObterPlanoDoPlanoDeTrabalhoAtual(doc);
            if (plane == null)
                return Result.Cancelled;

            List<FamilySymbol> listaPerfis = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .ToList();

            if (listaPerfis.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma familia estrutural foi encontrada.", "Perfis nao encontrados");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();

            TercasConfig config = null;

            TercasWindow wnd = new TercasWindow(listaPerfis, settings);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            config = wnd.BuildConfig();
            if (config == null || config.SymbolSelecionado == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuracao invalida.", "Dados incompletos");
                return Result.Failed;
            }

            TercasService service = new TercasService();
            return service.Executar(uidoc, doc, config, plane);
        }

        private Plane ObterPlanoDoPlanoDeTrabalhoAtual(Document doc)
        {
            View vistaAtiva = doc?.ActiveView;
            SketchPlane sketchPlane = vistaAtiva?.SketchPlane;
            if (sketchPlane == null)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "A vista ativa nao possui um plano de trabalho definido.\n\nDefina o plano de trabalho no Revit e execute o comando novamente.",
                    "Plano de trabalho ausente");
                return null;
            }

            Plane plane = sketchPlane.GetPlane();
            if (plane == null)
            {
                AppDialogService.ShowError(CommandName, "Nao foi possivel obter a geometria do plano de trabalho atual.", "Falha ao ler plano");
                return null;
            }

            return plane;
        }
    }
}
