#nullable enable
using FerramentaEMT.Services.Trelica;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class CotaFaixaBuilderTests
    {
        [Fact]
        public void FaixaPaineisBanzoSuperior_GeraSegmentosConsecutivos()
        {
            var faixa = CotaFaixaBuilder.FaixaPaineisBanzoSuperior(new[] { 0.0, 5.0, 12.0 });
            faixa.Tipo.Should().Be(CotaFaixaBuilder.Faixa.PaineisBanzoSuperior);
            faixa.Segmentos.Should().HaveCount(2);
            faixa.Segmentos[0].XInicio.Should().Be(0.0);
            faixa.Segmentos[0].XFim.Should().Be(5.0);
            faixa.Segmentos[1].XInicio.Should().Be(5.0);
            faixa.Segmentos[1].XFim.Should().Be(12.0);
            faixa.OffsetZPes.Should().BePositive();
        }

        [Fact]
        public void FaixaPaineisBanzoInferior_OffsetNegativo()
        {
            var faixa = CotaFaixaBuilder.FaixaPaineisBanzoInferior(new[] { 0.0, 5.0 });
            faixa.OffsetZPes.Should().BeNegative();
        }

        [Fact]
        public void FaixaVaoTotal_UmUnicoSegmento()
        {
            var faixa = CotaFaixaBuilder.FaixaVaoTotal(0.0, 38.367);
            faixa.Segmentos.Should().HaveCount(1);
            faixa.Segmentos[0].XInicio.Should().Be(0.0);
            faixa.Segmentos[0].XFim.Should().Be(38.367);
        }

        [Fact]
        public void FaixaAlturasMontantes_UmSegmentoPorMontante()
        {
            var faixa = CotaFaixaBuilder.FaixaAlturasMontantes(new[] { 2.5, 5.0, 7.5 });
            faixa.Segmentos.Should().HaveCount(3);
            faixa.Segmentos[0].XInicio.Should().Be(2.5);
            faixa.Segmentos[0].XFim.Should().Be(2.5); // cota vertical, mesma estacao
        }

        [Fact]
        public void FaixaVaosEntreApoios_DoisApoios_UmSegmento()
        {
            var faixa = CotaFaixaBuilder.FaixaVaosEntreApoios(new[] { 0.0, 38.367 });
            faixa.Segmentos.Should().HaveCount(1);
        }

        [Fact]
        public void FaixaVaosEntreApoios_TresApoiosContinua_DoisSegmentos()
        {
            var faixa = CotaFaixaBuilder.FaixaVaosEntreApoios(new[] { 0.0, 15.0, 30.0 });
            faixa.Segmentos.Should().HaveCount(2);
            faixa.Segmentos[0].XInicio.Should().Be(0.0);
            faixa.Segmentos[0].XFim.Should().Be(15.0);
            faixa.Segmentos[1].XInicio.Should().Be(15.0);
            faixa.Segmentos[1].XFim.Should().Be(30.0);
        }

        [Fact]
        public void SegmentosConsecutivos_NaoOrdenado_Lanca()
        {
            // Teste indireto via FaixaPaineisBanzoSuperior com lista fora de ordem
            var act = () => CotaFaixaBuilder.FaixaPaineisBanzoSuperior(new[] { 0.0, 5.0, 3.0 });
            act.Should().Throw<System.ArgumentException>();
        }
    }
}
