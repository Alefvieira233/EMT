using System.Collections.Generic;
using FluentAssertions;
using SteelBIM.Services.Cotas;
using Xunit;

namespace SteelBIM.Tests.Services.Cotas
{
    /// <summary>
    /// v2.8.11 (Onda D): testes da deduplicacao pura de referencias de cota
    /// (antes embutida em CotasService, Revit-bound, sem cobertura).
    /// </summary>
    public class CotasDeduplicadorTests
    {
        [Fact]
        public void Vazio_RetornaVazio()
        {
            CotasDeduplicador.IndicesAManter(new List<(double, string)>(), 0.5).Should().BeEmpty();
        }

        [Fact]
        public void OrdenaPorPosicao_ERetornaIndicesOriginais()
        {
            var itens = new List<(double, string)> { (30.0, "c"), (10.0, "a"), (20.0, "b") };
            // tolerancia pequena: nada colado -> mantem os 3, em ordem de posicao (10,20,30)
            CotasDeduplicador.IndicesAManter(itens, 0.001).Should().Equal(1, 2, 0);
        }

        [Fact]
        public void ChaveRepetida_Descartada()
        {
            var itens = new List<(double, string)> { (10.0, "a"), (20.0, "a"), (30.0, "b") };
            // segunda 'a' (pos 20) descartada por chave repetida -> mantem indices 0 e 2
            CotasDeduplicador.IndicesAManter(itens, 0.001).Should().Equal(0, 2);
        }

        [Fact]
        public void PosicoesDentroDaTolerancia_Coladas_Descartadas()
        {
            var itens = new List<(double, string)> { (10.0, "a"), (10.3, "b"), (10.6, "c"), (50.0, "d") };
            // tolerancia 0.5: 10.3 (|10-10.3|<=0.5) descartada; 10.6 vs ultima MANTIDA (10.0):
            // |10-10.6|=0.6>0.5 -> mantida; 50 mantida. -> indices 0, 2, 3
            CotasDeduplicador.IndicesAManter(itens, 0.5).Should().Equal(0, 2, 3);
        }

        [Fact]
        public void ChaveConsumidaMesmoQuandoPosicaoRejeita()
        {
            // espelha o original: Add(chave) acontece antes do teste de posicao.
            var itens = new List<(double, string)> { (10.0, "a"), (10.2, "b"), (10.25, "b") };
            // 10.2 'b' colada em 10.0 -> descartada por posicao, mas chave 'b' ja' consumida;
            // 10.25 'b' -> chave repetida -> descartada. Mantem so o indice 0.
            CotasDeduplicador.IndicesAManter(itens, 0.5).Should().Equal(0);
        }

        [Fact]
        public void ChaveNula_TratadaComoVazia()
        {
            var itens = new List<(double, string)> { (10.0, null!), (50.0, null!) };
            // ambas chave "" -> segunda descartada por chave repetida
            CotasDeduplicador.IndicesAManter(itens, 0.001).Should().Equal(0);
        }
    }
}
