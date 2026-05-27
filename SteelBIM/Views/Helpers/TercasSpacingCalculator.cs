#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Views.Helpers
{
    /// <summary>
    /// Math puro da janela <see cref="TercasSpacingWindow"/> — extraido
    /// para teste sem WPF/Revit attached.
    ///
    /// <para>v2.8.1 (Victor): a janela de espacamentos manuais ganhou 3
    /// comportamentos novos:</para>
    /// <list type="bullet">
    ///   <item>Quando o usuario muda o "Vao total", todos os espacamentos
    ///         devem ser <see cref="ScaleProportionally"/> em proporcao.</item>
    ///   <item>Quando o usuario muda um espacamento individual (excluindo o
    ///         ultimo), o ultimo deve ser <see cref="RecalculateLastAsRemainder"/>
    ///         pra manter soma = vao total.</item>
    ///   <item>Linha "Total" mostra a soma — verde se bate, vermelho se nao
    ///         (<see cref="IsTotalMatching"/>).</item>
    /// </list>
    /// </summary>
    public static class TercasSpacingCalculator
    {
        /// <summary>Tolerancia padrao em cm pra comparar somas vs vao total.</summary>
        public const double DefaultMatchToleranceCm = 0.1;

        /// <summary>
        /// Escala todos os <paramref name="values"/> proporcionalmente do
        /// <paramref name="oldTotal"/> pro <paramref name="newTotal"/>.
        /// Valores negativos sao clampados em zero (defensivo).
        ///
        /// <para>Edge cases:</para>
        /// <list type="bullet">
        ///   <item><c>oldTotal &lt;= 0</c>: retorna copia inalterada (evita div/0).</item>
        ///   <item><c>newTotal &lt;= 0</c>: retorna lista de zeros.</item>
        ///   <item><paramref name="values"/> null: retorna lista vazia.</item>
        /// </list>
        /// </summary>
        public static List<double> ScaleProportionally(double oldTotal, double newTotal, IList<double> values)
        {
            if (values == null)
                return new List<double>();
            if (oldTotal <= 0)
                return new List<double>(values);
            if (newTotal <= 0)
            {
                var zeros = new List<double>(values.Count);
                for (int i = 0; i < values.Count; i++)
                    zeros.Add(0);
                return zeros;
            }

            double scale = newTotal / oldTotal;
            var scaled = new List<double>(values.Count);
            foreach (double v in values)
                scaled.Add(Math.Max(0, v * scale));
            return scaled;
        }

        /// <summary>
        /// Substitui o ultimo valor da lista pelo remainder (<paramref name="total"/> - soma_dos_outros).
        /// Resultado nunca é negativo. Retorna nova lista (input nao mutado).
        ///
        /// <para>Edge cases:</para>
        /// <list type="bullet">
        ///   <item><paramref name="values"/> null ou vazio: retorna lista vazia.</item>
        ///   <item><paramref name="values"/> com 1 elemento: ultimo == total (substitui).</item>
        ///   <item>Soma dos outros &gt; total: ultimo vira 0 (clamp).</item>
        /// </list>
        /// </summary>
        public static List<double> RecalculateLastAsRemainder(double total, IList<double> values)
        {
            if (values == null || values.Count == 0)
                return new List<double>();

            var result = new List<double>(values);
            double sumOthers = 0;
            for (int i = 0; i < result.Count - 1; i++)
                sumOthers += result[i];

            double remainder = total - sumOthers;
            result[result.Count - 1] = Math.Max(0, remainder);
            return result;
        }

        /// <summary>
        /// True se a soma de <paramref name="values"/> bate com <paramref name="total"/>
        /// dentro da tolerancia <paramref name="toleranceCm"/>. Usado pra colorir
        /// o label "Total" verde/vermelho na UI.
        ///
        /// <para>Valores null sao tratados como lista vazia (soma = 0).</para>
        /// </summary>
        public static bool IsTotalMatching(double total, IList<double>? values, double toleranceCm = DefaultMatchToleranceCm)
        {
            double sum = 0;
            if (values != null)
            {
                foreach (double v in values)
                    sum += v;
            }
            return Math.Abs(sum - total) < toleranceCm;
        }

        /// <summary>1 cm em pés (constante exata: 100 cm/m * 304.8 mm/ft / 1000 mm/m).</summary>
        public const double FtPerCm = 1.0 / 30.48;

        /// <summary>
        /// v2.8.1 (Victor): calcula os parametros (0..1) ao longo da curva onde
        /// cada terca sera lancada. Helper PURO sem dependencia de Revit —
        /// permite teste sem Revit attached. Usado pelo
        /// <c>TercasService.CalcularParametrosPosicao</c>.
        ///
        /// <para>Quando <paramref name="usarEspacamentoManual"/> e
        /// <paramref name="espacamentosCm"/> tem exatamente
        /// <paramref name="quantidade"/>+1 valores E o vao é positivo, usa
        /// distancias customizadas. Caso contrario, cai pra distribuicao
        /// uniforme via <c>step = 1/(quantidade+1)</c>.</para>
        ///
        /// <para>Defensivo: clampa cada parametro em [0,1] pra nao extrapolar
        /// os limites da curva, e retorna lista vazia se
        /// <paramref name="quantidade"/> &lt; 1.</para>
        /// </summary>
        public static List<double> CalcularParametrosPosicao(
            int quantidade,
            bool usarEspacamentoManual,
            IList<double>? espacamentosCm,
            double vaoLineLengthFt)
        {
            if (quantidade < 1)
                return new List<double>();

            List<double> parametros = new List<double>(quantidade);
            bool podeManual = usarEspacamentoManual
                              && espacamentosCm != null
                              && espacamentosCm.Count == quantidade + 1
                              && vaoLineLengthFt > 0;

            if (podeManual)
            {
                // Soma acumulada das distancias / vao total = parametro de cada terca.
                double accFt = 0;
                for (int i = 0; i < quantidade; i++)
                {
                    accFt += espacamentosCm![i] * FtPerCm;
                    double par = Math.Clamp(accFt / vaoLineLengthFt, 0.0, 1.0);
                    parametros.Add(par);
                }
            }
            else
            {
                double step = 1.0 / (quantidade + 1);
                for (int i = 1; i <= quantidade; i++)
                    parametros.Add(step * i);
            }

            return parametros;
        }
    }
}
