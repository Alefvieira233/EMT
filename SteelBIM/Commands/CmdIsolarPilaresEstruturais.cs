using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdIsolarPilaresEstruturais : FerramentaCommandBase
    {
        protected override string CommandName => "Isolar Pilares";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;
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
