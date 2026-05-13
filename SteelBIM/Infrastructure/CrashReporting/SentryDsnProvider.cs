using System;
using System.IO;
using System.Threading;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Resolve o Sentry DSN em tempo de execucao.
    ///
    /// Espelha LicenseSecretProvider em padrao (Lazy + snapshot atomico +
    /// ResetCacheForTests + GetResolvedSource), com UMA diferenca consciente:
    /// quando nenhuma fonte responde, NAO lanca — retorna string vazia. DSN
    /// ausente eh modo silencioso valido (SentryReporter vira no-op).
    ///
    /// Ordem de prioridade:
    ///   1. Variavel de ambiente EMT_SENTRY_DSN (CI / dev local).
    ///   2. Arquivo %LOCALAPPDATA%\FerramentaEMT\sentry.dsn (deploy production).
    ///   3. Arquivo sentry.dsn ao lado do FerramentaEMT.dll (alternativa portable).
    ///   4. DevFallbackEmpty: retorna "" e SentryReporter loga
    ///      "Sentry disabled (no DSN configured)".
    ///
    /// IMPORTANTE: NUNCA hardcode DSN no codigo. NUNCA commit DSN no repo.
    /// .gitignore ja exclui sentry.dsn e *.sentry.dsn. ADR-007 documenta
    /// a fonte canonica em producao (LocalAppData via instalador).
    /// </summary>
    public static class SentryDsnProvider
    {
        public const string EnvVarName = "EMT_SENTRY_DSN";
        public const string DsnFileName = "sentry.dsn";

        // Snapshot atomico (dsn, source). Mesma tecnica do LicenseSecretProvider:
        // Lazy<T> com ExecutionAndPublication elimina a janela em que outro
        // thread poderia ler dsn != null mas source == NotResolved.
        private static Lazy<ResolvedDsn> _lazy = NewLazy();

        private static Lazy<ResolvedDsn> NewLazy() =>
            new Lazy<ResolvedDsn>(ResolveSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>Fonte resolvida do DSN, para diagnostico em log de startup.</summary>
        public enum DsnSource
        {
            NotResolved,
            EnvironmentVariable,
            LocalAppDataFile,
            AssemblyAdjacentFile,
            DevFallbackEmpty
        }

        private readonly struct ResolvedDsn
        {
            public ResolvedDsn(string dsn, DsnSource source) { Dsn = dsn; Source = source; }
            public string Dsn { get; }
            public DsnSource Source { get; }
        }

        /// <summary>
        /// DSN resolvido. String vazia significa "sem DSN configurado" — nao
        /// inicialize Sentry. Comportamento valido (modo silencioso).
        /// </summary>
        public static string GetDsn() => _lazy.Value.Dsn;

        /// <summary>Fonte resolvida do DSN. Util para log de startup.</summary>
        public static DsnSource GetResolvedSource() => _lazy.Value.Source;

        /// <summary>
        /// Limpa o cache de resolucao. Uso exclusivo em testes — sem isso,
        /// o primeiro Lazy.Value congela o resultado para o resto do processo.
        /// </summary>
        internal static void ResetCacheForTests()
        {
            Interlocked.Exchange(ref _lazy, NewLazy());
        }

        private static ResolvedDsn ResolveSnapshot()
        {
            DsnSource source;
            string dsn = ResolveFromSources(out source);
            return new ResolvedDsn(dsn, source);
        }

        private static string ResolveFromSources(out DsnSource source)
        {
            // 1. variavel de ambiente
            string envValue = SafeReadEnvVar();
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                source = DsnSource.EnvironmentVariable;
                return envValue.Trim();
            }

            // 2. arquivo em %LOCALAPPDATA%\FerramentaEMT
            string localAppDataPath = TryBuildLocalAppDataPath();
            string fromLocalAppData = SafeReadFile(localAppDataPath);
            if (!string.IsNullOrWhiteSpace(fromLocalAppData))
            {
                source = DsnSource.LocalAppDataFile;
                return fromLocalAppData.Trim();
            }

            // 3. arquivo ao lado do assembly
            string assemblyPath = TryBuildAssemblyAdjacentPath();
            string fromAssemblyDir = SafeReadFile(assemblyPath);
            if (!string.IsNullOrWhiteSpace(fromAssemblyDir))
            {
                source = DsnSource.AssemblyAdjacentFile;
                return fromAssemblyDir.Trim();
            }

            // 4. nenhuma fonte — modo silencioso. Diferente do LicenseSecretProvider
            //    que LANCA aqui, porque licenca ausente eh erro fatal e DSN ausente
            //    eh comportamento valido (dev local, instalacao portable sem cliente
            //    final, ou opt-out manual).
            source = DsnSource.DevFallbackEmpty;
            return string.Empty;
        }

        private static string SafeReadEnvVar()
        {
            try
            {
                return Environment.GetEnvironmentVariable(EnvVarName);
            }
            catch
            {
                return null;
            }
        }

        private static string TryBuildLocalAppDataPath()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root))
                    return null;
                return Path.Combine(root, "FerramentaEMT", DsnFileName);
            }
            catch
            {
                return null;
            }
        }

        private static string TryBuildAssemblyAdjacentPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(SentryDsnProvider).Assembly.Location);
                if (string.IsNullOrWhiteSpace(dir))
                    return null;
                return Path.Combine(dir, DsnFileName);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                return File.ReadAllText(path);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch
            {
                // Permissao negada / IO transient: nao escala — proxima fonte assume.
                return null;
            }
        }

        /// <summary>
        /// Indica se o arquivo de DSN existe mas esta vazio/whitespace-only.
        /// Sinal de bug de deploy (instalador gravou string vazia, por exemplo).
        /// Util para o startup logger distinguir "arquivo nao configurado" de
        /// "arquivo configurado errado".
        /// </summary>
        public static bool HasMalformedDsnFile(out string offendingPath)
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
