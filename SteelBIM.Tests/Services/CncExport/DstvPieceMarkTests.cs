using FluentAssertions;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    /// <summary>
    /// v2.8.11 (A6): testes da escolha pura da marca DSTV (precedencia config -> mark -> ID).
    /// </summary>
    public class DstvPieceMarkTests
    {
        [Fact]
        public void ParametroConfig_NaoVazio_TemPrecedencia()
        {
            DstvPieceMark.Escolher("V-12", "M-99", 555).Should().Be("V-12");
        }

        [Fact]
        public void SemConfig_UsaMark()
        {
            DstvPieceMark.Escolher(null, "M-99", 555).Should().Be("M-99");
            DstvPieceMark.Escolher("   ", "M-99", 555).Should().Be("M-99");
        }

        [Fact]
        public void SemConfigNemMark_UsaFallbackId()
        {
            DstvPieceMark.Escolher(null, null, 555).Should().Be("ID-555");
            DstvPieceMark.Escolher("", "  ", 42).Should().Be("ID-42");
        }
    }
}
