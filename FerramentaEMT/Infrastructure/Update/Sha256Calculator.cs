using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Helper puro para calcular SHA256 (hex lowercase) e parsear arquivos
    /// no formato canonico de checksums.txt:
    ///
    ///   &lt;hex64&gt;  &lt;filename&gt;
    ///
    /// (saida do <c>sha256sum</c> e <c>certutil -hashfile</c> com
    /// reformatacao). Sem IO de rede e sem logging — testavel em xUnit.
    /// </summary>
    public static class Sha256Calculator
    {
        /// <summary>
        /// Calcula SHA256 de um stream a partir da posicao atual.
        /// Retorna hex lowercase de 64 chars.
        /// </summary>
        public static string ComputeHex(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return ToHexLower(hash);
            }
        }

        /// <summary>
        /// Calcula SHA256 de um array de bytes. Retorna hex lowercase.
        /// </summary>
        public static string ComputeHex(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                return ToHexLower(hash);
            }
        }

        private static string ToHexLower(byte[] bytes)
        {
            char[] result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                result[i * 2] = HexChar((b >> 4) & 0xF);
                result[i * 2 + 1] = HexChar(b & 0xF);
            }
            return new string(result);
        }

        private static char HexChar(int nibble)
        {
            return (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
        }

        /// <summary>
        /// Procura o hash esperado para um arquivo num conteudo de checksums.txt.
        /// Aceita o formato canonico:
        ///
        ///   &lt;hex64&gt;  &lt;filename&gt;
        ///
        /// (dois espacos, mas tolera 1+ whitespace). Retorna null se nao encontrar
        /// ou se o conteudo for invalido.
        /// </summary>
        public static string FindHashForFile(string checksumsContent, string fileName)
        {
            if (string.IsNullOrWhiteSpace(checksumsContent)) return null;
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            string[] lines = checksumsContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length < 65) continue; // 64 hex + 1 space minimum

                // Hash eh tudo ate o primeiro whitespace
                int firstWs = -1;
                for (int i = 0; i < trimmed.Length; i++)
                {
                    if (char.IsWhiteSpace(trimmed[i])) { firstWs = i; break; }
                }
                if (firstWs < 0) continue;

                string hash = trimmed.Substring(0, firstWs);
                if (hash.Length != 64) continue;
                if (!IsHex(hash)) continue;

                string rest = trimmed.Substring(firstWs).TrimStart();
                // GNU sha256sum prefixa nome com '*' em modo binario — remover
                if (rest.StartsWith("*", StringComparison.Ordinal))
                {
                    rest = rest.Substring(1);
                }

                if (string.Equals(rest, fileName, StringComparison.Ordinal))
                {
                    return hash.ToLower(CultureInfo.InvariantCulture);
                }
            }
            return null;
        }

        private static bool IsHex(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }
    }
}
