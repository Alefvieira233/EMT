#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) para calculos geometricos sobre
    /// listas de pontos 2D projetados no plano da trelica (X = ao longo do banzo,
    /// Y = vertical). Todas as unidades em pes (coerente com Revit internal units).
    /// </summary>
    /// <remarks>
    /// Responsavel por:
    /// - Extrair painoes (paneis) de um banzo a partir dos nos ordenados.
    /// - Calcular altura de cada montante (distancia vertical entre os dois banzos na mesma estacao).
    /// - Detectar pontos de apoio (extremidades dos banzos).
    /// Ver secao 6.2 do docs/PLANO-LAPIDACAO.md.
    /// </remarks>
    public static class TrelicaGeometria
    {
        /// <summary>Tolerancia para igualdade entre coordenadas (1 mm ~ 0.00328 pes).</summary>
        public const double TolPesIgualdade = 0.00328;

        /// <summary>
        /// Dado uma lista de coordenadas X dos nos ao longo de um banzo, retorna
        /// a lista de larguras dos paineis (distancia entre nos consecutivos).
        /// Pontos duplicados (dentro de <see cref="TolPesIgualdade"/>) sao removidos.
        /// </summary>
        /// <param name="xNos">Coordenadas X dos nos (pode estar fora de ordem).</param>
        /// <returns>Lista de larguras dos paineis, em pes, na ordem do banzo.</returns>
        public static IReadOnlyList<double> LarguraDosPaineis(IEnumerable<double> xNos)
        {
            if (xNos is null) throw new ArgumentNullException(nameof(xNos));
            var ordenados = xNos.OrderBy(x => x).ToList();
            var limpos = new List<double>();
            foreach (var x in ordenados)
            {
                if (limpos.Count == 0 || Math.Abs(x - limpos[^1]) > TolPesIgualdade)
                    limpos.Add(x);
            }
            var paineis = new List<double>();
            for (int i = 1; i < limpos.Count; i++)
                paineis.Add(limpos[i] - limpos[i - 1]);
            return paineis;
        }

        /// <summary>
        /// Calcula o vao total (diferenca entre X minimo e X maximo) da lista de nos.
        /// </summary>
        public static double VaoTotal(IEnumerable<double> xNos)
        {
            if (xNos is null) throw new ArgumentNullException(nameof(xNos));
            var lista = xNos.ToList();
            if (lista.Count < 2) return 0.0;
            return lista.Max() - lista.Min();
        }

        /// <summary>
        /// Dada uma lista de estacoes X (posicao dos montantes), e duas funcoes que
        /// retornam Z do banzo superior e banzo inferior em cada X, calcula a
        /// altura (diferenca) em cada estacao.
        /// </summary>
        public static IReadOnlyList<double> AlturasPorEstacao(
            IEnumerable<double> xEstacoes,
            Func<double, double> zSuperior,
            Func<double, double> zInferior)
        {
            if (xEstacoes is null) throw new ArgumentNullException(nameof(xEstacoes));
            if (zSuperior is null) throw new ArgumentNullException(nameof(zSuperior));
            if (zInferior is null) throw new ArgumentNullException(nameof(zInferior));
            return xEstacoes.Select(x => Math.Max(0.0, zSuperior(x) - zInferior(x))).ToList();
        }

        /// <summary>
        /// Retorna o X do apoio esquerdo e do apoio direito — tipicamente os
        /// extremos do banzo inferior. Lanca se a lista tiver menos de 2 pontos.
        /// </summary>
        public static (double xEsq, double xDir) ExtremosApoio(IEnumerable<double> xBanzoInferior)
        {
            if (xBanzoInferior is null) throw new ArgumentNullException(nameof(xBanzoInferior));
            var lista = xBanzoInferior.ToList();
            if (lista.Count < 2)
                throw new ArgumentException("Banzo inferior precisa de pelo menos 2 nos.", nameof(xBanzoInferior));
            return (lista.Min(), lista.Max());
        }
    }
}
