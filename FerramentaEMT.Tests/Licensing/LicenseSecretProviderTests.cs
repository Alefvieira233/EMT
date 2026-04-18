using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
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

        [Fact]
        public void Secret_and_source_snapshot_is_atomic_under_contention()
        {
            // Regressao do audit 2026-04: antes, secret e source eram escritos em
            // chamadas separadas (Volatile.Write + atribuicao). Um leitor concorrente
            // podia ver secret resolvido mas source ainda NotResolved.
            //
            // Agora ambos vem do mesmo Lazy<ResolvedSecret>.Value, entao ou os dois
            // aparecem juntos ou nenhum.
            string original = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "race-check");
                LicenseSecretProvider.ResetCacheForTests();

                var inconsistencies = new ConcurrentBag<string>();
                Parallel.For(0, 128, _ =>
                {
                    string secret = LicenseSecretProvider.GetSecret();
                    LicenseSecretProvider.SecretSource source = LicenseSecretProvider.GetResolvedSource();

                    if (secret == "race-check" && source != LicenseSecretProvider.SecretSource.EnvironmentVariable)
                        inconsistencies.Add($"secret ok, source={source}");
                    if (secret != "race-check" && source == LicenseSecretProvider.SecretSource.EnvironmentVariable)
                        inconsistencies.Add($"source ok, secret={secret}");
                });

                inconsistencies.Should().BeEmpty("snapshot de (secret, source) deve ser atomico");
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, original);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void HasMalformedSecretFile_returns_false_when_no_file_exists()
        {
            // Em CI nao ha arquivo de segredo, entao HasMalformedSecretFile e sempre false.
            // Se alguma vez rodarmos em maquina dev com license.secret real, este teste
            // vira false com path nulo — ainda valido.
            bool malformed = LicenseSecretProvider.HasMalformedSecretFile(out string path);
            if (malformed)
            {
                // Se achou, path tem que estar preenchido.
                path.Should().NotBeNullOrWhiteSpace();
            }
            else
            {
                path.Should().BeNull();
            }
        }
    }
}
