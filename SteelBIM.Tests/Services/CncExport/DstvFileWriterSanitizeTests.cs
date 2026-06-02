using FluentAssertions;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    /// <summary>
    /// Cobre a transliteracao ASCII das marcas/notas de peca no NC1 (PT-BR -> ASCII
    /// legivel em vez de '?'), sem afetar a geometria de corte.
    /// </summary>
    public class DstvFileWriterSanitizeTests
    {
        [Theory]
        [InlineData("Galpão", "Galpao")]
        [InlineData("Treliça TR-01", "Trelica TR-01")]
        [InlineData("Pilar Ação", "Pilar Acao")]
        [InlineData("Içamento Nível", "Icamento Nivel")]
        public void SanitizeAscii_translitera_acentos_ptbr(string entrada, string esperado)
        {
            DstvFileWriter.SanitizeAscii(entrada).Should().Be(esperado);
        }

        [Fact]
        public void SanitizeAscii_eh_identidade_em_ascii_puro()
        {
            DstvFileWriter.SanitizeAscii("TR-01 W200x26.6").Should().Be("TR-01 W200x26.6");
        }

        [Fact]
        public void SanitizeAscii_vazio_ou_null()
        {
            DstvFileWriter.SanitizeAscii("").Should().Be("");
            DstvFileWriter.SanitizeAscii(null).Should().Be("");
        }

        [Fact]
        public void SanitizeAscii_simbolo_sem_mapeamento_vira_interrogacao()
        {
            // Caracteres nao-ASCII sem transliteracao definida caem para '?'.
            DstvFileWriter.SanitizeAscii("Peça º").Should().Be("Peca ?");
        }
    }
}
