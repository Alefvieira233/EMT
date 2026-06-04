#nullable enable
using FluentAssertions;
using SteelBIM.Services.Pintura;
using Xunit;

namespace SteelBIM.Tests.Services.Pintura
{
    public class PinturaFormulaTests
    {
        [Theory]
        [InlineData(1.00, true)]   // normal paralela ao eixo -> topo
        [InlineData(0.95, true)]   // levemente mitrado -> ainda topo
        [InlineData(0.90, true)]   // no limiar
        [InlineData(0.50, false)]  // diagonal -> lateral
        [InlineData(0.00, false)]  // perpendicular -> lateral
        public void EhFaceDeTopo_ClassificaPorParalelismoComOEixo(double dotAbs, bool esperado)
        {
            PinturaFormula.EhFaceDeTopo(dotAbs).Should().Be(esperado);
        }

        [Fact]
        public void AreaLiquida_DescontaToposE5Porcento()
        {
            // total=100, topos=4 -> base=96; 5% -> 91.2
            PinturaFormula.AreaLiquida(100.0, 4.0, descontarTopos: true, percentualDesconto: 5.0)
                .Should().BeApproximately(91.2, 1e-9);
        }

        [Fact]
        public void AreaLiquida_SemDescontarTopos_UsaTotal()
        {
            // total=100, sem topos, 5% -> 95
            PinturaFormula.AreaLiquida(100.0, 4.0, descontarTopos: false, percentualDesconto: 5.0)
                .Should().BeApproximately(95.0, 1e-9);
        }

        [Fact]
        public void AreaLiquida_SemDesconto_RetornaBaseInteira()
        {
            PinturaFormula.AreaLiquida(80.0, 0.0, descontarTopos: true, percentualDesconto: 0.0)
                .Should().BeApproximately(80.0, 1e-9);
        }

        [Theory]
        [InlineData(-10.0)] // negativo -> clampa em 0% (sem desconto)
        [InlineData(150.0)] // acima de 100 -> clampa em 100% (zera)
        public void AreaLiquida_PercentualForaDeFaixa_ClampaSemQuebrar(double pct)
        {
            double r = PinturaFormula.AreaLiquida(100.0, 0.0, descontarTopos: false, percentualDesconto: pct);
            r.Should().BeInRange(0.0, 100.0);
        }

        [Fact]
        public void AreaLiquida_NuncaNegativa_QuandoToposMaiorQueTotal()
        {
            PinturaFormula.AreaLiquida(2.0, 5.0, descontarTopos: true, percentualDesconto: 5.0)
                .Should().Be(0.0);
        }
    }
}
