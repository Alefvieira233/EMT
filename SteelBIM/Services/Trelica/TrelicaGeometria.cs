#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelBIM.Services.Trelica
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
            if (xNos is null)
                throw new ArgumentNullException(nameof(xNos));
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
            if (xNos is null)
                throw new ArgumentNullException(nameof(xNos));
            var lista = xNos.ToList();
            if (lista.Count < 2)
                return 0.0;
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
            if (xEstacoes is null)
                throw new ArgumentNullException(nameof(xEstacoes));
            if (zSuperior is null)
                throw new ArgumentNullException(nameof(zSuperior));
            if (zInferior is null)
                throw new ArgumentNullException(nameof(zInferior));
            return xEstacoes.Select(x => Math.Max(0.0, zSuperior(x) - zInferior(x))).ToList();
        }

        /// <summary>
        /// Retorna o X do apoio esquerdo e do apoio direito — tipicamente os
        /// extremos do banzo inferior. Lanca se a lista tiver menos de 2 pontos.
        /// </summary>
        public static (double xEsq, double xDir) ExtremosApoio(IEnumerable<double> xBanzoInferior)
        {
            if (xBanzoInferior is null)
                throw new ArgumentNullException(nameof(xBanzoInferior));
            var lista = xBanzoInferior.ToList();
            if (lista.Count < 2)
                throw new ArgumentException("Banzo inferior precisa de pelo menos 2 nos.", nameof(xBanzoInferior));
            return (lista.Min(), lista.Max());
        }

        /// <summary>
        /// Coleta os nos 2D distintos pertencentes ao banzo alvo, a partir de barras JA
        /// classificadas (passo 3 do pipeline) e JA projetadas no plano da vista. Cada barra
        /// entra com seu <see cref="TrelicaClassificador.TipoMembro"/> e seus dois extremos 2D.
        /// Nos coincidentes (dentro de <see cref="TolPesIgualdade"/>) sao unificados; o
        /// resultado vem ordenado por X.
        /// </summary>
        /// <remarks>
        /// Extraido de CotarTrelicaService para teste sem Revit. Substitui a logica antiga que
        /// re-classificava por inclinacao (<c>ClassificarPorInclinacao</c>), que NUNCA devolve
        /// BanzoSuperior/Inferior — a comparacao era sempre falsa e o pipeline abortava em
        /// "banzos nao detectados" para TODA trelica.
        /// </remarks>
        public static IReadOnlyList<(double X, double Z)> ColetarNosDoBanzo(
            IEnumerable<(TrelicaClassificador.TipoMembro Tipo, (double X, double Z) P0, (double X, double Z) P1)> barras,
            TrelicaClassificador.TipoMembro tipoBanzoAlvo)
        {
            if (barras is null)
                throw new ArgumentNullException(nameof(barras));

            var brutos = new List<(double X, double Z)>();
            foreach (var b in barras)
            {
                if (b.Tipo != tipoBanzoAlvo)
                    continue;
                brutos.Add(b.P0);
                brutos.Add(b.P1);
            }

            var unicos = new List<(double X, double Z)>();
            foreach (var p in brutos.OrderBy(p => p.X).ThenBy(p => p.Z))
            {
                bool jaExiste = unicos.Any(q =>
                    Math.Abs(q.X - p.X) <= TolPesIgualdade &&
                    Math.Abs(q.Z - p.Z) <= TolPesIgualdade);
                if (!jaExiste)
                    unicos.Add(p);
            }

            return unicos;
        }

        /// <summary>
        /// Retorna o indice do no cuja coordenada X esta mais proxima de <paramref name="alvoX"/>,
        /// desde que dentro de <paramref name="tolPes"/>. Retorna -1 se nenhum no estiver dentro
        /// da tolerancia — distingue "nao encontrado" de "no legitimamente em X≈0" (o sentinela
        /// antigo <c>noSup.X == 0</c> falhava nos dois casos).
        /// </summary>
        public static int IndiceNoMaisProximo(IReadOnlyList<double> xs, double alvoX, double tolPes)
        {
            if (xs is null)
                throw new ArgumentNullException(nameof(xs));
            int melhor = -1;
            double menor = double.MaxValue;
            for (int i = 0; i < xs.Count; i++)
            {
                double d = Math.Abs(xs[i] - alvoX);
                if (d < menor)
                {
                    menor = d;
                    melhor = i;
                }
            }
            return menor <= tolPes ? melhor : -1;
        }
    }
}
