#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models;
using SteelBIM.Services.PF;
using SteelBIM.Services.Portico;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// v2.8.14: "Gerar Projeto Completo (Pórtico)" — abra numa planta/3D, clique, preencha a janela
    /// e a função modela o galpão inteiro (pilares + treliça/viga + terças + contraventamentos +
    /// linha de corrente) com 1 clique, sem nenhum pick interativo.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarPorticoCompleto : FerramentaCommandBase
    {
        protected override string CommandName => "Gerar Projeto Completo";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<FamilySymbol> colunas = Coletar(doc, BuiltInCategory.OST_StructuralColumns);
            List<FamilySymbol> perfis = Coletar(doc, BuiltInCategory.OST_StructuralFraming);
            List<FamilySymbol> ligacoes = ColetarConexoes(doc);
            List<FamilySymbol> fundacoes = PfFoundationPlacementService.CollectFoundationSymbols(doc).ToList();

            if (colunas.Count == 0 || perfis.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Carregue ao menos uma família de pilar (coluna estrutural) e uma de perfil (viga/treliça) no projeto e tente de novo.",
                    "Perfis ausentes");
                return Result.Cancelled;
            }

            GerarPorticoWindow window = new GerarPorticoWindow(colunas, perfis, ligacoes, fundacoes);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            GerarPorticoConfig? config = window.BuildConfig();
            if (config == null)
                return Result.Cancelled;

            new GerarPorticoService().Executar(uidoc, config);
            return Result.Succeeded;
        }

        private static List<FamilySymbol> Coletar(Document doc, BuiltInCategory categoria)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(categoria)
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();
        }

        // Familias de conexao de terça: categoria cujo nome contem "onex" (Conexão/Conexión),
        // mesmo criterio do CmdInserirConexaoTercas.
        private static List<FamilySymbol> ColetarConexoes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => (s.Category?.Name ?? string.Empty).Contains("onex"))
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();
        }
    }
}
