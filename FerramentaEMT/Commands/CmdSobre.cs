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
    /// Comando "Licença → Sobre". Mostra versao, dados da licenca, fingerprint da maquina.
    /// IExternalCommand direto — nao requer licenca nem documento.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSobre : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Logger.Info("[CmdSobre] abrindo janela Sobre");
                var win = new AboutWindow();
                win.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[CmdSobre] falhou");
                AppDialogService.ShowError("Sobre",
                    "Não foi possível abrir a tela Sobre:\n\n" + ex.Message,
                    "Erro");
                return Result.Failed;
            }
        }
    }
}
