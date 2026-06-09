#nullable enable
using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    public class ListaMateriaisRomaneioFormatterTests
    {
        [Fact]
        public void Sufixo_Completo_ComKgPorM()
        {
            // pt-BR: separador decimal virgula
            ListaMateriaisRomaneioFormatter.Sufixo(24, 360.0, 11.2)
                .Should().Be("24 un · 360,0 m · 11,2 kg/m");
        }

        [Fact]
        public void Sufixo_ComprimentoZero_OmiteKgPorM()
        {
            ListaMateriaisRomaneioFormatter.Sufixo(3, 0.0, 0.0)
                .Should().Be("3 un · 0,0 m");
        }

        [Fact]
        public void Sufixo_KgPorMZero_OmiteTrechoKgPorM()
        {
            ListaMateriaisRomaneioFormatter.Sufixo(5, 12.5, 0.0)
                .Should().Be("5 un · 12,5 m");
        }
    }
}
