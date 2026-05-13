#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models.Bloco;
using static SteelBIM.Services.Bloco.RebarCreationService;

namespace SteelBIM.Services.Bloco
{
    internal static class SideSkinRebarService
    {
        public static int Generate(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoArmaduraLateralConfig config)
        {
            if (!config.LancarNasFacesX && !config.LancarNasFacesY) return 0;

            double cov = ToCm(config.CobrimentoCm);
            double zMin = frame.MinZ + cov;
            double zMax = frame.MaxZ - cov;
            if (zMax - zMin < ToCm(5.0)) return 0;

            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            List<double> zLevels = CalcularPosicoes(
                zMin, zMax, config.ModoQuantidade, config.Quantidade, config.EspacamentoCm);

            int created = 0;

            // Faces minX e maxX: barras correm ao longo de Y
            if (config.LancarNasFacesX)
            {
                double yMin = frame.MinY + cov;
                double yMax = frame.MaxY - cov;

                foreach (double xFace in new[] { frame.MinX + cov, frame.MaxX - cov })
                {
                    foreach (double z in zLevels)
                    {
                        XYZ p0 = new XYZ(xFace, yMin, z);
                        XYZ p1 = new XYZ(xFace, yMax, z);
                        IList<Curve> curves = BuildBarWithBends(frame, p0, p1, config.Dobra);
                        CreateOpenBar(doc, host, barType, frame.XAxis, curves, "Lateral-FaceX");
                        created++;
                    }
                }
            }

            // Faces minY e maxY: barras correm ao longo de X
            if (config.LancarNasFacesY)
            {
                double xMin = frame.MinX + cov;
                double xMax = frame.MaxX - cov;

                foreach (double yFace in new[] { frame.MinY + cov, frame.MaxY - cov })
                {
                    foreach (double z in zLevels)
                    {
                        XYZ p0 = new XYZ(xMin, yFace, z);
                        XYZ p1 = new XYZ(xMax, yFace, z);
                        IList<Curve> curves = BuildBarWithBends(frame, p0, p1, config.Dobra);
                        CreateOpenBar(doc, host, barType, frame.YAxis, curves, "Lateral-FaceY");
                        created++;
                    }
                }
            }

            return created;
        }
    }
}
