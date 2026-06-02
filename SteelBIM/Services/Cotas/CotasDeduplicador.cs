#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelBIM.Services.Cotas
{
    /// <summary>
    /// v2.8.11 (Onda D): logica PURA de deduplicacao de referencias de cota por posicao,
    /// extraida de <c>CotasService.RemoverDuplicadasPorPosicao</c> (que era Revit-bound e
    /// sem testes). Decide QUAIS referencias manter ao longo da linha de cota:
    ///   1. ordena por posicao;
    ///   2. descarta chave repetida (mesma referencia geometrica);
    ///   3. descarta posicoes dentro da tolerancia da ultima MANTIDA (evita cota colada).
    /// </summary>
    public static class CotasDeduplicador
    {
        /// <summary>
        /// Retorna os INDICES (na lista original <paramref name="itens"/>) a manter, em ordem
        /// crescente de posicao. Espelha exatamente o algoritmo original.
        /// </summary>
        public static List<int> IndicesAManter(
            IReadOnlyList<(double Posicao, string Chave)> itens,
            double tolerancia)
        {
            List<int> manter = new List<int>();
            if (itens == null || itens.Count == 0)
                return manter;

            List<int> ordem = Enumerable.Range(0, itens.Count)
                .OrderBy(i => itens[i].Posicao)
                .ToList();

            HashSet<string> chaves = new HashSet<string>(StringComparer.Ordinal);
            bool temUltima = false;
            double ultimaPos = 0.0;

            foreach (int i in ordem)
            {
                string chave = itens[i].Chave ?? string.Empty;
                // chave consumida mesmo que a posicao depois rejeite (igual ao original).
                if (!chaves.Add(chave))
                    continue;

                if (temUltima && Math.Abs(ultimaPos - itens[i].Posicao) <= tolerancia)
                    continue;

                manter.Add(i);
                ultimaPos = itens[i].Posicao;
                temUltima = true;
            }

            return manter;
        }
    }
}
