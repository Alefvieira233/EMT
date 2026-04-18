using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdLimparAgrupamentosVisuais : FerramentaCommandBase
    {
        protected override string CommandName => "Limpar Cor + Grupos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            string message = null;
            return AgrupamentoVisualService.LimparAgrupamentos(uidoc, ref message);
        }
    }
}
