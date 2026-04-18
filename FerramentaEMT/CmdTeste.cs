using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FerramentaEMT
{
    [Transaction(TransactionMode.Manual)]
    public class CmdTeste : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Autodesk.Revit.UI.TaskDialog.Show(
                "Ferramenta EMT",
                "Add-in carregado com sucesso.\n\nRevit 2025 + .NET 8 funcionando."
            );

            return Result.Succeeded;
        }
    }
}