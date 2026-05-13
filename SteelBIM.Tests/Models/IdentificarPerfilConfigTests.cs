using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class IdentificarPerfilConfigTests
    {
        [Fact]
        public void Constructor_Default_AllCategoriasTrue()
        {
            var config = new IdentificarPerfilConfig();

            config.IncluirVigas.Should().BeTrue();
            config.IncluirPilares.Should().BeTrue();
            config.IncluirContraventos.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_SubstituirTagsIsFalse()
        {
            var config = new IdentificarPerfilConfig();

            config.SubstituirTagsExistentes.Should().BeFalse();
        }

        [Fact]
        public void Constructor_Default_CantoneiraDuplaIsTrue()
        {
            var config = new IdentificarPerfilConfig();

            config.CantoneiraDupla.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_OffsetIs300()
        {
            var config = new IdentificarPerfilConfig();

            config.OffsetTagMm.Should().Be(300.0);
        }

        [Fact]
        public void TemCategoriaSelecionada_AllTrue_ReturnsTrue()
        {
            var config = new IdentificarPerfilConfig();

            config.TemCategoriaSelecionada().Should().BeTrue();
        }

        [Fact]
        public void TemCategoriaSelecionada_AllFalse_ReturnsFalse()
        {
            var config = new IdentificarPerfilConfig
            {
                IncluirVigas = false,
                IncluirPilares = false,
                IncluirContraventos = false
            };

            config.TemCategoriaSelecionada().Should().BeFalse();
        }
    }
}
