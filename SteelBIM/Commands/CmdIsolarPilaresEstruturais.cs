using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Utils;

namespace SteelBIM.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdIsolarPilaresEstruturais : FerramentaCommandBase
    {
        protected override string CommandName => "Isolar Pilares";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;
            if (view == null)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "Este comando precisa de uma vista ativa. Abra uma vista no Revit antes de executar.",
                    "Sem vista ativa");
                return Result.Cancelled;
            }
            ElementId categoriaId = new ElementId(BuiltInCategory.OST_StructuralColumns);

            using (Transaction t = new Transaction(doc, "Isolar Pilares Estruturais"))
            {
                t.Start();
                view.IsolateCategoriesTemporary(new List<ElementId> { categoriaId });
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
