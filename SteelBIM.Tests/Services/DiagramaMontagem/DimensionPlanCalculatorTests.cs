using System;
using SteelBIM.Services.DiagramaMontagem;
using Xunit;

namespace SteelBIM.Tests.Services.DiagramaMontagem
{
    /// <summary>
    /// Testes puros do <see cref="DimensionPlanCalculator"/> (sem Revit).
    /// Cobre geometria vetorial do plano de cota individual usado pelo
    /// Diagrama de Montagem (v2.6.5) — perpendicular-a-peca em vez de
    /// perpendicular-a-vista, com stagger par/impar e detecao de caso
    /// degenerado.
    /// </summary>
    public class DimensionPlanCalculatorTests
    {
        private const double Tol = 1e-6;
        private const double MmToFt = 1.0 / 304.8;

        private static void AssertVecEqual(Vec3 esperado, Vec3 obtido, double tol = Tol)
        {
            Assert.Equal(esperado.X, obtido.X, 6);
            Assert.Equal(esperado.Y, obtido.Y, 6);
            Assert.Equal(esperado.Z, obtido.Z, 6);
        }

        // ============================================================
        // PHASE 2 CHECKPOINT — Cowork sugeriu 5-6 InlineData cobrindo
        // peca horizontal, vertical, inclinada 30, stagger par/impar e
        // caso degenerado.
        // ============================================================

        [Fact]
        public void TercaHorizontal_OffsetPerpendicularNaDirecaoY()
        {
            // Terca tipica em Section View XZ (viewNormal = +Y):
            // peca de 1000mm ao longo de X, centrada na origem
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(1000 * MmToFt, 0, 0);
            Vec3 vn = new Vec3(0, 1, 0);
            double offset = 200 * MmToFt;

            var r = DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, offset, staggerExtra: false);

            // direcao = +X
            AssertVecEqual(new Vec3(1, 0, 0), r.Direcao);
            // perp = direcao x viewNormal = +X x +Y = +Z
            // origem = midpoint + perp * offset = (500mm, 0, 0) + (0, 0, 200mm)
            AssertVecEqual(new Vec3(500 * MmToFt, 0, 200 * MmToFt), r.Origem);
        }

        [Fact]
        public void MontanteVertical_OffsetPerpendicularNaDirecaoX()
        {
            // Montante vertical 1500mm na mesma vista XZ (viewNormal = +Y)
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(0, 0, 1500 * MmToFt);
            Vec3 vn = new Vec3(0, 1, 0);
            double offset = 200 * MmToFt;

            var r = DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, offset, staggerExtra: false);

            // direcao = +Z
            AssertVecEqual(new Vec3(0, 0, 1), r.Direcao);
            // perp = +Z x +Y = -X
            // origem = midpoint (0, 0, 750mm) + (-X * 200mm) = (-200mm, 0, 750mm)
            AssertVecEqual(new Vec3(-200 * MmToFt, 0, 750 * MmToFt), r.Origem);
        }

        [Fact]
        public void DiagonalInclinada_DirecaoUnitariaEOrigemForaDaPeca()
        {
            // Diagonal 1000mm a 45 graus no plano XZ (viewNormal = +Y)
            double comp = 1000 * MmToFt;
            double cat = comp / Math.Sqrt(2);
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(cat, 0, cat);
            Vec3 vn = new Vec3(0, 1, 0);
            double offset = 200 * MmToFt;

            var r = DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, offset, staggerExtra: false);

            // direcao normalizada = (1/sqrt(2), 0, 1/sqrt(2))
            Assert.Equal(1.0 / Math.Sqrt(2), r.Direcao.X, 6);
            Assert.Equal(0, r.Direcao.Y, 6);
            Assert.Equal(1.0 / Math.Sqrt(2), r.Direcao.Z, 6);

            // direcao tem comprimento unitario
            Assert.Equal(1.0, r.Direcao.Length, 6);

            // origem deve estar a `offset` do midpoint (mesma distancia perpendicular)
            Vec3 mid = new Vec3(cat * 0.5, 0, cat * 0.5);
            Vec3 deslocamento = r.Origem - mid;
            Assert.Equal(offset, deslocamento.Length, 6);
        }

        [Fact]
        public void StaggerPar_OffsetExtraDe100mm()
        {
            // Mesma peca de TercaHorizontal mas com staggerExtra=true
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(1000 * MmToFt, 0, 0);
            Vec3 vn = new Vec3(0, 1, 0);
            double offset = 200 * MmToFt;

            var rNormal = DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, offset, staggerExtra: false);
            var rStagger = DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, offset, staggerExtra: true);

            // Diferenca entre origens = 100mm na direcao perpendicular (+Z aqui)
            Vec3 dif = rStagger.Origem - rNormal.Origem;
            Assert.Equal(100 * MmToFt, dif.Length, 6);

            // Direcao nao muda com stagger
            AssertVecEqual(rNormal.Direcao, rStagger.Direcao);
        }

        [Fact]
        public void PecaParalelaAoViewNormal_LancaInvalidOperationException()
        {
            // Caso degenerado: peca ao longo de Y, vista com normal Y => perpendicular colapsa
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(0, 1000 * MmToFt, 0);
            Vec3 vn = new Vec3(0, 1, 0);

            Assert.Throws<InvalidOperationException>(() =>
                DimensionPlanCalculator.CalcularPlanoCota(p1, p2, vn, 200 * MmToFt, false));
        }

        [Fact]
        public void EndpointsCoincidentes_LancaArgumentException()
        {
            Vec3 p = new Vec3(1, 2, 3);
            Assert.Throws<ArgumentException>(() =>
                DimensionPlanCalculator.CalcularPlanoCota(p, p, new Vec3(0, 1, 0), 200 * MmToFt, false));
        }

        // ============================================================
        // DeveAplicarOverride — threshold 5mm (sugestao Cowork)
        // ============================================================

        [Theory]
        [InlineData(1.0, 1.0, false)]       // identico
        [InlineData(1.0, 1.001, false)]     // diferenca 0.3mm < 5mm
        [InlineData(1.0, 1.01, false)]      // diferenca 3.05mm < 5mm
        [InlineData(1.0, 1.02, true)]       // diferenca 6.1mm > 5mm
        [InlineData(2.0, 1.999, false)]     // diferenca 0.3mm < 5mm
        [InlineData(2.0, 1.98, true)]       // diferenca 6.1mm > 5mm (mesma diff em peca de 600mm vs 300mm)
        public void DeveAplicarOverride_RespeitaThresholdDe5mm(double geomFt, double fabFt, bool esperado)
        {
            // Os inlines acima estao em FT (Revit internal). Converter para mm so para clareza
            // de doc — o codigo trabalha em ft + threshold em mm internamente.
            Assert.Equal(esperado, DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt));
        }

        /// <summary>
        /// Cenario real reportado pelo Alef (imagem da diagonal U75x40x2.66):
        /// Cut Length de fabricacao = 1215mm; comprimento geometrico medido = 1224mm;
        /// diferenca 9mm > 5mm threshold => override DEVE ser aplicado para que a
        /// cota mostre 1215 (fabricacao) ao inves de 1224 (geometrico).
        /// </summary>
        [Fact]
        public void DeveAplicarOverride_DiagonalU75_Geom1224mm_Fab1215mm_DispOverride()
        {
            double geomFt = 1224.0 * MmToFt;
            double fabFt = 1215.0 * MmToFt;
            Assert.True(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt));
        }

        [Fact]
        public void DeveAplicarOverride_FabZeroOuNegativo_NaoAplica()
        {
            // Se nao temos lengthFab valido, nao podemos sugerir override
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(1.0, 0));
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(1.0, -0.5));
        }

        [Fact]
        public void DeveAplicarOverride_ThresholdCustomizavel()
        {
            // Diferenca 6mm: deve aplicar com threshold 5mm, mas nao com 10mm
            double geomFt = 1.0;
            double fabFt = 1.0 + 6 * MmToFt;
            Assert.True(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt, thresholdMm: 5));
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt, thresholdMm: 10));
        }
    }
}
