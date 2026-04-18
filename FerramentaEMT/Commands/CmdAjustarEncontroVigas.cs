using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdAjustarEncontroVigas : FerramentaCommandBase
    {
        protected override string CommandName => "Ajustar Encontro";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            FamilyInstance vigaPrincipal = SelecionarVigaPrincipal(uidoc, doc);
            if (vigaPrincipal == null)
                return Result.Cancelled;

            Reference referenciaCortador = SelecionarCortador(uidoc);
            if (referenciaCortador == null)
                return Result.Cancelled;

            Element cortador = doc.GetElement(referenciaCortador);
            if (!EhCortadorValido(cortador))
            {
                AppDialogService.ShowWarning(CommandName, "Selecione uma viga estrutural ou um pilar estrutural como elemento de encontro.", "Selecao invalida");
                return Result.Cancelled;
            }

            AjustarEncontroService service = new AjustarEncontroService();
            AjustarEncontroService.AjustarEncontroResultado resultado;

            using (Transaction t = new Transaction(doc, "Ajustar Encontro de Vigas"))
            {
                t.Start();
                resultado = service.Executar(doc, vigaPrincipal, cortador, referenciaCortador);
                t.Commit();
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { vigaPrincipal.Id, cortador.Id });

            if (resultado.Diagnostico.Count > 0 && !resultado.HouveAlteracao)
                Logger.Info("Diagnostico: {Msg}", string.Join(" | ", resultado.Diagnostico));

            return Result.Succeeded;
        }

        private FamilyInstance SelecionarVigaPrincipal(UIDocument uidoc, Document doc)
        {
            Reference referencia = uidoc.Selection.PickObject(
                ObjectType.Element,
                new FiltroVigasEstruturais(),
                "Selecione a viga principal que deve ser ajustada");

            return doc.GetElement(referencia) as FamilyInstance;
        }

        private Reference SelecionarCortador(UIDocument uidoc)
        {
            return uidoc.Selection.PickObject(
                ObjectType.PointOnElement,
                new FiltroVigasOuPilares(uidoc.Document),
                "Clique na viga ou no pilar que forma o encontro, de preferência perto da extremidade a ajustar");
        }

        private bool EhCortadorValido(Element elemento)
        {
            return AjustarEncontroService.EhCortadorValido(elemento);
        }

        private sealed class FiltroVigasEstruturais : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem is FamilyInstance fi &&
                fi.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming &&
                fi.StructuralType == StructuralType.Beam;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private sealed class FiltroVigasOuPilares : ISelectionFilter
        {
            private readonly Document _doc;

            public FiltroVigasOuPilares(Document doc)
            {
                _doc = doc;
            }

            public bool AllowElement(Element elem)
            {
                if (elem is not FamilyInstance fi || fi.Category == null)
                    return false;

                long categoryId = fi.Category.Id.Value;
                return categoryId == (long)BuiltInCategory.OST_StructuralFraming ||
                       categoryId == (long)BuiltInCategory.OST_StructuralColumns;
            }

            public bool AllowReference(Reference reference, XYZ position) =>
                reference != null && AllowElement(_doc.GetElement(reference));
        }
    }
}
