using System;
using System.IO;
using System.Text;
using System.Threading;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Identificador anonimo de instalacao para telemetria PostHog.
    ///
    /// Garantias CRITICAS (auditadas via teste):
    ///   1. UUID v4 puro — gerado via Guid.NewGuid() (RNG do CLR).
    ///   2. NUNCA derivado de Environment.MachineName, MAC address,
    ///      BIOS, fingerprint, username, ou qualquer outra fonte que
    ///      identifique o usuario ou a maquina.
    ///   3. Persistido em %LocalAppData%\FerramentaEMT\session-id.json
    ///      pelo lifetime daquela instalacao. Reset = deletar o arquivo
    ///      manualmente.
    ///
    /// O criterio anonimo eh por design (briefing PR-4 §4.4): Alef sabe
    /// quantos usuarios ativos tem, NAO sabe QUEM. Documentado em
    /// ADR-008 + Privacy Policy (PR-6).
    ///
    /// Override de path para tests: env var EMT_SESSION_ID_PATH.
    /// </summary>
    public static class SessionIdProvider
    {
        public const string FileName = "session-id.json";
        public const string TestPathOverrideEnvVar = "EMT_SESSION_ID_PATH";

        private static readonly object _lock = new object();
        private static string _cachedSessionId;

        /// <summary>
        /// Retorna o session ID. Cria + persiste se ainda nao existir.
        /// Idempotente, thread-safe. Try/catch raiz: se IO falhar
        /// retorna um Guid in-memory (sem persistir) para nao quebrar
        /// a telemetria. Esse fallback ainda eh anonimo — apenas nao
        /// persiste entre boots.
        /// </summary>
        public static string GetOrCreate()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cachedSessionId))
                    return _cachedSessionId;

                string path = ResolvePath();
                string id = TryRead(path);
                if (!string.IsNullOrEmpty(id) && IsValidGuid(id))
                {
                    _cachedSessionId = id;
                    return _cachedSessionId;
                }

                // Cria novo via CLR-managed RNG. NUNCA combina com
                // MachineName / username / hardware.
                Guid newId = Guid.NewGuid();
                string asString = newId.ToString("D"); // 8-4-4-4-12 lowercase

                TryWrite(path, asString);
                _cachedSessionId = asString;
                return _cachedSessionId;
            }
        }

        /// <summary>Primeiros 8 chars do UUID — util para log de startup.</summary>
        public static string GetShortPrefix()
        {
            string id = GetOrCreate();
            if (string.IsNullOrEmpty(id) || id.Length < 8) return id ?? string.Empty;
            return id.Substring(0, 8);
        }

        /// <summary>Path resolvido (env override ou %LocalAppData%).</summary>
        public static string FilePath => ResolvePath();

        internal static void ResetCacheForTests()
        {
            lock (_lock) { _cachedSessionId = null; }
        }

        private static string ResolvePath()
        {
            try
            {
                string overridePath = Environment.GetEnvironmentVariable(TestPathOverrideEnvVar);
                if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
            }
            catch { /* ignore */ }

            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root)) return null;
                return Path.Combine(root, "FerramentaEMT", FileName);
            }
            catch { return null; }
        }

        private static string TryRead(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!File.Exists(path)) return null;
                string raw = File.ReadAllText(path);
                return ParseJson(raw);
            }
            catch { return null; }
        }

        private static void TryWrite(string path, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                StringBuilder sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"session_id\": \"").Append(sessionId).Append("\",\n");
                sb.Append("  \"created_at_utc\": \"")
                  .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                  .Append("\"\n");
                sb.Append("}\n");

                File.WriteAllText(path, sb.ToString());
            }
            catch { /* falha de IO eh nao-fatal — id em memoria persiste so na sessao */ }
        }

        // Parser minimo. Nao usar System.Text.Json aqui pra reduzir
        // surface de deps — somos um Provider de boot, executa antes
        // de tudo.
        private static string ParseJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            const string key = "\"session_id\"";
            int idx = raw.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = raw.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            int firstQuote = raw.IndexOf('"', colon + 1);
            if (firstQuote < 0) return null;
            int secondQuote = raw.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return null;
            return raw.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static bool IsValidGuid(string s)
        {
            return Guid.TryParse(s, out _);
        }
    }
}
