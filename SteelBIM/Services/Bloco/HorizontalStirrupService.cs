#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.Bloco;
using static FerramentaEMT.Services.Bloco.RebarCreationService;

namespace FerramentaEMT.Services.Bloco
{
    internal static class HorizontalStirrupService
    {
        public static int Generate(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoEstriboHorizontalConfig config)
        {
            double cov = ToCm(config.CobrimentoCm);
            double xMin = frame.MinX + cov; double xMax = frame.MaxX - cov;
            double yMin = frame.MinY + cov; double yMax = frame.MaxY - cov;
            double zMin = frame.MinZ + cov; double zMax = frame.MaxZ - cov;

            if (xMax - xMin < ToCm(5.0) || yMax - yMin < ToCm(5.0) || zMax - zMin < ToCm(5.0))
                return 0;

            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            RebarHookType? hook = GetHookTypeByAngle(doc, (int)config.AnguloGarras);

            List<double> zPositions = CalcularPosicoes(
                zMin, zMax, config.ModoQuantidade, config.Quantidade, config.EspacamentoCm);

            int created = 0;
            foreach (double z in zPositions)
            {
                // Retangulo no plano XY: cantos em sentido anti-horario visto de cima
                IList<Curve> rect = BuildRectangle(frame,
                    new XYZ(xMin, yMin, z),
                    new XYZ(xMax, yMin, z),
                    new XYZ(xMax, yMax, z),
                    new XYZ(xMin, yMax, z));
                CreateClosedStirrup(doc, host, barType, frame.ZAxis, rect, hook, "EstriboHoriz");
                created++;
            }

            return created;
        }
    }
}
