using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class GerarVistaPecaConfigTests
    {
        [Fact]
        public void Constructor_Default_EscopoIsSelecaoManual()
        {
            var config = new GerarVistaPecaConfig();

            config.Escopo.Should().Be(EscopoSelecaoPeca.SelecaoManual);
        }

        [Fact]
        public void Constructor_Default_VistasEnabled()
        {
            var config = new GerarVistaPecaConfig();

            config.CriarVistaLongitudinal.Should().BeTrue();
            config.CriarCorteTransversal.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_ScaleIs20()
        {
            var config = new GerarVistaPecaConfig();

            config.EscalaVista.Should().Be(20);
        }

        [Fact]
        public void Constructor_Default_MargemIs150()
        {
            var config = new GerarVistaPecaConfig();

            config.MargemMm.Should().Be(150);
        }

        [Fact]
        public void Constructor_Default_CriarFolhaIsFalse()
        {
            var config = new GerarVistaPecaConfig();

            config.CriarFolha.Should().BeFalse();
        }

        [Fact]
        public void TemVistasSelecionadas_BothTrue_ReturnsTrue()
        {
            var config = new GerarVistaPecaConfig();

            config.TemVistasSelecionadas().Should().BeTrue();
        }

        [Fact]
        public void TemVistasSelecionadas_BothFalse_ReturnsFalse()
        {
            var config = new GerarVistaPecaConfig
            {
                CriarVistaLongitudinal = false,
                CriarCorteTransversal = false
            };

            config.TemVistasSelecionadas().Should().BeFalse();
        }

        [Fact]
        public void Constructor_Default_FiltroCategoriaIsTodos()
        {
            var config = new GerarVistaPecaConfig();

            config.FiltroCategoria.Should().Be(VistaPecaCategoriaFiltro.Todos);
        }
    }
}
