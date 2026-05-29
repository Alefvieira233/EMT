using System;
using SteelBIM.Services.DiagramaMontagem;
using Xunit;

namespace SteelBIM.Tests.Services.DiagramaMontagem
{
    /// <summary>
    /// Testes puros do <see cref="DimensionPlanCalculator"/> (sem Revit).
    /// Cobre:
    /// 1. Geometria vetorial do plano de cota (perpendicular-a-peca);
    /// 2. v2.6.6: offset adaptativo baseado em sectionDepth + sectionWidth
    ///    (max/2 + clearance) — substitui offset fixo + stagger da v2.6.5;
    /// 3. Threshold de ValueOverride (5mm, sugestao Cowork).
    /// </summary>
    public class DimensionPlanCalculatorTests
    {
        private const double Tol = 1e-6;
        private const double MmToFt = 1.0 / 304.8;

        private static double Mm(double mm) => mm * MmToFt;

        private static void AssertVecEqual(Vec3 esperado, Vec3 obtido, double tol = Tol)
        {
            Assert.Equal(esperado.X, obtido.X, 6);
            Assert.Equal(esperado.Y, obtido.Y, 6);
            Assert.Equal(esperado.Z, obtido.Z, 6);
        }

        // ============================================================
        // CalcularHalfSectionPerp — sub-helper publico v2.6.6
        // ============================================================

        /// <summary>
        /// Validacoes nominais do calculo half-section pra perfis usados na
        /// pratica do escritorio + cenario degenerado (sem params).
        /// max(depth, width) / 2 (conservador: cobre orientacao desconhecida).
        /// </summary>
        [Theory]
        [InlineData(75.0, 40.0, 37.5)]       // U75x40 -> half = 75/2
        [InlineData(100.0, 50.0, 50.0)]      // U100x50 -> half = 100/2
        [InlineData(360.0, 172.0, 180.0)]    // W360x57.8 -> half = 360/2
        [InlineData(50.0, 50.0, 25.0)]       // HSS50x50 quadrado -> half = 50/2
        [InlineData(40.0, 75.0, 37.5)]       // U75x40 rotacionado (width > depth)
        [InlineData(0.0, 0.0, 100.0)]        // fallback: sem params standard -> 100mm
        public void CalcularHalfSectionPerp_MaxDimDividePor2(double depthMm, double widthMm, double expectedHalfMm)
        {
            double depthFt = Mm(depthMm);
            double widthFt = Mm(widthMm);
            double expectedHalfFt = Mm(expectedHalfMm);

            double obtidoFt = DimensionPlanCalculator.CalcularHalfSectionPerp(depthFt, widthFt);

            Assert.Equal(expectedHalfFt, obtidoFt, 6);
        }

        [Fact]
        public void CalcularHalfSectionPerp_NegativosTratadosComoZero_DispOFallback()
        {
            double obtidoFt = DimensionPlanCalculator.CalcularHalfSectionPerp(-5 * MmToFt, -3 * MmToFt);
            Assert.Equal(Mm(100.0), obtidoFt, 6);
        }

        // ============================================================
        // CalcularPlanoCota — geometria vetorial + offset adaptativo
        // ============================================================

        [Fact]
        public void TercaHorizontal_U75x40_OffsetTotal72dot5mmEmZ()
        {
            // Terca U75x40 em Section View XZ (viewNormal = +Y):
            // peca de 1000mm ao longo de X, centrada na origem
            // halfSection = max(75,40)/2 = 37.5mm; clearance 35mm; offset total 72.5mm
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(Mm(1000), 0, 0);
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                clearanceFt: Mm(35));

            // direcao = +X
            AssertVecEqual(new Vec3(1, 0, 0), r.Direcao);
            // perp = direcao x viewNormal = +X x +Y = +Z
            // origem = midpoint + perp * 72.5mm = (500mm, 0, 0) + (0, 0, 72.5mm)
            AssertVecEqual(new Vec3(Mm(500), 0, Mm(72.5)), r.Origem);
        }

        [Fact]
        public void MontanteVertical_U100x50_OffsetTotal85mmEmMinusX()
        {
            // Montante U100x50 vertical 1500mm na mesma vista XZ (viewNormal = +Y)
            // halfSection = max(100,50)/2 = 50mm; clearance 35mm; offset total 85mm
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(0, 0, Mm(1500));
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(100), sectionWidthFt: Mm(50),
                clearanceFt: Mm(35));

            // direcao = +Z
            AssertVecEqual(new Vec3(0, 0, 1), r.Direcao);
            // perp = +Z x +Y = -X
            // origem = midpoint (0, 0, 750mm) + (-X * 85mm) = (-85mm, 0, 750mm)
            AssertVecEqual(new Vec3(-Mm(85), 0, Mm(750)), r.Origem);
        }

        [Fact]
        public void DiagonalInclinada_OrigemAOffsetAdaptativoDoMidpoint()
        {
            // Diagonal U75x40 de 1000mm a 45 graus no plano XZ (viewNormal = +Y)
            // halfSection = max(75,40)/2 = 37.5mm; clearance 35mm; offset total 72.5mm
            double comp = Mm(1000);
            double cat = comp / Math.Sqrt(2);
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(cat, 0, cat);
            Vec3 vn = new Vec3(0, 1, 0);
            double expectedOffsetFt = Mm(72.5);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                clearanceFt: Mm(35));

            // direcao normalizada = (1/sqrt(2), 0, 1/sqrt(2))
            Assert.Equal(1.0 / Math.Sqrt(2), r.Direcao.X, 6);
            Assert.Equal(0, r.Direcao.Y, 6);
            Assert.Equal(1.0 / Math.Sqrt(2), r.Direcao.Z, 6);

            // direcao tem comprimento unitario
            Assert.Equal(1.0, r.Direcao.Length, 6);

            // origem deve estar a `expectedOffsetFt` do midpoint, na perpendicular
            Vec3 mid = new Vec3(cat * 0.5, 0, cat * 0.5);
            Vec3 deslocamento = r.Origem - mid;
            Assert.Equal(expectedOffsetFt, deslocamento.Length, 6);
        }

        /// <summary>
        /// Cenario real do Alef: diagonal U75x40x2.66 numa Section View tipica.
        /// Validacao do offset adaptativo na orientacao real do escritorio.
        /// </summary>
        [Fact]
        public void DiagonalU75x40_OffsetTotalIgualA72dot5mm()
        {
            // Peca diagonal qualquer — so verificando que offset total = 72.5mm
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(Mm(800), 0, Mm(600));
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                clearanceFt: Mm(35));

            Vec3 mid = (p1 + p2) * 0.5;
            double offsetMedido = (r.Origem - mid).Length;
            Assert.Equal(Mm(72.5), offsetMedido, 6);
        }

        /// <summary>
        /// Cenario real do Alef: montante U100x50x3.04. Offset esperado 85mm.
        /// </summary>
        [Fact]
        public void MontanteU100x50_OffsetTotalIgualA85mm()
        {
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(0, 0, Mm(1082));
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(100), sectionWidthFt: Mm(50),
                clearanceFt: Mm(35));

            Vec3 mid = (p1 + p2) * 0.5;
            double offsetMedido = (r.Origem - mid).Length;
            Assert.Equal(Mm(85.0), offsetMedido, 6);
        }

        /// <summary>
        /// Peca em familia sem parametros STRUCTURAL_SECTION_COMMON_*: usa
        /// fallback 100mm + clearance configurado. Caller deve logar Debug.
        /// </summary>
        [Fact]
        public void SemParamsStandard_FallbackHalf100mmMaisClearance()
        {
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(Mm(1000), 0, 0);
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: 0.0, sectionWidthFt: 0.0,
                clearanceFt: Mm(35));

            // fallback half = 100mm; offset total = 100 + 35 = 135mm
            Vec3 mid = (p1 + p2) * 0.5;
            double offsetMedido = (r.Origem - mid).Length;
            Assert.Equal(Mm(135.0), offsetMedido, 6);
        }

        /// <summary>
        /// Clearance zero: cota cola exatamente na face externa do perfil
        /// (offset = halfSectionPerp). Edge case util pra cliente avancado.
        /// </summary>
        [Fact]
        public void ClearanceZero_CotaColaNaFaceExterna_U75()
        {
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(Mm(1000), 0, 0);
            Vec3 vn = new Vec3(0, 1, 0);

            var r = DimensionPlanCalculator.CalcularPlanoCota(
                p1, p2, vn,
                sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                clearanceFt: 0.0);

            Vec3 mid = (p1 + p2) * 0.5;
            double offsetMedido = (r.Origem - mid).Length;
            Assert.Equal(Mm(37.5), offsetMedido, 6);
        }

        [Fact]
        public void PecaParalelaAoViewNormal_LancaInvalidOperationException()
        {
            // Caso degenerado: peca ao longo de Y, vista com normal Y => perpendicular colapsa
            Vec3 p1 = new Vec3(0, 0, 0);
            Vec3 p2 = new Vec3(0, Mm(1000), 0);
            Vec3 vn = new Vec3(0, 1, 0);

            Assert.Throws<InvalidOperationException>(() =>
                DimensionPlanCalculator.CalcularPlanoCota(
                    p1, p2, vn,
                    sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                    clearanceFt: Mm(35)));
        }

        [Fact]
        public void EndpointsCoincidentes_LancaArgumentException()
        {
            Vec3 p = new Vec3(1, 2, 3);
            Assert.Throws<ArgumentException>(() =>
                DimensionPlanCalculator.CalcularPlanoCota(
                    p, p, new Vec3(0, 1, 0),
                    sectionDepthFt: Mm(75), sectionWidthFt: Mm(40),
                    clearanceFt: Mm(35)));
        }

        // ============================================================
        // DeveAplicarOverride — threshold 5mm (logica intacta da v2.6.5)
        // ============================================================

        [Theory]
        [InlineData(1.0, 1.0, false)]       // identico
        [InlineData(1.0, 1.001, false)]     // diferenca 0.3mm < 5mm
        [InlineData(1.0, 1.01, false)]      // diferenca 3.05mm < 5mm
        [InlineData(1.0, 1.02, true)]       // diferenca 6.1mm > 5mm
        [InlineData(2.0, 1.999, false)]     // diferenca 0.3mm < 5mm
        [InlineData(2.0, 1.98, true)]       // diferenca 6.1mm > 5mm
        public void DeveAplicarOverride_RespeitaThresholdDe5mm(double geomFt, double fabFt, bool esperado)
        {
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
            double geomFt = Mm(1224.0);
            double fabFt = Mm(1215.0);
            Assert.True(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt));
        }

        [Fact]
        public void DeveAplicarOverride_FabZeroOuNegativo_NaoAplica()
        {
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(1.0, 0));
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(1.0, -0.5));
        }

        [Fact]
        public void DeveAplicarOverride_ThresholdCustomizavel()
        {
            double geomFt = 1.0;
            double fabFt = 1.0 + Mm(6);
            Assert.True(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt, thresholdMm: 5));
            Assert.False(DimensionPlanCalculator.DeveAplicarOverride(geomFt, fabFt, thresholdMm: 10));
        }

        // ============================================================
        // v2.8.8 — ProjetarPontoNoPlano (Onda 1 do fix Diagrama Montagem)
        // ============================================================
        //
        // Em qualquer plano definido por (origem, normal unitaria), a projecao
        // de um ponto P e' P - normal * ((P - origem) . normal). Isso traz P
        // pro plano mantendo a componente paralela ao plano intacta.
        //
        // Em Section Views do Revit, isso e' usado pra trazer o ponto base de
        // um Grid (que pode estar em Z qualquer) pro plano da vista — antes
        // disso, a Line.CreateBound usada pra criar a Dimension ficava
        // desalinhada das Refs e o Revit rejeitava no commit (bug v2.8.7).

        [Fact]
        public void ProjetarPontoNoPlano_PontoNoProprioPlano_RetornaIgual()
        {
            // Plano XY (normal = +Z, origem = 0,0,0). Ponto (5,3,0) ja' esta no plano.
            Vec3 ponto = new Vec3(5, 3, 0);
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 normal = new Vec3(0, 0, 1);

            Vec3 result = DimensionPlanCalculator.ProjetarPontoNoPlano(ponto, origem, normal);

            AssertVecEqual(ponto, result);
        }

        [Fact]
        public void ProjetarPontoNoPlano_PontoAcimaDoPlanoXY_ProjetaParaZero()
        {
            // Plano XY (normal +Z). Ponto (2, 3, 7) -> projeta pra (2, 3, 0).
            Vec3 ponto = new Vec3(2, 3, 7);
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 normal = new Vec3(0, 0, 1);

            Vec3 result = DimensionPlanCalculator.ProjetarPontoNoPlano(ponto, origem, normal);

            AssertVecEqual(new Vec3(2, 3, 0), result);
        }

        [Fact]
        public void ProjetarPontoNoPlano_PlanoOblique_ProjecaoConsistente()
        {
            // Plano com normal (1,0,0) (plano YZ). Ponto (10, 5, 3) -> projeta
            // pra (0, 5, 3) (X some, Y e Z preservados).
            Vec3 ponto = new Vec3(10, 5, 3);
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 normal = new Vec3(1, 0, 0);

            Vec3 result = DimensionPlanCalculator.ProjetarPontoNoPlano(ponto, origem, normal);

            AssertVecEqual(new Vec3(0, 5, 3), result);
        }

        [Fact]
        public void ProjetarPontoNoPlano_OrigemDeslocada_RespeitaOffset()
        {
            // Plano paralelo a XY mas em Z=10. Ponto (3, 4, 25) -> projeta pra (3, 4, 10).
            Vec3 ponto = new Vec3(3, 4, 25);
            Vec3 origem = new Vec3(0, 0, 10);
            Vec3 normal = new Vec3(0, 0, 1);

            Vec3 result = DimensionPlanCalculator.ProjetarPontoNoPlano(ponto, origem, normal);

            AssertVecEqual(new Vec3(3, 4, 10), result);
        }

        // ============================================================
        // v2.8.8 — ProjetarPontoEm2DDaVista (Onda 1)
        // ============================================================

        [Fact]
        public void ProjetarPontoEm2DDaVista_PontoNaOrigem_RetornaZeroZero()
        {
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 1, 0);

            (double u, double v) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(
                new Vec3(0, 0, 0), origem, right, up);

            Assert.Equal(0, u, 9);
            Assert.Equal(0, v, 9);
        }

        [Fact]
        public void ProjetarPontoEm2DDaVista_PontoNoEixoRight_RetornaUNaoZero()
        {
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 1, 0);

            (double u, double v) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(
                new Vec3(5, 0, 0), origem, right, up);

            Assert.Equal(5, u, 9);
            Assert.Equal(0, v, 9);
        }

        [Fact]
        public void ProjetarPontoEm2DDaVista_OrigemDeslocada_SubtraiOrigemAntes()
        {
            // Origem em (10, 20, 0). Ponto absoluto (15, 25, 0) deve dar (5, 5).
            Vec3 origem = new Vec3(10, 20, 0);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 1, 0);

            (double u, double v) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(
                new Vec3(15, 25, 0), origem, right, up);

            Assert.Equal(5, u, 9);
            Assert.Equal(5, v, 9);
        }

        // ============================================================
        // v2.8.8 — ReconstruirPonto3DDaVista (Onda 1, inverso da 2D)
        // ============================================================

        [Fact]
        public void ReconstruirPonto3DDaVista_Inverso_DaProjecao2D_RoundTrip()
        {
            // Round-trip: 3D -> 2D -> 3D deve voltar pro mesmo ponto (assumindo
            // ponto no plano da vista).
            Vec3 origem = new Vec3(100, 200, 50);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 0, 1);
            Vec3 pontoOriginal = new Vec3(105, 200, 55);

            (double u, double v) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(
                pontoOriginal, origem, right, up);
            Vec3 reconstruido = DimensionPlanCalculator.ReconstruirPonto3DDaVista(
                u, v, origem, right, up);

            AssertVecEqual(pontoOriginal, reconstruido);
        }

        [Fact]
        public void ReconstruirPonto3DDaVista_UVZerados_RetornaOrigem()
        {
            Vec3 origem = new Vec3(7, 14, 21);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 0, 1);

            Vec3 result = DimensionPlanCalculator.ReconstruirPonto3DDaVista(0, 0, origem, right, up);

            AssertVecEqual(origem, result);
        }

        // ============================================================
        // v2.8.8 — Vec3.Dot (helper novo da Onda 1)
        // ============================================================

        [Fact]
        public void Vec3_Dot_OrtogonalDaZero()
        {
            Vec3 a = new Vec3(1, 0, 0);
            Vec3 b = new Vec3(0, 1, 0);
            Assert.Equal(0, a.Dot(b), 9);
        }

        [Fact]
        public void Vec3_Dot_ParalelosMesmoSentido_DaProdutoDosModulos()
        {
            Vec3 a = new Vec3(3, 4, 0);
            Vec3 b = new Vec3(6, 8, 0);
            // |a| = 5, |b| = 10, paralelos => dot = 50
            Assert.Equal(50, a.Dot(b), 9);
        }

        [Fact]
        public void Vec3_Dot_AntiparalelosNegativo()
        {
            Vec3 a = new Vec3(1, 0, 0);
            Vec3 b = new Vec3(-2, 0, 0);
            Assert.Equal(-2, a.Dot(b), 9);
        }

        // ============================================================
        // v2.8.8 — Cenario integrado: ordenar Grids da esquerda pra direita
        // ============================================================
        //
        // Simula o que CriarCotasEntreEixos faz no Service: dado 3 Grids em
        // pontos arbitrarios e uma Section View com RightDirection conhecida,
        // a ordenacao pelo U deve dar a sequencia geometricamente esperada.

        [Fact]
        public void Cenario_OrdenarGridsPorU_GalpaoSimples()
        {
            // Vista olhando pra Y (ViewDirection = -Y), Right = +X, Up = +Z, Origem = (0,0,0)
            Vec3 origem = new Vec3(0, 0, 0);
            Vec3 right = new Vec3(1, 0, 0);
            Vec3 up = new Vec3(0, 0, 1);
            Vec3 normalPlano = new Vec3(0, -1, 0);

            // 3 Grids em pontos arbitrarios em XYZ.
            // GridA em X=0, GridB em X=5000mm, GridC em X=10000mm.
            // Y de cada Grid e' arbitrario (Grid e' linha vertical no XY do mundo).
            Vec3 gA = new Vec3(0, 50, 100);            // Y e Z arbitrarios
            Vec3 gB = new Vec3(Mm(5000), -30, 200);
            Vec3 gC = new Vec3(Mm(10000), 70, 50);

            // Projetar cada um no plano da vista (esperado: Y vai a 0)
            Vec3 gAProj = DimensionPlanCalculator.ProjetarPontoNoPlano(gA, origem, normalPlano);
            Vec3 gBProj = DimensionPlanCalculator.ProjetarPontoNoPlano(gB, origem, normalPlano);
            Vec3 gCProj = DimensionPlanCalculator.ProjetarPontoNoPlano(gC, origem, normalPlano);

            // U deve refletir a posicao em X (esquerda → direita)
            (double uA, _) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(gAProj, origem, right, up);
            (double uB, _) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(gBProj, origem, right, up);
            (double uC, _) = DimensionPlanCalculator.ProjetarPontoEm2DDaVista(gCProj, origem, right, up);

            Assert.Equal(0, uA, 6);
            Assert.Equal(Mm(5000), uB, 6);
            Assert.Equal(Mm(10000), uC, 6);
            Assert.True(uA < uB && uB < uC);
        }
    }
}
