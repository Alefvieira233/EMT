using System;
using FerramentaEMT.Licensing;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Licensing
{
    [Collection("LicensingSerial")]
    public class KeySignerTests
    {
        private static LicensePayload SamplePayload(string email = "alef@exemplo.com", int diasFuturo = 365)
        {
            DateTime now = DateTime.UtcNow;
            return new LicensePayload
            {
                Email = email,
                IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
                ExpiresAtUnix = ((DateTimeOffset)now.AddDays(diasFuturo)).ToUnixTimeSeconds(),
                Version = 1,
            };
        }

        [Fact]
        public void Sign_then_Verify_returns_equivalent_payload()
        {
            string savedEnv = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "test-secret-keysigner");
                LicenseSecretProvider.ResetCacheForTests();

                var original = SamplePayload();

                string token = KeySigner.Sign(original);
                LicensePayload decoded = KeySigner.Verify(token);

                decoded.Should().NotBeNull();
                decoded.Email.Should().Be(original.Email);
                decoded.IssuedAtUnix.Should().Be(original.IssuedAtUnix);
                decoded.ExpiresAtUnix.Should().Be(original.ExpiresAtUnix);
                decoded.Version.Should().Be(original.Version);
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, savedEnv);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Verify_returns_null_for_garbage_input()
        {
            KeySigner.Verify("not.a.real.token").Should().BeNull();
            KeySigner.Verify("").Should().BeNull();
            KeySigner.Verify(null).Should().BeNull();
            KeySigner.Verify("nodot").Should().BeNull();
        }

        [Fact]
        public void Verify_returns_null_when_payload_is_tampered()
        {
            string savedEnv = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "test-secret-keysigner");
                LicenseSecretProvider.ResetCacheForTests();

                string token = KeySigner.Sign(SamplePayload());
                int dot = token.IndexOf('.');
                // troca o primeiro caractere do payload por um diferente — invalida o HMAC
                string tampered = (token[0] == 'A' ? 'B' : 'A') + token.Substring(1);

                KeySigner.Verify(tampered).Should().BeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, savedEnv);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Verify_returns_null_when_signature_is_tampered()
        {
            string savedEnv = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "test-secret-keysigner");
                LicenseSecretProvider.ResetCacheForTests();

                string token = KeySigner.Sign(SamplePayload());
                // muda o ultimo caractere (parte da assinatura)
                char last = token[token.Length - 1];
                char repl = last == 'A' ? 'B' : 'A';
                string tampered = token.Substring(0, token.Length - 1) + repl;

                KeySigner.Verify(tampered).Should().BeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, savedEnv);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }

        [Fact]
        public void Sign_throws_on_null_payload()
        {
            Action act = () => KeySigner.Sign(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Sign_is_deterministic_for_same_payload()
        {
            string savedEnv = Environment.GetEnvironmentVariable(LicenseSecretProvider.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "test-secret-keysigner");
                LicenseSecretProvider.ResetCacheForTests();

                var p = SamplePayload();
                string a = KeySigner.Sign(p);
                string b = KeySigner.Sign(p);
                a.Should().Be(b);
            }
            finally
            {
                Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, savedEnv);
                LicenseSecretProvider.ResetCacheForTests();
            }
        }
    }
}
