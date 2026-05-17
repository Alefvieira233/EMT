using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.DiagramaMontagem;
using SteelBIM.Services.DiagramaMontagem;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarDiagramaMontagem : FerramentaCommandBase
    {
        protected override string CommandName => "Diagrama de Montagem";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Pre-selecao obrigatoria (mesmo padrao do v2.1.2)
            ICollection<ElementId> selecionados = uidoc.Selection.GetElementIds();
            if (selecionados == null || selecionados.Count == 0)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Selecione os elementos no Revit ANTES de abrir este comando.\n\n" +
                    "Fluxo correto:\n" +
                    "1. Selecione os elementos estruturais (vigas, pilares, treliças)\n" +
                    "2. Execute o comando Diagrama de Montagem\n" +
                    "3. Confirme a configuracao e clique em Gerar",
                    "Selecao obrigatoria");
                return Result.Cancelled;
            }

            // Janela coleta config
            var window = new DiagramaMontagemWindow(uidoc, selecionados.ToList());
            bool? ok = window.ShowDialog();
            if (ok != true)
                return Result.Cancelled;

            DiagramaMontagemConfig config = window.BuildConfig();
            if (config == null)
                return Result.Cancelled;

            // Executar service
            var service = new DiagramaMontagemService();
            var resultado = service.Executar(uidoc, selecionados.ToList(), config);

            if (resultado.Sucesso)
            {
                string avisos = resultado.Avisos.Count > 0
                    ? "\n\nAvisos:\n- " + string.Join("\n- ", resultado.Avisos)
                    : "";
                AppDialogService.ShowInfo(
                    CommandName,
                    resultado.Mensagem + avisos,
                    "Diagrama gerado");
                return Result.Succeeded;
            }
            else
            {
                AppDialogService.ShowError(
                    CommandName,
                    resultado.Mensagem,
                    "Falha");
                return Result.Failed;
            }
        }
    }
}
