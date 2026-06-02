using FluentAssertions;
using SteelBIM.Licensing;
using Xunit;

namespace SteelBIM.Tests.Licensing
{
    /// <summary>
    /// Guard de GO-LIVE da licenca. Roda no CI e impede que uma build seja publicada
    /// com a chave publica ainda no placeholder (que faria todo comando cair em
    /// "trial silencioso" — nenhuma licenca paga validaria).
    /// </summary>
    [Collection("LicensingSerial")]
    public class LicenseKeysTests
    {
        [Fact]
        public void ChavePublica_NaoEhPlaceholder_LicencaAtivaEmProducao()
        {
            // Se alguem reverter LicenseKeys.PublicKeySpkiBase64 para o placeholder,
            // este teste FALHA o CI — barreira antes de shippar build sem licenca paga.
            // (Verifica a const diretamente, independente de TestOverride.)
            LicenseKeys.ChavePublicaEhPlaceholder.Should().BeFalse();
        }

        [Fact]
        public void CreatePublicKey_ImportaUmSpkiP256Valido()
        {
            // A chave ativa (embarcada ou override de teste) deve ser um SPKI P-256 valido.
            using var ec = LicenseKeys.CreatePublicKey();
            ec.Should().NotBeNull();
            ec.KeySize.Should().Be(256); // prime256v1 / P-256
        }
    }
}
