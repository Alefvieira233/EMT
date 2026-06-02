#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.10 — Logica pura extraida de <see cref="ListaMateriaisExportService"/>
    /// (Etapa C do plano Onda 3). Identifica e parseia peso linear (kg/m)
    /// a partir de nomes/valores de parametros — Revit popula isso em
    /// formatos heterogeneos (pt-BR "59,42 kg/m", en-US "59.42 kg/m",
    /// "Mass per Unit Length" etc).
    ///
    /// 100% sem dependencia de Autodesk.Revit.DB — testavel via xUnit. O
    /// service Revit-bound delega a triagem de Parameter (storage type,
    /// definition.name) e usa este helper pra normalizar e parsear strings.
    /// </summary>
    public static class ListaMateriaisPesoLinearParser
    {
        /// <summary>
        /// Tokens (lowercase) que, quando aparecem no nome ou valor de um
        /// parametro, indicam que ele carrega peso linear (kg/m). Cobre
        /// variantes PT-BR + EN-US + nomenclatura Revit.
        /// </summary>
        public static readonly string[] TokensPesoLinear =
        {
            "kg/m", "kg por m", "kg por metro", "kg metro",
            "peso linear", "massa linear",
            "peso por metro", "massa por metro",
            "mass per unit length", "mass per length", "mass/length",
            "weight per unit length", "weight per length", "weight/length",
            "unit mass", "unit weight"
        };

        /// <summary>Regex para extrair primeiro numero (com sinal/decimal) de uma string.</summary>
        private static readonly Regex NumeroTextoRegex =
            new Regex(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// Normaliza um valor pra comparacao culture-invariant: trim + lowercase.
        /// String null/whitespace → string.Empty.
        /// </summary>
        public static string NormalizarToken(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? string.Empty
                : valor.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// True se o nome do parametro contem qualquer token de peso linear conhecido.
        /// Reusado pra: triagem por nome (ex: "Mass per Unit Length", "Peso Linear").
        /// </summary>
        public static bool ParametroNomeIndicaKgPorMetro(string? nomeParametro)
        {
            string nome = NormalizarToken(nomeParametro);
            return !string.IsNullOrWhiteSpace(nome) && TokensPesoLinear.Any(nome.Contains);
        }

        /// <summary>
        /// Tenta extrair valor kg/m de uma string que pode estar em diferentes
        /// formatos: "59,42 kg/m" (pt-BR), "59.42 kg/m" (en-US), "1,234.56 kg/m"
        /// (en-US com thousand sep), "1.234,56 kg/m" (pt-BR com thousand sep).
        ///
        /// Heuristica: se contiver AMBOS '.' e ',' o ultimo separador e' o
        /// decimal e o outro vira thousand sep (descartado).
        /// </summary>
        /// <returns>true se conseguiu parsear um valor &gt; 0; false caso contrario.</returns>
        public static bool TryParsePesoLinearStringKgM(string? texto, out double pesoLinearKgM)
        {
            pesoLinearKgM = 0.0;
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            Match match = NumeroTextoRegex.Match(texto);
            if (!match.Success)
                return false;

            string numero = match.Value;
            if (numero.Contains(",") && numero.Contains("."))
            {
                numero = numero.LastIndexOf(',') > numero.LastIndexOf('.')
                    ? numero.Replace(".", string.Empty).Replace(',', '.')
                    : numero.Replace(",", string.Empty);
            }
            else
            {
                numero = numero.Replace(',', '.');
            }

            return double.TryParse(numero, NumberStyles.Float, CultureInfo.InvariantCulture, out pesoLinearKgM) &&
                   pesoLinearKgM > 0.0;
        }
    }
}
