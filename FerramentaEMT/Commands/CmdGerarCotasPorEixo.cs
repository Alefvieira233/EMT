using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarCotasPorEixo : FerramentaCommandBase
    {
        protected override string CommandName => "Cotas por Eixo";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            var service = new CotasService();
            service.ExecutarAutomatico(uidoc);

            return Result.Succeeded;
        }
    }
}
