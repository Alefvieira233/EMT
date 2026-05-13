using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace SteelBIM.Services.PF
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
        /// Detecta estaca individual (nao bloco). Heuristica por geometria: bbox alongado
        /// verticalmente — altura (dz) e pelo menos 3x maior que a maior dimensao horizontal
        /// (max(dx, dy)). Sem essa heuristica, qualquer OST_StructuralFoundation seria
        /// detectado como pile, colidindo com IsTwoPileCap.
        /// </summary>
        public static bool IsStructuralPile(Element element)
        {
            if (!(element is FamilyInstance fi))
                return false;

            if (fi.Category?.BuiltInCategory != BuiltInCategory.OST_StructuralFoundation)
                return false;

            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null)
                return false;

            double dx = Math.Abs(bbox.Max.X - bbox.Min.X);
            double dy = Math.Abs(bbox.Max.Y - bbox.Min.Y);
            double dz = Math.Abs(bbox.Max.Z - bbox.Min.Z);

            double horizontalMax = Math.Max(dx, dy);
            if (horizontalMax <= 1e-6)
                return false;

            // Estaca: altura >= 3x a maior dimensao horizontal (alongada vertical)
            return (dz / horizontalMax) >= 3.0;
        }

        /// <summary>
        /// Detecta blocos de duas estacas (familia de fundacao estrutural).
        /// Adicionado na incorporacao Victor Wave 2 — usado pelo
        /// CmdPfInserirAcosBlocoDuasEstacas e PfTwoPileCapRebarService.
        ///
        /// Onda 1 (Victor Final): bloco e qualquer fundacao que NAO seja
        /// estaca alongada vertical. Antes desta refinacao, IsTwoPileCap aceitava
        /// estacas individuais — o que fazia o CmdPfInserirAcosBlocoDuasEstacas
        /// tentar lancar barras de bloco em estaca, comportamento indesejado.
        /// </summary>
        public static bool IsTwoPileCap(Element element)
        {
            if (!(element is FamilyInstance fi))
                return false;

            if (fi.Category?.BuiltInCategory != BuiltInCategory.OST_StructuralFoundation)
                return false;

            return !IsStructuralPile(element);
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

        /// <summary>
        /// Arredonda um valor de ordenacao (horizontal ou vertical, em pes do Revit)
        /// para o multiplo mais proximo da toleranica dada. Usado pelo
        /// PfFoundationPlacementService para agrupar fundacoes em "linhas" e "colunas"
        /// quando o usuario tem grids fora do snap perfeito do Revit.
        /// </summary>
        public static double GetSnappedOrder(double rawOrder, double toleranceFt)
        {
            if (toleranceFt <= 0.0)
                return rawOrder;

            return Math.Round(rawOrder / toleranceFt) * toleranceFt;
        }

        /// <summary>
        /// Retorna os extents (Min/Max) das coordenadas horizontal e vertical do elemento
        /// projetadas no plano da vista. Usado pelo PfFoundationPlacementService para
        /// detectar a "caixa" ocupada por cada fundacao na vista (e agrupar por linhas).
        /// </summary>
        public static (double MinHorizontal, double MaxHorizontal, double MinVertical, double MaxVertical) GetViewOrderExtents(
            Element element,
            View view)
        {
            List<XYZ> points = GetOrderingPoints(element, view);
            if (points.Count == 0)
            {
                double horizontal = GetHorizontalOrder(view, XYZ.Zero);
                double vertical = GetVerticalOrder(view, XYZ.Zero);
                return (horizontal, horizontal, vertical, vertical);
            }

            List<double> horizontals = points.Select(pt => GetHorizontalOrder(view, pt)).ToList();
            List<double> verticals = points.Select(pt => GetVerticalOrder(view, pt)).ToList();

            return (
                horizontals.Min(),
                horizontals.Max(),
                verticals.Min(),
                verticals.Max());
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

        private static List<XYZ> GetOrderingPoints(Element element, View view)
        {
            List<XYZ> points = new List<XYZ>();

            if (element?.Location is LocationPoint lp)
                points.Add(lp.Point);

            if (element?.Location is LocationCurve lc && lc.Curve != null)
            {
                points.Add(lc.Curve.GetEndPoint(0));
                points.Add(lc.Curve.GetEndPoint(1));
            }

            BoundingBoxXYZ bbox = element?.get_BoundingBox(view) ?? element?.get_BoundingBox(null);
            if (bbox != null)
            {
                points.AddRange(GetBoundingBoxCorners(bbox));
            }

            if (points.Count == 0 && element != null)
                points.Add(GetRepresentativePoint(element, view));

            return points;
        }

        private static IEnumerable<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ bbox)
        {
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z);
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
