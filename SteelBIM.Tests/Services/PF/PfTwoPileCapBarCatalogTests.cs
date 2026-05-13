using System.Linq;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.PF
{
    /// <summary>
    /// Testes do catalogo estatico de 14 barras de bloco de 2 estacas
    /// (Tipo4). Garantem estabilidade dos dados — o catalogo eh a fonte de
    /// verdade pro comando CmdPfInserirAcosBlocoDuasEstacas e qualquer mudanca
    /// nele muda o lancamento de armadura na maquina do usuario.
    /// </summary>
    public class PfTwoPileCapBarCatalogTests
    {
        [Fact]
        public void Tipo4_PossuiQuatorzePosicoes()
        {
            PfTwoPileCapBarCatalog.Tipo4.Count.Should().Be(14);
        }

        [Fact]
        public void Tipo4_PosicoesSaoSequenciaisDeUmACatorze()
        {
            var posicoes = PfTwoPileCapBarCatalog.Tipo4.Select(p => p.Posicao).ToList();

            posicoes.Should().BeEquivalentTo(Enumerable.Range(1, 14));
        }

        [Fact]
        public void Get_PosicaoExistente_RetornaBarra()
        {
            var bar = PfTwoPileCapBarCatalog.Get(4);

            bar.Should().NotBeNull();
            bar.Posicao.Should().Be(4);
            bar.DiametroMm.Should().Be(16.0);
            bar.ComprimentoCm.Should().Be(407.0);
        }

        [Fact]
        public void Get_PosicaoInexistente_RetornaNull()
        {
            PfTwoPileCapBarCatalog.Get(99).Should().BeNull();
            PfTwoPileCapBarCatalog.Get(0).Should().BeNull();
            PfTwoPileCapBarCatalog.Get(-1).Should().BeNull();
        }

        [Fact]
        public void Tipo4_QuantidadePorBloco_EhUmTercoDoPdf()
        {
            // A regra e: QuantidadePorBloco = QuantidadeTotalPdf / 3 (3 blocos por planta).
            foreach (var bar in PfTwoPileCapBarCatalog.Tipo4)
            {
                bar.QuantidadePorBloco.Should().Be(bar.QuantidadeTotalPdf / 3);
            }
        }

        [Fact]
        public void Tipo4_TodasBarrasTemDescricao()
        {
            foreach (var bar in PfTwoPileCapBarCatalog.Tipo4)
            {
                bar.DescricaoForma.Should().NotBeNullOrWhiteSpace();
            }
        }

        [Fact]
        public void Tipo4_TodasBarrasTemDiametroValido()
        {
            foreach (var bar in PfTwoPileCapBarCatalog.Tipo4)
            {
                bar.DiametroMm.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void Tipo4_TodasBarrasTemComprimentoValido()
        {
            foreach (var bar in PfTwoPileCapBarCatalog.Tipo4)
            {
                bar.ComprimentoCm.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void Tipo4_PosicoesChaveMantemDiametroEsperado()
        {
            // Snapshot das posicoes mais criticas do catalogo. Se alguma mudar,
            // a barra precisa ser atualizada tambem no servico de lancamento.
            PfTwoPileCapBarCatalog.Get(1).DiametroMm.Should().Be(12.5); // longitudinal superior
            PfTwoPileCapBarCatalog.Get(4).DiametroMm.Should().Be(16.0); // principal inferior
            PfTwoPileCapBarCatalog.Get(13).DiametroMm.Should().Be(10.0); // calice vertical
        }

        [Fact]
        public void Tipo4_FormasCobremVariantesEspecificas()
        {
            var formas = PfTwoPileCapBarCatalog.Tipo4.Select(p => p.Forma).Distinct().ToList();

            formas.Should().Contain(PfTwoPileCapBarShape.FormaEspecial);
            formas.Should().Contain(PfTwoPileCapBarShape.U);
            formas.Should().Contain(PfTwoPileCapBarShape.EstriboVertical);
            formas.Should().Contain(PfTwoPileCapBarShape.RetanguloFechado);
            formas.Should().Contain(PfTwoPileCapBarShape.CaliceVertical);
        }
    }
}
