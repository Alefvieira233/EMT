using FluentAssertions;
using SteelBIM.Models.Conexoes;
using Xunit;

namespace SteelBIM.Tests.Models.Conexoes
{
    /// <summary>
    /// Cobertura do shape do resultado retornado pelo PlacaBaseLancamentoService.
    /// Onda 6 (Victor Final): payload puro (sem Revit), so contagens e Resumo formatado.
    /// </summary>
    public class PlacaBaseLancamentoResultadoTests
    {
        [Fact]
        public void Defaults_AllZeroes()
        {
            var r = new PlacaBaseLancamentoResultado();
            r.PilaresAnalisados.Should().Be(0);
            r.PlacasInseridas.Should().Be(0);
            r.PilaresSemApoio.Should().Be(0);
            r.PilaresSemFaceSuperior.Should().Be(0);
            r.FalhasInsercao.Should().Be(0);
        }

        [Fact]
        public void PilaresAnalisados_RoundTrip()
        {
            var r = new PlacaBaseLancamentoResultado { PilaresAnalisados = 12 };
            r.PilaresAnalisados.Should().Be(12);
        }

        [Fact]
        public void PlacasInseridas_RoundTrip()
        {
            var r = new PlacaBaseLancamentoResultado { PlacasInseridas = 8 };
            r.PlacasInseridas.Should().Be(8);
        }

        [Fact]
        public void PilaresSemApoio_RoundTrip()
        {
            var r = new PlacaBaseLancamentoResultado { PilaresSemApoio = 2 };
            r.PilaresSemApoio.Should().Be(2);
        }

        [Fact]
        public void PilaresSemFaceSuperior_RoundTrip()
        {
            var r = new PlacaBaseLancamentoResultado { PilaresSemFaceSuperior = 1 };
            r.PilaresSemFaceSuperior.Should().Be(1);
        }

        [Fact]
        public void FalhasInsercao_RoundTrip()
        {
            var r = new PlacaBaseLancamentoResultado { FalhasInsercao = 1 };
            r.FalhasInsercao.Should().Be(1);
        }

        [Fact]
        public void Resumo_FormatPreservaTodasContagens()
        {
            var r = new PlacaBaseLancamentoResultado
            {
                PilaresAnalisados = 12,
                PlacasInseridas = 8,
                PilaresSemApoio = 2,
                PilaresSemFaceSuperior = 1,
                FalhasInsercao = 1
            };

            string texto = r.Resumo();
            texto.Should().Contain("12");
            texto.Should().Contain("8");
            texto.Should().Contain("2");
            texto.Should().Contain("1");
            texto.Should().Contain("Pilares analisados");
            texto.Should().Contain("Placas inseridas");
        }

        [Fact]
        public void Resumo_ZeroContagens_GeraTextoLegivel()
        {
            var r = new PlacaBaseLancamentoResultado();
            string texto = r.Resumo();
            texto.Should().NotBeNullOrWhiteSpace();
            texto.Should().Contain("Pilares analisados: 0");
        }
    }
}
