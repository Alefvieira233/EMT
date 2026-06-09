#nullable enable
using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    public class GuardaCorpoCalculoTests
    {
        private const double Eps = 1e-9;

        [Theory]
        [InlineData(10.0, 3.0, 4)]  // ceil(3.33) = 4
        [InlineData(9.0, 3.0, 3)]   // ceil(3.0) = 3
        [InlineData(2.0, 5.0, 1)]   // ceil(0.4) = 1
        [InlineData(0.0, 3.0, 1)]   // minimo 1
        [InlineData(10.0, 0.0, 1)]  // espacamento invalido -> 1
        [InlineData(10.0, -2.0, 1)] // negativo -> 1
        public void SegmentosPorEspacamento(double comprimento, double espMax, int esperado)
        {
            GuardaCorpoCalculo.SegmentosPorEspacamento(comprimento, espMax, Eps).Should().Be(esperado);
        }

        [Fact]
        public void AlturasTravessas_DistribuiUniformemente()
        {
            GuardaCorpoCalculo.AlturasTravessas(12.0, 3, Eps)
                .Should().Equal(3.0, 6.0, 9.0); // passo = 12/4
        }

        [Fact]
        public void AlturasTravessas_UmaTravessa_NoMeio()
        {
            GuardaCorpoCalculo.AlturasTravessas(10.0, 1, Eps)
                .Should().Equal(5.0);
        }

        [Theory]
        [InlineData(12.0, 0)]   // quantidade <= 0 -> vazio
        [InlineData(0.0, 3)]    // altura <= eps -> vazio
        public void AlturasTravessas_CasosVazios(double alturaTotal, int qtd)
        {
            GuardaCorpoCalculo.AlturasTravessas(alturaTotal, qtd, Eps).Should().BeEmpty();
        }
    }
}
