using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace FerramentaEMT.Services.PF
{
    internal static class PfElementService
    {
        public static List<Element> GetSelectionOrPick(
            UIDocument uidoc,
            Func<Element, bool> predicate,
            string prompt)
        {
            List<Element> selecionados = uidoc.Selection.GetElementIds()
                .Select(uidoc.Document.GetElement)
                .Where(x => x != null && predicate(x))
                .ToList();

            if (selecionados.Count > 0)
                return selecionados;

            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PredicateSelectionFilter(predicate),
                    prompt);

                return refs
                    .Select(x => uidoc.Document.GetElement(x))
                    .Where(x => x != null && predicate(x))
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<Element>();
            }
        }

        public static List<ElementId> CollectIdsInView(Document doc, View view, Func<FamilyInstance, bool> predicate)
        {
            return new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>()
                .Where(x => predicate?.Invoke(x as FamilyInstance) == true)
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        public static List<ElementId> CollectIdsInView(Document doc, View view, Func<Element, bool> predicate)
        {
            return new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>()
                .Where(predicate)
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        public static bool IsStructuralColumn(Element element)
        {
            return element is FamilyInstance fi &&
                   fi.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralColumns;
        }

        public static bool IsStructuralBeam(Element element)
        {
            return element is FamilyInstance fi &&
                   fi.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralFraming &&
                   !IsPfModelElement(fi, "laje") &&
                   !IsPfModelElement(fi, "consolo");
        }

        /// <summary>
        /// Detecta blocos de duas estacas (familia de fundacao estrutural).
        /// Adicionado na incorporacao Victor Wave 2 — usado pelo
        /// CmdPfInserirAcosBlocoDuasEstacas e PfTwoPileCapRebarService.
        /// </summary>
        public static bool IsTwoPileCap(Element element)
        {
            if (!(element is FamilyInstance fi))
                return false;

            if (fi.Category?.BuiltInCategory != BuiltInCategory.OST_StructuralFoundation)
                return false;

            return true;
        }

        public static bool IsPfLaje(Element element)
        {
            return element?.Category?.BuiltInCategory == BuiltInCategory.OST_Floors ||
                   (element is FamilyInstance fi && IsPfModelElement(fi, "laje"));
        }

        public static bool IsPfConsolo(Element element)
        {
            return element is FamilyInstance fi && IsPfModelElement(fi, "consolo");
        }

        public static XYZ GetRepresentativePoint(Element element, View view)
        {
            if (element?.Location is LocationPoint lp)
                return lp.Point;

            if (element?.Location is LocationCurve lc)
            {
                Curve curve = lc.Curve;
                if (curve != null)
                    return (curve.GetEndPoint(0) + curve.GetEndPoint(1)) / 2.0;
            }

            BoundingBoxXYZ bbox = element?.get_BoundingBox(view) ?? element?.get_BoundingBox(null);
            if (bbox != null)
                return (bbox.Min + bbox.Max) / 2.0;

            return XYZ.Zero;
        }

        public static bool TrySetElementMark(Element element, string value)
        {
            if (element == null)
                return false;

            Parameter mark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (CanWrite(mark))
            {
                mark.Set(value);
                return true;
            }

            foreach (string parameterName in new[] { "Mark", "Marca" })
            {
                Parameter fallback = element.LookupParameter(parameterName);
                if (CanWrite(fallback))
                {
                    fallback.Set(value);
                    return true;
                }
            }

            return false;
        }

        public static bool IsPfModelElement(FamilyInstance instance, string expectedModelToken)
        {
            string model = GetModelValue(instance?.Symbol);
            if (string.IsNullOrWhiteSpace(model))
                model = GetModelValue(instance);

            return Normalize(model).Contains(Normalize(expectedModelToken));
        }

        public static double GetHorizontalOrder(View view, XYZ point)
        {
            XYZ origin = view?.Origin ?? XYZ.Zero;
            XYZ right = view?.RightDirection ?? XYZ.BasisX;
            return (point - origin).DotProduct(right);
        }

        public static double GetVerticalOrder(View view, XYZ point)
        {
            XYZ origin = view?.Origin ?? XYZ.Zero;
            XYZ up = view?.UpDirection ?? XYZ.BasisY;
            return (point - origin).DotProduct(up);
        }

        public static int GetBeamAxisGroup(Element element, View view)
        {
            if (!(element?.Location is LocationCurve lc) || lc.Curve == null)
                return 2;

            XYZ direction = (lc.Curve.GetEndPoint(1) - lc.Curve.GetEndPoint(0));
            if (direction.IsZeroLength())
                return 2;

            XYZ right = view?.RightDirection ?? XYZ.BasisX;
            XYZ up = view?.UpDirection ?? XYZ.BasisY;

            double onRight = Math.Abs(direction.Normalize().DotProduct(right));
            double onUp = Math.Abs(direction.Normalize().DotProduct(up));
            return onRight >= onUp ? 0 : 1;
        }

        public static string GetHostPreview(Element element)
        {
            if (element == null)
                return string.Empty;

            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null)
                return string.Empty;

            double dx = ToCentimeters(bbox.Max.X - bbox.Min.X);
            double dy = ToCentimeters(bbox.Max.Y - bbox.Min.Y);
            double dz = ToCentimeters(bbox.Max.Z - bbox.Min.Z);

            if (IsStructuralColumn(element))
                return $"Amostra selecionada: seção aproximada {dx:F1} x {dy:F1} cm | altura {dz:F1} cm";

            if (IsStructuralBeam(element))
            {
                double comprimento = element.Location is LocationCurve lc && lc.Curve != null
                    ? ToCentimeters(lc.Curve.Length)
                    : Math.Max(dx, dy);
                double largura = Math.Min(dx, dy);
                return $"Amostra selecionada: seção aproximada {largura:F1} x {dz:F1} cm | comprimento {comprimento:F1} cm";
            }

            return $"Amostra selecionada: {dx:F1} x {dy:F1} x {dz:F1} cm";
        }

        private static string GetModelValue(Element element)
        {
            if (element == null)
                return string.Empty;

            foreach (string parameterName in new[] { "Modelo", "Model", "MODELO" })
            {
                Parameter parameter = element.LookupParameter(parameterName);
                if (parameter == null)
                    continue;

                string value = parameter.AsString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;

                value = parameter.AsValueString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }

        private static bool CanWrite(Parameter parameter)
        {
            return parameter != null &&
                   !parameter.IsReadOnly &&
                   parameter.StorageType == StorageType.String;
        }

        private static double ToCentimeters(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Centimeters);
        }

        private sealed class PredicateSelectionFilter : ISelectionFilter
        {
            private readonly Func<Element, bool> _predicate;

            public PredicateSelectionFilter(Func<Element, bool> predicate)
            {
                _predicate = predicate;
            }

            public bool AllowElement(Element elem)
            {
                return _predicate?.Invoke(elem) == true;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
