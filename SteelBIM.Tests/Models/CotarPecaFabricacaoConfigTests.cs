#nullable enable
using FluentAssertions;
using SteelBIM.Models;
using Xunit;

namespace SteelBIM.Tests.Models
{
    public class CotarPecaFabricacaoConfigTests
    {
        [Fact]
        public void TemCotaSelecionada_PadraoEhVerdadeiro()
        {
            new CotarPecaFabricacaoConfig().TemCotaSelecionada().Should().BeTrue();
        }

        [Fact]
        public void TemCotaSelecionada_TodasFalsas_RetornaFalso()
        {
            var c = new CotarPecaFabricacaoConfig
            {
                CotarComprimento = false,
                CotarAlturaPerfil = false,
                CotarLarguraMesa = false,
                CotarFuros = false,
                CotarDistanciaBorda = false
            };
            c.TemCotaSelecionada().Should().BeFalse();
        }

        [Fact]
        public void TemCotaSelecionada_UmaUnicaSelecao_RetornaVerdadeiro()
        {
            var c = new CotarPecaFabricacaoConfig
            {
                CotarComprimento = false,
                CotarAlturaPerfil = false,
                CotarLarguraMesa = true, // unica marcada
                CotarFuros = false,
                CotarDistanciaBorda = false
            };
            c.TemCotaSelecionada().Should().BeTrue();
        }
    }
}
