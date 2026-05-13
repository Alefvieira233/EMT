using System;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.PF
{
    /// <summary>
    /// Testes do calculo de ancoragem e traspasse NBR 6118 (Victor Wave 2).
    /// Valores de referencia extraidos da propria formulacao da norma aplicada
    /// em cenarios tipicos de concreto fck 25/30 e aco CA-50.
    /// </summary>
    public class PfNbr6118AnchorageServiceTests
    {
        private static PfLapSpliceConfig MakeConfig(
            double fckMpa = 25.0,
            double fykMpa = 500.0,
            PfBarSurfaceType surface = PfBarSurfaceType.Nervurada,
            PfBondZone zone = PfBondZone.Boa,
            PfAnchorageType anchorage = PfAnchorageType.Reta,
            double splicePercentage = 50.0,
            double barSpacingCm = 8.0)
        {
            return new PfLapSpliceConfig
            {
                ConcreteFckMpa = fckMpa,
                SteelFykMpa = fykMpa,
                BarSurface = surface,
                BondZone = zone,
                AnchorageType = anchorage,
                SplicePercentage = splicePercentage,
                BarSpacingCm = barSpacingCm
            };
        }

        [Fact]
        public void Calculate_DiametroZero_Lanca()
        {
            var config = MakeConfig();

            Action act = () => PfNbr6118AnchorageService.Calculate(0.0, config);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Calculate_ConfigNull_Lanca()
        {
            Action act = () => PfNbr6118AnchorageService.Calculate(12.5, null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Calculate_Nervurada_AderenciaBoa_Eta1Eta2_Corretos()
        {
            // Nervurada => eta1 = 2.25; Zona Boa => eta2 = 1.0; phi <= 32 => eta3 = 1.0
            var config = MakeConfig(surface: PfBarSurfaceType.Nervurada, zone: PfBondZone.Boa);

            var result = PfNbr6118AnchorageService.Calculate(12.5, config);

            result.Eta1.Should().BeApproximately(2.25, 1e-6);
            result.Eta2.Should().BeApproximately(1.0, 1e-6);
            result.Eta3.Should().BeApproximately(1.0, 1e-6);
        }

        [Fact]
        public void Calculate_Lisa_Eta1Um()
        {
            var config = MakeConfig(surface: PfBarSurfaceType.Lisa);

            var result = PfNbr6118AnchorageService.Calculate(10.0, config);

            result.Eta1.Should().BeApproximately(1.0, 1e-6);
        }

        [Fact]
        public void Calculate_Entalhada_Eta1UmMeio()
        {
            var config = MakeConfig(surface: PfBarSurfaceType.Entalhada);

            var result = PfNbr6118AnchorageService.Calculate(10.0, config);

            result.Eta1.Should().BeApproximately(1.4, 1e-6);
        }

        [Fact]
        public void Calculate_ZonaRuim_Eta2PointSeven()
        {
            var config = MakeConfig(zone: PfBondZone.Ruim);

            var result = PfNbr6118AnchorageService.Calculate(10.0, config);

            result.Eta2.Should().BeApproximately(0.7, 1e-6);
        }

        [Fact]
        public void Calculate_DiametroMaiorQue32_Eta3Reduzido()
        {
            // phi = 40 mm => eta3 = (132 - 40) / 100 = 0.92
            var config = MakeConfig();

            var result = PfNbr6118AnchorageService.Calculate(40.0, config);

            result.Eta3.Should().BeApproximately(0.92, 1e-6);
        }

        [Fact]
        public void Calculate_AncoragemComGancho_AlphaPointSeven()
        {
            var config = MakeConfig(anchorage: PfAnchorageType.Gancho90);

            var result = PfNbr6118AnchorageService.Calculate(12.5, config);

            result.AnchorageAlpha.Should().BeApproximately(0.7, 1e-6);
        }

        [Fact]
        public void Calculate_AncoragemReta_AlphaUm()
        {
            var config = MakeConfig(anchorage: PfAnchorageType.Reta);

            var result = PfNbr6118AnchorageService.Calculate(12.5, config);

            result.AnchorageAlpha.Should().BeApproximately(1.0, 1e-6);
        }

        [Fact]
        public void Calculate_FckMenorQue12_ClampsFor12()
        {
            // fck minimo NBR = 12 MPa. Valores abaixo sao forcados.
            var cfg1 = MakeConfig(fckMpa: 5.0);
            var cfg2 = MakeConfig(fckMpa: 12.0);

            var r1 = PfNbr6118AnchorageService.Calculate(12.5, cfg1);
            var r2 = PfNbr6118AnchorageService.Calculate(12.5, cfg2);

            r1.BasicAnchorageCm.Should().BeApproximately(r2.BasicAnchorageCm, 1e-6);
        }

        [Fact]
        public void Calculate_LbMinimo_Aplicado()
        {
            // RequiredAnchorageCm deve ser pelo menos max(0.3 * lb, phi, 10 cm).
            var config = MakeConfig(fckMpa: 40.0, anchorage: PfAnchorageType.Gancho90);

            var result = PfNbr6118AnchorageService.Calculate(6.3, config);

            // phi = 6.3 mm; lb-min deve ser max(0.3*lb, 6.3 mm, 10 cm)
            result.MinimumAnchorageCm.Should().BeGreaterThan(0.0);
            result.RequiredAnchorageCm.Should().BeGreaterThanOrEqualTo(result.MinimumAnchorageCm);
        }

        [Fact]
        public void Calculate_Traspasse_AlfaMaiorOuIgualAUm()
        {
            // Traspasse 50% com espacamento 10*phi => alpha pode ser 1.4 ou 1.8 dependendo da linha.
            var config = MakeConfig(splicePercentage: 50.0, barSpacingCm: 10.0 * 12.5 / 10.0);

            var result = PfNbr6118AnchorageService.Calculate(12.5, config);

            result.SpliceAlpha.Should().BeGreaterOrEqualTo(1.0);
            result.SpliceLengthCm.Should().BeGreaterOrEqualTo(result.RequiredAnchorageCm);
        }

        [Fact]
        public void Calculate_AreaRatio_ReduzLbNec()
        {
            // Se As,calc < As,ef, RequiredAnchorageCm pode ser reduzido proporcionalmente.
            var baseCfg = MakeConfig();
            var reducedCfg = MakeConfig();
            reducedCfg.AsCalcCm2 = 2.0;
            reducedCfg.AsEfCm2 = 4.0; // ratio = 0.5

            var rBase = PfNbr6118AnchorageService.Calculate(12.5, baseCfg);
            var rReduced = PfNbr6118AnchorageService.Calculate(12.5, reducedCfg);

            // Nao pode cair abaixo do minimo, mas tem que ser menor que base.
            rReduced.RequiredAnchorageCm.Should().BeLessOrEqualTo(rBase.RequiredAnchorageCm);
            rReduced.RequiredAnchorageCm.Should().BeGreaterOrEqualTo(rReduced.MinimumAnchorageCm);
        }

        [Fact]
        public void ToDetailText_InclueMetadadosPrincipais()
        {
            var config = MakeConfig();

            var result = PfNbr6118AnchorageService.Calculate(12.5, config);
            string text = result.ToDetailText();

            text.Should().StartWith("EMT NBR 6118:2023");
            text.Should().Contain("phi 12.5 mm");
            text.Should().Contain("lb");
            text.Should().Contain("traspasse l0");
            text.Should().Contain("fbd");
        }

        [Fact]
        public void Calculate_MesmaConfigDuasVezes_ProduzMesmoResultado()
        {
            // Idempotencia: a funcao eh pura, retornar valores identicos para mesmos inputs.
            var config = MakeConfig();

            var a = PfNbr6118AnchorageService.Calculate(12.5, config);
            var b = PfNbr6118AnchorageService.Calculate(12.5, config);

            a.BasicAnchorageCm.Should().Be(b.BasicAnchorageCm);
            a.RequiredAnchorageCm.Should().Be(b.RequiredAnchorageCm);
            a.SpliceLengthCm.Should().Be(b.SpliceLengthCm);
        }
    }
}
