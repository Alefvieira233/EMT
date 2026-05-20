using System.Text.RegularExpressions;

namespace SteelBIM.Infrastructure
{
    /// <summary>
    /// Sanitizador de PII cross-cutting. Aplicado por:
    ///   - SentryOptionsBuilder.ScrubAndTag (PR-3, BeforeSend hook).
    ///   - TelemetryOptionsBuilder.ApplySuperProperties (PR-4, evento PostHog).
    /// Puro, deterministic, sem deps de Revit.
    ///
    /// Movido em PR-4 de Infrastructure/CrashReporting/ pra o namespace pai
    /// porque o scope passou a ser cross-cutting (crash + telemetry + futuras).
    /// Ver ADR-008 §"Reuso do PiiScrubber".
    ///
    /// O QUE REMOVE (5 padroes, ordem nao importa):
    ///   1. Email — qualquer 'foo@bar.tld' (com subdominio, '+' tag, '.', '-')
    ///      vira <EMAIL>.
    ///   2. Path Windows com username — 'C:\Users\joao\...' vira '<USER>\...'
    ///      (preserva o resto do path; case-insensitive na letra do drive
    ///      e no literal "Users").
    ///   3. Path Windows localizado PT-BR — 'C:\Usuários\joao\...' ou
    ///      'C:\Usuarios\joao\...' vira '<USER>\...'. v2.6.1 (auditoria PHASE 6.3).
    ///   4. Path UNC Windows — '\\server\share\...' vira '<UNC>\...'.
    ///      Frequentemente carrega nome de cliente no share name
    ///      ('\\nas01\projeto-vulcaflex\'). v2.6.1 (auditoria PHASE 6.3).
    ///   5. Filename Revit (.rvt, .rfa, .rte, .rft) — 'Projeto Vulcaflex.rvt'
    ///      vira '<REVIT_FILE>.rvt' (extensao preservada). v2.6.1 (auditoria
    ///      PHASE 6.3) — nomes de arquivo .rvt frequentemente identificam clientes.
    ///
    /// O QUE PRESERVA:
    ///   - Class/method names em stack frames (nao sao PII).
    ///   - Linha + coluna de stack traces.
    ///   - Source files .cs/.xaml em stack frames (nao sao PII).
    ///   - Tipos / numeros / outras strings que nao casem com os padroes.
    ///   - Paths Linux/Mac (/home/joao/) — explicitamente fora do escopo
    ///     (plugin Revit Windows-only).
    ///   - Username em UNC apos o share name (gap conhecido — apenas
    ///     server+share sao scrubbed; documentado abaixo).
    ///
    /// Decisao registrada no ADR-007 §PII Scrubbing + ADR-008 §Reuso.
    /// Expandido em v2.6.1 (hotfix P0 SECURITY-2) apos auditoria senior
    /// 2026-05-19 PHASE 6.3 identificar 3 vetores escapando.
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

        // v2.6.1 (hotfix P0 SECURITY-2): Windows user path localizado PT-BR.
        // Em Windows portugues, a pasta "Users" e localizada para "Usuarios"
        // (sem acento) ou "Usuários" (com acento, raro mas existe em algumas
        // instalacoes). Mesmo padrao do WindowsUserPathRegex, para o caso
        // localizado.
        private static readonly Regex WindowsUserPathPtBrRegex = new Regex(
            @"[A-Za-z]:\\Usu[aá]rios\\[^\\]+\\",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // v2.6.1 (hotfix P0 SECURITY-2): UNC path Windows.
        // '\\server\share\...' — scrubba server+share (2 segmentos) viram
        // '<UNC>\'. Frequentemente o share name carrega nome de cliente
        // (\\nas01\projetos-vulcaflex\). Gap conhecido: username APOS o share
        // continua exposto (\\nas\share\joao\... vira <UNC>\joao\...). Aceito
        // pela auditoria porque (a) o vetor primario era o share name, (b)
        // scrubbing mais agressivo (3+ segmentos) corre risco de comer paths
        // legitimos de subpastas.
        private static readonly Regex WindowsUncPathRegex = new Regex(
            @"\\\\[^\\]+\\[^\\]+\\",
            RegexOptions.Compiled);

        // v2.6.1 (hotfix P0 SECURITY-2): filename Revit.
        // .rvt (modelo), .rfa (familia), .rte (template), .rft (family template).
        // Frequentemente identificam cliente ("Projeto Cliente XYZ.rvt").
        // Preserva a extensao via $1 para que stack traces continuem dizendo
        // "era um .rvt" (rastreabilidade tecnica sem PII).
        // Word boundary (\b) impede match em substrings tipo "myextrft.cs".
        private static readonly Regex RevitFilenameRegex = new Regex(
            @"[^\\/:*?""<>|\s]+\.(rvt|rfa|rte|rft)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Sanitiza a string. Null → null (defensivo, nao throw).
        /// String vazia → vazia. String sem PII → intocada.
        ///
        /// Ordem dos passes: emails > paths drive (EN + PT-BR) > UNC > revit filename.
        /// A ordem importa apenas para o revit filename: ele deve rodar APOS
        /// os paths para nao "comer" um caminho completo como filename.
        /// </summary>
        public static string Scrub(string input)
        {
            if (input == null)
                return null;
            if (input.Length == 0)
                return input;

            string scrubbed = EmailRegex.Replace(input, "<EMAIL>");
            scrubbed = WindowsUserPathRegex.Replace(scrubbed, "<USER>\\");
            scrubbed = WindowsUserPathPtBrRegex.Replace(scrubbed, "<USER>\\");
            scrubbed = WindowsUncPathRegex.Replace(scrubbed, "<UNC>\\");
            scrubbed = RevitFilenameRegex.Replace(scrubbed, "<REVIT_FILE>.$1");
            return scrubbed;
        }
    }
}
