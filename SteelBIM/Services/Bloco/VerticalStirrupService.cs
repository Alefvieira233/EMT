#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.Bloco;
using static FerramentaEMT.Services.Bloco.RebarCreationService;

namespace FerramentaEMT.Services.Bloco
{
    internal static class VerticalStirrupService
    {
        public static int Generate(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoEstriboVerticalConfig config)
        {
            double cov = ToCm(config.CobrimentoCm);
            double yMin = frame.MinY + cov; double yMax = frame.MaxY - cov;
            double xMin = frame.MinX + cov; double xMax = frame.MaxX - cov;
            double zMin = frame.MinZ + cov; double zMax = frame.MaxZ - cov;

            if (xMax - xMin < ToCm(5.0) || yMax - yMin < ToCm(5.0) || zMax - zMin < ToCm(5.0))
                return 0;

            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            RebarHookType? hook = GetHookTypeByAngle(doc, (int)config.AnguloGarras);
            int created = 0;

            // DirX: quadros no plano YZ, distribuidos ao longo de X
            if (config.DirX.Ativo)
            {
                List<double> xPositions = CalcularPosicoes(
                    xMin, xMax, config.DirX.ModoQuantidade, config.DirX.Quantidade, config.DirX.EspacamentoCm);

                foreach (double x in xPositions)
                {
                    // Retangulo no plano YZ: cantos (x,yMin,zMin), (x,yMax,zMin), (x,yMax,zMax), (x,yMin,zMax)
                    IList<Curve> rect = BuildRectangle(frame,
                        new XYZ(x, yMin, zMin),
                        new XYZ(x, yMax, zMin),
                        new XYZ(x, yMax, zMax),
                        new XYZ(x, yMin, zMax));
                    CreateClosedStirrup(doc, host, barType, frame.XAxis, rect, hook, "EstriboVert-X");
                    created++;
                }
            }

            // DirY: quadros no plano XZ, distribuidos ao longo de Y
            if (config.DirY.Ativo)
            {
                List<double> yPositions = CalcularPosicoes(
                    yMin, yMax, config.DirY.ModoQuantidade, config.DirY.Quantidade, config.DirY.EspacamentoCm);

                foreach (double y in yPositions)
                {
                    // Retangulo no plano XZ: cantos (xMin,y,zMin), (xMax,y,zMin), (xMax,y,zMax), (xMin,y,zMax)
                    IList<Curve> rect = BuildRectangle(frame,
                        new XYZ(xMin, y, zMin),
                        new XYZ(xMax, y, zMin),
                        new XYZ(xMax, y, zMax),
                        new XYZ(xMin, y, zMax));
                    CreateClosedStirrup(doc, host, barType, frame.YAxis, rect, hook, "EstriboVert-Y");
                    created++;
                }
            }

            return created;
        }
    }
}
