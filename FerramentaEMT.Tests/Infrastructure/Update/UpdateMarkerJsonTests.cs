using System;
using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    public class UpdateMarkerJsonTests
    {
        [Fact]
        public void Roundtrip_preserva_todos_os_campos()
        {
            UpdateMarker original = new UpdateMarker
            {
                Version = "v1.7.0",
                ZipPath = @"C:\Users\X\AppData\Local\FerramentaEMT\Updates\1.7.0.zip",
                Sha256 = "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789",
                DownloadedAtUtc = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
                AttemptCount = 1,
            };

            string json = UpdateMarkerJson.Serialize(original);
            UpdateMarker round = UpdateMarkerJson.DeserializeOrNull(json);

            round.Should().NotBeNull();
            round.Version.Should().Be(original.Version);
            round.ZipPath.Should().Be(original.ZipPath);
            round.Sha256.Should().Be(original.Sha256);
            round.DownloadedAtUtc.Should().Be(original.DownloadedAtUtc);
            round.AttemptCount.Should().Be(original.AttemptCount);
        }

        [Fact]
        public void DeserializeOrNull_json_corrompido_retorna_null()
        {
            UpdateMarkerJson.DeserializeOrNull("{ broken !!!").Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void DeserializeOrNull_vazio_retorna_null(string json)
        {
            UpdateMarkerJson.DeserializeOrNull(json).Should().BeNull();
        }

        [Fact]
        public void DeserializeOrNull_sem_Version_retorna_null()
        {
            // Marker sem Version eh invalido pelo contrato — caller deve descartar
            string json = @"{ ""ZipPath"": ""C:\\foo.zip"", ""AttemptCount"": 0 }";
            UpdateMarkerJson.DeserializeOrNull(json).Should().BeNull();
        }

        [Fact]
        public void Serialize_null_lanca()
        {
            Action act = () => UpdateMarkerJson.Serialize(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
