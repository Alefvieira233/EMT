using System;
using FerramentaEMT.Licensing;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Licensing
{
    /// <summary>
    /// Cobre a resolucao de prioridades do segredo HMAC.
    /// Cada teste limpa o cache via ResetCacheForTests e restaura a env var no finally.
    /// Colecao compartilhada com KeySignerTests para serializar e evitar corrida na env var.
    /// </summary>
    [Collection("LicensingSerial")]
    public class LicenseSecretProviderTests
    {
        [Fact]
        public void Resolves_from_environment_variable_when_set()
        {
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "from-env-var");
                LicenseSecretProvider.ResetCacheForTests();

                LicenseSecretProvider.GetSecret().Should().Be("from-env-var");
                LicenseSecretProvider.GetResolvedSource()
                    .Should().Be(LicenseSecretProvider.SecretSource.EnvironmentVariable);
                LicenseSecretProvider.IsUsingDevOnlyFallback().Should().BeFalse();
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Falls_back_to_DevOnly_when_no_source_is_configured()
        {
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, null);
                LicenseSecretProvider.ResetCacheForTests();

                string secret = LicenseSecretProvider.GetSecret();
                // A menos que exista license.secret em LOCALAPPDATA ou ao lado do assembly
                // (cenario nao esperado em CI), deve cair no fallback DEV_ONLY.
                LicenseSecretProvider.SecretSource source = LicenseSecretProvider.GetResolvedSource();
                source.Should().Match(s =>
                    s == LicenseSecretProvider.SecretSource.DevOnlyFallback ||
                    s == LicenseSecretProvider.SecretSource.LocalAppDataFile ||
                    s == LicenseSecretProvider.SecretSource.AssemblyAdjacentFile);
                secret.Should().NotBeNullOrWhiteSpace();
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void GetSecret_caches_after_first_call()
        {
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "first-value");
                LicenseSecretProvider.ResetCacheForTests();
                string first = LicenseSecretProvider.GetSecret();

                // Mudar a env var nao deve refletir (esta cacheado)
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "second-value");
                string second = LicenseSecretProvider.GetSecret();

                second.Should().Be(first);
                second.Should().Be("first-value");
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Trims_whitespace_from_resolved_secret()
        {
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "  with-spaces  \r\n");
                LicenseSecretProvider.ResetCacheForTests();

                LicenseSecretProvider.GetSecret().Should().Be("with-spaces");
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Whitespace_only_env_var_is_ignored()
        {
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "   ");
                LicenseSecretProvider.ResetCacheForTests();

                LicenseSecretProvider.GetResolvedSource()
                    .Should().NotBe(LicenseSecretProvider.SecretSource.EnvironmentVariable);
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }
    }
}
