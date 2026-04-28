using System;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Models.Privacy;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Privacy
{
    public class PrivacyJsonTests
    {
        [Fact]
        public void Roundtrip_preserva_todos_os_campos()
        {
            PrivacySettings original = new PrivacySettings
            {
                ConsentVersion = 1,
                AutoUpdate = ConsentState.Granted,
                LastUpdateCheckUtc = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
                SkippedUpdateVersion = "1.7.1",
                CrashReports = ConsentState.Denied,
                Telemetry = ConsentState.Unset,
            };

            string json = PrivacyJson.Serialize(original);
            PrivacySettings round = PrivacyJson.DeserializeOrDefault(json);

            round.ConsentVersion.Should().Be(1);
            round.AutoUpdate.Should().Be(ConsentState.Granted);
            round.LastUpdateCheckUtc.Should().Be(original.LastUpdateCheckUtc);
            round.SkippedUpdateVersion.Should().Be("1.7.1");
            round.CrashReports.Should().Be(ConsentState.Denied);
            round.Telemetry.Should().Be(ConsentState.Unset);
        }

        [Fact]
        public void DeserializeOrDefault_string_vazia_retorna_defaults()
        {
            PrivacySettings result = PrivacyJson.DeserializeOrDefault("");
            result.Should().NotBeNull();
            result.ConsentVersion.Should().Be(0);
            result.AutoUpdate.Should().Be(ConsentState.Unset);
            result.CrashReports.Should().Be(ConsentState.Unset);
            result.Telemetry.Should().Be(ConsentState.Unset);
        }

        [Fact]
        public void DeserializeOrDefault_null_retorna_defaults()
        {
            PrivacySettings result = PrivacyJson.DeserializeOrDefault(null);
            result.Should().NotBeNull();
            result.AutoUpdate.Should().Be(ConsentState.Unset);
        }

        [Fact]
        public void DeserializeOrDefault_json_corrompido_retorna_defaults()
        {
            PrivacySettings result = PrivacyJson.DeserializeOrDefault("{ broken json !!!");
            result.Should().NotBeNull();
            result.AutoUpdate.Should().Be(ConsentState.Unset);
        }

        [Fact]
        public void DeserializeOrDefault_json_parcial_preenche_o_que_existir()
        {
            // Frente compatibilidade: PR-3/4 vao adicionar campos que esta versao nao conhece;
            // versao antiga nao deve crashar lendo um privacy.json com campos extras.
            string json = @"{ ""ConsentVersion"": 2, ""AutoUpdate"": 1, ""CampoFuturoNovo"": ""xyz"" }";
            PrivacySettings result = PrivacyJson.DeserializeOrDefault(json);
            result.ConsentVersion.Should().Be(2);
            result.AutoUpdate.Should().Be(ConsentState.Granted);
        }

        [Fact]
        public void Serialize_null_lanca_ArgumentNullException()
        {
            Action act = () => PrivacyJson.Serialize(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Serialize_emite_json_legivel()
        {
            // Smoke test: confere que ConsentVersion e AutoUpdate aparecem no JSON
            // (formato exato pode mudar; importante eh round-trip preservar).
            PrivacySettings settings = new PrivacySettings
            {
                ConsentVersion = 1,
                AutoUpdate = ConsentState.Granted,
            };

            string json = PrivacyJson.Serialize(settings);
            json.Should().Contain("\"ConsentVersion\"");
            json.Should().Contain("\"AutoUpdate\"");
        }
    }
}
