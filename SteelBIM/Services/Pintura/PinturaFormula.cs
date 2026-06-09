#nullable enable
namespace SteelBIM.Services.Pintura
{
    /// <summary>
    /// Logica PURA (sem Revit) do calculo de area de pintura de perfis metalicos.
    /// A area bruta vem da soma das faces do solido; as faces de "topo" (corte das barras,
    /// normal paralela ao eixo) sao opcionalmente descontadas; aplica-se um percentual de
    /// desconto (parafusos/folgas). Testavel por xUnit.
    /// </summary>
    public static class PinturaFormula
    {
        /// <summary>
        /// Uma face planar e' de "topo" (corte da barra) quando a normal e' ~paralela ao eixo
        /// do membro: |normal . eixo| >= limiar. Faces laterais tem produto ~0.
        /// </summary>
        public static bool EhFaceDeTopo(double dotAbsNormalEixo, double limiar = 0.9)
        {
            return dotAbsNormalEixo >= limiar;
        }

        /// <summary>
        /// Area liquida de pintura (mesma unidade da entrada). Desconta os topos (se pedido) e
        /// aplica o percentual de desconto (0..100). Nunca negativa; percentual fora de faixa e' clampado.
        /// </summary>
        public static double AreaLiquida(double areaTotal, double areaTopos, bool descontarTopos, double percentualDesconto)
        {
            double baseArea = descontarTopos ? areaTotal - areaTopos : areaTotal;
            if (baseArea < 0.0)
                baseArea = 0.0;

            double pct = percentualDesconto;
            if (pct < 0.0)
                pct = 0.0;
            if (pct > 100.0)
                pct = 100.0;

            double fator = 1.0 - pct / 100.0;
            return baseArea * fator;
        }
    }
}
