#nullable enable
using System;

namespace SteelBIM.Services
{
    /// <summary>
    /// Logica PURA (sem Revit) do dimensionamento de escada: contagem de degraus, regra de Blondel e
    /// inclinacao. Extraido de EscadaService para teste por xUnit; o servico delega mantendo o
    /// comportamento exato (entradas em pes, conversoes via constantes do chamador).
    /// </summary>
    public static class EscadaCalculo
    {
        /// <summary>Nº de degraus: usa o valor do usuario quando &gt; 0; senao deriva de
        /// round(totalRise/espelhoDesejado), com minimo 1.</summary>
        public static int ContarDegraus(int quantidadeConfig, double totalRiseFt, double espelhoDesejadoFt)
        {
            if (quantidadeConfig > 0)
                return quantidadeConfig;
            if (espelhoDesejadoFt <= 0.0)
                return 1;
            return Math.Max(1, (int)Math.Round(totalRiseFt / espelhoDesejadoFt));
        }

        /// <summary>Inclinacao em graus a partir da subida e da projecao horizontal (mesma unidade).</summary>
        public static double InclinacaoGraus(double riseFt, double runHorizontalFt)
        {
            return Math.Atan2(riseFt, runHorizontalFt) * (180.0 / Math.PI);
        }

        /// <summary>Valor de Blondel em cm: (2*espelho + piso) convertido por ftPerCm.</summary>
        public static double BlondelCm(double espelhoFt, double pisoFt, double ftPerCm)
        {
            return (2.0 * espelhoFt + pisoFt) / ftPerCm;
        }

        /// <summary>Blondel dentro da faixa recomendada 63–65 cm.</summary>
        public static bool BlondelDentroDaFaixa(double blondelCm)
        {
            return blondelCm >= 63.0 && blondelCm <= 65.0;
        }
    }
}
