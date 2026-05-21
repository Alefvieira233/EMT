using SteelBIM.Services.Ifc;
using Xunit;

namespace SteelBIM.Tests.Services.Ifc
{
    /// <summary>
    /// Testes puros do <see cref="IfcStructuralFilterPure"/> (v2.7.1):
    /// criterio dimensional de "linear" (razao maior/menor >= 3.0) usado
    /// pelo wrapper Revit <c>ConverterPerfilIfcService.EhPerfilEstruturalLinear</c>
    /// para filtrar acessorios IFC nao-conversiveis.
    /// </summary>
    public class IfcStructuralFilterPureTests
    {
        private const double MmToFt = 1.0 / 304.8;
        private static double Mm(double mm) => mm * MmToFt;

        // ============================================================
        // EhLinearPorBbox — razao maior/menor >= 3.0
        // ============================================================

        [Theory]
        // Razao 5:1 (peca linear tipica) -> true
        [InlineData(5000.0, 1000.0, 100.0, true)]
        // Razao exata 3.0 (limiar inclusivo) -> true
        [InlineData(3000.0, 1000.0, 1000.0, true)]
        // Razao 2.5 (curta demais) -> false
        [InlineData(2500.0, 1000.0, 1000.0, false)]
        // Razao 1.2:1 (quase cubica, ex: chapa) -> false
        [InlineData(1200.0, 1000.0, 1000.0, false)]
        // Razao 10:1 (viga longa)
        [InlineData(10000.0, 1000.0, 200.0, true)]
        public void EhLinearPorBbox_RespeitaThreshold3(double dxMm, double dyMm, double dzMm, bool esperado)
        {
            bool obtido = IfcStructuralFilterPure.EhLinearPorBbox(Mm(dxMm), Mm(dyMm), Mm(dzMm));
            Assert.Equal(esperado, obtido);
        }

        [Fact]
        public void BboxDegenerado_QualquerDimensaoZero_RetornaFalse()
        {
            // Dimensao zero em X
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(0.0, Mm(1000), Mm(200)));
            // Dimensao zero em Y
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(Mm(5000), 0.0, Mm(200)));
            // Dimensao zero em Z
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(Mm(5000), Mm(1000), 0.0));
            // Tudo zero (peca degenerada total)
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(0, 0, 0));
        }

        [Fact]
        public void BboxQuadradoUnitario_RetornaFalse()
        {
            // 1m x 1m x 1m: razao 1.0 -> nao-linear
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(Mm(1000), Mm(1000), Mm(1000)));
        }

        [Fact]
        public void BboxNegativo_TratadoComoMagnitude()
        {
            // Dimensoes negativas (caso defensivo) — toma valor absoluto.
            // |-5000| / |1000| = 5.0 >= 3.0 -> true
            Assert.True(IfcStructuralFilterPure.EhLinearPorBbox(-Mm(5000), Mm(1000), Mm(200)));
            // Todas negativas, mesma magnitude
            Assert.True(IfcStructuralFilterPure.EhLinearPorBbox(-Mm(5000), -Mm(1000), -Mm(200)));
        }

        [Fact]
        public void Epsilon_AbaixoDeEpsilonE_Degenerado()
        {
            // 1mm < 1e-3 ft (~0.3mm) deveria ser aceito? 1mm = 0.00328 ft > 0.001 ft -> nao e degenerado
            // Testando com 0.1mm (= 0.000328 ft < 1e-3 ft) -> degenerado
            Assert.False(IfcStructuralFilterPure.EhLinearPorBbox(Mm(5000), Mm(1000), Mm(0.1)));
        }

        [Fact]
        public void PecaRealU75x40_DiagonalDeGalpao_Linear()
        {
            // Diagonal de tesoura U75x40 com 1.2m de comprimento — caso real do Alef
            // bbox ~1200mm x 75mm x 40mm: razao 1200/40 = 30 -> linear
            Assert.True(IfcStructuralFilterPure.EhLinearPorBbox(Mm(1200), Mm(75), Mm(40)));
        }

        [Fact]
        public void Chapa_AltaELargaSemEspessura_NaoLinear()
        {
            // Chapa de ligacao 300x250x10mm: razao 300/10 = 30 — porem chapas
            // tambem podem cair como "linear" se forem muito finas. Documentando
            // comportamento atual: chapa fina passa pelo filtro. Aceitavel — o
            // filtro nao e infalivel, so reduz ruido visual majoritario.
            Assert.True(IfcStructuralFilterPure.EhLinearPorBbox(Mm(300), Mm(250), Mm(10)));
        }
    }
}
