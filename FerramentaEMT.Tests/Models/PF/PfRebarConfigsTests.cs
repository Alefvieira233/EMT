#nullable enable
using FerramentaEMT.Models.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Models.PF
{
    public class PfRebarConfigsTests
    {
        [Fact]
        public void PfColumnStirrupsConfig_Defaults()
        {
            PfColumnStirrupsConfig c = new PfColumnStirrupsConfig();
            c.BarTypeName.Should().Be(string.Empty);
            c.CobrimentoCm.Should().Be(3.0);
            c.EspacamentoInferiorCm.Should().Be(12.0);
            c.EspacamentoCentralCm.Should().Be(20.0);
            c.EspacamentoSuperiorCm.Should().Be(12.0);
            c.AlturaZonaExtremidadeCm.Should().Be(60.0);
        }

        [Fact]
        public void PfColumnBarsConfig_Defaults()
        {
            PfColumnBarsConfig c = new PfColumnBarsConfig();
            c.BarTypeName.Should().Be(string.Empty);
            c.CobrimentoCm.Should().Be(3.0);
            c.QuantidadeLargura.Should().Be(2);
            c.QuantidadeProfundidade.Should().Be(2);
        }

        [Fact]
        public void PfBeamStirrupsConfig_Defaults()
        {
            PfBeamStirrupsConfig c = new PfBeamStirrupsConfig();
            c.CobrimentoCm.Should().Be(3.0);
            c.EspacamentoApoioCm.Should().Be(12.0);
            c.EspacamentoCentralCm.Should().Be(20.0);
            c.ComprimentoZonaApoioCm.Should().Be(60.0);
        }

        [Fact]
        public void PfBeamBarsConfig_Defaults()
        {
            PfBeamBarsConfig c = new PfBeamBarsConfig();
            c.CobrimentoCm.Should().Be(3.0);
            c.QuantidadeSuperior.Should().Be(2);
            c.QuantidadeInferior.Should().Be(2);
            c.QuantidadeLateral.Should().Be(0);
            c.ComprimentoGanchoCm.Should().Be(10.0);
            c.ModoPonta.Should().Be(PfBeamBarEndMode.DobraInterna);
        }

        [Fact]
        public void PfConsoloRebarConfig_Defaults()
        {
            PfConsoloRebarConfig c = new PfConsoloRebarConfig();
            c.NumeroTirantes.Should().Be(4);
            c.ComprimentoTiranteCm.Should().Be(100.0);
            c.NumeroSuspensoes.Should().Be(4);
            c.ComprimentoSuspensaoCm.Should().Be(60.0);
            c.QuantidadeEstribosVerticais.Should().Be(5);
            c.QuantidadeEstribosHorizontais.Should().Be(5);
        }

        [Fact]
        public void PfBeamBarEndMode_EnumValues()
        {
            ((int)PfBeamBarEndMode.Reta).Should().Be(0);
            ((int)PfBeamBarEndMode.DobraInterna).Should().Be(1);
        }

        [Fact]
        public void PfColumnStirrupsConfig_Mutavel()
        {
            PfColumnStirrupsConfig c = new PfColumnStirrupsConfig
            {
                BarTypeName = "10mm CA-50",
                CobrimentoCm = 2.5,
                EspacamentoInferiorCm = 10,
                EspacamentoCentralCm = 15,
                EspacamentoSuperiorCm = 10,
                AlturaZonaExtremidadeCm = 80
            };
            c.BarTypeName.Should().Be("10mm CA-50");
            c.CobrimentoCm.Should().Be(2.5);
            c.EspacamentoInferiorCm.Should().Be(10);
            c.AlturaZonaExtremidadeCm.Should().Be(80);
        }

        [Fact]
        public void PfBeamBarsConfig_ModoPonta_PodeSerReta()
        {
            PfBeamBarsConfig c = new PfBeamBarsConfig { ModoPonta = PfBeamBarEndMode.Reta };
            c.ModoPonta.Should().Be(PfBeamBarEndMode.Reta);
        }
    }
}
