#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) que constroi a especificacao das
    /// cinco faixas de cotas padrao EMT para elevacao de trelica.
    /// </summary>
    /// <remarks>
    /// As cinco faixas (ver docs/reference-projects/cobertura-samsung/NOTAS.md):
    ///   1. Paineis do banzo superior (acima da trelica).
    ///   2. Vao entre apoios + vao total (faixa superior, cota acumulada).
    ///   3. Paineis do banzo inferior (abaixo da trelica).
    ///   4. Vao total (faixa inferior-inferior).
    ///   5. Altura de cada montante (cota vertical, texto rotacionado 90 graus).
    ///
    /// Esta classe APENAS produz a especificacao (lista de segmentos por faixa).
    /// A colocacao das cotas reais no Revit e feita por CotarTrelicaService usando
    /// Document.Create.NewDimension(view, line, refArray).
    /// </remarks>
    public static class CotaFaixaBuilder
    {
        /// <summary>Indice da faixa na elevacao.</summary>
        public enum Faixa
        {
            PaineisBanzoSuperior = 1,
            VaosEntreApoios = 2,
            PaineisBanzoInferior = 3,
            VaoTotal = 4,
            AlturasMontantes = 5
        }

        /// <summary>Especificacao de uma cota de faixa (pares de pontos X + rotulo).</summary>
        public sealed record SegmentoCota(double XInicio, double XFim, string? RotuloOverride);

        /// <summary>Especificacao completa de uma faixa de cotas.
        /// <paramref name="OffsetZPes"/> e' em Z (altura na elevacao) — positivo acima da trelica,
        /// negativo abaixo. Sempre medido a partir do eixo do banzo mais proximo.</summary>
        public sealed record FaixaCotas(Faixa Tipo, IReadOnlyList<SegmentoCota> Segmentos, double OffsetZPes);

        /// <summary>Offset vertical padrao (em pes) da cota em relacao a trelica. 0.5 pe ~ 152 mm.</summary>
        public const double OffsetFaixaPes = 0.5;

        /// <summary>
        /// Monta a faixa 1 (paineis do banzo superior).
        /// </summary>
        public static FaixaCotas FaixaPaineisBanzoSuperior(IReadOnlyList<double> xNosSuperior, double offsetAcima = OffsetFaixaPes)
        {
            return new FaixaCotas(Faixa.PaineisBanzoSuperior, SegmentosConsecutivos(xNosSuperior), offsetAcima);
        }

        /// <summary>
        /// Monta a faixa 2 (vaos entre apoios). xApoios deve conter 2+ X de apoio
        /// (tipicamente 2 — esq e dir — para trelica simples; mais para continuas).
        /// </summary>
        public static FaixaCotas FaixaVaosEntreApoios(IReadOnlyList<double> xApoios, double offsetAcima = OffsetFaixaPes * 2)
        {
            return new FaixaCotas(Faixa.VaosEntreApoios, SegmentosConsecutivos(xApoios), offsetAcima);
        }

        /// <summary>
        /// Monta a faixa 3 (paineis do banzo inferior).
        /// </summary>
        public static FaixaCotas FaixaPaineisBanzoInferior(IReadOnlyList<double> xNosInferior, double offsetAbaixo = OffsetFaixaPes)
        {
            return new FaixaCotas(Faixa.PaineisBanzoInferior, SegmentosConsecutivos(xNosInferior), -Math.Abs(offsetAbaixo));
        }

        /// <summary>
        /// Monta a faixa 4 (vao total da trelica).
        /// </summary>
        public static FaixaCotas FaixaVaoTotal(double xEsquerda, double xDireita, double offsetAbaixo = OffsetFaixaPes * 2)
        {
            var segs = new[] { new SegmentoCota(xEsquerda, xDireita, null) };
            return new FaixaCotas(Faixa.VaoTotal, segs, -Math.Abs(offsetAbaixo));
        }

        /// <summary>
        /// Monta a faixa 5 (altura de cada montante). Cada segmento cobre do
        /// banzo inferior ao banzo superior na estacao X do montante.
        /// </summary>
        public static FaixaCotas FaixaAlturasMontantes(IReadOnlyList<double> xMontantes)
        {
            if (xMontantes is null) throw new ArgumentNullException(nameof(xMontantes));
            var segs = xMontantes.Select(x => new SegmentoCota(x, x, null)).ToList();
            return new FaixaCotas(Faixa.AlturasMontantes, segs, 0.0);
        }

        /// <summary>
        /// Transforma uma lista de coordenadas (ordenadas crescente) em segmentos
        /// entre pontos consecutivos. Ex: [0, 1000, 2500] -> [(0,1000), (1000,2500)].
        /// Lanca <see cref="ArgumentException"/> se a entrada nao estiver ordenada.
        /// </summary>
        internal static IReadOnlyList<SegmentoCota> SegmentosConsecutivos(IReadOnlyList<double> xs)
        {
            if (xs is null) throw new ArgumentNullException(nameof(xs));
            if (xs.Count < 2) return Array.Empty<SegmentoCota>();
            for (int i = 1; i < xs.Count; i++)
            {
                if (xs[i] < xs[i - 1])
                    throw new ArgumentException(
                        "Lista de coordenadas precisa estar ordenada crescente.", nameof(xs));
            }
            var segs = new List<SegmentoCota>(xs.Count - 1);
            for (int i = 1; i < xs.Count; i++)
                segs.Add(new SegmentoCota(xs[i - 1], xs[i], null));
            return segs;
        }
    }
}
