#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace FerramentaEMT.Services.Bloco
{
    internal static class BlockGeometryService
    {
        public static BlocoHostFrame BuildFrame(Element host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            BoundingBoxXYZ? bbox = host.get_BoundingBox(null);
            if (bbox == null)
                throw new InvalidOperationException("Nao foi possivel ler a caixa envolvente do bloco.");

            XYZ xAxis = XYZ.BasisX;
            XYZ origin = new XYZ(
                (bbox.Min.X + bbox.Max.X) / 2.0,
                (bbox.Min.Y + bbox.Max.Y) / 2.0,
                bbox.Min.Z);

            if (host is FamilyInstance fi)
            {
                XYZ h = NormalizeHorizontal(fi.GetTransform().BasisX);
                if (!h.IsZeroLength()) xAxis = h;
                if (fi.Location is LocationPoint lp)
                    origin = new XYZ(lp.Point.X, lp.Point.Y, bbox.Min.Z);
            }

            XYZ yAxis = XYZ.BasisZ.CrossProduct(xAxis).Normalize();

            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = xAxis;
            t.BasisY = yAxis;
            t.BasisZ = XYZ.BasisZ;

            Transform inv = t.Inverse;
            IEnumerable<XYZ> locals = BoundingCorners(bbox).Select(inv.OfPoint);

            return new BlocoHostFrame(
                t,
                locals.Min(p => p.X), locals.Max(p => p.X),
                locals.Min(p => p.Y), locals.Max(p => p.Y),
                locals.Min(p => p.Z), locals.Max(p => p.Z));
        }

        public static bool CanHostRebar(Element? host)
            => host != null && RebarHostData.GetRebarHostData(host) != null;

        private static XYZ NormalizeHorizontal(XYZ v)
        {
            XYZ h = new XYZ(v.X, v.Y, 0.0);
            return h.IsZeroLength() ? XYZ.BasisX : h.Normalize();
        }

        private static IEnumerable<XYZ> BoundingCorners(BoundingBoxXYZ bbox)
        {
            for (int i = 0; i < 8; i++)
                yield return new XYZ(
                    (i & 1) == 0 ? bbox.Min.X : bbox.Max.X,
                    (i & 2) == 0 ? bbox.Min.Y : bbox.Max.Y,
                    (i & 4) == 0 ? bbox.Min.Z : bbox.Max.Z);
        }
    }

    internal sealed class BlocoHostFrame
    {
        public BlocoHostFrame(Transform localToWorld,
            double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
        {
            LocalToWorld = localToWorld;
            MinX = minX; MaxX = maxX;
            MinY = minY; MaxY = maxY;
            MinZ = minZ; MaxZ = maxZ;
        }

        public Transform LocalToWorld { get; }
        public XYZ XAxis => LocalToWorld.BasisX;
        public XYZ YAxis => LocalToWorld.BasisY;
        public XYZ ZAxis => LocalToWorld.BasisZ;
        public double MinX { get; }
        public double MaxX { get; }
        public double MinY { get; }
        public double MaxY { get; }
        public double MinZ { get; }
        public double MaxZ { get; }
        public double LengthX => MaxX - MinX;
        public double LengthY => MaxY - MinY;
        public double LengthZ => MaxZ - MinZ;

        public XYZ WorldPoint(double x, double y, double z)
            => LocalToWorld.OfPoint(new XYZ(x, y, z));
    }
}
