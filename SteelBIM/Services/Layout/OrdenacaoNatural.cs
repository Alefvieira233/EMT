#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Services.Layout
{
    /// <summary>
    /// v2.8.13: ordenacao NATURAL (humana) de nomes — "TR-2" vem antes de "TR-10" (e nao
    /// depois, como no ordinal ASCII). Compara trechos de digitos numericamente e o resto
    /// por texto (case-insensitive). Pura/testavel. Detalhe que separa amador de profissional
    /// ao pranchar/ordenar vistas.
    /// </summary>
    public static class OrdenacaoNatural
    {
        public static int Comparar(string? a, string? b)
        {
            string x = a ?? string.Empty;
            string y = b ?? string.Empty;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                bool digX = char.IsDigit(x[i]);
                bool digY = char.IsDigit(y[j]);

                if (digX && digY)
                {
                    // compara o bloco numerico inteiro pelo valor (ignora zeros a esquerda).
                    int iniX = i;
                    while (i < x.Length && char.IsDigit(x[i]))
                        i++;
                    int iniY = j;
                    while (j < y.Length && char.IsDigit(y[j]))
                        j++;

                    string numX = x.Substring(iniX, i - iniX).TrimStart('0');
                    string numY = y.Substring(iniY, j - iniY).TrimStart('0');
                    if (numX.Length != numY.Length)
                        return numX.Length - numY.Length;
                    int cmpNum = string.CompareOrdinal(numX, numY);
                    if (cmpNum != 0)
                        return cmpNum;
                }
                else
                {
                    char cx = char.ToUpperInvariant(x[i]);
                    char cy = char.ToUpperInvariant(y[j]);
                    if (cx != cy)
                        return cx.CompareTo(cy);
                    i++;
                    j++;
                }
            }

            return (x.Length - i) - (y.Length - j);
        }

        /// <summary>Comparer pronto para List.Sort/OrderBy.</summary>
        public static IComparer<string> Comparer { get; } = Comparer<string>.Create(Comparar);
    }
}
