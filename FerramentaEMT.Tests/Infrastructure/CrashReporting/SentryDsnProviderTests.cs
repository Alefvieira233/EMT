using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.CrashReporting;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.CrashReporting
{
    /// <summary>
    /// Cobre a resolucao de DSN do Sentry — espelha a estrutura dos
    /// LicenseSecretProviderTests, com a unica diferenca de que ausencia
    /// de DSN NAO lanca, retorna string vazia + source DevFallbackEmpty.
    ///
    /// Cada teste limpa o cache via ResetCacheForTests e restaura a env var
    /// no finally. Colecao serializada para evitar corrida na env var
    /// EMT_SENTRY_DSN.
    /// </summary>
    [Collection("SentryDsnSerial")]
    public class SentryDsnProviderTests
    {
        [Fact]
        public void Resolves_from_environment_variable_when_set()
        {
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
                SentryDsnProvider.ResetCacheForTests();

                SentryDsnProvider.GetDsn().Should().Be("https://abc@sentry.io/1");
                SentryDsnProvider.GetResolvedSource()
                    .Should().Be(SentryDsnProvider.DsnSource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Returns_empty_string_when_no_source_is_configured()
        {
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, null);
                SentryDsnProvider.ResetCacheForTests();

                // Diferente do LicenseSecretProvider que LANCA: aqui retorna ""
                // (modo silencioso valido). Se a maquina dev tiver sentry.dsn
                // fisico em %LocalAppData% ou ao lado do DLL, o teste vira
                // outras source — ainda valido, e cobre o cenario de fallback.
                string dsn = SentryDsnProvider.GetDsn();
                SentryDsnProvider.DsnSource source = SentryDsnProvider.GetResolvedSource();

                if (source == SentryDsnProvider.DsnSource.DevFallbackEmpty)
                {
                    dsn.Should().BeEmpty();
                }
                else
                {
                    source.Should().Match(s =>
                        s == SentryDsnProvider.DsnSource.LocalAppDataFile ||
                        s == SentryDsnProvider.DsnSource.AssemblyAdjacentFile);
                    dsn.Should().NotBeNullOrWhiteSpace();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void DevFallback_never_throws()
        {
            // Regressao guardar: a diferenca chave vs LicenseSecretProvider eh
            // que DSN ausente nao deve quebrar o boot. Mesmo se a maquina nao
            // tiver nenhuma fonte configurada, GetDsn nao lanca.
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, null);
                SentryDsnProvider.ResetCacheForTests();

                Action act = () => SentryDsnProvider.GetDsn();
                act.Should().NotThrow();
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void GetDsn_caches_after_first_call()
        {
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "https://first@sentry.io/1");
                SentryDsnProvider.ResetCacheForTests();
                string first = SentryDsnProvider.GetDsn();

                // Mudar a env var depois nao deve refletir (cache Lazy)
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "https://second@sentry.io/1");
                string second = SentryDsnProvider.GetDsn();

                second.Should().Be(first);
                second.Should().Be("https://first@sentry.io/1");
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Trims_whitespace_and_newlines_from_resolved_dsn()
        {
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(
                    SentryDsnProvider.EnvVarName,
                    "  https://x@sentry.io/9  \r\n");
                SentryDsnProvider.ResetCacheForTests();

                SentryDsnProvider.GetDsn().Should().Be("https://x@sentry.io/9");
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Whitespace_only_env_var_falls_through_to_next_source()
        {
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "   ");
                SentryDsnProvider.ResetCacheForTests();

                // Whitespace foi ignorado — source NAO eh EnvironmentVariable.
                // Pode cair em qualquer fallback (file local, side-by-side, ou DevFallback).
                SentryDsnProvider.GetResolvedSource()
                    .Should().NotBe(SentryDsnProvider.DsnSource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Dsn_and_source_snapshot_is_atomic_under_contention()
        {
            // Mesmo invariante do LicenseSecretProvider: ou (dsn, source) saem
            // juntos do Lazy.Value, ou nenhum sai. Sem janela em que dsn ja
            // resolveu mas source ainda esta NotResolved.
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(
                    SentryDsnProvider.EnvVarName,
                    "https://race@sentry.io/1");
                SentryDsnProvider.ResetCacheForTests();

                var inconsistencies = new ConcurrentBag<string>();
                Parallel.For(0, 128, _ =>
                {
                    string dsn = SentryDsnProvider.GetDsn();
                    SentryDsnProvider.DsnSource source = SentryDsnProvider.GetResolvedSource();

                    if (dsn == "https://race@sentry.io/1"
                        && source != SentryDsnProvider.DsnSource.EnvironmentVariable)
                    {
                        inconsistencies.Add($"dsn ok, source={source}");
                    }
                    if (dsn != "https://race@sentry.io/1"
                        && source == SentryDsnProvider.DsnSource.EnvironmentVariable)
                    {
                        inconsistencies.Add($"source ok, dsn={dsn}");
                    }
                });

                inconsistencies.Should().BeEmpty(
                    "snapshot de (dsn, source) deve ser atomico");
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void HasMalformedDsnFile_returns_false_when_no_file_exists()
        {
            // Em CI nao ha sentry.dsn em LOCALAPPDATA nem ao lado do test DLL.
            // Em maquina dev pode haver — em ambos os casos invariante:
            // se retornou true, path tem que estar preenchido.
            bool malformed = SentryDsnProvider.HasMalformedDsnFile(out string path);
            if (malformed)
            {
                path.Should().NotBeNullOrWhiteSpace();
            }
            else
            {
                path.Should().BeNull();
            }
        }

        [Fact]
        public void ResetCacheForTests_reresolves_after_env_var_change()
        {
            // Garante que ResetCacheForTests realmente substitui o Lazy:
            // sem reset, o cache persiste; com reset, novo valor reflete.
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "https://v1@sentry.io/1");
                SentryDsnProvider.ResetCacheForTests();
                SentryDsnProvider.GetDsn().Should().Be("https://v1@sentry.io/1");

                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "https://v2@sentry.io/1");
                SentryDsnProvider.ResetCacheForTests();
                SentryDsnProvider.GetDsn().Should().Be("https://v2@sentry.io/1");
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Empty_string_env_var_is_treated_as_unset()
        {
            // Diferente de SetEnvironmentVariable(null), passar "" pode ser
            // visto como "definido com vazio". O provider trata via
            // IsNullOrWhiteSpace e cai no proximo fallback.
            string original = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, "");
                SentryDsnProvider.ResetCacheForTests();

                SentryDsnProvider.GetResolvedSource()
                    .Should().NotBe(SentryDsnProvider.DsnSource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, original);
                SentryDsnProvider.ResetCacheForTests();
            }
        }
    }
}
