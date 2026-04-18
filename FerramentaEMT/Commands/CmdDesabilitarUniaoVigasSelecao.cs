using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdDesabilitarUniaoVigasSelecao : FerramentaCommandBase
    {
        protected override string CommandName => "Desabilitar União - Vigas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<FamilyInstance> vigas = ObterVigasSelecionadas(uidoc, doc);
            if (vigas.Count == 0)
                vigas = PedirSelecaoDeVigas(uidoc, doc);

            if (vigas.Count == 0)
            {
                AppDialogService.ShowWarning("Desabilitar União - Vigas", "Nenhuma viga estrutural válida foi selecionada.", "Nenhuma viga encontrada");
                return Result.Cancelled;
            }

            int processadas = 0;
            using (Transaction t = new Transaction(doc, "Desabilitar União das Vigas - Seleção"))
            {
                t.Start();
                foreach (FamilyInstance viga in vigas)
                {
                    RevitUtils.DisallowJoins(viga);
                    processadas++;
                }
                t.Commit();
            }

            uidoc.Selection.SetElementIds(vigas.Select(v => v.Id).ToList());

            AppDialogService.ShowInfo(
                "Desabilitar União - Vigas",
                $"União desabilitada nos dois extremos.\n\nVigas processadas: {processadas}",
                "Processamento concluído");

            return Result.Succeeded;
        }

        private List<FamilyInstance> ObterVigasSelecionadas(UIDocument uidoc, Document doc)
        {
            return uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Where(EhVigaEstrutural)
                .ToList();
        }

        private List<FamilyInstance> PedirSelecaoDeVigas(UIDocument uidoc, Document doc)
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroVigasEstruturais(),
                    "Selecione as vigas para desabilitar a união nos dois extremos");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<FamilyInstance>()
                    .Where(EhVigaEstrutural)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<FamilyInstance>();
            }
        }

        private bool EhVigaEstrutural(FamilyInstance instancia) =>
            instancia.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;

        private sealed class FiltroVigasEstruturais : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem is FamilyInstance fi &&
                fi.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
