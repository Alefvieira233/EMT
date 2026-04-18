using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Smoke
{
    /// <summary>
    /// Testes de smoke garantem que a infraestrutura de teste funciona.
    /// Devem rodar em &lt; 100ms.
    /// </summary>
    public class SmokeTests
    {
        [Fact]
        public void Sanity_Check_Passa()
        {
            // Arrange
            int dois = 2;

            // Act
            int resultado = dois + 2;

            // Assert
            resultado.Should().Be(4);
        }

        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(2, 3, 5)]
        [InlineData(0, 0, 0)]
        [InlineData(-1, 1, 0)]
        public void Soma_Inteiros_Funciona(int a, int b, int esperado)
        {
            // Act
            int resultado = a + b;

            // Assert
            resultado.Should().Be(esperado);
        }
    }
}
