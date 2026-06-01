using System.Collections.Generic;
using System.Linq;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    /// <summary>
    /// v2.8.10 (Etapa D): testes do helper puro de contorno AK. Garante que
    /// o contorno produzido pelo extrator Revit-bound (DstvHeaderBuilder) e'
    /// SEMPRE fechado antes de chegar no DstvFileWriter (que confia no input).
    /// </summary>
    public class DstvContornoAkBuilderTests
    {
        // ============================================
        // FecharContorno
        // ============================================

        [Fact]
        public void FecharContorno_Null_RetornaListaVazia()
        {
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(null);
            Assert.Empty(r);
        }

        [Fact]
        public void FecharContorno_Vazio_RetornaListaVazia()
        {
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(new List<(double, double, double)>());
            Assert.Empty(r);
        }

        [Fact]
        public void FecharContorno_JaFechado_NaoDuplica()
        {
            // Quadrado 100x100 ja' com o primeiro repetido no fim.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,   0.0,   0.0),
                (100.0, 0.0,   0.0),
                (100.0, 100.0, 0.0),
                (0.0,   100.0, 0.0),
                (0.0,   0.0,   0.0),
            };
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(input);
            Assert.Equal(5, r.Count);
            Assert.Equal((0.0, 0.0, 0.0), r[0]);
            Assert.Equal((0.0, 0.0, 0.0), r[4]);
        }

        [Fact]
        public void FecharContorno_Aberto_AdicionaPrimeiroNoFim()
        {
            // Quadrado 100x100 SEM fechamento.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,   0.0,   0.0),
                (100.0, 0.0,   0.0),
                (100.0, 100.0, 0.0),
                (0.0,   100.0, 0.0),
            };
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(input);
            Assert.Equal(5, r.Count);
            Assert.Equal((0.0, 0.0, 0.0), r[0]);
            Assert.Equal((0.0, 0.0, 0.0), r[4]);
        }

        [Fact]
        public void FecharContorno_ToleranciaRespeitada()
        {
            // Primeiro e ultimo a 0.0005mm de distancia (dentro da tol default 0.001) → ja' fechado.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,    0.0,    0.0),
                (100.0,  0.0,    0.0),
                (0.0005, 0.0005, 0.0),
            };
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(input);
            Assert.Equal(3, r.Count); // nao duplica
        }

        [Fact]
        public void FecharContorno_RetornaListaNova()
        {
            // Imutabilidade: a lista de entrada nao deve ser mutada.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,   0.0,   0.0),
                (100.0, 0.0,   0.0),
                (100.0, 100.0, 0.0),
            };
            int countAntes = input.Count;
            DstvContornoAkBuilder.FecharContorno(input);
            Assert.Equal(countAntes, input.Count);
        }

        [Fact]
        public void FecharContorno_PreservaRaios()
        {
            // Pontos com arcos (raio != 0): valor preservado nos pontos originais.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,    380.48,  0.0),
                (238.41, 0.0,     0.0),
                (620.0,  0.0,     0.0),
                (362.90, 349.90, -13.0), // arco
                (349.90, 362.90,  0.0),
            };
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.FecharContorno(input);
            // Quinto ponto (arco) deve manter raio -13
            (double X, double Y, double Raio) arco = r.First(p => p.Y == 349.90);
            Assert.Equal(-13.0, arco.Raio);
        }

        // ============================================
        // Retangulo
        // ============================================

        [Fact]
        public void Retangulo_ProduzCincoPontosFechado()
        {
            List<(double X, double Y, double Raio)> r = DstvContornoAkBuilder.Retangulo(620.0, 520.0);
            Assert.Equal(5, r.Count);
            Assert.Equal((0.0, 0.0, 0.0), r[0]);
            Assert.Equal((620.0, 0.0, 0.0), r[1]);
            Assert.Equal((620.0, 520.0, 0.0), r[2]);
            Assert.Equal((0.0, 520.0, 0.0), r[3]);
            Assert.Equal((0.0, 0.0, 0.0), r[4]); // fechado
        }

        // ============================================
        // Normalizar
        // ============================================

        // Contorno do CH02 (aberto, sem fechamento), na convencao alvo do fabricante:
        // canto minimo em (0,0), maior extensao (620) no X, winding anti-horario.
        private static List<(double X, double Y, double Raio)> Ch02Aberto() => new()
        {
            (0.0,    380.48,  0.0),
            (238.41, 0.0,     0.0),
            (620.0,  0.0,     0.0),
            (620.0,  349.90,  0.0),
            (362.90, 349.90,  0.0),
            (349.90, 362.90,  0.0),
            (349.90, 520.0,   0.0),
            (0.0,    520.0,   0.0),
        };

        private static double AreaAssinada(List<(double X, double Y, double Raio)> p)
        {
            double s = 0.0;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Count];
                s += (a.X * b.Y) - (b.X * a.Y);
            }
            return s / 2.0;
        }

        // Chave canonica de um ciclo: independe do ponto de inicio E do sentido.
        // Permite comparar dois contornos como o "mesmo poligono".
        private static string ChaveCiclica(List<(double X, double Y, double Raio)> p)
        {
            var b = p.Select(t => $"{t.X:F2},{t.Y:F2}").ToList();
            IEnumerable<string> Rot(List<string> xs) =>
                Enumerable.Range(0, xs.Count).Select(i => string.Join("|", xs.Skip(i).Concat(xs.Take(i))));
            var todas = Rot(b).ToList();
            var rev = Enumerable.Reverse(b).ToList();
            todas.AddRange(Rot(rev));
            return todas.Min()!;
        }

        [Fact]
        public void Normalizar_Null_RetornaListaVazia()
        {
            Assert.Empty(DstvContornoAkBuilder.Normalizar(null));
        }

        [Fact]
        public void Normalizar_TransladaParaCantoMinimo()
        {
            var input = new List<(double X, double Y, double Raio)>
            {
                (1000.0, 500.0, 0.0),
                (1620.0, 500.0, 0.0),
                (1620.0, 1020.0, 0.0),
                (1000.0, 1020.0, 0.0),
            };
            var r = DstvContornoAkBuilder.Normalizar(input);
            Assert.Equal(0.0, r.Min(p => p.X), 6);
            Assert.Equal(0.0, r.Min(p => p.Y), 6);
            Assert.Equal(620.0, r.Max(p => p.X), 6);
            Assert.Equal(520.0, r.Max(p => p.Y), 6);
        }

        [Fact]
        public void Normalizar_MaiorExtensaoVaiParaX()
        {
            // Retangulo "em pe": extensao Y (620) > X (520). Deve transpor.
            var input = new List<(double X, double Y, double Raio)>
            {
                (0.0,   0.0,   0.0),
                (520.0, 0.0,   0.0),
                (520.0, 620.0, 0.0),
                (0.0,   620.0, 0.0),
            };
            var r = DstvContornoAkBuilder.Normalizar(input);
            Assert.Equal(620.0, r.Max(p => p.X), 6); // comprimento agora no X
            Assert.Equal(520.0, r.Max(p => p.Y), 6);
        }

        [Fact]
        public void Normalizar_GaranteWindingAntiHorario()
        {
            // Entrada em sentido horario (CW).
            var cw = new List<(double X, double Y, double Raio)>
            {
                (0.0,   0.0,   0.0),
                (0.0,   520.0, 0.0),
                (620.0, 520.0, 0.0),
                (620.0, 0.0,   0.0),
            };
            Assert.True(AreaAssinada(cw) < 0, "pre-condicao: entrada deve ser horaria");
            var r = DstvContornoAkBuilder.Normalizar(cw);
            Assert.True(AreaAssinada(r) > 0, "saida deve ser anti-horaria (CCW)");
        }

        [Theory]
        // offset, swap (troca eixos), reverse (inverte winding)
        [InlineData(0.0, 0.0, false, false)]
        [InlineData(1000.0, -500.0, false, false)]
        [InlineData(-37.5, 12.3, true, false)]
        [InlineData(250.0, 80.0, false, true)]
        [InlineData(-10.0, -10.0, true, true)]
        public void Normalizar_RecuperaConvencaoDoFabricante_SobQualquerTransformacao(
            double ox, double oy, bool swap, bool reverse)
        {
            List<(double X, double Y, double Raio)> alvo = Ch02Aberto();

            // Aplica transformacao adversarial sobre o contorno alvo.
            var t = alvo.Select(p => (p.X + ox, p.Y + oy, p.Raio)).ToList();
            if (swap)
                t = t.Select(p => (p.Item2, p.Item1, p.Item3)).ToList();
            if (reverse)
                t.Reverse();

            var r = DstvContornoAkBuilder.Normalizar(t);

            // Mesma geometria do alvo (independente de inicio/sentido)...
            Assert.Equal(ChaveCiclica(alvo), ChaveCiclica(r));
            // ...e ja' na convencao: canto em (0,0), comprimento no X, CCW.
            Assert.Equal(0.0, r.Min(p => p.X), 6);
            Assert.Equal(0.0, r.Min(p => p.Y), 6);
            Assert.True(r.Max(p => p.X) >= r.Max(p => p.Y));
            Assert.True(AreaAssinada(r) > 0);
        }

        [Fact]
        public void Normalizar_Idempotente()
        {
            var uma = DstvContornoAkBuilder.Normalizar(Ch02Aberto());
            var duas = DstvContornoAkBuilder.Normalizar(uma);
            Assert.Equal(ChaveCiclica(uma), ChaveCiclica(duas));
            Assert.Equal(uma.Count, duas.Count);
        }
    }
}
