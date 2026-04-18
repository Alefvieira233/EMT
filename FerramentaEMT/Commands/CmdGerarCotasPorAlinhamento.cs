using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarCotasPorAlinhamento : FerramentaCommandBase
    {
        protected override string CommandName => "Cotas por Alinhamento";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            var service = new CotasService();
            service.Executar(uidoc);

            return Result.Succeeded;
        }
    }
}
