using FluentAssertions;
using Xunit;
using SteelBIM.Models;

namespace SteelBIM.Tests.Models
{
    /// <summary>
    /// Cobertura do DTO de configuracao para o CortarElementosWindow.
    /// Onda 6 (Victor Final): valida defaults, set/get e cobertura do enum CortarElementosEscopo.
    /// </summary>
    public class CortarElementosConfigTests
    {
        [Fact]
        public void Defaults_ProducesExpectedValues()
        {
            var config = new CortarElementosConfig();

            config.Escopo.Should().Be(CortarElementosEscopo.VistaAtiva);
            config.IncluirPisos.Should().BeTrue();
            config.IncluirQuadroEstrutural.Should().BeTrue();
            config.IncluirParedes.Should().BeFalse();
            config.IncluirPilaresEstruturais.Should().BeTrue();
            config.IncluirColunas.Should().BeTrue();
        }

        [Theory]
        [InlineData(CortarElementosEscopo.Selecao)]
        [InlineData(CortarElementosEscopo.VistaAtiva)]
        [InlineData(CortarElementosEscopo.Modelo)]
        public void Escopo_RoundTrip(CortarElementosEscopo escopo)
        {
            var config = new CortarElementosConfig { Escopo = escopo };
            config.Escopo.Should().Be(escopo);
        }

        [Fact]
        public void IncluirPisos_RoundTrip()
        {
            var config = new CortarElementosConfig { IncluirPisos = false };
            config.IncluirPisos.Should().BeFalse();
        }

        [Fact]
        public void IncluirQuadroEstrutural_RoundTrip()
        {
            var config = new CortarElementosConfig { IncluirQuadroEstrutural = false };
            config.IncluirQuadroEstrutural.Should().BeFalse();
        }

        [Fact]
        public void IncluirParedes_RoundTrip()
        {
            var config = new CortarElementosConfig { IncluirParedes = true };
            config.IncluirParedes.Should().BeTrue();
        }

        [Fact]
        public void IncluirPilaresEstruturais_RoundTrip()
        {
            var config = new CortarElementosConfig { IncluirPilaresEstruturais = false };
            config.IncluirPilaresEstruturais.Should().BeFalse();
        }

        [Fact]
        public void IncluirColunas_RoundTrip()
        {
            var config = new CortarElementosConfig { IncluirColunas = false };
            config.IncluirColunas.Should().BeFalse();
        }

        [Fact]
        public void EscopoEnum_HasExpectedNumericValues()
        {
            // O numero importa porque os valores podem ser serializados em settings persistidos
            // (AppSettings.LastSelectedCortarEscopo, se existir). Mudar quebra round-trip.
            ((int)CortarElementosEscopo.Selecao).Should().Be(0);
            ((int)CortarElementosEscopo.VistaAtiva).Should().Be(1);
            ((int)CortarElementosEscopo.Modelo).Should().Be(2);
        }
    }
}
