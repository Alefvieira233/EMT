#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.Bloco;
using static FerramentaEMT.Services.Bloco.RebarCreationService;

namespace FerramentaEMT.Services.Bloco
{
    internal static class BottomRebarService
    {
        public static int Generate(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoBarraInferiorConfig config)
        {
            int created = 0;

            if (config.BarsX.Ativo && !string.IsNullOrWhiteSpace(config.BarsX.BarTypeName))
                created += GenerateDirection(doc, host, frame, config.BarsX, isAlongX: true, "Inferior-X");

            if (config.BarsY.Ativo && !string.IsNullOrWhiteSpace(config.BarsY.BarTypeName))
                created += GenerateDirection(doc, host, frame, config.BarsY, isAlongX: false, "Inferior-Y");

            return created;
        }

        private static int GenerateDirection(
            Document doc, Element host, BlocoHostFrame frame,
            BlocoBarraDirecaoConfig cfg, bool isAlongX, string label)
        {
            double cov = ToCm(cfg.CobrimentoCm);
            double z = frame.MinZ + cov + ToCm(cfg.OffsetZCm);

            double runMin, runMax, distMin, distMax;
            if (isAlongX)
            {
                runMin = frame.MinX + cov; runMax = frame.MaxX - cov;
                distMin = frame.MinY + cov; distMax = frame.MaxY - cov;
            }
            else
            {
                runMin = frame.MinY + cov; runMax = frame.MaxY - cov;
                distMin = frame.MinX + cov; distMax = frame.MaxX - cov;
            }

            if (runMax - runMin < ToCm(5.0) || distMax - distMin < ToCm(5.0))
                return 0;

            RebarBarType barType = GetBarType(doc, cfg.BarTypeName);
            List<double> positions = CalcularPosicoes(
                distMin, distMax, cfg.ModoQuantidade, cfg.Quantidade, cfg.EspacamentoCm);

            XYZ normal = isAlongX ? frame.YAxis : frame.XAxis;
            int created = 0;

            foreach (double pos in positions)
            {
                XYZ p0 = isAlongX
                    ? new XYZ(runMin, pos, z)
                    : new XYZ(pos, runMin, z);
                XYZ p1 = isAlongX
                    ? new XYZ(runMax, pos, z)
                    : new XYZ(pos, runMax, z);

                IList<Curve> curves = BuildBarWithBends(frame, p0, p1, cfg.Dobra);
                CreateOpenBar(doc, host, barType, normal, curves, label);
                created++;
            }

            return created;
        }
    }
}
