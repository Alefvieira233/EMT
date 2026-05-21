using SteelBIM.Services.DiagramaMontagem;
using Xunit;

namespace SteelBIM.Tests.Services.DiagramaMontagem
{
    /// <summary>
    /// Testes puros do <see cref="SectionBoxBuilder"/> (sem Revit). Cobre:
    /// 1. v2.6.9: <see cref="SectionBoxBuilder.CalcularPlanta"/> — vista superior
    ///    (Transform com BasisZ=-Z, up=+Y norte, bbox XY/Z + margem);
    /// 2. Regressao retroativa de <see cref="SectionBoxBuilder.CalcularElevacao"/>
    ///    (comportamento original v2.3.0+ extraido em v2.6.9 para cobrir esta
    ///    feature sem testes ate entao).
    /// </summary>
    public class SectionBoxBuilderTests
    {
        private const double MmToFt = 1.0 / 304.8;
        private static double Mm(double mm) => mm * MmToFt;

        private static void AssertVecEqual(Vec3 esperado, Vec3 obtido)
        {
            Assert.Equal(esperado.X, obtido.X, 6);
            Assert.Equal(esperado.Y, obtido.Y, 6);
            Assert.Equal(esperado.Z, obtido.Z, 6);
        }

        // ============================================================
        // CalcularPlanta — vista superior (v2.6.9)
        // ============================================================

        /// <summary>
        /// Caso tipico: galpao pequeno 10x10m, alturas variando de 0 a 6m.
        /// Margem 500mm em todas as direcoes.
        /// </summary>
        [Fact]
        public void Planta_BBox10x10m_OrigemNoCentro_BasisZNegativo()
        {
            Vec3 bbMin = new Vec3(0, 0, 0);
            Vec3 bbMax = new Vec3(Mm(10000), Mm(10000), Mm(6000));
            double margemFt = Mm(500);

            var r = SectionBoxBuilder.CalcularPlanta(bbMin, bbMax, margemFt);

            // Origem = centro geometrico dos elementos
            AssertVecEqual(new Vec3(Mm(5000), Mm(5000), Mm(3000)), r.OrigemTransform);

            // Transform: direita=+X, up=+Y (norte), observador olha pra -Z
            AssertVecEqual(new Vec3(1, 0, 0), r.BasisX);
            AssertVecEqual(new Vec3(0, 1, 0), r.BasisY);
            AssertVecEqual(new Vec3(0, 0, -1), r.BasisZ);

            // Bbox local: largura 11m (10m + 2x500mm), altura 11m, profundidade 7m (6m + 2x500mm)
            // Centralizado no origin
            AssertVecEqual(new Vec3(-Mm(5500), -Mm(5500), -Mm(3500)), r.BboxMin);
            AssertVecEqual(new Vec3(Mm(5500), Mm(5500), Mm(3500)), r.BboxMax);
        }

        /// <summary>
        /// Caso assimetrico: terca/viga isolada de 10m em X, so 3m em Y.
        /// </summary>
        [Fact]
        public void Planta_BBox10x3m_RetangularLongo()
        {
            Vec3 bbMin = new Vec3(0, 0, 0);
            Vec3 bbMax = new Vec3(Mm(10000), Mm(3000), Mm(500));
            double margemFt = Mm(500);

            var r = SectionBoxBuilder.CalcularPlanta(bbMin, bbMax, margemFt);

            // Largura = 10m + 2x500 = 11m; Altura = 3m + 2x500 = 4m
            Assert.Equal(Mm(11000), r.BboxMax.X - r.BboxMin.X, 6);
            Assert.Equal(Mm(4000), r.BboxMax.Y - r.BboxMin.Y, 6);
            // Profundidade Z = 500mm + 2x500 = 1500mm
            Assert.Equal(Mm(1500), r.BboxMax.Z - r.BboxMin.Z, 6);
        }

        /// <summary>
        /// Validacao explicita: margem aplicada nos 4 lados (X+Y) E na profundidade (Z).
        /// </summary>
        [Theory]
        [InlineData(100.0)]    // 100mm
        [InlineData(500.0)]    // default
        [InlineData(1000.0)]   // 1m
        public void Planta_MargemAplicadaNos4LadosEZ(double margemMm)
        {
            Vec3 bbMin = new Vec3(0, 0, 0);
            Vec3 bbMax = new Vec3(Mm(5000), Mm(4000), Mm(2000));
            double margemFt = Mm(margemMm);

            var r = SectionBoxBuilder.CalcularPlanta(bbMin, bbMax, margemFt);

            double larguraExpected = Mm(5000) + 2 * margemFt;
            double alturaExpected = Mm(4000) + 2 * margemFt;
            double profundidadeExpected = Mm(2000) + 2 * margemFt;

            Assert.Equal(larguraExpected, r.BboxMax.X - r.BboxMin.X, 6);
            Assert.Equal(alturaExpected, r.BboxMax.Y - r.BboxMin.Y, 6);
            Assert.Equal(profundidadeExpected, r.BboxMax.Z - r.BboxMin.Z, 6);
        }

        /// <summary>
        /// Caso degenerado: 1 elemento isolado (bbox quase nulo). Helper deve
        /// retornar sem lancar, com bbox local = apenas margem (2x500mm em cada eixo).
        /// </summary>
        [Fact]
        public void Planta_BBoxDegenerado_NaoLanca()
        {
            Vec3 pontoUnico = new Vec3(Mm(1234), Mm(5678), Mm(910));
            double margemFt = Mm(500);

            // bbox degenerado: min == max
            var r = SectionBoxBuilder.CalcularPlanta(pontoUnico, pontoUnico, margemFt);

            // Origem = o ponto unico
            AssertVecEqual(pontoUnico, r.OrigemTransform);

            // Bbox local = 1m em cada eixo (so margem)
            Assert.Equal(Mm(1000), r.BboxMax.X - r.BboxMin.X, 6);
            Assert.Equal(Mm(1000), r.BboxMax.Y - r.BboxMin.Y, 6);
            Assert.Equal(Mm(1000), r.BboxMax.Z - r.BboxMin.Z, 6);

            // BasisZ continua -Z
            AssertVecEqual(new Vec3(0, 0, -1), r.BasisZ);
        }

        // ============================================================
        // CalcularElevacao — regressao retroativa v2.3.0+
        // (comportamento original extraido em v2.6.9; testes garantem
        //  que branch ja existente continua funcionando apos refactor)
        // ============================================================

        /// <summary>
        /// Elevacao paralela ao eixo X: observador em Y- olhando para +Y.
        /// direita=+X, up=+Z, viewDir=-Y. Comportamento v2.3.0+.
        /// </summary>
        [Fact]
        public void Elevacao_ParaleloEixoX_RegressionV264()
        {
            Vec3 bbMin = new Vec3(0, 0, 0);
            Vec3 bbMax = new Vec3(Mm(15000), Mm(8000), Mm(6000));
            double margemFt = Mm(500);

            var r = SectionBoxBuilder.CalcularElevacao(bbMin, bbMax, margemFt, paraleloAoX: true);

            // Origem = centro geometrico
            AssertVecEqual(new Vec3(Mm(7500), Mm(4000), Mm(3000)), r.OrigemTransform);

            // Transform: direita=+X, up=+Z, viewDir=-Y
            AssertVecEqual(new Vec3(1, 0, 0), r.BasisX);
            AssertVecEqual(new Vec3(0, 0, 1), r.BasisY);
            AssertVecEqual(new Vec3(0, -1, 0), r.BasisZ);

            // Bbox local:
            //  largura (X local) = extX + 2*margem = 15000 + 1000 = 16000mm
            //  altura (Y local = Z mundial) = extZ + 2*margem = 6000 + 1000 = 7000mm
            //  profundidade (Z local = Y mundial) = extY + 2*margem = 8000 + 1000 = 9000mm
            Assert.Equal(Mm(16000), r.BboxMax.X - r.BboxMin.X, 6);
            Assert.Equal(Mm(7000), r.BboxMax.Y - r.BboxMin.Y, 6);
            Assert.Equal(Mm(9000), r.BboxMax.Z - r.BboxMin.Z, 6);
        }

        /// <summary>
        /// Elevacao paralela ao eixo Y: observador em X- olhando para +X.
        /// direita=+Y, up=+Z, viewDir=+X. Comportamento v2.3.0+.
        /// </summary>
        [Fact]
        public void Elevacao_ParaleloEixoY_RegressionV264()
        {
            Vec3 bbMin = new Vec3(0, 0, 0);
            Vec3 bbMax = new Vec3(Mm(15000), Mm(8000), Mm(6000));
            double margemFt = Mm(500);

            var r = SectionBoxBuilder.CalcularElevacao(bbMin, bbMax, margemFt, paraleloAoX: false);

            // Origem = mesmo centro
            AssertVecEqual(new Vec3(Mm(7500), Mm(4000), Mm(3000)), r.OrigemTransform);

            // Transform: direita=+Y, up=+Z, viewDir=+X
            AssertVecEqual(new Vec3(0, 1, 0), r.BasisX);
            AssertVecEqual(new Vec3(0, 0, 1), r.BasisY);
            AssertVecEqual(new Vec3(1, 0, 0), r.BasisZ);

            // Bbox local:
            //  largura (X local = Y mundial) = extY + 2*margem = 8000 + 1000 = 9000mm
            //  altura (Y local = Z mundial) = extZ + 2*margem = 6000 + 1000 = 7000mm
            //  profundidade (Z local = X mundial) = extX + 2*margem = 15000 + 1000 = 16000mm
            Assert.Equal(Mm(9000), r.BboxMax.X - r.BboxMin.X, 6);
            Assert.Equal(Mm(7000), r.BboxMax.Y - r.BboxMin.Y, 6);
            Assert.Equal(Mm(16000), r.BboxMax.Z - r.BboxMin.Z, 6);
        }
    }
}
