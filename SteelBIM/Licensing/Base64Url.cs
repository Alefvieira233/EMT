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

            byte[] bytes = Convert.FromBase64String(b64);

            // Defesa-em-profundidade contra Base64 nao-canonico (descoberto pelo
            // teste KeySigner.Verify_returns_null_when_signature_is_tampered):
            // o ultimo char Base64 carrega bits sobressalentes (2 bits nao usados
            // quando os bytes nao sao multiplos de 3). Convert.FromBase64String
            // aceita esses bits como zero ou nao-zero indistintamente, permitindo
            // multiplas representacoes Base64 para o mesmo array de bytes.
            //
            // Em sistema de licenciamento isso permite reedicao trivial do mesmo
            // token (anti-fingerprinting). HMAC continua seguro contra forja, mas
            // a unicidade do token quebra. Para produto comercial: re-codifica e
            // compara — entrada nao-canonica e rejeitada explicitamente.
            string canonical = Encode(bytes);
            if (!string.Equals(canonical, text, StringComparison.Ordinal))
                throw new FormatException("Base64URL nao-canonico (bits sobressalentes diferentes de zero).");

            return bytes;
        }
    }
}
