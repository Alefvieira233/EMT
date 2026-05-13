using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class CotarTrelicaConfigTests
    {
        [Fact]
        public void Constructor_Default_AllFlagsTrue()
        {
            var config = new CotarTrelicaConfig();

            config.CotarPaineisBanzoSuperior.Should().BeTrue();
            config.CotarVaosEntreApoios.Should().BeTrue();
            config.CotarPaineisBanzoInferior.Should().BeTrue();
            config.CotarVaoTotal.Should().BeTrue();
            config.CotarAlturaMontantes.Should().BeTrue();
            config.IdentificarPerfis.Should().BeTrue();
            config.CantoneiraDupla.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_OffsetIs500()
        {
            var config = new CotarTrelicaConfig();

            config.OffsetFaixaMm.Should().Be(500.0);
        }

        [Fact]
        public void Properties_CanBeModified()
        {
            var config = new CotarTrelicaConfig();

            config.CotarPaineisBanzoSuperior = false;
            config.OffsetFaixaMm = 300.0;

            config.CotarPaineisBanzoSuperior.Should().BeFalse();
            config.OffsetFaixaMm.Should().Be(300.0);
        }
    }
}
