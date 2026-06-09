#nullable enable
using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    public class EscadaCalculoTests
    {
        [Fact]
        public void ContarDegraus_UsaValorDoUsuarioQuandoPositivo()
        {
            EscadaCalculo.ContarDegraus(5, 10.0, 2.0).Should().Be(5);
        }

        [Theory]
        [InlineData(0, 10.0, 2.0, 5)]   // round(5.0) = 5
        [InlineData(0, 9.0, 2.0, 4)]    // round(4.5) = 4 (Math.Round banker's -> par)
        [InlineData(0, 1.0, 10.0, 1)]   // round(0.1) = 0 -> minimo 1
        [InlineData(0, 10.0, 0.0, 1)]   // espelho invalido -> 1 (guard div/0)
        public void ContarDegraus_DerivaQuandoConfigZero(int cfg, double rise, double espelho, int esperado)
        {
            EscadaCalculo.ContarDegraus(cfg, rise, espelho).Should().Be(esperado);
        }

        [Theory]
        [InlineData(1.0, 1.0, 45.0)]
        [InlineData(1.0, 0.0, 90.0)]
        [InlineData(0.0, 1.0, 0.0)]
        public void InclinacaoGraus(double rise, double run, double esperado)
        {
            EscadaCalculo.InclinacaoGraus(rise, run).Should().BeApproximately(esperado, 1e-9);
        }

        [Fact]
        public void BlondelCm_AplicaFormula()
        {
            // (2*0.3 + 0.2) / 1.0 = 0.8
            EscadaCalculo.BlondelCm(0.3, 0.2, 1.0).Should().BeApproximately(0.8, 1e-9);
        }

        [Theory]
        [InlineData(63.0, true)]
        [InlineData(64.0, true)]
        [InlineData(65.0, true)]
        [InlineData(62.9, false)]
        [InlineData(65.1, false)]
        public void BlondelDentroDaFaixa(double cm, bool esperado)
        {
            EscadaCalculo.BlondelDentroDaFaixa(cm).Should().Be(esperado);
        }
    }
}
