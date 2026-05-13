using FluentAssertions;
using Xunit;
using SteelBIM.Models.PF;

namespace SteelBIM.Tests.Models.PF
{
    /// <summary>
    /// Cobertura do DTO da F4 Acos de Estaca (Onda 3.5).
    /// Onda 6 (Victor Final): valida defaults, round-trip e composicao com PfColumnStirrupsConfig + PfLapSpliceConfig.
    /// </summary>
    public class PfEstacaRebarConfigTests
    {
        [Fact]
        public void Defaults_ProducesExpectedValues()
        {
            var config = new PfEstacaRebarConfig();
            config.BarTypeName.Should().Be(string.Empty);
            config.CobrimentoCm.Should().Be(5.0);
            config.QuantidadeBarras.Should().Be(6);
            config.InserirEstribos.Should().BeFalse();
            config.Estribos.Should().NotBeNull();
            config.Traspasse.Should().NotBeNull();
        }

        [Fact]
        public void BarTypeName_RoundTrip()
        {
            var config = new PfEstacaRebarConfig { BarTypeName = "8.0 mm" };
            config.BarTypeName.Should().Be("8.0 mm");
        }

        [Fact]
        public void CobrimentoCm_RoundTrip()
        {
            var config = new PfEstacaRebarConfig { CobrimentoCm = 3.5 };
            config.CobrimentoCm.Should().Be(3.5);
        }

        [Fact]
        public void QuantidadeBarras_RoundTrip()
        {
            var config = new PfEstacaRebarConfig { QuantidadeBarras = 12 };
            config.QuantidadeBarras.Should().Be(12);
        }

        [Fact]
        public void InserirEstribos_RoundTrip()
        {
            var config = new PfEstacaRebarConfig { InserirEstribos = true };
            config.InserirEstribos.Should().BeTrue();
        }

        [Fact]
        public void Estribos_ReadOnly_MasMutavel()
        {
            // Propriedade get-only mas a instancia interna eh mutavel (padrao composition).
            var config = new PfEstacaRebarConfig();
            config.Estribos.DiametroMm = 8.0;
            config.Estribos.DiametroMm.Should().Be(8.0);
        }

        [Fact]
        public void Traspasse_ReadOnly_MasMutavel()
        {
            var config = new PfEstacaRebarConfig();
            config.Traspasse.Enabled = true;
            config.Traspasse.MaxBarLengthCm = 1500.0;
            config.Traspasse.Enabled.Should().BeTrue();
            config.Traspasse.MaxBarLengthCm.Should().Be(1500.0);
        }
    }
}
