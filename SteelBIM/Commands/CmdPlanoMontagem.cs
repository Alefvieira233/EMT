using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando para abrir a janela de Plano de Montagem (Erection Plan).
    /// Permite atribuir etapas de montagem a elementos, visualizar plano,
    /// aplicar destaque visual, e exportar relatório Excel.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdPlanoMontagem : FerramentaCommandBase
    {
        protected override string CommandName => "Plano de Montagem";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            try
            {
                // Abre a janela de configuração do plano de montagem
                var window = new PlanoMontagemWindow(uidoc);
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
