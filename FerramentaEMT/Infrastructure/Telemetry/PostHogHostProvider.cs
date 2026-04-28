using System;
using System.Threading;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Resolve o endpoint do PostHog em runtime. Default eh
    /// <c>eu.posthog.com</c> (LGPD-friendly: dados na Uniao Europeia,
    /// jurisdicao mais alinhada com clientes brasileiros que us.posthog.com).
    /// Override via env var EMT_POSTHOG_HOST (CI/dev override; clientes
    /// self-hosted PostHog).
    ///
    /// Lazy + ResetCacheForTests, mesmo padrao do PostHogApiKeyProvider.
    /// Documentado no ADR-008 §"Endpoint privacy".
    /// </summary>
    public static class PostHogHostProvider
    {
        public const string EnvVarName = "EMT_POSTHOG_HOST";
        public const string DefaultHost = "https://eu.posthog.com";

        public enum HostSource
        {
            NotResolved,
            EnvironmentVariable,
            Default
        }

        private static Lazy<ResolvedHost> _lazy = NewLazy();

        private static Lazy<ResolvedHost> NewLazy() =>
            new Lazy<ResolvedHost>(ResolveSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly struct ResolvedHost
        {
            public ResolvedHost(string host, HostSource source) { Host = host; Source = source; }
            public string Host { get; }
            public HostSource Source { get; }
        }

        public static string GetHost() => _lazy.Value.Host;
        public static HostSource GetResolvedSource() => _lazy.Value.Source;

        internal static void ResetCacheForTests()
        {
            Interlocked.Exchange(ref _lazy, NewLazy());
        }

        private static ResolvedHost ResolveSnapshot()
        {
            string envValue = SafeReadEnvVar();
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return new ResolvedHost(envValue.Trim(), HostSource.EnvironmentVariable);
            }
            return new ResolvedHost(DefaultHost, HostSource.Default);
        }

        private static string SafeReadEnvVar()
        {
            try { return Environment.GetEnvironmentVariable(EnvVarName); }
            catch { return null; }
        }
    }
}
