using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
                ExportarListaMateriaisWindow janela = new ExportarListaMateriaisWindow(uidoc);
                bool? resultado = janela.ShowDialog();
                if (resultado != true)
                    return Result.Cancelled;

                ExportarListaMateriaisConfig config = janela.BuildConfig();
                if (config == null)
                {
                    AppDialogService.ShowWarning(CommandName, "Configuração inválida.", "Dados incompletos");
                    return Result.Failed;
                }

                ListaMateriaisExportService service = new ListaMateriaisExportService();
                var message = string.Empty;
                return service.Exportar(uidoc, config, ref message);
            }
            catch (FileNotFoundException ex) when (EhDependenciaExcelAusente(ex))
            {
                AppDialogService.ShowError(CommandName, MontarMensagemDependenciaAusente(), "Dependência ausente");
                return Result.Failed;
            }
            catch (FileLoadException ex) when (EhDependenciaExcelAusente(ex))
            {
                AppDialogService.ShowError(CommandName, MontarMensagemDependenciaAusente(), "Dependência ausente");
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
