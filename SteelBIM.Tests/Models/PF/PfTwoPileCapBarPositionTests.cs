using System.Globalization;
using System.Threading;
using FerramentaEMT.Models.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Models.PF
{
    /// <summary>
    /// Testes de <see cref="PfTwoPileCapBarPosition.ToComment"/> — o formato gerado
    /// vai parar no parametro Comments do Revit e eh a marca nas pranchas. Qualquer
    /// mudanca neste formato precisa ser coordenada com quem consome o Comment em
    /// schedules/marcacao do modelo.
    /// </summary>
    public class PfTwoPileCapBarPositionTests
    {
        [Fact]
        public void Nome_ConcatenaComPosicao()
        {
            var bar = new PfTwoPileCapBarPosition { Posicao = 7 };

            bar.Nome.Should().Be("N7");
        }

        [Fact]
        public void ToComment_FormatoCompletoComEspacamento()
        {
            var bar = new PfTwoPileCapBarPosition
            {
                Posicao = 3,
                DiametroMm = 6.3,
                ComprimentoCm = 205,
                EspacamentoCm = 15,
                Forma = PfTwoPileCapBarShape.EstriboVertical,
                DescricaoForma = "estribo/linha de distribuicao inferior do bloco"
            };

            bar.ToComment().Should().Be(
                "N3 - POS 3 - diam. 6.3 - C/15 - C=205 - estribo/linha de distribuicao inferior do bloco");
        }

        [Fact]
        public void ToComment_SemEspacamento_OmiteFragmento()
        {
            var bar = new PfTwoPileCapBarPosition
            {
                Posicao = 4,
                DiametroMm = 16.0,
                ComprimentoCm = 407,
                EspacamentoCm = 0,
                Forma = PfTwoPileCapBarShape.FormaEspecial,
                DescricaoForma = "barra principal inferior com dobras R=4"
            };

            bar.ToComment().Should().Be(
                "N4 - POS 4 - diam. 16 - C=407 - barra principal inferior com dobras R=4");
        }

        [Fact]
        public void ToComment_CultureInvariant_PtBr()
        {
            // Regressao historica: formatadores numericos com culture-sensitive
            // emitiam virgula como separador decimal em pt-BR. O Comment precisa
            // ser ASCII-safe para integracao com terceiros.
            CultureInfo cultureAntes = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");

                var bar = new PfTwoPileCapBarPosition
                {
                    Posicao = 5,
                    DiametroMm = 12.5,
                    ComprimentoCm = 325.75,
                    EspacamentoCm = 12.5,
                    Forma = PfTwoPileCapBarShape.FormaEspecial,
                    DescricaoForma = "barra longitudinal inferior N5"
                };

                string comment = bar.ToComment();

                comment.Should().NotContain(",");
                comment.Should().Contain("12.5");
                comment.Should().Contain("325.75");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = cultureAntes;
            }
        }

        [Fact]
        public void ToComment_DefaultsVazios_SaiLimpo()
        {
            var bar = new PfTwoPileCapBarPosition();
            // Posicao=0, DiametroMm=0, ComprimentoCm=0, EspacamentoCm=0, DescricaoForma=""
            bar.ToComment().Should().Be("N0 - POS 0 - diam. 0 - C=0 - ");
        }

        [Theory]
        [InlineData(PfTwoPileCapBarShape.Reta)]
        [InlineData(PfTwoPileCapBarShape.U)]
        [InlineData(PfTwoPileCapBarShape.RetanguloFechado)]
        [InlineData(PfTwoPileCapBarShape.EstriboVertical)]
        [InlineData(PfTwoPileCapBarShape.CaliceVertical)]
        [InlineData(PfTwoPileCapBarShape.FormaEspecial)]
        public void Forma_SuportaTodasAsVariantes(PfTwoPileCapBarShape forma)
        {
            var bar = new PfTwoPileCapBarPosition { Forma = forma };

            bar.Forma.Should().Be(forma);
        }
    }
}
