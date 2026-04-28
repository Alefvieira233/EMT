using System.Text.RegularExpressions;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Sanitizador de PII para mensagens / stack traces / breadcrumbs antes
    /// de enviar ao Sentry. Puro, deterministic, sem deps de Revit.
    ///
    /// O QUE REMOVE:
    ///   1. Email — qualquer 'foo@bar.tld' (com subdominio, '+' tag, '.', '-')
    ///      vira <EMAIL>.
    ///   2. Path Windows com username — 'C:\Users\joao\...' vira '<USER>\...'
    ///      (preserva o resto do path; case-insensitive na letra do drive
    ///      e no literal "Users").
    ///
    /// O QUE PRESERVA:
    ///   - Class/method names em stack frames (nao sao PII).
    ///   - Linha + coluna de stack traces.
    ///   - Tipos / numeros / outras strings que nao casem com os padroes.
    ///   - Paths Linux/Mac (/home/joao/) — explicitamente fora do escopo.
    ///   - UNC paths (\\server\share\joao\) — diferente de drive letter.
    ///
    /// Decisao registrada no ADR-007 §PII Scrubbing.
    /// </summary>
    public static class PiiScrubber
    {
        // Email pattern — case-insensitive. Cobre:
        //   joao@exemplo.com
        //   user.name@sub.domain.co.uk
        //   user+tag@gmail.com
        //   alef-vieira@empresa.io
        // Nao cobre RFC 5322 inteira (overengineering), so o que aparece em
        // mensagem de exception ou stack frame realisticamente.
        private static readonly Regex EmailRegex = new Regex(
            @"[A-Za-z0-9][\w.+\-]*@[A-Za-z0-9][\w.\-]*\.\w{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Windows user path: C:\Users\<username>\
        // Letra do drive: A-Z (case-insensitive — c:\ tambem casa).
        // Separador "Users": case-insensitive (users\, USERS\).
        // Username: qualquer coisa exceto barra (sequencia de chars sem \).
        // Substitui por '<USER>\' preservando o resto do path.
        private static readonly Regex WindowsUserPathRegex = new Regex(
            @"[A-Za-z]:\\Users\\[^\\]+\\",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Sanitiza a string. Null → null (defensivo, nao throw).
        /// String vazia → vazia. String sem PII → intocada.
        /// </summary>
        public static string Scrub(string input)
        {
            if (input == null) return null;
            if (input.Length == 0) return input;

            // Ordem nao importa: emails nao casam com pathos, e vice-versa.
            string scrubbed = EmailRegex.Replace(input, "<EMAIL>");
            scrubbed = WindowsUserPathRegex.Replace(scrubbed, "<USER>\\");
            return scrubbed;
        }
    }
}
