#nullable enable
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services.Conexoes;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdLancarPlacasBase : FerramentaCommandBase
    {
        protected override string CommandName => "Lançar Placas de Base";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            var window = new PlacaBaseConfigWindow(doc);
            if (!window.HasSymbols)
            {
                return NothingToDo("Nenhuma família face-based/work plane-based compatível foi encontrada no modelo.");
            }

            bool? accepted = window.ShowDialog();
            if (accepted != true)
                return Result.Cancelled;

            var config = window.BuildConfig();
            if (config == null)
            {
                ShowWarning("Configuração inválida.", "Dados incompletos");
                return Result.Cancelled;
            }

            var service = new PlacaBaseLancamentoService();
            var resultado = service.Lancar(doc, config);

            if (resultado.PlacasInseridas == 0)
            {
                ShowWarning(resultado.Resumo(), "Nenhuma placa inserida");
                return Result.Cancelled;
            }

            if (resultado.PilaresSemApoio > 0 || resultado.PilaresSemFaceSuperior > 0 || resultado.FalhasInsercao > 0)
            {
                ShowWarning(resultado.Resumo(), "Lançamento concluído com avisos");
            }
            else
            {
                ShowSuccess(resultado.Resumo(), "Lançamento concluído");
            }

            return Result.Succeeded;
        }
    }
}
