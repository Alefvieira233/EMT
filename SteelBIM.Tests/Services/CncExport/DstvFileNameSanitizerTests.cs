#nullable enable
using FerramentaEMT.Services.CncExport;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.CncExport
{
    public class DstvFileNameSanitizerTests
    {
        [Theory]
        [InlineData("W310x38", "W310x38")]
        [InlineData("PECA-01", "PECA-01")]
        [InlineData("perfil_123", "perfil_123")]
        public void Sanitize_ValidName_Unchanged(string input, string expected)
        {
            DstvFileNameSanitizer.Sanitize(input).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sanitize_EmptyOrNull_ReturnsDefault(string? input)
        {
            DstvFileNameSanitizer.Sanitize(input).Should().Be("peca");
        }

        [Theory]
        [InlineData("a/b", "a_b")]
        [InlineData("a\\b", "a_b")]
        [InlineData("a:b", "a_b")]
        [InlineData("a*b?c", "a_b_c")]
        [InlineData("a<b>c", "a_b_c")]
        [InlineData("a|b\"c", "a_b_c")]
        public void Sanitize_InvalidChars_ReplacedWithUnderscore(string input, string expected)
        {
            DstvFileNameSanitizer.Sanitize(input).Should().Be(expected);
        }

        [Fact]
        public void Sanitize_TrimsWhitespace()
        {
            DstvFileNameSanitizer.Sanitize("  nome  ").Should().Be("nome");
        }

        [Fact]
        public void Sanitize_OnlyInvalidChars_ReturnsWithUnderscores()
        {
            // "///" vira "___" (sem trim que remova); ainda valido
            DstvFileNameSanitizer.Sanitize("///").Should().Be("___");
        }

        [Fact]
        public void Sanitize_PreservesUnicode()
        {
            DstvFileNameSanitizer.Sanitize("peça-ção").Should().Be("peça-ção");
        }

        [Fact]
        public void Sanitize_MixedWithWhitespaceAndInvalids()
        {
            DstvFileNameSanitizer.Sanitize("  W/310:38  ").Should().Be("W_310_38");
        }
    }
}
