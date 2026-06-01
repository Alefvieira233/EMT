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
    }
}
