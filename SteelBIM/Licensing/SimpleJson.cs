using System;
using System.Globalization;
using System.Text;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Serializador/desserializador minimo para <see cref="LicensePayload"/>.
    /// Foco em ser deterministico (mesmo bytes para os mesmos dados — importante para
    /// que o HMAC sempre bata) e nao depender de System.Text.Json (evita problema
    /// de assembly binding no Revit).
    /// </summary>
    /// <remarks>
    /// Formato fixo (ordem das chaves importa para o HMAC):
    ///   {"e":"alef@x.com","i":1729900800,"x":1761436800,"v":1}
    /// </remarks>
    internal static class SimpleJson
    {
        public static string Serialize(LicensePayload p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));

            StringBuilder sb = new StringBuilder(128);
            sb.Append("{\"e\":\"");
            EscapeString(sb, p.Email ?? string.Empty);
            sb.Append("\",\"i\":");
            sb.Append(p.IssuedAtUnix.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"x\":");
            sb.Append(p.ExpiresAtUnix.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"v\":");
            sb.Append(p.Version.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        public static LicensePayload Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("json vazio");

            // Parser hand-rolled MUITO simples — assume o formato exato gerado por Serialize.
            // E aceitavel porque so consumimos JSON gerado por nos mesmos.
            int idx = 0;
            ExpectChar(json, ref idx, '{');

            string email = null;
            long issued = 0;
            long expires = 0;
            int version = 1;

            while (true)
            {
                SkipWhitespace(json, ref idx);
                if (idx >= json.Length) break;
                if (json[idx] == '}') break;

                if (json[idx] == ',') { idx++; SkipWhitespace(json, ref idx); }

                string key = ReadString(json, ref idx);
                SkipWhitespace(json, ref idx);
                ExpectChar(json, ref idx, ':');
                SkipWhitespace(json, ref idx);

                switch (key)
                {
                    case "e":
                        email = ReadString(json, ref idx);
                        break;
                    case "i":
                        issued = ReadLong(json, ref idx);
                        break;
                    case "x":
                        expires = ReadLong(json, ref idx);
                        break;
                    case "v":
                        version = (int)ReadLong(json, ref idx);
                        break;
                    default:
                        // chave desconhecida: pula valor (string ou numero)
                        SkipValue(json, ref idx);
                        break;
                }
            }

            return new LicensePayload
            {
                Email = email,
                IssuedAtUnix = issued,
                ExpiresAtUnix = expires,
                Version = version,
            };
        }

        // ---- helpers do parser ------------------------------------------

        private static void ExpectChar(string s, ref int i, char expected)
        {
            if (i >= s.Length || s[i] != expected)
                throw new FormatException($"Esperado '{expected}' em pos {i}");
            i++;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            ExpectChar(s, ref i, '"');
            StringBuilder sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char esc = s[i + 1];
                    sb.Append(esc switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        _ => esc,
                    });
                    i += 2;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            ExpectChar(s, ref i, '"');
            return sb.ToString();
        }

        private static long ReadLong(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && s[i] == '-') i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            string slice = s.Substring(start, i - start);
            return long.Parse(slice, CultureInfo.InvariantCulture);
        }

        private static void SkipValue(string s, ref int i)
        {
            if (i >= s.Length) return;
            if (s[i] == '"')
            {
                ReadString(s, ref i);
            }
            else
            {
                while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
            }
        }

        private static void EscapeString(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
        }
    }
}
