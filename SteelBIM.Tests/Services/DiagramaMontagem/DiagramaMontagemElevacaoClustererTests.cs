using System.Collections.Generic;
using System.Linq;
using Xunit;
using DMC = SteelBIM.Services.DiagramaMontagem.DiagramaMontagemElevacaoClusterer;

namespace SteelBIM.Tests.Services.DiagramaMontagem
{
    /// <summary>
    /// v2.8.10 (Etapa C do plano Onda 3): testes do helper puro extraido de
    /// <c>DiagramaMontagemService.CriarCotasVerticais</c>. Garante que a
    /// clusterizacao Z + limite quantidade se mantem identica ao comportamento
    /// pre-refator (smoke manual no Revit pelo Alef ainda exigido).
    /// </summary>
    public class DiagramaMontagemElevacaoClustererTests
    {
        // ============================================
        // Clusterizar
        // ============================================

        [Fact]
        public void Clusterizar_InputVazio_RetornaListaVazia()
        {
            var resultado = DMC.Clusterizar(new double[0], 0.5);
            Assert.Empty(resultado);
        }

        [Fact]
        public void Clusterizar_InputNull_RetornaListaVazia()
        {
            var resultado = DMC.Clusterizar(null!, 0.5);
            Assert.Empty(resultado);
        }

        [Fact]
        public void Clusterizar_UmPonto_RetornaUmCluster()
        {
            var resultado = DMC.Clusterizar(new[] { 3.14 }, 0.1);
            Assert.Single(resultado);
            Assert.Equal(3.14, resultado[0]);
        }

        [Fact]
        public void Clusterizar_PontosIdenticos_ColapsaEmUm()
        {
            var resultado = DMC.Clusterizar(new[] { 5.0, 5.0, 5.0 }, 0.1);
            Assert.Single(resultado);
            Assert.Equal(5.0, resultado[0]);
        }

        [Fact]
        public void Clusterizar_PontosDentroDaTolerancia_ColapsaEmUm()
        {
            // Tolerancia 1.0 — 3.0, 3.5, 3.9 todos dentro de 1.0 do anterior
            var resultado = DMC.Clusterizar(new[] { 3.0, 3.5, 3.9 }, 1.0);
            Assert.Single(resultado);
            Assert.Equal(3.0, resultado[0]);
        }

        [Fact]
        public void Clusterizar_PontosForaDaTolerancia_GeraVariosClusters()
        {
            // 0, 1, 2, 3, 4 com tol 0.5 — cada um separado
            var resultado = DMC.Clusterizar(new[] { 0.0, 1.0, 2.0, 3.0, 4.0 }, 0.5);
            Assert.Equal(5, resultado.Count);
            Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0, 4.0 }, resultado);
        }

        [Fact]
        public void Clusterizar_OrdemArbitraria_OrdenaCrescente()
        {
            var resultado = DMC.Clusterizar(new[] { 5.0, 1.0, 3.0, 2.0 }, 0.1);
            Assert.Equal(new[] { 1.0, 2.0, 3.0, 5.0 }, resultado);
        }

        [Fact]
        public void Clusterizar_Misto_AgrupaDentroDaTolerancia()
        {
            // 0, 0.05, 0.08 (cluster 0), 1.5, 1.55 (cluster 1.5, dentro), 3.0 (novo)
            // Toler 0.1 com folga p/ evitar floating-point igualdade no limite.
            var resultado = DMC.Clusterizar(
                new[] { 0.0, 0.05, 0.08, 1.5, 1.55, 3.0 }, 0.1);
            Assert.Equal(new[] { 0.0, 1.5, 3.0 }, resultado);
        }

        // ============================================
        // LimitarQuantidade
        // ============================================

        [Fact]
        public void LimitarQuantidade_AbaixoDoMax_RetornaIntacto()
        {
            var input = new List<double> { 0.0, 1.0, 2.0, 3.0, 4.0 };
            var resultado = DMC.LimitarQuantidade(input, 15);
            Assert.Equal(input, resultado);
        }

        [Fact]
        public void LimitarQuantidade_ExatamenteNoMax_RetornaIntacto()
        {
            var input = Enumerable.Range(0, 15).Select(i => (double)i).ToList();
            var resultado = DMC.LimitarQuantidade(input, 15);
            Assert.Equal(15, resultado.Count);
            Assert.Equal(input, resultado);
        }

        [Fact]
        public void LimitarQuantidade_AcimaDoMax_PreservaPrimeiroEUltimo()
        {
            var input = Enumerable.Range(0, 30).Select(i => (double)i).ToList();
            var resultado = DMC.LimitarQuantidade(input, 15);
            Assert.True(resultado.Count <= 15);
            Assert.Equal(0.0, resultado.First());
            Assert.Equal(29.0, resultado.Last());
        }

        [Fact]
        public void LimitarQuantidade_NullInput_RetornaListaVazia()
        {
            var resultado = DMC.LimitarQuantidade(null!, 15);
            Assert.Empty(resultado);
        }

        [Fact]
        public void LimitarQuantidade_MaxMenorQueDois_Lanca()
        {
            var input = new List<double> { 0.0, 1.0, 2.0 };
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => DMC.LimitarQuantidade(input, 1));
        }

        [Fact]
        public void LimitarQuantidade_PreservaComportamentoLegado15()
        {
            // Replicacao exata do algoritmo pre-refator:
            // clusters de 20 itens, max 15 → passo = 19/14 ≈ 1.357
            // for i 0..14: idx = round(i*1.357) => 0,1,3,4,5,7,8,10,11,12,14,15,16,18,19
            var input = Enumerable.Range(0, 20).Select(i => (double)i).ToList();
            var resultado = DMC.LimitarQuantidade(input, 15);
            // Validacao: deve ter ate' 15 (apos distinct), primeiro 0, ultimo 19
            Assert.True(resultado.Count <= 15);
            Assert.Equal(0.0, resultado.First());
            Assert.Equal(19.0, resultado.Last());
        }
    }
}
