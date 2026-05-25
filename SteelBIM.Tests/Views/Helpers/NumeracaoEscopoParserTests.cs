using FluentAssertions;
using SteelBIM.Models;
using SteelBIM.Views.Helpers;
using Xunit;

namespace SteelBIM.Tests.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3): testes do NumeracaoEscopoParser extraido
    /// do code-behind do NumeracaoItensWindow. Fallback eh sempre
    /// VistaAtiva (escopo mais conservador).
    /// </summary>
    public class NumeracaoEscopoParserTests
    {
        [Theory]
        [InlineData("VistaAtiva", NumeracaoEscopo.VistaAtiva)]
        [InlineData("vistaativa", NumeracaoEscopo.VistaAtiva)]
        [InlineData("VISTAATIVA", NumeracaoEscopo.VistaAtiva)]
        public void Parse_valor_valido_caseinsensitive_retorna_enum(string input, NumeracaoEscopo expected)
        {
            NumeracaoEscopoParser.Parse(input).Should().Be(expected);
        }

        [Fact]
        public void Parse_null_retorna_VistaAtiva()
        {
            NumeracaoEscopoParser.Parse(null).Should().Be(NumeracaoEscopo.VistaAtiva);
        }

        [Fact]
        public void Parse_vazio_retorna_VistaAtiva()
        {
            NumeracaoEscopoParser.Parse(string.Empty).Should().Be(NumeracaoEscopo.VistaAtiva);
        }

        [Fact]
        public void Parse_valor_nao_existente_retorna_VistaAtiva()
        {
            NumeracaoEscopoParser.Parse("FooBar").Should().Be(NumeracaoEscopo.VistaAtiva);
        }

        [Fact]
        public void Parse_valor_lixo_retorna_VistaAtiva()
        {
            // Garante que input acidental nao seleciona escopo amplo por engano
            NumeracaoEscopoParser.Parse("!@#$%").Should().Be(NumeracaoEscopo.VistaAtiva);
        }
    }
}
