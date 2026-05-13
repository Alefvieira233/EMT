using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class TagearTrelicaConfigTests
    {
        [Fact]
        public void Constructor_Default_AllFlagsTrue()
        {
            var config = new TagearTrelicaConfig();

            config.TagearBanzoSuperior.Should().BeTrue();
            config.TagearBanzoInferior.Should().BeTrue();
            config.TagearMontantes.Should().BeTrue();
            config.TagearDiagonais.Should().BeTrue();
            config.CantoneiraDupla.Should().BeTrue();
            config.CriarRotuloBanzos.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_OffsetIs300()
        {
            var config = new TagearTrelicaConfig();

            config.OffsetTagMm.Should().Be(300.0);
        }

        [Fact]
        public void TemTipoSelecionado_AllTrue_ReturnsTrue()
        {
            var config = new TagearTrelicaConfig();

            config.TemTipoSelecionado().Should().BeTrue();
        }

        [Fact]
        public void TemTipoSelecionado_AllFalse_ReturnsFalse()
        {
            var config = new TagearTrelicaConfig
            {
                TagearBanzoSuperior = false,
                TagearBanzoInferior = false,
                TagearMontantes = false,
                TagearDiagonais = false
            };

            config.TemTipoSelecionado().Should().BeFalse();
        }

        [Fact]
        public void TemTipoSelecionado_OnlyOneTrue_ReturnsTrue()
        {
            var config = new TagearTrelicaConfig
            {
                TagearBanzoSuperior = false,
                TagearBanzoInferior = false,
                TagearMontantes = true,
                TagearDiagonais = false
            };

            config.TemTipoSelecionado().Should().BeTrue();
        }
    }
}
