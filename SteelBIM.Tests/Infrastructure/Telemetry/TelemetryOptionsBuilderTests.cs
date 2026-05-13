using System.Collections.Generic;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre o builder de telemetry options:
    ///   - 6 super properties presentes (version, revit_version, os,
    ///     culture, license_state, session_id).
    ///   - Scrubbing de PII em values string (delegando ao PiiScrubber).
    ///   - ScrubAndTag preserva tipos nao-string das properties originais.
    ///   - Fallbacks "unknown"/"Unknown" para inputs vazios/null.
    /// </summary>
    public class TelemetryOptionsBuilderTests
    {
        // ---------- BuildSuperProperties ----------

        [Fact]
        public void BuildSuperProperties_includes_all_six_required_keys()
        {
            IReadOnlyDictionary<string, string> props = TelemetryOptionsBuilder.BuildSuperProperties(
                "1.7.0", "Trial", "550e8400-e29b-41d4-a716-446655440000");

            props.Should().ContainKeys("version", "revit_version", "os", "culture", "license_state", "session_id");
            props["version"].Should().Be("1.7.0");
            props["revit_version"].Should().Be("2025");
            props["license_state"].Should().Be("Trial");
            props["session_id"].Should().Be("550e8400-e29b-41d4-a716-446655440000");
            props["os"].Should().NotBeNullOrWhiteSpace();
            props["culture"].Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void BuildSuperProperties_uses_unknown_for_null_release()
        {
            IReadOnlyDictionary<string, string> props = TelemetryOptionsBuilder.BuildSuperProperties(
                null, "Valid", "session-x");
            props["version"].Should().Be("unknown");
        }

        [Fact]
        public void BuildSuperProperties_uses_unknown_for_empty_license_state()
        {
            IReadOnlyDictionary<string, string> props = TelemetryOptionsBuilder.BuildSuperProperties(
                "1.7.0", "", "session-x");
            props["license_state"].Should().Be("Unknown");
        }

        [Fact]
        public void BuildSuperProperties_uses_unknown_for_null_session_id()
        {
            IReadOnlyDictionary<string, string> props = TelemetryOptionsBuilder.BuildSuperProperties(
                "1.7.0", "Valid", null);
            props["session_id"].Should().Be("unknown");
        }

        // ---------- ScrubProperties ----------

        [Fact]
        public void ScrubProperties_substitutes_email_in_string_value()
        {
            var props = new Dictionary<string, object> { { "msg", "joao@x.com falhou" } };
            IReadOnlyDictionary<string, object> scrubbed = TelemetryOptionsBuilder.ScrubProperties(props);

            scrubbed["msg"].Should().Be("<EMAIL> falhou");
        }

        [Fact]
        public void ScrubProperties_substitutes_windows_path_in_string_value()
        {
            var props = new Dictionary<string, object>
            {
                { "path", @"C:\Users\maria\Desktop\file.rvt" }
            };
            IReadOnlyDictionary<string, object> scrubbed = TelemetryOptionsBuilder.ScrubProperties(props);

            scrubbed["path"].Should().Be(@"<USER>\Desktop\file.rvt");
        }

        [Fact]
        public void ScrubProperties_preserves_non_string_values()
        {
            var props = new Dictionary<string, object>
            {
                { "duration_ms", 1234 },
                { "success", true },
                { "ratio", 0.85 },
            };
            IReadOnlyDictionary<string, object> scrubbed = TelemetryOptionsBuilder.ScrubProperties(props);

            scrubbed["duration_ms"].Should().Be(1234);
            scrubbed["success"].Should().Be(true);
            scrubbed["ratio"].Should().Be(0.85);
        }

        [Fact]
        public void ScrubProperties_handles_null_and_empty_input()
        {
            TelemetryOptionsBuilder.ScrubProperties(null).Should().BeEmpty();
            TelemetryOptionsBuilder.ScrubProperties(new Dictionary<string, object>()).Should().BeEmpty();
        }

        // ---------- ScrubAndTag (combined) ----------

        [Fact]
        public void ScrubAndTag_returns_null_when_event_is_null()
        {
            TelemetryEvent result = TelemetryOptionsBuilder.ScrubAndTag(null, "1.7.0", "Trial", "session-x");
            result.Should().BeNull();
        }

        [Fact]
        public void ScrubAndTag_merges_scrubbed_properties_with_super_properties()
        {
            var props = new Dictionary<string, object>
            {
                { "command_name", "CmdGerarTrelica" },
                { "duration_ms", 1500 },
                { "user_email", "alef@empresa.com" },  // sera scrubbado
            };
            TelemetryEvent input = new TelemetryEvent("command.executed", props);

            TelemetryEvent result = TelemetryOptionsBuilder.ScrubAndTag(
                input, "1.7.0", "Trial", "550e8400-e29b-41d4-a716-446655440000");

            result.Name.Should().Be("command.executed");
            result.Properties["command_name"].Should().Be("CmdGerarTrelica");
            result.Properties["duration_ms"].Should().Be(1500);
            result.Properties["user_email"].Should().Be("<EMAIL>");
            // Super properties merged
            result.Properties["version"].Should().Be("1.7.0");
            result.Properties["revit_version"].Should().Be("2025");
            result.Properties["license_state"].Should().Be("Trial");
            result.Properties["session_id"].Should().Be("550e8400-e29b-41d4-a716-446655440000");
        }
    }
}
