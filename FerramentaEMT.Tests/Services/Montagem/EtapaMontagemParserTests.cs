#nullable enable
using FerramentaEMT.Services.Montagem;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Montagem
{
    public class EtapaMontagemParserTests
    {
        [Theory]
        [InlineData("Etapa:5", 5)]
        [InlineData("Etapa:1", 1)]
        [InlineData("Etapa:42", 42)]
        [InlineData("Etapa:9999", 9999)]
        public void Parse_ValidExact_ReturnsNumber(string input, int expected)
        {
            EtapaMontagemParser.Parse(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("etapa:7", 7)]
        [InlineData("ETAPA:3", 3)]
        [InlineData("EtApA:12", 12)]
        public void Parse_CaseInsensitive(string input, int expected)
        {
            EtapaMontagemParser.Parse(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("prefixo texto Etapa:5", 5)]
        [InlineData("Etapa:5; comentario extra", 5)]
        [InlineData("   Etapa:8   ", 8)]
        [InlineData("Etapa:5_etc", 5)]
        public void Parse_WithSurroundingText(string input, int expected)
        {
            EtapaMontagemParser.Parse(input).Should().Be(expected);
        }

        [Fact]
        public void Parse_MultipleOccurrences_FirstWins()
        {
            EtapaMontagemParser.Parse("Etapa:3 e depois Etapa:7").Should().Be(3);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("sem etapa aqui")]
        [InlineData("Etapa:abc")]
        [InlineData("Etapa:")]
        [InlineData("Etapa: 5")] // espaco antes do digito quebra
        [InlineData("Etapa-5")]
        public void Parse_InvalidOrMissing_ReturnsZero(string? input)
        {
            EtapaMontagemParser.Parse(input).Should().Be(0);
        }

        [Theory]
        [InlineData("Etapa:0", 0)]
        [InlineData("Etapa:-5", 0)] // sinal nao e digito, para no '-'
        public void Parse_NonPositive_ReturnsZero(string input, int expected)
        {
            EtapaMontagemParser.Parse(input).Should().Be(expected);
        }

        [Fact]
        public void TryParse_ValidReturnsTrue()
        {
            bool ok = EtapaMontagemParser.TryParse("Etapa:5", out int etapa);
            ok.Should().BeTrue();
            etapa.Should().Be(5);
        }

        [Fact]
        public void TryParse_InvalidReturnsFalseAndZero()
        {
            bool ok = EtapaMontagemParser.TryParse("xxx", out int etapa);
            ok.Should().BeFalse();
            etapa.Should().Be(0);
        }
    }
}
