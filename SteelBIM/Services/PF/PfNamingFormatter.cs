using System.Globalization;

namespace FerramentaEMT.Services.PF
{
    /// <summary>
    /// Formatador puro (sem Revit) para montagem de valores de nomeacao PF.
    /// Usa <see cref="CultureInfo.InvariantCulture"/> para garantir que numeros
    /// nao ganhem separador de milhar ou virgula decimal em maquinas pt-BR/de-DE.
    /// </summary>
    internal static class PfNamingFormatter
    {
        /// <summary>
        /// Monta "{prefixo}{numero}{sufixo}" culture-invariant.
        /// Prefixo/sufixo nulos sao tratados como string vazia.
        /// </summary>
        public static string Formatar(string prefixo, int numero, string sufixo)
        {
            string p = prefixo ?? string.Empty;
            string s = sufixo ?? string.Empty;
            string n = numero.ToString(CultureInfo.InvariantCulture);
            return p + n + s;
        }
    }
}
