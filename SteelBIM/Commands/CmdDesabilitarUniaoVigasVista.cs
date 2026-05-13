using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdDesabilitarUniaoVigasVista : FerramentaCommandBase
    {
        protected override string CommandName => "Desabilitar União - Vigas da Vista";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            View view = doc.ActiveView;

            List<FamilyInstance> vigas = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            if (vigas.Count == 0)
            {
                AppDialogService.ShowWarning("Desabilitar União - Vigas da Vista", "Nenhuma viga estrutural foi encontrada na vista ativa.", "Nenhuma viga encontrada");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Desabilitar União das Vigas - Vista"))
            {
                t.Start();
                foreach (FamilyInstance viga in vigas)
                    RevitUtils.DisallowJoins(viga);
                t.Commit();
            }

            uidoc.Selection.SetElementIds(vigas.Select(v => v.Id).ToList());

            AppDialogService.ShowInfo(
                "Desabilitar União - Vigas da Vista",
                $"União desabilitada nos dois extremos.\n\nVigas processadas: {vigas.Count}",
                "Processamento concluído");

            return Result.Succeeded;
        }
    }
}
