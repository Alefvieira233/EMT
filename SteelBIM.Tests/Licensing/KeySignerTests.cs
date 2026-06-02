using System;
using System.Security.Cryptography;
using FluentAssertions;
using SteelBIM.Licensing;
using Xunit;

namespace SteelBIM.Tests.Licensing
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
            LicenseTestKeys.InstallEphemeral();

            var original = SamplePayload();

            string token = KeySigner.Sign(original);
            LicensePayload decoded = KeySigner.Verify(token);

            decoded.Should().NotBeNull();
            decoded.Email.Should().Be(original.Email);
            decoded.IssuedAtUnix.Should().Be(original.IssuedAtUnix);
            decoded.ExpiresAtUnix.Should().Be(original.ExpiresAtUnix);
            decoded.Version.Should().Be(original.Version);
        }

        [Fact]
        public void Verify_returns_null_for_garbage_input()
        {
            // Mesmo sem chave instalada, garbage deve retornar null (fail-closed).
            // O TestOverride pode ou nao estar setado por outros testes — nao importa.
            KeySigner.Verify("not.a.real.token").Should().BeNull();
            KeySigner.Verify("").Should().BeNull();
            KeySigner.Verify(null).Should().BeNull();
            KeySigner.Verify("nodot").Should().BeNull();
        }

        [Fact]
        public void Verify_returns_null_when_payload_is_tampered()
        {
            LicenseTestKeys.InstallEphemeral();

            string token = KeySigner.Sign(SamplePayload());
            int dot = token.IndexOf('.');
            // troca o primeiro caractere do payload por um diferente — invalida a assinatura
            string tampered = (token[0] == 'A' ? 'B' : 'A') + token.Substring(1);

            KeySigner.Verify(tampered).Should().BeNull();
        }

        [Fact]
        public void Verify_returns_null_when_signature_is_tampered()
        {
            LicenseTestKeys.InstallEphemeral();

            string token = KeySigner.Sign(SamplePayload());
            // muda o ultimo caractere (parte da assinatura)
            char last = token[token.Length - 1];
            char repl = last == 'A' ? 'B' : 'A';
            string tampered = token.Substring(0, token.Length - 1) + repl;

            KeySigner.Verify(tampered).Should().BeNull();
        }

        [Fact]
        public void Sign_throws_on_null_payload()
        {
            LicenseTestKeys.InstallEphemeral();
            Action act = () => KeySigner.Sign(null);
            act.Should().Throw<ArgumentNullException>();
        }

        // ECDSA e' randomizado: SignData gera assinatura diferente a cada chamada.
        // O teste de determinismo da era HMAC nao se aplica mais (removido).

        [Fact]
        public void Verify_ComChavePublicaErrada_RetornaNull()
        {
            // Cenario: token foi assinado com a chave instalada, mas tentamos verificar
            // com OUTRA chave publica (par diferente). A verificacao deve falhar.
            // Isso confirma a separacao real entre privada (assina) e publica (verifica).
            LicenseTestKeys.InstallEphemeral();
            string token = KeySigner.Sign(SamplePayload());

            using ECDsa outra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            KeySigner.Verify(token, outra).Should().BeNull();   // chave errada rejeita
            KeySigner.Verify(token).Should().NotBeNull();        // chave correta valida
        }
    }
}
