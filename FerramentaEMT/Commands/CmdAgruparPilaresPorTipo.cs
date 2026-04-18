using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAgruparPilaresPorTipo : FerramentaCommandBase
    {
        protected override string CommandName => "Agrupar Pilares";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            string message = null;
            return AgrupamentoVisualService.AgruparPilares(uidoc, ref message);
        }
    }
}
