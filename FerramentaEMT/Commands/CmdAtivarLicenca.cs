using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando do ribbon "Licença → Ativar". Abre a janela de ativacao.
    /// Implementa IExternalCommand diretamente (sem FerramentaCommandBase) porque:
    ///   1) NAO requer licenca (senao o usuario ficaria preso)
    ///   2) NAO requer documento aberto (deve funcionar mesmo sem projeto)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAtivarLicenca : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Logger.Info("[CmdAtivarLicenca] abrindo janela de ativacao");
                var win = new LicenseActivationWindow();
                win.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[CmdAtivarLicenca] falhou");
                AppDialogService.ShowError("Ativar Licença",
                    "Não foi possível abrir a tela de ativação:\n\n" + ex.Message,
                    "Erro");
                return Result.Failed;
            }
        }
    }
}
