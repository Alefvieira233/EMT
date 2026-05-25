using FluentAssertions;
using SteelBIM.Models.DiagramaMontagem;
using SteelBIM.Services.DiagramaMontagem;
using Xunit;

namespace SteelBIM.Tests.Services.DiagramaMontagem
{
    /// <summary>
    /// v2.8.0 F12 (Wave 3): testes do view namer extraido do
    /// DiagramaMontagemService. Antes inline na orquestracao;
    /// agora pura + testavel.
    /// </summary>
    public class DiagramaMontagemViewNamerTests
    {
        [Fact]
        public void Superior_ganha_sufixo_Planta()
        {
            DiagramaMontagemViewNamer.BuildContextualName("Diagrama de Montagem", OrientacaoDiagrama.Superior)
                .Should().Be("Diagrama de Montagem (Planta)");
        }

        [Theory]
        [InlineData(OrientacaoDiagrama.Auto)]
        [InlineData(OrientacaoDiagrama.ParaleloEixoX)]
        [InlineData(OrientacaoDiagrama.ParaleloEixoY)]
        public void Outras_orientacoes_nao_ganham_sufixo(OrientacaoDiagrama orientacao)
        {
            DiagramaMontagemViewNamer.BuildContextualName("Diagrama de Montagem", orientacao)
                .Should().Be("Diagrama de Montagem");
        }

        [Fact]
        public void Null_tratado_como_string_vazia()
        {
            DiagramaMontagemViewNamer.BuildContextualName(null, OrientacaoDiagrama.Auto)
                .Should().Be(string.Empty);
        }

        [Fact]
        public void Null_com_Superior_da_so_sufixo()
        {
            DiagramaMontagemViewNamer.BuildContextualName(null, OrientacaoDiagrama.Superior)
                .Should().Be(" (Planta)");
        }

        [Fact]
        public void String_vazia_com_Superior_da_so_sufixo()
        {
            DiagramaMontagemViewNamer.BuildContextualName(string.Empty, OrientacaoDiagrama.Superior)
                .Should().Be(" (Planta)");
        }

        [Fact]
        public void Nome_com_caracteres_especiais_preservado()
        {
            DiagramaMontagemViewNamer.BuildContextualName("Pórtico Eixo A-1 (cobertura)", OrientacaoDiagrama.Superior)
                .Should().Be("Pórtico Eixo A-1 (cobertura) (Planta)");
        }
    }
}
