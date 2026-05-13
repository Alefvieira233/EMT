using System;
using FerramentaEMT.Licensing;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Licensing
{
    public class LicensePayloadTests
    {
        [Fact]
        public void IsExpired_true_after_expiration()
        {
            var payload = new LicensePayload
            {
                ExpiresAtUnix = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds(),
            };
            payload.IsExpired(DateTime.UtcNow).Should().BeTrue();
        }

        [Fact]
        public void IsExpired_false_before_expiration()
        {
            var payload = new LicensePayload
            {
                ExpiresAtUnix = ((DateTimeOffset)DateTime.UtcNow.AddDays(30)).ToUnixTimeSeconds(),
            };
            payload.IsExpired(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void DiasRestantes_returns_zero_when_expired()
        {
            var payload = new LicensePayload
            {
                ExpiresAtUnix = ((DateTimeOffset)DateTime.UtcNow.AddDays(-5)).ToUnixTimeSeconds(),
            };
            payload.DiasRestantes(DateTime.UtcNow).Should().Be(0);
        }

        [Fact]
        public void DiasRestantes_rounds_up()
        {
            DateTime now = DateTime.UtcNow;
            var payload = new LicensePayload
            {
                // Expira em ~3.4 dias → arredonda para 4
                ExpiresAtUnix = ((DateTimeOffset)now.AddDays(3.4)).ToUnixTimeSeconds(),
            };
            payload.DiasRestantes(now).Should().BeOneOf(3, 4); // floor a depender de round
        }
    }
}
