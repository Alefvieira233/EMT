using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Utils
{
    public static class RevitUtils
    {
        public const double FT_PER_MM = 0.00328083989501312;
        public const double FT_PER_CM = 0.0328083989501312;
        public const double EPS = 1e-9;

        public static Level GetElementLevel(Document doc, Element el)
        {
            if (el == null) return null;

            if (el.LevelId != ElementId.InvalidElementId)
                return doc.GetElement(el.LevelId) as Level;

            Parameter p = el.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
                return doc.GetElement(p.AsElementId()) as Level;

            if (doc.ActiveView != null && doc.ActiveView.GenLevel != null)
                return doc.ActiveView.GenLevel;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();
        }

        public static XYZ SafeNormalize(XYZ v)
        {
            if (v == null) return XYZ.Zero;
            double len = v.GetLength();
            if (len < EPS) return XYZ.Zero;
            return v.Normalize();
        }

        public static bool IsZeroVector(XYZ v)
        {
            if (v == null) return true;
            return v.GetLength() < EPS;
        }

        public static Curve GetElementCurve(Element el)
        {
            if (el == null) return null;

            LocationCurve lc = el.Location as LocationCurve;
            if (lc != null && lc.Curve != null)
                return lc.Curve;

            ModelCurve mc = el as ModelCurve;
            if (mc != null && mc.GeometryCurve != null)
                return mc.GeometryCurve;

            DetailCurve dc = el as DetailCurve;
            if (dc != null && dc.GeometryCurve != null)
                return dc.GeometryCurve;

            return null;
        }

        public static XYZ ProjectPointOntoPlane(XYZ p, Plane plane)
        {
            XYZ v = p - plane.Origin;
            double dist = v.DotProduct(plane.Normal);
            return p - dist * plane.Normal;
        }

        public static Line ProjectLineOntoPlane(Line line, Plane plane)
        {
            XYZ p0 = ProjectPointOntoPlane(line.GetEndPoint(0), plane);
            XYZ p1 = ProjectPointOntoPlane(line.GetEndPoint(1), plane);

            if (p0.DistanceTo(p1) < EPS)
                return null;

            return Line.CreateBound(p0, p1);
        }

        public static Line EnsureSameDirection(Line refLine, Line testLine)
        {
            if (refLine == null || testLine == null) return testLine;

            XYZ dRef = SafeNormalize(refLine.GetEndPoint(1) - refLine.GetEndPoint(0));
            XYZ dTest = SafeNormalize(testLine.GetEndPoint(1) - testLine.GetEndPoint(0));

            if (dRef.DotProduct(dTest) < 0)
                return Line.CreateBound(testLine.GetEndPoint(1), testLine.GetEndPoint(0));

            return testLine;
        }

        public static Line ReverseLine(Line line)
        {
            if (line == null) return null;
            return Line.CreateBound(line.GetEndPoint(1), line.GetEndPoint(0));
        }

        public static void SetZJustification(FamilyInstance fi, int zJustificationValue)
        {
            if (fi == null) return;

            try
            {
                Parameter p = fi.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
                if (p != null && !p.IsReadOnly)
                    p.Set(zJustificationValue);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao definir Z justification"); }
        }

        public static void SetYZOffsets(FamilyInstance fi, double y, double z)
        {
            if (fi == null) return;

            try
            {
                Parameter py = fi.get_Parameter(BuiltInParameter.Y_OFFSET_VALUE);
                if (py != null && !py.IsReadOnly)
                    py.Set(y);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao definir offset Y"); }

            try
            {
                Parameter pz = fi.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE);
                if (pz != null && !pz.IsReadOnly)
                    pz.Set(z);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao definir offset Z"); }
        }

        public static void SetSectionRotation(FamilyInstance fi, double angleRad)
        {
            if (fi == null) return;

            try
            {
                Parameter rot = fi.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE);
                if (rot != null && !rot.IsReadOnly)
                    rot.Set(angleRad);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao definir rotacao da secao"); }
        }

        public static void DisallowJoins(FamilyInstance fi)
        {
            if (fi == null) return;

            try
            {
                StructuralFramingUtils.DisallowJoinAtEnd(fi, 0);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao desabilitar juncao na extremidade 0"); }

            try
            {
                StructuralFramingUtils.DisallowJoinAtEnd(fi, 1);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao desabilitar juncao na extremidade 1"); }
        }

        public static void AllowJoins(FamilyInstance fi)
        {
            if (fi == null) return;

            try
            {
                StructuralFramingUtils.AllowJoinAtEnd(fi, 0);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao habilitar juncao na extremidade 0"); }

            try
            {
                StructuralFramingUtils.AllowJoinAtEnd(fi, 1);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao habilitar juncao na extremidade 1"); }
        }

        public static void TryJoinGeometry(Document doc, Element first, Element second)
        {
            if (doc == null || first == null || second == null || first.Id == second.Id)
                return;

            try
            {
                if (!JoinGeometryUtils.AreElementsJoined(doc, first, second))
                    JoinGeometryUtils.JoinGeometry(doc, first, second);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao unir geometria entre elementos"); }
        }

        public static double DegToRad(double deg)
        {
            return deg * System.Math.PI / 180.0;
        }

        public static Plane CreatePlaneBy3Points(XYZ p1, XYZ p2, XYZ p3)
        {
            XYZ v1 = p2 - p1;
            XYZ v2 = p3 - p1;
            XYZ normal = v1.CrossProduct(v2);

            if (normal.GetLength() < EPS)
                return null;

            normal = normal.Normalize();

            XYZ xVec = SafeNormalize(v1);
            if (IsZeroVector(xVec))
                return null;

            XYZ yVec = normal.CrossProduct(xVec);
            yVec = SafeNormalize(yVec);

            if (IsZeroVector(yVec))
                return null;

            return Plane.CreateByOriginAndBasis(p1, xVec, yVec);
        }

        public static bool TryGetLineFromPickedElement(
            Autodesk.Revit.UI.UIDocument uidoc,
            string prompt,
            out Element pickedElement,
            out Line pickedLine)
        {
            pickedElement = null;
            pickedLine = null;

            try
            {
                Reference r = uidoc.Selection.PickObject(ObjectType.Element, prompt);
                if (r == null) return false;

                pickedElement = uidoc.Document.GetElement(r);
                Curve c = GetElementCurve(pickedElement);

                if (c == null) return false;

                Line line = c as Line;
                if (line == null)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    if (p0.DistanceTo(p1) < EPS)
                        return false;

                    line = Line.CreateBound(p0, p1);
                }

                pickedLine = line;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static double ParameterAlongLine(Line line, XYZ pt)
        {
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);
            XYZ dir = p1 - p0;
            double len2 = dir.DotProduct(dir);

            if (len2 < EPS) return 0.0;

            return (pt - p0).DotProduct(dir) / len2;
        }

        public static bool TryIntersectLines2DInPlane(Line line1, Line line2, Plane plane, out XYZ intersection3D)
        {
            intersection3D = null;

            XYZ origin = plane.Origin;
            XYZ ux = plane.XVec.Normalize();
            XYZ uy = plane.YVec.Normalize();

            XYZ a1 = line1.GetEndPoint(0);
            XYZ a2 = line1.GetEndPoint(1);
            XYZ b1 = line2.GetEndPoint(0);
            XYZ b2 = line2.GetEndPoint(1);

            double a1x = (a1 - origin).DotProduct(ux);
            double a1y = (a1 - origin).DotProduct(uy);
            double a2x = (a2 - origin).DotProduct(ux);
            double a2y = (a2 - origin).DotProduct(uy);

            double b1x = (b1 - origin).DotProduct(ux);
            double b1y = (b1 - origin).DotProduct(uy);
            double b2x = (b2 - origin).DotProduct(ux);
            double b2y = (b2 - origin).DotProduct(uy);

            double rX = a2x - a1x;
            double rY = a2y - a1y;
            double sX = b2x - b1x;
            double sY = b2y - b1y;

            double denom = rX * sY - rY * sX;
            if (System.Math.Abs(denom) < EPS)
                return false;

            double qpx = b1x - a1x;
            double qpy = b1y - a1y;

            double t = (qpx * sY - qpy * sX) / denom;
            double u = (qpx * rY - qpy * rX) / denom;

            if (t < -1e-6 || t > 1.0 + 1e-6 || u < -1e-6 || u > 1.0 + 1e-6)
                return false;

            XYZ hit = a1 + (a2 - a1) * t;
            intersection3D = ProjectPointOntoPlane(hit, plane);
            return true;
        }

        public static List<XYZ> GetCutPointsOnTerca(Line eixoTerca, Plane plane, List<Curve> curvasBanzos)
        {
            List<XYZ> pts = new List<XYZ>();

            if (eixoTerca == null || plane == null || curvasBanzos == null || curvasBanzos.Count == 0)
                return pts;

            foreach (Curve c in curvasBanzos)
            {
                if (c == null) continue;

                Line banzoLine = c as Line;
                if (banzoLine == null)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);
                    if (p0.DistanceTo(p1) < EPS) continue;
                    banzoLine = Line.CreateBound(p0, p1);
                }

                Line banzoProj = ProjectLineOntoPlane(banzoLine, plane);
                if (banzoProj == null) continue;

                XYZ hit;
                if (TryIntersectLines2DInPlane(eixoTerca, banzoProj, plane, out hit))
                {
                    double t = ParameterAlongLine(eixoTerca, hit);

                    if (t > 1e-6 && t < 1.0 - 1e-6)
                    {
                        bool exists = false;
                        foreach (XYZ p in pts)
                        {
                            if (p.DistanceTo(hit) < 1e-4)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                            pts.Add(hit);
                    }
                }
            }

            return pts.OrderBy(p => ParameterAlongLine(eixoTerca, p)).ToList();
        }
    }
}
