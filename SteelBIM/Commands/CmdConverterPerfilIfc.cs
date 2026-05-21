using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Models.Ifc;
using SteelBIM.Services.Ifc;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdConverterPerfilIfc : FerramentaCommandBase
    {
        protected override string CommandName => "Converter Perfis IFC";

        // v2.7.0 BUG 3: 50 -> 60 (mais conservador, evita match dimensional fraco
        // que confundia perfis similares de bibliotecas diferentes do projeto).
        private const int ScoreMinimo = 60;
        private const string ParamPadrao = "IfcMaterial";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            var service = new ConverterPerfilIfcService();
            List<Element> elementosIfc = service.ColetarElementosIfc(doc);

            if (elementosIfc.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Nenhum elemento IFC (com parametro IfcGUID) foi encontrado no modelo.\n\n" +
                    "Importe um arquivo IFC antes de executar este comando.",
                    "Elementos IFC nao encontrados");
                return Result.Cancelled;
            }

            List<SymbolItem> perfisDisponiveis = ColetarPerfisDisponiveis(doc);

            if (perfisDisponiveis.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Nenhuma familia de perfil estrutural foi encontrada no projeto.\n\n" +
                    "Carregue as familias de Structural Framing ou Structural Columns antes de executar este comando.",
                    "Perfis nao encontrados");
                return Result.Cancelled;
            }

            List<Level> niveis = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            AppSettings settings = AppSettings.Load();
            string paramInicial = string.IsNullOrWhiteSpace(settings.LastConverterIfcParamIfc)
                ? ParamPadrao
                : settings.LastConverterIfcParamIfc;

            List<ElementoIfcViewModel> viewModels =
                ConstruirViewModels(elementosIfc, perfisDisponiveis, paramInicial);

            List<string> paramsDisponiveis = viewModels
                .SelectMany(vm => vm.ParametrosIfc.Keys)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var wnd = new ConverterPerfilIfcWindow(viewModels, paramsDisponiveis, paramInicial, niveis, settings);
            bool? resultado = wnd.ShowDialog();
            if (resultado != true)
                return Result.Cancelled;

            ConverterPerfilIfcConfig config = wnd.BuildConfig();
            if (config == null || config.Conversoes.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Nenhum elemento selecionado para converter ou nenhum perfil de destino atribuido.",
                    "Nada a converter");
                return Result.Cancelled;
            }

            (int convertidos, int ignorados) = service.Executar(doc, config);

            AppDialogService.ShowInfo(
                CommandName,
                $"Conversao concluida.\n\n" +
                $"Convertidos: {convertidos}\n" +
                $"Ignorados (sem eixo ou nivel detectavel): {ignorados}",
                "Conversao concluida");

            return Result.Succeeded;
        }

        private List<SymbolItem> ColetarPerfisDisponiveis(Document doc)
        {
            var perfis = new List<FamilySymbol>();

            perfis.AddRange(new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>());

            perfis.AddRange(new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>());

            return perfis
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .Select(s => new SymbolItem(s))
                .ToList();
        }

        private List<ElementoIfcViewModel> ConstruirViewModels(
            List<Element> elementos,
            List<SymbolItem> perfisDisponiveis,
            string nomeParam)
        {
            var lista = new List<ElementoIfcViewModel>();

            foreach (Element e in elementos)
            {
                Dictionary<string, string> paramsIfc =
                    ConverterPerfilIfcService.ColetarParametrosIfc(e);

                string ifcName = paramsIfc.TryGetValue("IfcName", out string n) ? n : $"#{e.Id}";
                string valorParam = paramsIfc.TryGetValue(nomeParam, out string v) ? v : string.Empty;
                string secao = IfcMaterialParser.ExtrairNomeSecao(valorParam);

                var vm = new ElementoIfcViewModel(perfisDisponiveis)
                {
                    ElementId = e.Id,
                    IfcName = ifcName,
                    IfcMaterial = valorParam,
                    Categoria = e.Category?.Name ?? string.Empty,
                    SecaoSugerida = secao
                };

                foreach (var kv in paramsIfc)
                    vm.ParametrosIfc[kv.Key] = kv.Value;

                vm.PerfilSelecionado = EncontrarMelhorCandidato(secao, perfisDisponiveis);

                lista.Add(vm);
            }

            return lista;
        }

        private SymbolItem EncontrarMelhorCandidato(
            string secao,
            List<SymbolItem> perfisDisponiveis)
        {
            if (string.IsNullOrWhiteSpace(secao))
                return null;

            SymbolItem melhor = null;
            int melhorScore = ScoreMinimo - 1;

            foreach (SymbolItem si in perfisDisponiveis)
            {
                int score = IfcMaterialParser.CalcularScore(secao, si.Symbol.Name);
                if (score > melhorScore)
                {
                    melhorScore = score;
                    melhor = si;
                }
            }

            return melhor;
        }
    }
}
