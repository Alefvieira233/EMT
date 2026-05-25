using FluentAssertions;
using SteelBIM.Views.Helpers;
using Xunit;

namespace SteelBIM.Tests.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3): testes do UniformPositionDistributor extraido
    /// do code-behind das Pf*BarsWindow. Diferente de PfRebarServicePure.DistributePositionsMm
    /// porque toleramos input ruim (UI live preview).
    /// </summary>
    public class UniformPositionDistributorTests
    {
        // ---------- Edge cases tolerantes ----------

        [Fact]
        public void Count_zero_retorna_lista_vazia()
        {
            UniformPositionDistributor.Distribute(0, 0, 100).Should().BeEmpty();
        }

        [Fact]
        public void Count_negativo_retorna_lista_vazia()
        {
            UniformPositionDistributor.Distribute(-1, 0, 100).Should().BeEmpty();
        }

        [Fact]
        public void Max_menor_que_min_retorna_lista_vazia()
        {
            UniformPositionDistributor.Distribute(3, 100, 0).Should().BeEmpty();
        }

        // ---------- Casos centro ----------

        [Fact]
        public void Count_um_retorna_centro()
        {
            var positions = UniformPositionDistributor.Distribute(1, 0, 100);
            positions.Should().HaveCount(1);
            positions[0].Should().Be(50);
        }

        [Fact]
        public void Range_zero_retorna_centro()
        {
            // Max == Min: centro = min = max
            var positions = UniformPositionDistributor.Distribute(5, 50, 50);
            positions.Should().HaveCount(1);
            positions[0].Should().Be(50);
        }

        [Fact]
        public void Range_menor_que_001_retorna_centro()
        {
            // Tolerancia: range minimo 0.001
            var positions = UniformPositionDistributor.Distribute(5, 50.0, 50.0005);
            positions.Should().HaveCount(1);
            positions[0].Should().BeApproximately(50.00025, 0.0001);
        }

        // ---------- Distribuicao normal ----------

        [Fact]
        public void Count_dois_retorna_extremos()
        {
            var positions = UniformPositionDistributor.Distribute(2, 0, 100);
            positions.Should().HaveCount(2);
            positions[0].Should().Be(0);
            positions[1].Should().Be(100);
        }

        [Fact]
        public void Count_tres_retorna_inicio_meio_fim()
        {
            var positions = UniformPositionDistributor.Distribute(3, 0, 100);
            positions.Should().HaveCount(3);
            positions[0].Should().Be(0);
            positions[1].Should().Be(50);
            positions[2].Should().Be(100);
        }

        [Fact]
        public void Count_cinco_distribui_uniformemente()
        {
            var positions = UniformPositionDistributor.Distribute(5, 0, 100);
            positions.Should().HaveCount(5);
            positions[0].Should().Be(0);
            positions[1].Should().Be(25);
            positions[2].Should().Be(50);
            positions[3].Should().Be(75);
            positions[4].Should().Be(100);
        }

        [Fact]
        public void Count_onze_em_um_metro_da_dez_centimetros_de_passo()
        {
            // 11 posicoes em [0, 100] = 10 vaos de 10cm
            var positions = UniformPositionDistributor.Distribute(11, 0, 100);
            positions.Should().HaveCount(11);
            for (int i = 0; i < 11; i++)
                positions[i].Should().BeApproximately(i * 10.0, 0.001);
        }

        [Fact]
        public void Aceita_min_negativo()
        {
            var positions = UniformPositionDistributor.Distribute(3, -50, 50);
            positions[0].Should().Be(-50);
            positions[1].Should().Be(0);
            positions[2].Should().Be(50);
        }
    }
}
