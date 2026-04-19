using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Services.CncExport;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdExportarDstv : FerramentaCommandBase
    {
        protected override string CommandName => "Exportar DSTV/NC1";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            ExportarDstvWindow janela = new ExportarDstvWindow(uidoc);
            bool? ok = janela.ShowDialog();
            if (ok != true)
                return Result.Cancelled;

            ExportarDstvConfig config = janela.BuildConfig();

            DstvExportService service = new DstvExportService();

            // ===== FASE 1: coleta =====
            // Pode abrir PickObjects (modal Revit). Nao usa RevitProgressHost aqui
            // porque deixariamos a janela de progresso vazia por tras da selecao nativa.
            FerramentaEMT.Core.Result<DstvExportService.ColetaResult> coleta =
                service.ColetarElementos(uidoc, config);

            if (coleta.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, coleta.Error, "Nao foi possivel iniciar exportacao");
                return Result.Failed;
            }

            if (coleta.Value.Cancelado)
                return Result.Cancelled;

            System.Collections.Generic.IReadOnlyList<Autodesk.Revit.DB.FamilyInstance> elementos =
                coleta.Value.Elementos;

            // ===== FASE 2: processamento com progress + cancel =====
            // ADR-004: RevitProgressHost abre janela com barra + botao Cancelar,
            // corre o servico no mesmo thread (Revit API single-threaded) e bombeia
            // o dispatcher entre eventos de IProgress para a UI atualizar.
            FerramentaEMT.Core.Result<DstvExportService.ResultadoExport> outcome;
            try
            {
                outcome = RevitProgressHost.Run(
                    title: CommandName,
                    headline: "Exportando DSTV/NC1...",
                    work: (progress, ct) => service.Executar(uidoc, elementos, config, progress, ct));
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (outcome.IsFailure)
            {
                AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel exportar");
                return Result.Failed;
            }

            DstvExportService.ResultadoExport resultado = outcome.Value;

            // ===== POS-PROCESSAMENTO =====
            // Feedback e abertura de pasta sao decisao do comando (service e "mudo" por ADR-003).
            // Sobrescreve o sucesso com warning quando ha NC1s com dimensao zerada — usuario precisa
            // saber antes de enviar pra maquina CNC.
            string resumo = DstvExportService.BuildResumoText(resultado);

            if (resultado.ArquivosComDimensaoZerada > 0)
            {
                AppDialogService.ShowWarning(CommandName, resumo, "Exportacao concluida com avisos");
            }
            else if (resultado.ArquivosGerados > 0)
            {
                AppDialogService.ShowInfo(CommandName, resumo, "Exportacao concluida");
            }

            if (config.AbrirPastaAposExportar && resultado.ArquivosGerados > 0)
                DstvExportService.AbrirPastaNoExplorer(config.PastaDestino);

            return resultado.ArquivosGerados > 0
                ? Result.Succeeded
                : Result.Failed;
        }
    }
}
