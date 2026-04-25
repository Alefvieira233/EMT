using System;
using FerramentaEMT.Models.PF;

namespace FerramentaEMT.Services.PF
{
    internal static class PfNbr6118AnchorageService
    {
        private const double GammaC = 1.4;
        private const double GammaS = 1.15;

        public static PfAnchorageResult Calculate(double diameterMm, PfLapSpliceConfig config)
        {
            if (diameterMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(diameterMm), "Diametro da barra deve ser maior que zero.");
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            double fck = Math.Max(12.0, config.ConcreteFckMpa);
            double fctkInf = CalculateFctkInf(fck);
            double fctd = fctkInf / GammaC;
            double fyd = Math.Max(250.0, config.SteelFykMpa) / GammaS;

            double eta1 = GetEta1(config.BarSurface);
            double eta2 = config.BondZone == PfBondZone.Boa ? 1.0 : 0.7;
            double eta3 = diameterMm <= 32.0 ? 1.0 : (132.0 - diameterMm) / 100.0;
            eta3 = Math.Max(0.7, eta3);

            double fbd = eta1 * eta2 * eta3 * fctd;
            double lbCm = ((diameterMm / 4.0) * (fyd / fbd)) / 10.0;
            double anchorageAlpha = GetAnchorageAlpha(config.AnchorageType);
            double areaRatio = config.AsCalcCm2 > 0.0 && config.AsEfCm2 > 0.0
                ? Math.Min(1.0, config.AsCalcCm2 / config.AsEfCm2)
                : 1.0;

            double lbNecCm = anchorageAlpha * lbCm * areaRatio;
            double lbMinCm = Math.Max(0.3 * lbCm, Math.Max(diameterMm, 10.0));
            lbNecCm = Math.Max(lbNecCm, lbMinCm);

            double spliceAlpha = GetSpliceAlpha(config.SplicePercentage, config.BarSpacingCm, diameterMm);
            double l0Cm = spliceAlpha * lbNecCm;
            double l0MinCm = Math.Max(0.3 * spliceAlpha * lbNecCm, Math.Max(1.5 * diameterMm, 20.0));
            l0Cm = Math.Max(l0Cm, l0MinCm);

            return new PfAnchorageResult(
                diameterMm,
                fbd,
                eta1,
                eta2,
                eta3,
                lbCm,
                lbNecCm,
                lbMinCm,
                l0Cm,
                l0MinCm,
                anchorageAlpha,
                spliceAlpha,
                fctkInf,
                fyd);
        }

        private static double CalculateFctkInf(double fck)
        {
            double fctm = fck <= 50.0
                ? 0.3 * Math.Pow(fck, 2.0 / 3.0)
                : 2.12 * Math.Log(1.0 + (0.11 * fck));

            return 0.7 * fctm;
        }

        private static double GetEta1(PfBarSurfaceType surfaceType)
        {
            switch (surfaceType)
            {
                case PfBarSurfaceType.Lisa:
                    return 1.0;
                case PfBarSurfaceType.Entalhada:
                    return 1.4;
                default:
                    return 2.25;
            }
        }

        private static double GetAnchorageAlpha(PfAnchorageType anchorageType)
        {
            return anchorageType == PfAnchorageType.Reta ? 1.0 : 0.7;
        }

        private static double GetSpliceAlpha(double splicePercentage, double barSpacingCm, double diameterMm)
        {
            double phiCm = diameterMm / 10.0;
            double spacingRatio = phiCm > 0.0 ? barSpacingCm / phiCm : 0.0;
            int column = spacingRatio <= 5.0 ? 0 : spacingRatio <= 10.0 ? 1 : 2;

            int row;
            if (splicePercentage <= 20.0)
                row = 0;
            else if (splicePercentage <= 25.0)
                row = 1;
            else if (splicePercentage <= 33.0)
                row = 2;
            else if (splicePercentage <= 50.0)
                row = 3;
            else
                row = 4;

            double[,] table =
            {
                { 1.2, 1.4, 1.6, 1.8, 2.0 },
                { 1.0, 1.0, 1.2, 1.4, 1.6 },
                { 1.0, 1.0, 1.0, 1.2, 1.4 }
            };

            return table[column, row];
        }
    }

    internal sealed class PfAnchorageResult
    {
        public PfAnchorageResult(
            double diameterMm,
            double fbdMpa,
            double eta1,
            double eta2,
            double eta3,
            double basicAnchorageCm,
            double requiredAnchorageCm,
            double minimumAnchorageCm,
            double spliceLengthCm,
            double minimumSpliceLengthCm,
            double anchorageAlpha,
            double spliceAlpha,
            double fctkInfMpa,
            double fydMpa)
        {
            DiameterMm = diameterMm;
            FbdMpa = fbdMpa;
            Eta1 = eta1;
            Eta2 = eta2;
            Eta3 = eta3;
            BasicAnchorageCm = basicAnchorageCm;
            RequiredAnchorageCm = requiredAnchorageCm;
            MinimumAnchorageCm = minimumAnchorageCm;
            SpliceLengthCm = spliceLengthCm;
            MinimumSpliceLengthCm = minimumSpliceLengthCm;
            AnchorageAlpha = anchorageAlpha;
            SpliceAlpha = spliceAlpha;
            FctkInfMpa = fctkInfMpa;
            FydMpa = fydMpa;
        }

        public double DiameterMm { get; }
        public double FbdMpa { get; }
        public double Eta1 { get; }
        public double Eta2 { get; }
        public double Eta3 { get; }
        public double BasicAnchorageCm { get; }
        public double RequiredAnchorageCm { get; }
        public double MinimumAnchorageCm { get; }
        public double SpliceLengthCm { get; }
        public double MinimumSpliceLengthCm { get; }
        public double AnchorageAlpha { get; }
        public double SpliceAlpha { get; }
        public double FctkInfMpa { get; }
        public double FydMpa { get; }

        public string ToDetailText()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "EMT NBR 6118:2023 | phi {0:0.#} mm | lb {1:0.#} cm | lb,nec {2:0.#} cm | traspasse l0 {3:0.#} cm | fbd {4:0.###} MPa",
                DiameterMm,
                BasicAnchorageCm,
                RequiredAnchorageCm,
                SpliceLengthCm,
                FbdMpa);
        }
    }
}
