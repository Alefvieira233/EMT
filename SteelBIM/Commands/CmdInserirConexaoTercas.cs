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
    /// Comando "Inserir Conexao de Terca".
    ///
    /// <para>v2.8.1 inicial: 1 pick (terças) + dedup XY 50mm + rotação manual.</para>
    ///
    /// <para>v2.8.2 (algoritmo face-based validado): 2 picks separados —
    /// terças + vigas de apoio. Service projeta a extremidade da terça na
    /// curva da viga mais próxima, obtém a maior face planar do solid da
    /// terça (alma em U/C/I), e insere face-based sem rotações manuais.</para>
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdInserirConexaoTercas : FerramentaCommandBase
    {
        protected override string CommandName => "Inserir Conexão de Terça";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // 1. PICK #1 — Terças (filter framing).
            IList<Reference> tercasRefs;
            try
            {
                tercasRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StructuralFramingSelectionFilter(),
                    "Selecione as TERÇAS que receberão a conexão (Enter para confirmar)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (tercasRefs == null || tercasRefs.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma terça foi selecionada.", "Seleção vazia");
                return Result.Cancelled;
            }

            // 2. PICK #2 — Vigas de apoio (filter beam). NOVO em v2.8.2.
            // Necessário pra projetar o endpoint da terça na curva da viga.
            IList<Reference> vigasRefs;
            try
            {
                vigasRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StructuralBeamSelectionFilter(),
                    "Selecione as VIGAS DE APOIO que receberão as conexões (Enter para confirmar)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (vigasRefs == null || vigasRefs.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma viga de apoio foi selecionada.", "Seleção vazia");
                return Result.Cancelled;
            }

            // 3. Carrega famílias de conexão estrutural.
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

            // 4. Abre a janela com o contexto das tercas + vigas ja selecionadas.
            ConexaoTercasWindow wnd = new ConexaoTercasWindow(simbolos, tercasRefs.Count, vigasRefs.Count);
            if (wnd.ShowDialog() != true)
                return Result.Cancelled;

            var config = wnd.BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                return Result.Failed;
            }

            // Anexa as vigas pickadas na config antes de enviar pro service.
            config.VigasRefs = vigasRefs.ToList();

            return new ConexaoTercasService().Executar(uidoc, doc, config, tercasRefs);
        }
    }
}
