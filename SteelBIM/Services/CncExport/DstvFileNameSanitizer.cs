#nullable enable
using System;
using System.IO;
using System.Text;

namespace FerramentaEMT.Services.CncExport
{
    /// <summary>
    /// Sanitiza nomes de peca para uso em nomes de arquivo DSTV/NC1.
    /// Pure C# — sem dependencia Revit, permite teste unitario.
    /// </summary>
    public static class DstvFileNameSanitizer
    {
        private const string DefaultName = "peca";

        /// <summary>
        /// Substitui caracteres invalidos por '_' e faz trim.
        /// Retorna "peca" para entrada vazia/null/whitespace.
        /// </summary>
        public static string Sanitize(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return DefaultName;

            var sb = new StringBuilder(nome!.Length);
            char[] invalidos = Path.GetInvalidFileNameChars();
            foreach (char c in nome!)
            {
                if (Array.IndexOf(invalidos, c) >= 0)
                    sb.Append('_');
                else
                    sb.Append(c);
            }

            string resultado = sb.ToString().Trim();
            return resultado.Length == 0 ? DefaultName : resultado;
        }
    }
}
