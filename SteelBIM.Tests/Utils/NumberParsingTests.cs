using System.Globalization;
using System.Threading;
using FerramentaEMT.Utils;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Utils
{
    public class NumberParsingTests
    {
        [Theory]
        [InlineData("1.5", 1.5)]
        [InlineData("0", 0)]
        [InlineData("1000.25", 1000.25)]
        [InlineData("  3.14  ", 3.14)]
        [InlineData("-2.5", -2.5)]
        [InlineData("1e3", 1000)]
        public void TryParseDouble_accepts_invariant_culture(string input, double expected)
        {
            NumberParsing.TryParseDouble(input, out double value).Should().BeTrue();
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("1,5", 1.5)]
        [InlineData("0,0", 0)]
        [InlineData("-2,5", -2.5)]
        [InlineData("  3,14  ", 3.14)]
        public void TryParseDouble_accepts_ptBR_culture_when_invariant_fails(string input, double expected)
        {
            NumberParsing.TryParseDouble(input, out double value).Should().BeTrue();
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("3.5m")]
        public void TryParseDouble_returns_false_for_invalid_input(string input)
        {
            NumberParsing.TryParseDouble(input, out double value).Should().BeFalse();
            value.Should().Be(0);
        }

        [Fact]
        public void ParseDoubleOrDefault_returns_value_when_valid()
        {
            NumberParsing.ParseDoubleOrDefault("3.14", 99).Should().Be(3.14);
            NumberParsing.ParseDoubleOrDefault("2,71", 99).Should().Be(2.71);
        }

        [Theory]
        [InlineData(null, 42.0)]
        [InlineData("", 42.0)]
        [InlineData("abc", 42.0)]
        public void ParseDoubleOrDefault_returns_fallback_for_invalid_input(string input, double fallback)
        {
            NumberParsing.ParseDoubleOrDefault(input, fallback).Should().Be(fallback);
        }

        [Fact]
        public void Culture_does_not_affect_parsing()
        {
            // Forcar culture pt-BR no thread atual e confirmar que "1.5" (invariant)
            // continua sendo aceito — protege contra dev/prod em locales diferentes.
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
                NumberParsing.TryParseDouble("1.5", out double v).Should().BeTrue();
                v.Should().Be(1.5);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Culture_does_not_affect_parsing_de_DE()
        {
            // de-DE usa ponto como separador de milhar e virgula como decimal.
            // Quer prova definitiva que nao caimos em CurrentCulture.
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                // "1,5" deve ser lido como 1.5 (via fallback pt-BR) nao como erro
                NumberParsing.TryParseDouble("1,5", out double v).Should().BeTrue();
                v.Should().Be(1.5);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
