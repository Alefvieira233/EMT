using System;
using System.IO;
using System.Threading;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Resolve a API key do PostHog em tempo de execucao.
    ///
    /// Espelho 1:1 do <c>SentryDsnProvider</c> (PR-3): Lazy + snapshot
    /// atomico + ResetCacheForTests + GetResolvedSource. Mesma decisao
    /// consciente: ausencia de API key NAO lanca, retorna string vazia.
    /// Modo silencioso valido (TelemetryReporter vira no-op).
    ///
    /// Ordem de prioridade:
    ///   1. Variavel de ambiente EMT_POSTHOG_API_KEY (CI / dev local).
    ///   2. Arquivo %LOCALAPPDATA%\FerramentaEMT\posthog.apikey (deploy).
    ///   3. Arquivo posthog.apikey ao lado do FerramentaEMT.dll (portable).
    ///   4. DevFallbackEmpty: retorna "" e TelemetryReporter loga
    ///      "Telemetry disabled (no API key configured)".
    ///
    /// IMPORTANTE: NUNCA hardcode API key no codigo. NUNCA commit no repo.
    /// .gitignore ja exclui posthog.apikey + *.posthog.apikey. ADR-008
    /// documenta a fonte canonica em producao.
    /// </summary>
    public static class PostHogApiKeyProvider
    {
        public const string EnvVarName = "EMT_POSTHOG_API_KEY";
        public const string ApiKeyFileName = "posthog.apikey";

        private static Lazy<ResolvedApiKey> _lazy = NewLazy();

        private static Lazy<ResolvedApiKey> NewLazy() =>
            new Lazy<ResolvedApiKey>(ResolveSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>Fonte resolvida da API key, para diagnostico.</summary>
        public enum ApiKeySource
        {
            NotResolved,
            EnvironmentVariable,
            LocalAppDataFile,
            AssemblyAdjacentFile,
            DevFallbackEmpty
        }

        private readonly struct ResolvedApiKey
        {
            public ResolvedApiKey(string apiKey, ApiKeySource source) { ApiKey = apiKey; Source = source; }
            public string ApiKey { get; }
            public ApiKeySource Source { get; }
        }

        /// <summary>API key resolvida. String vazia = no-op (modo silencioso).</summary>
        public static string GetApiKey() => _lazy.Value.ApiKey;

        /// <summary>Fonte resolvida. Util para log de startup.</summary>
        public static ApiKeySource GetResolvedSource() => _lazy.Value.Source;

        internal static void ResetCacheForTests()
        {
            Interlocked.Exchange(ref _lazy, NewLazy());
        }

        private static ResolvedApiKey ResolveSnapshot()
        {
            ApiKeySource source;
            string key = ResolveFromSources(out source);
            return new ResolvedApiKey(key, source);
        }

        private static string ResolveFromSources(out ApiKeySource source)
        {
            string envValue = SafeReadEnvVar();
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                source = ApiKeySource.EnvironmentVariable;
                return envValue.Trim();
            }

            string localAppDataPath = TryBuildLocalAppDataPath();
            string fromLocalAppData = SafeReadFile(localAppDataPath);
            if (!string.IsNullOrWhiteSpace(fromLocalAppData))
            {
                source = ApiKeySource.LocalAppDataFile;
                return fromLocalAppData.Trim();
            }

            string assemblyPath = TryBuildAssemblyAdjacentPath();
            string fromAssemblyDir = SafeReadFile(assemblyPath);
            if (!string.IsNullOrWhiteSpace(fromAssemblyDir))
            {
                source = ApiKeySource.AssemblyAdjacentFile;
                return fromAssemblyDir.Trim();
            }

            source = ApiKeySource.DevFallbackEmpty;
            return string.Empty;
        }

        private static string SafeReadEnvVar()
        {
            try { return Environment.GetEnvironmentVariable(EnvVarName); }
            catch { return null; }
        }

        private static string TryBuildLocalAppDataPath()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root)) return null;
                return Path.Combine(root, "FerramentaEMT", ApiKeyFileName);
            }
            catch { return null; }
        }

        private static string TryBuildAssemblyAdjacentPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(PostHogApiKeyProvider).Assembly.Location);
                if (string.IsNullOrWhiteSpace(dir)) return null;
                return Path.Combine(dir, ApiKeyFileName);
            }
            catch { return null; }
        }

        private static string SafeReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return File.ReadAllText(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch { return null; }
        }

        /// <summary>
        /// Indica se algum arquivo de API key existe mas esta vazio. Sinal
        /// de bug de deploy. Util para o startup logger.
        /// </summary>
        public static bool HasMalformedApiKeyFile(out string offendingPath)
        {
            offendingPath = null;
            foreach (string candidate in new[] { TryBuildLocalAppDataPath(), TryBuildAssemblyAdjacentPath() })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string raw;
                try { raw = File.Exists(candidate) ? File.ReadAllText(candidate) : null; }
                catch { continue; }
                if (raw != null && string.IsNullOrWhiteSpace(raw))
                {
                    offendingPath = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
