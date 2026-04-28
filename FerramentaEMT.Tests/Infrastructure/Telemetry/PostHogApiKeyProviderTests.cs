using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre a resolucao de API key — espelho dos SentryDsnProviderTests
    /// (PR-3). Mesma estrutura de Collection serializada, mesmo conjunto
    /// de cenarios. A unica diferenca semantica vs Sentry eh a chave:
    /// EMT_POSTHOG_API_KEY em vez de EMT_SENTRY_DSN.
    /// </summary>
    [Collection("PostHogApiKeySerial")]
    public class PostHogApiKeyProviderTests
    {
        [Fact]
        public void Resolves_from_environment_variable_when_set()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_abc123");
                PostHogApiKeyProvider.ResetCacheForTests();

                PostHogApiKeyProvider.GetApiKey().Should().Be("phc_abc123");
                PostHogApiKeyProvider.GetResolvedSource()
                    .Should().Be(PostHogApiKeyProvider.ApiKeySource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Returns_empty_string_when_no_source_is_configured()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, null);
                PostHogApiKeyProvider.ResetCacheForTests();

                string apiKey = PostHogApiKeyProvider.GetApiKey();
                PostHogApiKeyProvider.ApiKeySource source = PostHogApiKeyProvider.GetResolvedSource();

                if (source == PostHogApiKeyProvider.ApiKeySource.DevFallbackEmpty)
                {
                    apiKey.Should().BeEmpty();
                }
                else
                {
                    source.Should().Match(s =>
                        s == PostHogApiKeyProvider.ApiKeySource.LocalAppDataFile ||
                        s == PostHogApiKeyProvider.ApiKeySource.AssemblyAdjacentFile);
                    apiKey.Should().NotBeNullOrWhiteSpace();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void DevFallback_never_throws()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, null);
                PostHogApiKeyProvider.ResetCacheForTests();

                Action act = () => PostHogApiKeyProvider.GetApiKey();
                act.Should().NotThrow();
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void GetApiKey_caches_after_first_call()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_first");
                PostHogApiKeyProvider.ResetCacheForTests();
                string first = PostHogApiKeyProvider.GetApiKey();

                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_second");
                string second = PostHogApiKeyProvider.GetApiKey();

                second.Should().Be(first);
                second.Should().Be("phc_first");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Trims_whitespace_and_newlines_from_resolved_key()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(
                    PostHogApiKeyProvider.EnvVarName,
                    "  phc_trimmed  \r\n");
                PostHogApiKeyProvider.ResetCacheForTests();

                PostHogApiKeyProvider.GetApiKey().Should().Be("phc_trimmed");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Whitespace_only_env_var_falls_through_to_next_source()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "   ");
                PostHogApiKeyProvider.ResetCacheForTests();

                PostHogApiKeyProvider.GetResolvedSource()
                    .Should().NotBe(PostHogApiKeyProvider.ApiKeySource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void ApiKey_and_source_snapshot_is_atomic_under_contention()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_race");
                PostHogApiKeyProvider.ResetCacheForTests();

                var inconsistencies = new ConcurrentBag<string>();
                Parallel.For(0, 128, _ =>
                {
                    string apiKey = PostHogApiKeyProvider.GetApiKey();
                    PostHogApiKeyProvider.ApiKeySource source = PostHogApiKeyProvider.GetResolvedSource();

                    if (apiKey == "phc_race"
                        && source != PostHogApiKeyProvider.ApiKeySource.EnvironmentVariable)
                    {
                        inconsistencies.Add($"key ok, source={source}");
                    }
                    if (apiKey != "phc_race"
                        && source == PostHogApiKeyProvider.ApiKeySource.EnvironmentVariable)
                    {
                        inconsistencies.Add($"source ok, key={apiKey}");
                    }
                });

                inconsistencies.Should().BeEmpty(
                    "snapshot de (apiKey, source) deve ser atomico");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void HasMalformedApiKeyFile_returns_false_when_no_file_exists()
        {
            bool malformed = PostHogApiKeyProvider.HasMalformedApiKeyFile(out string path);
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
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_v1");
                PostHogApiKeyProvider.ResetCacheForTests();
                PostHogApiKeyProvider.GetApiKey().Should().Be("phc_v1");

                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "phc_v2");
                PostHogApiKeyProvider.ResetCacheForTests();
                PostHogApiKeyProvider.GetApiKey().Should().Be("phc_v2");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Empty_string_env_var_is_treated_as_unset()
        {
            string original = Environment.GetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, "");
                PostHogApiKeyProvider.ResetCacheForTests();

                PostHogApiKeyProvider.GetResolvedSource()
                    .Should().NotBe(PostHogApiKeyProvider.ApiKeySource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogApiKeyProvider.EnvVarName, original);
                PostHogApiKeyProvider.ResetCacheForTests();
            }
        }
    }
}
