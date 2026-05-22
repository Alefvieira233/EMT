using System;
using System.Collections.Generic;
using SteelBIM.Services.DiagramaMontagem;
using SteelBIM.Services.Ifc;
using Xunit;

namespace SteelBIM.Tests.Services.Ifc
{
    /// <summary>
    /// Testes puros do <see cref="SectionOrientationExtractor"/> (v2.7.4):
    /// extracao do vetor de referencia da secao transversal a partir de
    /// <see cref="FaceData"/> + eixo, usado por
    /// <c>ConverterPerfilIfcService.TentarAplicarRotacaoSecao</c> para
    /// preservar rotacao IFC ao criar FamilyInstance nativa.
    ///
    /// Helper original do Victor (co-autor 50/50). Suite valida criterio
    /// "face lateral" (|dot(n, eixo)| <= 0.85) + maior-area-vence + projecao
    /// no plano perpendicular ao eixo.
    /// </summary>
    public class SectionOrientationExtractorTests
    {
        private const double Tol = 1e-6;

        private static Vec3 Norm(double x, double y, double z)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            return new Vec3(x / len, y / len, z / len);
        }

        // ============================================================
        // Theory: 3 eixos + faces validas + caps -> retorna projecao da maior
        // ============================================================

        public static IEnumerable<object[]> EixosDosVerticais => new List<object[]>
        {
            // (eixo, normalLateralMaior, normalEsperada)
            new object[] { new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 0, 1) }, // eixo X
            new object[] { new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(1, 0, 0) }, // eixo Y
            new object[] { new Vec3(0, 0, 1), new Vec3(0, 1, 0), new Vec3(0, 1, 0) }, // eixo Z
        };

        [Theory]
        [MemberData(nameof(EixosDosVerticais))]
        public void ExtrairReferenciaSecao_FacesValidas_RetornaNormalDaMaior(
            Vec3 eixo, Vec3 normalLateralMaior, Vec3 normalEsperada)
        {
            var faces = new List<FaceData>
            {
                // 2 caps (normal paralela ao eixo) — devem ser excluidos
                new FaceData(new Vec3(0, 0, 0), eixo, 100.0),
                new FaceData(new Vec3(0, 0, 0), new Vec3(-eixo.X, -eixo.Y, -eixo.Z), 100.0),

                // 4 faces laterais (normais perp ao eixo). A de maior area = `normalLateralMaior`
                new FaceData(new Vec3(0, 0, 0), normalLateralMaior, 500.0),
                new FaceData(new Vec3(0, 0, 0), new Vec3(-normalLateralMaior.X, -normalLateralMaior.Y, -normalLateralMaior.Z), 480.0),
                new FaceData(new Vec3(0, 0, 0), Norm(0.0, 0.7, 0.0), 200.0),
                new FaceData(new Vec3(0, 0, 0), Norm(0.0, -0.7, 0.0), 200.0),
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);

            Assert.True(r.HasValue);
            // A normal escolhida ja esta perp ao eixo entao projecao = normal
            Assert.Equal(normalEsperada.X, r.Value.X, 6);
            Assert.Equal(normalEsperada.Y, r.Value.Y, 6);
            Assert.Equal(normalEsperada.Z, r.Value.Z, 6);
        }

        // ============================================================
        // Threshold dot > 0.85 (cos ~32 graus) exclui caps
        // ============================================================

        [Fact]
        public void FaceCap_PerpendicularAoEixo_Excluida_RetornaNull()
        {
            Vec3 eixo = new Vec3(1, 0, 0);
            // Unica face: normal paralela ao eixo (dot=1.0 > 0.85) — cap, deve ser excluida
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), eixo, 500.0),
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);
            Assert.Null(r);
        }

        [Fact]
        public void FaceComDotIgualA85_PassaThreshold()
        {
            // dot exato = 0.85 -> EXCLUIDA (threshold e estritamente > 0.85)
            // Mas dot = 0.84 -> ACEITA (face lateral valida)
            Vec3 eixo = new Vec3(1, 0, 0);
            Vec3 normalAceitavel = Norm(0.84, 0.5426, 0); // dot=0.84 com eixo
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), normalAceitavel, 500.0),
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);
            Assert.True(r.HasValue);
        }

        // ============================================================
        // Maior area vence entre faces laterais validas
        // ============================================================

        [Fact]
        public void MaiorAreaDentreFacesValidas_Vence()
        {
            Vec3 eixo = new Vec3(0, 0, 1);
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), new Vec3(1, 0, 0), 100.0),  // pequena
                new FaceData(new Vec3(0, 0, 0), new Vec3(0, 1, 0), 500.0),  // GRANDE
                new FaceData(new Vec3(0, 0, 0), new Vec3(-1, 0, 0), 200.0), // media
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);

            Assert.True(r.HasValue);
            // Maior area -> normal (0, 1, 0)
            Assert.Equal(0, r.Value.X, 6);
            Assert.Equal(1, r.Value.Y, 6);
            Assert.Equal(0, r.Value.Z, 6);
        }

        // ============================================================
        // Edge cases — defensivos
        // ============================================================

        [Fact]
        public void TodasFacesCaps_RetornaNull()
        {
            // Todas as faces tem normal paralela ao eixo (dot > 0.85)
            Vec3 eixo = new Vec3(1, 0, 0);
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), eixo, 100.0),
                new FaceData(new Vec3(0, 0, 0), new Vec3(-1, 0, 0), 100.0),
                new FaceData(new Vec3(0, 0, 0), Norm(0.95, 0.3122, 0), 50.0), // dot=0.95
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);
            Assert.Null(r);
        }

        [Fact]
        public void ListaVazia_RetornaNull()
        {
            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(
                new List<FaceData>(), new Vec3(1, 0, 0));
            Assert.Null(r);
        }

        [Fact]
        public void ListaNull_RetornaNull()
        {
            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(
                null, new Vec3(1, 0, 0));
            Assert.Null(r);
        }

        [Fact]
        public void FaceComAreaZero_Ignorada()
        {
            Vec3 eixo = new Vec3(0, 0, 1);
            var faces = new List<FaceData>
            {
                // Maior area mas degenerada (zero) — deve ser ignorada
                new FaceData(new Vec3(0, 0, 0), new Vec3(1, 0, 0), 0.0),
                // Menor area mas valida — deve vencer
                new FaceData(new Vec3(0, 0, 0), new Vec3(0, 1, 0), 100.0),
            };

            Vec3? r = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixo);

            Assert.True(r.HasValue);
            Assert.Equal(0, r.Value.X, 6);
            Assert.Equal(1, r.Value.Y, 6);
            Assert.Equal(0, r.Value.Z, 6);
        }
    }
}
