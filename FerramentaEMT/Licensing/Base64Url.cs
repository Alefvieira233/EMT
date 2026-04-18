using System;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Codificacao Base64URL (variante segura para URL: usa '-' '_' no lugar de '+' '/' e
    /// remove o padding '=' do final).
    /// Mais curta e amigavel quando o usuario tem que copiar/colar a chave.
    /// </summary>
    internal static class Base64Url
    {
        public static string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string b64 = Convert.ToBase64String(data);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static byte[] Decode(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            string b64 = text.Replace('-', '+').Replace('_', '/');

            // Restaurar padding '=' que foi removido na codificacao
            int mod4 = b64.Length % 4;
            if (mod4 == 2) b64 += "==";
            else if (mod4 == 3) b64 += "=";
            else if (mod4 == 1) throw new FormatException("Base64URL invalido (length % 4 == 1).");

            return Convert.FromBase64String(b64);
        }
    }
}
