#nullable enable
using FerramentaEMT.Services.Trelica;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class TrelicaPerfilFormatterTests
    {
        [Theory]
        [InlineData("L 76x76x6.3", 1, "L 76x76x6.3")]
        [InlineData("L 76x76x6.3", 2, "2x L 76x76x6.3")]
        [InlineData("W 200 x 26.6", 1, "W 200 x 26.6")]
        [InlineData("U 150 x 65 x 4.76", 1, "U 150 x 65 x 4.76")]
        [InlineData("Cantoneira L 76x76x6.3", 1, "Cantoneira L 76x76x6.3")]
        [InlineData("Cantoneira L 76x76x6.3", 2, "2x Cantoneira L 76x76x6.3")]
        public void Formatar_CombinacoesBasicas(string nome, int mult, string esperado)
        {
            TrelicaPerfilFormatter.Formatar(nome, mult).Should().Be(esperado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Formatar_VazioOuNull_Placeholder(string? nome)
        {
            TrelicaPerfilFormatter.Formatar(nome).Should().Be(TrelicaPerfilFormatter.Placeholder);
        }

        [Fact]
        public void Formatar_MultiplicadorZero_TratadoComoSimples()
        {
            TrelicaPerfilFormatter.Formatar("W 200 x 26.6", 0).Should().Be("W 200 x 26.6");
        }

        [Theory]
        [InlineData("L 76x76x6.3", true)]
        [InlineData("L-75x75x8", true)]
        [InlineData("L75x75x8", true)]
        [InlineData("W 200", false)]
        [InlineData("U 150", false)]
        [InlineData("", false)]
        public void EhCantoneira(string nome, bool esperado)
        {
            TrelicaPerfilFormatter.EhCantoneira(nome).Should().Be(esperado);
        }
    }
}
