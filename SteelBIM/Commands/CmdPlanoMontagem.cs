using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// Comando para abrir a janela de Sequenciamento BIM (Erection Plan).
    /// Permite atribuir etapas de montagem a elementos, visualizar plano,
    /// aplicar destaque visual, e exportar relatório Excel.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdPlanoMontagem : FerramentaCommandBase
    {
        protected override string CommandName => "Sequenciamento BIM";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            try
            {
                // Pre-selecao obrigatoria. Alinha com padrao dos outros comandos
                // do plugin (PickObjects dentro de ShowDialog modal causa deadlock
                // do thread principal do Revit — bug fatal corrigido em v2.1.2).
                ICollection<ElementId> selecionados = uidoc.Selection.GetElementIds();
                if (selecionados == null || selecionados.Count == 0)
                {
                    AppDialogService.ShowWarning(
                        CommandName,
                        "Selecione os elementos no Revit ANTES de abrir este comando.\n\n" +
                        "Fluxo correto:\n" +
                        "1. Selecione os elementos estruturais que receberao a etapa\n" +
                        "2. Execute o comando Sequenciamento BIM\n" +
                        "3. Informe o numero da etapa na janela\n" +
                        "4. Clique em Atribuir",
                        "Selecao obrigatoria");
                    return Result.Cancelled;
                }

                // Janela WPF coleta APENAS configuracao (etapa + descricao).
                // Service usa os IDs ja selecionados — nao picka mais.
                var window = new PlanoMontagemWindow(uidoc, selecionados.ToList());
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
