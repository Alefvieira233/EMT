using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Core;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdExportarListaMateriais : FerramentaCommandBase
    {
        protected override string CommandName => "Exportar Lista de Materiais";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            try
            {
                // ===== FASE 1: configuracao =====
                // Janela modal do Revit (ShowDialog) ANTES da UI de progresso — se abrirmos
                // a barra agora, ficaria vazia atras do modal. Padrao consistente com DSTV.
                ExportarListaMateriaisWindow janela = new ExportarListaMateriaisWindow(uidoc);
                bool? resultado = janela.ShowDialog();
                if (resultado != true)
                    return Result.Cancelled;

                ExportarListaMateriaisConfig config = janela.BuildConfig();
                if (config == null)
                {
                    AppDialogService.ShowWarning(CommandName, "Configuracao invalida.", "Dados incompletos");
                    return Result.Failed;
                }

                ListaMateriaisExportService service = new ListaMateriaisExportService();

                // ===== FASE 2: processamento com progress + cancel =====
                // ADR-004: RevitProgressHost corre no mesmo thread (Revit API single-threaded)
                // e bombeia o dispatcher para a UI atualizar a barra de progresso e receber
                // clicks no botao Cancelar.
                FerramentaEMT.Core.Result<ListaMateriaisExportService.ResultadoExport> outcome;
                try
                {
                    outcome = RevitProgressHost.Run(
                        title: CommandName,
                        headline: "Exportando lista de materiais...",
                        work: (progress, ct) => service.Exportar(uidoc, config, progress, ct));
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

                // ===== POS-PROCESSAMENTO =====
                // Feedback e decisao do comando (service e "mudo" por ADR-003).
                ListaMateriaisExportService.ResultadoExport r = outcome.Value;
                string resumo = ListaMateriaisExportService.BuildResumoText(r);
                AppDialogService.ShowInfo(CommandName, resumo, "Arquivo gerado");

                return Result.Succeeded;
            }
            catch (FileNotFoundException ex) when (EhDependenciaExcelAusente(ex))
            {
                AppDialogService.ShowError(CommandName, MontarMensagemDependenciaAusente(), "Dependencia ausente");
                return Result.Failed;
            }
            catch (FileLoadException ex) when (EhDependenciaExcelAusente(ex))
            {
                AppDialogService.ShowError(CommandName, MontarMensagemDependenciaAusente(), "Dependencia ausente");
                return Result.Failed;
            }
        }

        private static bool EhDependenciaExcelAusente(Exception ex)
        {
            string nomeArquivo = ex switch
            {
                FileNotFoundException fileNotFound => fileNotFound.FileName,
                FileLoadException fileLoad => fileLoad.FileName,
                _ => string.Empty
            };

            string texto = $"{nomeArquivo} {ex.Message}";
            return texto.IndexOf("ClosedXML", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string MontarMensagemDependenciaAusente()
        {
            return
                "A exportacao para .xlsx nao depende do Excel instalado, mas a biblioteca ClosedXML nao foi encontrada.\n\n" +
                "Para usar este comando em outro computador, copie a pasta completa do add-in, e nao apenas o arquivo FerramentaEMT.dll.\n\n" +
                "Arquivos obrigatorios:\n" +
                "- FerramentaEMT.dll\n" +
                "- FerramentaEMT.deps.json\n" +
                "- ClosedXML.dll\n" +
                "- ClosedXML.Parser.dll\n" +
                "- DocumentFormat.OpenXml.dll\n" +
                "- DocumentFormat.OpenXml.Framework.dll\n" +
                "- demais DLLs geradas na mesma pasta\n\n" +
                "Use preferencialmente a pasta artifacts\\deploy\\<Configuracao>\\net8.0-windows gerada pelo build.";
        }
    }
}
