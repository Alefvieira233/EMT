#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models.Bloco;
using static SteelBIM.Services.Bloco.RebarCreationService;

namespace SteelBIM.Services.Bloco
{
    internal static class TransverseBandService
    {
        public static int Generate(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoFaixaTransversalConfig config)
        {
            double cov = ToCm(config.CobrimentoCm);
            double z = frame.MinZ + ToCm(config.PosicaoZCm);
            double esp = ToCm(config.EspacamentoCm);

            int created = 0;

            if (config.Direcao == BlocoDirecao.ApenasX || config.Direcao == BlocoDirecao.AmbosXeY)
                created += GenerateAlongX(doc, host, frame, config.BarTypeName, cov, z, esp, config.Dobra);

            if (config.Direcao == BlocoDirecao.ApenasY || config.Direcao == BlocoDirecao.AmbosXeY)
                created += GenerateAlongY(doc, host, frame, config.BarTypeName, cov, z, esp, config.Dobra);

            return created;
        }

        private static int GenerateAlongX(
            Document doc, Element host, BlocoHostFrame frame,
            string barTypeName, double cov, double z, double spacing,
            BlocoRebarBendConfig dobra)
        {
            double xMin = frame.MinX + cov;
            double xMax = frame.MaxX - cov;
            double yMin = frame.MinY + cov;
            double yMax = frame.MaxY - cov;
            if (xMax - xMin < ToCm(5.0))
                return 0;

            RebarBarType barType = GetBarType(doc, barTypeName);
            List<double> positions = DistributeBySpacing(yMin, yMax, spacing);
            int created = 0;

            foreach (double y in positions)
            {
                XYZ p0 = new XYZ(xMin, y, z);
                XYZ p1 = new XYZ(xMax, y, z);
                IList<Curve> curves = BuildBarWithBends(frame, p0, p1, dobra);
                CreateOpenBar(doc, host, barType, frame.YAxis, curves, "FaixaTrans-X");
                created++;
            }
            return created;
        }

        private static int GenerateAlongY(
            Document doc, Element host, BlocoHostFrame frame,
            string barTypeName, double cov, double z, double spacing,
            BlocoRebarBendConfig dobra)
        {
            double xMin = frame.MinX + cov;
            double xMax = frame.MaxX - cov;
            double yMin = frame.MinY + cov;
            double yMax = frame.MaxY - cov;
            if (yMax - yMin < ToCm(5.0))
                return 0;

            RebarBarType barType = GetBarType(doc, barTypeName);
            List<double> positions = DistributeBySpacing(xMin, xMax, spacing);
            int created = 0;

            foreach (double x in positions)
            {
                XYZ p0 = new XYZ(x, yMin, z);
                XYZ p1 = new XYZ(x, yMax, z);
                IList<Curve> curves = BuildBarWithBends(frame, p0, p1, dobra);
                CreateOpenBar(doc, host, barType, frame.XAxis, curves, "FaixaTrans-Y");
                created++;
            }
            return created;
        }

        private static List<double> DistributeBySpacing(double min, double max, double spacing)
        {
            if (spacing <= ToCm(1.0))
                return new List<double> { min, max };
            List<double> result = new List<double>();
            double pos = min;
            while (pos <= max + ToCm(1.0))
            {
                result.Add(System.Math.Min(pos, max));
                pos += spacing;
            }
            if (result.Count > 0 && max - result[result.Count - 1] > ToCm(5.0))
                result.Add(max);
            return result;
        }
    }
}
