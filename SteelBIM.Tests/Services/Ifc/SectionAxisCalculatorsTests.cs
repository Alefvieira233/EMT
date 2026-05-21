using System;
using System.Collections.Generic;
using SteelBIM.Services.DiagramaMontagem;
using SteelBIM.Services.Ifc;
using Xunit;

namespace SteelBIM.Tests.Services.Ifc
{
    /// <summary>
    /// Testes puros (sem Revit) dos calculadores de eixo usados pelo
    /// <c>SectionAxisExtractor</c> (BUG 1 v2.7.0): caps planares como
    /// primario + PCA como fallback. Critico: preserva inclinacao 3D real,
    /// vs approach legado AABB world-aligned que destruia diagonais.
    /// </summary>
    public class SectionAxisCalculatorsTests
    {
        private const double Tol = 1e-6;
        private const double MmToFt = 1.0 / 304.8;
        private static double Mm(double mm) => mm * MmToFt;

        // ============================================================
        // Helpers para gerar geometria de teste sintetica
        // ============================================================

        /// <summary>
        /// Gera 8 vertices de um paralelepipedo orientado: centro em
        /// <paramref name="origem"/>, eixo principal ao longo de <paramref name="eixoDir"/>
        /// com comprimento <paramref name="comp"/>, e seccao quadrada <paramref name="largura"/>.
        /// </summary>
        private static List<Vec3> GerarVerticesParalelepipedo(
            Vec3 origem, Vec3 eixoDir, double comp, double largura)
        {
            // Construir base ortogonal: eixoDir + 2 vetores perp
            Vec3 dir = eixoDir.Normalize();
            Vec3 up = new Vec3(0, 0, 1);
            // Se dir == up, usar X como fallback
            double dotUp = dir.X * up.X + dir.Y * up.Y + dir.Z * up.Z;
            if (Math.Abs(dotUp) > 0.99)
                up = new Vec3(1, 0, 0);
            Vec3 perp1 = dir.Cross(up).Normalize();
            Vec3 perp2 = dir.Cross(perp1).Normalize();

            double meioC = comp / 2.0;
            double meioL = largura / 2.0;
            var v = new List<Vec3>(8);
            for (int sc = -1; sc <= 1; sc += 2)
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        v.Add(origem + dir * (sc * meioC) + perp1 * (s1 * meioL) + perp2 * (s2 * meioL));
                    }
            return v;
        }

        /// <summary>
        /// Gera 2 PlanarFaces extremas (caps) de um paralelepipedo orientado:
        /// uma em -dir, outra em +dir, ambas perpendiculares ao eixo, com
        /// areas iguais.
        /// </summary>
        private static List<FaceData> GerarCapsParalelepipedo(
            Vec3 origem, Vec3 eixoDir, double comp, double largura)
        {
            Vec3 dir = eixoDir.Normalize();
            double meioC = comp / 2.0;
            double area = largura * largura;
            return new List<FaceData>
            {
                new FaceData(origem + dir * -meioC, dir * -1, area),
                new FaceData(origem + dir * meioC, dir, area),
            };
        }

        // ============================================================
        // CapsAxisCalculator — approach principal
        // ============================================================

        [Fact]
        public void Caps_PecaHorizontalX_ExtraiLinhaXAtoX()
        {
            Vec3 origem = new Vec3(Mm(500), 0, 0);
            List<FaceData> caps = GerarCapsParalelepipedo(origem, new Vec3(1, 0, 0), Mm(1000), Mm(75));

            AxisLineData? r = CapsAxisCalculator.Identificar(caps);
            Assert.True(r.HasValue);

            // Endpoints proximos de (0,0,0) e (1000mm,0,0)
            Vec3 s = r.Value.Start;
            Vec3 e = r.Value.End;
            // Ordem nao garantida, mas distancia total deve ser ~1000mm
            Assert.Equal(Mm(1000), (e - s).Length, 6);
        }

        [Fact]
        public void Caps_PecaVerticalZ_PreservaDirecao()
        {
            Vec3 origem = new Vec3(0, 0, Mm(750));
            List<FaceData> caps = GerarCapsParalelepipedo(origem, new Vec3(0, 0, 1), Mm(1500), Mm(100));

            AxisLineData? r = CapsAxisCalculator.Identificar(caps);
            Assert.True(r.HasValue);

            // Eixo deve estar ao longo de Z
            Vec3 axis = (r.Value.End - r.Value.Start).Normalize();
            Assert.Equal(0, axis.X, 6);
            Assert.Equal(0, axis.Y, 6);
            Assert.Equal(1, Math.Abs(axis.Z), 6);
        }

        [Theory]
        [InlineData(30.0)]  // Diagonal 30 graus
        [InlineData(45.0)]  // Diagonal 45 graus (caso classico bug Victor)
        [InlineData(60.0)]  // Diagonal 60 graus
        public void Caps_DiagonalInclinada_PreservaAngulo(double graus)
        {
            double rad = graus * Math.PI / 180.0;
            Vec3 eixo = new Vec3(Math.Cos(rad), 0, Math.Sin(rad)).Normalize();
            List<FaceData> caps = GerarCapsParalelepipedo(new Vec3(0, 0, 0), eixo, Mm(1000), Mm(75));

            AxisLineData? r = CapsAxisCalculator.Identificar(caps);
            Assert.True(r.HasValue);

            Vec3 axisOut = (r.Value.End - r.Value.Start).Normalize();
            // Anglo entre axisOut e eixo esperado deve ser ~0 ou ~180
            double dot = Math.Abs(axisOut.X * eixo.X + axisOut.Y * eixo.Y + axisOut.Z * eixo.Z);
            Assert.True(dot > 0.999, $"Angulo {graus}deg nao preservado: dot={dot}");
        }

        [Fact]
        public void Caps_FacesNaoAntiParalelas_RetornaNull()
        {
            // 2 faces com normais perpendiculares (nao caps validos)
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), new Vec3(1, 0, 0), Mm(75) * Mm(75)),
                new FaceData(new Vec3(0, Mm(100), 0), new Vec3(0, 1, 0), Mm(75) * Mm(75)),
            };

            AxisLineData? r = CapsAxisCalculator.Identificar(faces);
            Assert.Null(r);
        }

        [Fact]
        public void Caps_AreasMuitoDiferentes_RetornaNull()
        {
            // Faces anti-paralelas mas areas com diff > 30%
            var faces = new List<FaceData>
            {
                new FaceData(new Vec3(0, 0, 0), new Vec3(-1, 0, 0), Mm(75) * Mm(75)),
                new FaceData(new Vec3(Mm(1000), 0, 0), new Vec3(1, 0, 0), Mm(75) * Mm(75) * 0.5),  // metade da area
            };

            AxisLineData? r = CapsAxisCalculator.Identificar(faces);
            Assert.Null(r);
        }

        [Fact]
        public void Caps_ListaVazia_RetornaNull()
        {
            Assert.Null(CapsAxisCalculator.Identificar(new List<FaceData>()));
            Assert.Null(CapsAxisCalculator.Identificar(null));
        }

        // ============================================================
        // PcaAxisCalculator — fallback
        // ============================================================

        [Fact]
        public void Pca_PecaHorizontalX_ExtraiEixoX()
        {
            // 8 vertices de paralelepipedo horizontal X 1000x75x75
            var verts = GerarVerticesParalelepipedo(new Vec3(Mm(500), 0, 0), new Vec3(1, 0, 0), Mm(1000), Mm(75));

            AxisLineData? r = PcaAxisCalculator.CalcularEixoPrincipal(verts);
            Assert.True(r.HasValue);

            Vec3 axis = (r.Value.End - r.Value.Start).Normalize();
            Assert.Equal(1, Math.Abs(axis.X), 5);
            Assert.Equal(0, axis.Y, 5);
            Assert.Equal(0, axis.Z, 5);
        }

        [Fact]
        public void Pca_DiagonalInclinada45deg_PreservaAngulo()
        {
            Vec3 eixo = new Vec3(Math.Cos(Math.PI / 4), 0, Math.Sin(Math.PI / 4));
            var verts = GerarVerticesParalelepipedo(new Vec3(0, 0, 0), eixo, Mm(1000), Mm(75));

            AxisLineData? r = PcaAxisCalculator.CalcularEixoPrincipal(verts);
            Assert.True(r.HasValue);

            Vec3 axisOut = (r.Value.End - r.Value.Start).Normalize();
            double dot = Math.Abs(axisOut.X * eixo.X + axisOut.Y * eixo.Y + axisOut.Z * eixo.Z);
            Assert.True(dot > 0.99, $"PCA degradou angulo: dot={dot}");

            // Comprimento extraido deve estar proximo de 1000mm (com margem por
            // PCA projetar ate cantos do bbox, nao exatamente as faces)
            double comp = (r.Value.End - r.Value.Start).Length;
            Assert.InRange(comp, Mm(900), Mm(1100));
        }

        [Fact]
        public void Pca_GeometriaIrregular_RetornaEixoDominante()
        {
            // Nuvem alongada em X com ruido pequeno em Y e Z
            var verts = new List<Vec3>();
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                double x = Mm(i * 10);  // 0..1000mm em X
                double y = Mm((rng.NextDouble() - 0.5) * 20);  // +-10mm ruido
                double z = Mm((rng.NextDouble() - 0.5) * 20);
                verts.Add(new Vec3(x, y, z));
            }

            AxisLineData? r = PcaAxisCalculator.CalcularEixoPrincipal(verts);
            Assert.True(r.HasValue);

            Vec3 axis = (r.Value.End - r.Value.Start).Normalize();
            Assert.True(Math.Abs(axis.X) > 0.95, $"PCA nao identificou X como dominante: {axis.X}");
        }

        [Fact]
        public void Pca_VerticesColapsados_RetornaNull()
        {
            // Todos os vertices no mesmo ponto
            Vec3 p = new Vec3(1, 2, 3);
            var verts = new List<Vec3> { p, p, p, p };
            Assert.Null(PcaAxisCalculator.CalcularEixoPrincipal(verts));
        }

        [Fact]
        public void Pca_MenosDe2Vertices_RetornaNull()
        {
            Assert.Null(PcaAxisCalculator.CalcularEixoPrincipal(new List<Vec3>()));
            Assert.Null(PcaAxisCalculator.CalcularEixoPrincipal(new List<Vec3> { new Vec3(0, 0, 0) }));
            Assert.Null(PcaAxisCalculator.CalcularEixoPrincipal(null));
        }
    }
}
