#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Services
{
    /// <summary>
    /// Logica PURA (sem Revit) do guarda-corpo: distribuicao de travessas e segmentacao de postes.
    /// Extraido de GuardaCorpoService para teste por xUnit; o servico delega mantendo o comportamento.
    /// </summary>
    public static class GuardaCorpoCalculo
    {
        /// <summary>Nº de segmentos (vaos) para cobrir 'comprimento' com passo &lt;= espacamentoMaximo.
        /// Minimo 1; se o espacamento for invalido (&lt;= eps) tambem retorna 1.</summary>
        public static int SegmentosPorEspacamento(double comprimento, double espacamentoMaximo, double eps)
        {
            if (espacamentoMaximo <= eps)
                return 1;
            return Math.Max(1, (int)Math.Ceiling(comprimento / espacamentoMaximo));
        }

        /// <summary>Alturas (mesma unidade da entrada) das travessas intermediarias, distribuidas
        /// uniformemente: passo = alturaTotal/(qtd+1), em i*passo para i=1..qtd. Lista vazia se
        /// alturaTotal &lt;= eps ou qtd &lt;= 0.</summary>
        public static List<double> AlturasTravessas(double alturaTotal, int quantidadeTravessas, double eps)
        {
            var alturas = new List<double>();
            if (alturaTotal <= eps || quantidadeTravessas <= 0)
                return alturas;

            double passo = alturaTotal / (quantidadeTravessas + 1);
            for (int i = 1; i <= quantidadeTravessas; i++)
                alturas.Add(passo * i);

            return alturas;
        }
    }
}
