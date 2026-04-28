using System;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre o provider de host: default eu.posthog.com (LGPD-friendly),
    /// override via env var EMT_POSTHOG_HOST.
    /// </summary>
    [Collection("PostHogApiKeySerial")]
    public class PostHogHostProviderTests
    {
        [Fact]
        public void Default_host_is_eu_posthog_com_when_env_var_unset()
        {
            string original = Environment.GetEnvironmentVariable(PostHogHostProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, null);
                PostHogHostProvider.ResetCacheForTests();

                PostHogHostProvider.GetHost().Should().Be("https://eu.posthog.com");
                PostHogHostProvider.GetResolvedSource()
                    .Should().Be(PostHogHostProvider.HostSource.Default);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, original);
                PostHogHostProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Env_var_overrides_default_host()
        {
            string original = Environment.GetEnvironmentVariable(PostHogHostProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(
                    PostHogHostProvider.EnvVarName,
                    "https://posthog.example.com");
                PostHogHostProvider.ResetCacheForTests();

                PostHogHostProvider.GetHost().Should().Be("https://posthog.example.com");
                PostHogHostProvider.GetResolvedSource()
                    .Should().Be(PostHogHostProvider.HostSource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, original);
                PostHogHostProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Whitespace_env_var_falls_back_to_default()
        {
            string original = Environment.GetEnvironmentVariable(PostHogHostProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, "   ");
                PostHogHostProvider.ResetCacheForTests();

                PostHogHostProvider.GetHost().Should().Be("https://eu.posthog.com");
                PostHogHostProvider.GetResolvedSource()
                    .Should().Be(PostHogHostProvider.HostSource.Default);
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, original);
                PostHogHostProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void GetHost_caches_after_first_call()
        {
            string original = Environment.GetEnvironmentVariable(PostHogHostProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(
                    PostHogHostProvider.EnvVarName,
                    "https://h1.example.com");
                PostHogHostProvider.ResetCacheForTests();
                string first = PostHogHostProvider.GetHost();

                Environment.SetEnvironmentVariable(
                    PostHogHostProvider.EnvVarName,
                    "https://h2.example.com");
                string second = PostHogHostProvider.GetHost();

                second.Should().Be(first);
                second.Should().Be("https://h1.example.com");
            }
            finally
            {
                Environment.SetEnvironmentVariable(PostHogHostProvider.EnvVarName, original);
                PostHogHostProvider.ResetCacheForTests();
            }
        }
    }
}
