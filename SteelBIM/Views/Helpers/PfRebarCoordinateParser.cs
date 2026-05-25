using System;
using System.Collections.Generic;
using SteelBIM.Models.PF;
using SteelBIM.Utils;

namespace SteelBIM.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3 — Strangler Fig pattern): logica pura extraida
    /// do code-behind de <c>PfColumnBarsWindow</c> e <c>PfBeamBarsWindow</c>.
    /// Antes era <c>private static</c> em cada Window, inacessivel pra teste
    /// xUnit. Agora <c>public static</c> aqui — Windows delegam.
    ///
    /// <para>
    /// Por que "Helpers" e nao "ViewModel": a refactor completa pra MVVM
    /// exigiria infra nova (BaseViewModel + RelayCommand + DataBinding
    /// migration) + validacao manual no Revit pra cada window. Escopo
    /// pragmatico: extrair logica testavel sem mexer nos bindings.
    /// </para>
    /// </summary>
    public static class PfRebarCoordinateParser
    {
        /// <summary>
        /// Parseia coordenadas X;Y de barras de armadura em formato linha-por-linha.
        ///
        /// <para>
        /// Formato aceito: cada linha eh <c>"X;Y"</c> (separador <c>;</c> ou TAB)
        /// com valores em centimetros. Linhas vazias sao ignoradas. Linhas com
        /// formato invalido geram <paramref name="error"/> nao-vazio e retorno vazio.
        /// </para>
        ///
        /// <para>
        /// <b>Casos:</b>
        /// <list type="bullet">
        /// <item>Input null/vazio → lista vazia, sem error</item>
        /// <item>Linha com so 1 valor ou 3+ valores → error "Linha N: use o formato X(cm); Y(cm)."</item>
        /// <item>Valor nao-numerico → error "Linha N: X e Y precisam ser numeros em centimetros."</item>
        /// </list>
        /// </para>
        /// </summary>
        public static List<PfColumnBarCoordinate> ParseCoordinates(string text, out string error)
        {
            error = string.Empty;
            List<PfColumnBarCoordinate> coordinates = new List<PfColumnBarCoordinate>();

            if (string.IsNullOrWhiteSpace(text))
                return coordinates;

            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    error = $"Linha {i + 1}: use o formato X(cm); Y(cm).";
                    return new List<PfColumnBarCoordinate>();
                }

                if (!NumberParsing.TryParseDouble(parts[0].Trim(), out double xCm) ||
                    !NumberParsing.TryParseDouble(parts[1].Trim(), out double yCm))
                {
                    error = $"Linha {i + 1}: X e Y precisam ser numeros em centimetros.";
                    return new List<PfColumnBarCoordinate>();
                }

                coordinates.Add(new PfColumnBarCoordinate
                {
                    XCm = xCm,
                    YCm = yCm
                });
            }

            return coordinates;
        }
    }
}
