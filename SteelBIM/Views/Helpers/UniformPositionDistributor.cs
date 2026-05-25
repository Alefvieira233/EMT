using System.Collections.Generic;

namespace SteelBIM.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3 — Strangler Fig): logica de distribuicao uniforme
    /// de posicoes extraida do code-behind de <c>PfColumnBarsWindow</c> +
    /// <c>PfBeamBarsWindow</c>. Usada no PREVIEW do canvas, em cm (separada
    /// da versao em <c>PfRebarServicePure.DistributePositionsMm</c> que
    /// trabalha em mm e tem edge cases diferentes pra placement real Revit).
    ///
    /// <para>
    /// <b>Diferencas da versao em PfRebarServicePure:</b>
    /// <list type="bullet">
    /// <item>Aceita count = 0 → retorna lista vazia (Pure clampa pra 1)</item>
    /// <item>Aceita max &lt; min → retorna lista vazia (Pure assume max ≥ min)</item>
    /// <item>Tolerancia de range minimo eh 0.001 (Pure usa 10mm)</item>
    /// </list>
    /// Essas diferencas existem porque o preview UI tolera input ruim
    /// (usuario digitando) e prefere "nada" a um valor enganoso, enquanto
    /// o service real placement assume input ja validado.
    /// </para>
    /// </summary>
    public static class UniformPositionDistributor
    {
        /// <summary>
        /// Distribui <paramref name="count"/> posicoes igualmente espaçadas
        /// entre <paramref name="min"/> e <paramref name="max"/>. Unidade
        /// agnostica — caller decide (cm pra preview, mm pra outras).
        /// </summary>
        public static List<double> Distribute(int count, double min, double max)
        {
            if (count <= 0 || max < min)
                return new List<double>();

            if (count == 1 || max - min <= 0.001)
                return new List<double> { (min + max) / 2.0 };

            List<double> values = new List<double>();
            double step = (max - min) / (count - 1);
            for (int i = 0; i < count; i++)
                values.Add(min + (step * i));

            return values;
        }
    }
}
