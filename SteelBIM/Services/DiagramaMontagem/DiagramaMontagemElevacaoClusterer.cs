#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelBIM.Services.DiagramaMontagem
{
    /// <summary>
    /// v2.8.10 — Logica pura extraida de <see cref="DiagramaMontagemService.CriarCotasVerticais"/>
    /// (Etapa C do plano Onda 3). Clusteriza pontos Z (em pes) por tolerancia e
    /// limita a quantidade de cotas pra nao poluir a vista.
    ///
    /// 100% sem dependencia de Autodesk.Revit.DB — testavel via xUnit. O service
    /// Revit-bound delega coleta de pontos + posicionamento de SpotElevation
    /// (a parte que precisa de Document/Element/Reference).
    /// </summary>
    public static class DiagramaMontagemElevacaoClusterer
    {
        /// <summary>Limite superior de cotas verticais — regra do plano para nao poluir.</summary>
        public const int MaxClustersDefault = 15;

        /// <summary>
        /// Agrupa pontos Z proximos dentro da tolerancia <paramref name="tolFt"/>.
        /// Aceita pontos em qualquer ordem; retorna ordenado crescente.
        /// </summary>
        /// <param name="pontosZ">Pontos Z brutos (em pes — Revit internal units).</param>
        /// <param name="tolFt">Tolerancia em pes; pontos com diff &lt;= tolFt caem no mesmo cluster.</param>
        /// <returns>Lista ordenada de Z's representativos dos clusters. Vazia se input vazio.</returns>
        public static List<double> Clusterizar(IEnumerable<double> pontosZ, double tolFt)
        {
            if (pontosZ == null)
                return new List<double>();

            List<double> ordenados = pontosZ.OrderBy(z => z).ToList();
            if (ordenados.Count == 0)
                return new List<double>();

            var clusters = new List<double> { ordenados[0] };
            double zClusterAtual = ordenados[0];

            foreach (double z in ordenados.Skip(1))
            {
                if (z - zClusterAtual > tolFt)
                {
                    clusters.Add(z);
                    zClusterAtual = z;
                }
            }

            return clusters;
        }

        /// <summary>
        /// Reduz lista de clusters para no maximo <paramref name="maxClusters"/> elementos
        /// preservando primeiro e ultimo, espacando os intermediarios uniformemente.
        /// Se ja' couber, retorna a mesma lista.
        /// </summary>
        public static List<double> LimitarQuantidade(List<double> clusters, int maxClusters = MaxClustersDefault)
        {
            if (clusters == null)
                return new List<double>();
            if (maxClusters < 2)
                throw new ArgumentOutOfRangeException(nameof(maxClusters), "maxClusters deve ser >= 2");
            if (clusters.Count <= maxClusters)
                return clusters;

            var reduzido = new List<double>();
            double passo = (clusters.Count - 1) / (double)(maxClusters - 1);
            for (int i = 0; i < maxClusters; i++)
                reduzido.Add(clusters[(int)Math.Round(i * passo)]);

            return reduzido.Distinct().ToList();
        }
    }
}
