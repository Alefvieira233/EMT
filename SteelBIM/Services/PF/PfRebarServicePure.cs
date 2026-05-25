using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SteelBIM.Services.PF
{
    /// <summary>
    /// v2.7.9 (auditoria 2026-05-25 #1 — bloqueador critico): logica pura
    /// extraida de <see cref="PfRebarService"/> (2178 LOC, zero tests diretos).
    /// Esses metodos eram <c>private static</c> sem deps de instancia, mas
    /// inacessiveis pra testes. Agora <c>public static</c> no Pure — Revit
    /// service delega.
    ///
    /// <para>
    /// <b>Contrato:</b> tudo em milimetros (mm). Caller faz conversao
    /// feet↔mm via Revit <c>UnitUtils</c>. Mantem Pure 100% sem deps de
    /// Autodesk.Revit.DB — testavel direto via xUnit sem stubs.
    /// </para>
    ///
    /// <para>
    /// <b>Cobertura:</b> ver <c>PfRebarServicePureTests.cs</c>. Esses sao os
    /// PRIMEIROS testes diretos do nucleo PF — refactor seguinte (v2.8+)
    /// pode extrair mais lojica conforme cobertura cresce.
    /// </para>
    /// </summary>
    public static class PfRebarServicePure
    {
        /// <summary>
        /// Segmento minimo de barra reconhecido pelo gerador (mm). Trecho menor
        /// que isso e descartado (nao gera barra). Default 50mm — NBR considera
        /// barras &lt; 5cm como "pontas perdidas".
        /// </summary>
        public const double MinSegmentMm = 50.0;

        /// <summary>
        /// Comprimento minimo de pedaco de barra entre splices (mm). Garante
        /// que apos calcular traspasse, sobre pelo menos 30cm de barra util.
        /// </summary>
        public const double MinPieceMm = 300.0;

        /// <summary>
        /// Range de barra continua: [StartMm, EndMm] ao longo de eixo
        /// (vertical pra pilar, longitudinal pra viga).
        /// </summary>
        public readonly struct BarRange
        {
            public BarRange(double startMm, double endMm)
            {
                StartMm = startMm;
                EndMm = endMm;
            }

            public double StartMm { get; }
            public double EndMm { get; }
            public double LengthMm => EndMm - StartMm;

            public override string ToString()
                => $"BarRange[{StartMm:F1}..{EndMm:F1}mm = {LengthMm:F1}mm]";
        }

        /// <summary>
        /// Particiona barra continua de [<paramref name="startMm"/>, <paramref name="endMm"/>]
        /// em pedacos de comprimento maximo <paramref name="maxPieceLengthMm"/>, com traspasse
        /// (overlap) <paramref name="lapLengthMm"/> entre pedacos consecutivos.
        ///
        /// <para>
        /// <b>Stagger:</b> <paramref name="staggerIndex"/> deslocacao da primeira juncao —
        /// barras vizinhas usam indices diferentes pra evitar emendas alinhadas
        /// (boa pratica NBR pra distribuir esforco).
        /// </para>
        ///
        /// <para>
        /// <b>Casos especiais:</b>
        /// <list type="bullet">
        /// <item>Trecho total &lt;= MinSegmentMm → lista vazia (nao vale a pena)</item>
        /// <item>Pedaco caberia inteiro (total &lt;= maxPiece + 1mm) → 1 range so</item>
        /// <item>maxPiece &lt;= lapLength + MinPieceMm → throw (config invalida)</item>
        /// </list>
        /// </para>
        /// </summary>
        public static List<BarRange> BuildLapRangesMm(
            double startMm,
            double endMm,
            double maxPieceLengthMm,
            double lapLengthMm,
            int staggerIndex)
        {
            double totalLength = endMm - startMm;
            if (totalLength <= MinSegmentMm)
                return new List<BarRange>();

            if (maxPieceLengthMm <= MinSegmentMm || totalLength <= maxPieceLengthMm + 1.0)
                return new List<BarRange> { new BarRange(startMm, endMm) };

            if (maxPieceLengthMm <= lapLengthMm + MinPieceMm)
                throw new InvalidOperationException(
                    "O comprimento maximo da barra precisa ser maior que o traspasse calculado.");

            List<BarRange> ranges = new List<BarRange>();
            double usefulStep = maxPieceLengthMm - lapLengthMm;
            double staggerStep = Math.Min(lapLengthMm / 2.0, usefulStep / 3.0);
            double firstReduction = (Math.Abs(staggerIndex) % 3) * staggerStep;
            double currentStart = startMm;
            double currentEnd = Math.Min(endMm, startMm + maxPieceLengthMm - firstReduction);

            while (currentEnd < endMm - 1.0)
            {
                if (currentEnd - currentStart < MinPieceMm)
                    currentEnd = Math.Min(endMm, currentStart + MinPieceMm);

                ranges.Add(new BarRange(currentStart, currentEnd));
                currentStart = currentEnd - lapLengthMm;
                currentEnd = Math.Min(endMm, currentStart + maxPieceLengthMm);
            }

            if (endMm - currentStart >= MinSegmentMm)
                ranges.Add(new BarRange(currentStart, endMm));

            return ranges.Count == 0
                ? new List<BarRange> { new BarRange(startMm, endMm) }
                : ranges;
        }

        /// <summary>
        /// Distribui <paramref name="count"/> posicoes igualmente espaçadas entre
        /// [<paramref name="minMm"/>, <paramref name="maxMm"/>]. Use pra posicionar
        /// estribos ou barras longitudinais.
        ///
        /// <para>
        /// <b>Casos especiais:</b>
        /// <list type="bullet">
        /// <item>count &lt;= 0 → tratado como 1</item>
        /// <item>range &lt;= 10mm → retorna so o centro (nao vale espalhar)</item>
        /// <item>count == 1 → retorna so o centro</item>
        /// </list>
        /// </para>
        /// </summary>
        public static List<double> DistributePositionsMm(int count, double minMm, double maxMm)
        {
            count = Math.Max(1, count);
            if (maxMm - minMm <= 10.0)
                return new List<double> { (minMm + maxMm) / 2.0 };

            if (count == 1)
                return new List<double> { (minMm + maxMm) / 2.0 };

            List<double> values = new List<double>();
            double step = (maxMm - minMm) / (count - 1);
            for (int i = 0; i < count; i++)
                values.Add(minMm + (step * i));

            return values;
        }

        /// <summary>
        /// Normaliza texto pra comparacao culture-invariant: trim + lowercase +
        /// remove diacriticos + remove tudo que nao for letra/digito.
        /// "Pilar Â° 12" → "pilar12".
        /// </summary>
        public static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            normalized = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
            return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
        }

        /// <summary>
        /// Sanitiza mensagem de erro pra exibicao em dialog: trim + remove
        /// quebras de linha. Fallback "falha desconhecida." pra null/empty.
        /// </summary>
        public static string LimparMensagem(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "falha desconhecida."
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>
        /// Formata diametro de barra pra token usavel em nome de tipo Revit:
        /// 12.5 mm → "125". Usa InvariantCulture pra evitar virgula em pt-BR.
        /// </summary>
        public static string FormatDiameterToken(double diameterMm)
        {
            return diameterMm.ToString("0.###", CultureInfo.InvariantCulture)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);
        }

        /// <summary>Conversao matematica radianos → graus.</summary>
        public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

        /// <summary>Conversao matematica graus → radianos.</summary>
        public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
