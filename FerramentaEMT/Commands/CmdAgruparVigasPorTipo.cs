using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAgruparVigasPorTipo : FerramentaCommandBase
    {
        protected override string CommandName => "Agrupar Vigas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            string message = null;
            return AgrupamentoVisualService.AgruparVigas(uidoc, ref message);
        }
    }
}
