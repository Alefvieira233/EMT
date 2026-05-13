using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class MarcarPecasConfigTests
    {
        [Fact]
        public void Constructor_Default_EscopoIsVistaAtiva()
        {
            var config = new MarcarPecasConfig();

            config.Escopo.Should().Be(EscopoMarcacao.VistaAtiva);
        }

        [Fact]
        public void Constructor_Default_AllCategoriasTrue()
        {
            var config = new MarcarPecasConfig();

            config.MarcarVigas.Should().BeTrue();
            config.MarcarPilares.Should().BeTrue();
            config.MarcarContraventamentos.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_PrefixosCorretos()
        {
            var config = new MarcarPecasConfig();

            config.PrefixoVigas.Should().Be("V");
            config.PrefixoPilares.Should().Be("P");
            config.PrefixoContraventamentos.Should().Be("C");
        }

        [Fact]
        public void Constructor_Default_NumeroInicialIs1()
        {
            var config = new MarcarPecasConfig();

            config.NumeroInicial.Should().Be(1);
            config.Digitos.Should().Be(3);
        }

        [Fact]
        public void TemCategoriaSelecionada_AllTrue_ReturnsTrue()
        {
            var config = new MarcarPecasConfig();

            config.TemCategoriaSelecionada().Should().BeTrue();
        }

        [Fact]
        public void TemCategoriaSelecionada_AllFalse_ReturnsFalse()
        {
            var config = new MarcarPecasConfig
            {
                MarcarVigas = false,
                MarcarPilares = false,
                MarcarContraventamentos = false
            };

            config.TemCategoriaSelecionada().Should().BeFalse();
        }

        [Theory]
        [InlineData("Viga", "V")]
        [InlineData("Pilar", "P")]
        [InlineData("Contraventamento", "C")]
        [InlineData("Desconhecido", "X")]
        public void ObterPrefixo_ReturnsCorrectPrefix(string categoria, string expected)
        {
            var config = new MarcarPecasConfig();

            config.ObterPrefixo(categoria).Should().Be(expected);
        }

        [Theory]
        [InlineData(1, 3, "001")]
        [InlineData(42, 3, "042")]
        [InlineData(1, 5, "00001")]
        [InlineData(999, 3, "999")]
        public void FormatarNumero_FormatsCorrectly(int numero, int digitos, string expected)
        {
            var config = new MarcarPecasConfig { Digitos = digitos };

            config.FormatarNumero(numero).Should().Be(expected);
        }

        [Fact]
        public void Constructor_Default_DestinoIsParametroMark()
        {
            var config = new MarcarPecasConfig();

            config.Destino.Should().Be(DestinoMarca.ParametroMark);
        }

        [Fact]
        public void Constructor_Default_SobrescreverExistentesIsFalse()
        {
            var config = new MarcarPecasConfig();

            config.SobrescreverExistentes.Should().BeFalse();
        }

        [Fact]
        public void Constructor_Default_DestaqueVisualIsTrue()
        {
            var config = new MarcarPecasConfig();

            config.DestaqueVisual.Should().BeTrue();
        }
    }
}
