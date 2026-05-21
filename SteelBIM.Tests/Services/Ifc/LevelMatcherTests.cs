using SteelBIM.Services.Ifc;
using Xunit;

namespace SteelBIM.Tests.Services.Ifc
{
    /// <summary>
    /// Testes puros do <see cref="LevelMatcherPure"/> (BUG 2 v2.7.0):
    /// atribuir nivel mais proximo do Z do bbox em vez de cair no fallback
    /// fixo que descobiquetava pecas em pisos diferentes.
    /// </summary>
    public class LevelMatcherTests
    {
        private const double MmToFt = 1.0 / 304.8;
        private static double Mm(double mm) => mm * MmToFt;

        [Fact]
        public void Vazio_RetornaMenos1()
        {
            Assert.Equal(-1, LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(new double[0], 100));
            Assert.Equal(-1, LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(null, 100));
        }

        [Theory]
        [InlineData(0, new[] { 0.0, 3000.0, 6000.0 }, 0)]      // exato no piso 0
        [InlineData(3000, new[] { 0.0, 3000.0, 6000.0 }, 1)]   // exato no piso 1
        [InlineData(6000, new[] { 0.0, 3000.0, 6000.0 }, 2)]   // exato no piso 2
        [InlineData(1400, new[] { 0.0, 3000.0, 6000.0 }, 0)]   // mais perto do 0
        [InlineData(1600, new[] { 0.0, 3000.0, 6000.0 }, 1)]   // mais perto do 1
        [InlineData(4400, new[] { 0.0, 3000.0, 6000.0 }, 1)]   // mais perto do 1
        [InlineData(4600, new[] { 0.0, 3000.0, 6000.0 }, 2)]   // mais perto do 2
        public void EncontraIndiceMaisProximo(double zMm, double[] elevsMm, int indiceEsperado)
        {
            double[] elevsFt = new double[elevsMm.Length];
            for (int i = 0; i < elevsMm.Length; i++)
                elevsFt[i] = Mm(elevsMm[i]);

            int idx = LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevsFt, Mm(zMm));
            Assert.Equal(indiceEsperado, idx);
        }

        [Fact]
        public void Empate_RetornaIndiceMenor_Determinismo()
        {
            // Z exatamente no meio entre 0 e 3000 -> 1500mm. Empate. Deve retornar 0.
            double[] elevsFt = new[] { Mm(0), Mm(3000) };
            int idx = LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevsFt, Mm(1500));
            Assert.Equal(0, idx);
        }

        [Fact]
        public void NivelUnico_SempreRetornaZero()
        {
            double[] elevsFt = new[] { Mm(0) };
            Assert.Equal(0, LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevsFt, Mm(0)));
            Assert.Equal(0, LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevsFt, Mm(50000)));
            Assert.Equal(0, LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevsFt, Mm(-1000)));
        }
    }
}
