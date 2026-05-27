#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SteelBIM.Services;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// Comando "Inserir Conexao de Terca" (v2.8.1, Victor).
    ///
    /// <para>Fluxo:</para>
    /// <list type="number">
    ///   <item>PickObjects multiseleciona as tercas que receberao a conexao.</item>
    ///   <item>Busca familias carregadas cuja categoria contém "onex" (cobre
    ///         pt-BR "Conexoes Estruturais" e en-US "Connections").</item>
    ///   <item>Abre janela com combos familia/tipo, checkboxes extremidades/meio,
    ///         expander de parametros da familia, ajuste fino.</item>
    ///   <item>Delega ao ConexaoTercasService que insere as instancias.</item>
    /// </list>
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdInserirConexaoTercas : FerramentaCommandBase
    {
        protected override string CommandName => "Inserir Conexão de Terça";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // 1. Seleciona as tercas no modelo antes de abrir a janela de configuracao.
            IList<Reference> refs;
            try
            {
                refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StructuralFramingSelectionFilter(),
                    "Selecione as TERÇAS que receberão a conexão (Enter para confirmar)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (refs == null || refs.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma terça foi selecionada.", "Seleção vazia");
                return Result.Cancelled;
            }

            // 2. Carrega familias de conexao estrutural.
            // Filtra por nome de categoria pois OST_StructuralConnections pode nao existir
            // na versao do SDK em uso — busca por substring "onex" para cobrir pt-BR e en-US.
            List<FamilySymbol> simbolos = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => (s.Category?.Name ?? string.Empty).Contains("onex"))
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();

            if (simbolos.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Nenhuma família de Conexão Estrutural foi encontrada no projeto.\n\n" +
                    "Carregue a família 'Conexão estrutural - terça.rfa' antes de executar este comando.",
                    "Família não encontrada");
                return Result.Cancelled;
            }

            // 3. Abre a janela com o contexto das tercas ja selecionadas.
            ConexaoTercasWindow wnd = new ConexaoTercasWindow(simbolos, refs.Count);
            if (wnd.ShowDialog() != true)
                return Result.Cancelled;

            var config = wnd.BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            return new ConexaoTercasService().Executar(uidoc, doc, config, refs);
        }
    }
}
