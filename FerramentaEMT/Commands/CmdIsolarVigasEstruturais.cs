using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdIsolarVigasEstruturais : FerramentaCommandBase
    {
        protected override string CommandName => "Isolar Vigas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;
            ElementId categoriaId = new ElementId(BuiltInCategory.OST_StructuralFraming);

            using (Transaction t = new Transaction(doc, "Isolar Vigas Estruturais"))
            {
                t.Start();
                view.IsolateCategoriesTemporary(new List<ElementId> { categoriaId });
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
