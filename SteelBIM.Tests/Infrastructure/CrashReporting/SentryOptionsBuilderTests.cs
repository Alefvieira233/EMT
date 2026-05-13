using System;
using FerramentaEMT.Infrastructure.CrashReporting;
using FluentAssertions;
using Sentry;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.CrashReporting
{
    /// <summary>
    /// Cobre o builder de SentryOptions:
    ///   - Sample rates / breadcrumbs / DSN / release sao aplicados como esperado.
    ///   - ScrubAndTag injeta as 5 tags obrigatorias.
    ///   - ScrubAndTag remove email + path nas mensagens (delegando pro PiiScrubber).
    ///   - License state eh resolvido lazy via provider, com fallback "Unknown".
    /// </summary>
    public class SentryOptionsBuilderTests
    {
        // ---------- Build(...) ----------

        [Fact]
        public void Build_assigns_dsn_and_release()
        {
            SentryOptions options = SentryOptionsBuilder.Build(
                "https://test@sentry.io/1",
                "1.6.0",
                () => "Trial");

            options.Dsn.Should().Be("https://test@sentry.io/1");
            options.Release.Should().Be("1.6.0");
        }

        [Fact]
        public void Build_disables_traces_and_profiling()
        {
            SentryOptions options = SentryOptionsBuilder.Build(
                "https://test@sentry.io/1",
                "1.6.0",
                () => "Valid");

            options.TracesSampleRate.Should().Be(0.0);
            options.ProfilesSampleRate.Should().Be(0.0);
        }

        [Fact]
        public void Build_sets_errors_sample_rate_to_full()
        {
            SentryOptions options = SentryOptionsBuilder.Build(
                "https://test@sentry.io/1",
                "1.6.0",
                () => "Valid");

            options.SampleRate.Should().Be(1.0f);
        }

        [Fact]
        public void Build_caps_breadcrumbs_to_30()
        {
            SentryOptions options = SentryOptionsBuilder.Build(
                "https://test@sentry.io/1",
                "1.6.0",
                () => "Valid");

            options.MaxBreadcrumbs.Should().Be(30);
        }

        [Fact]
        public void Build_disables_session_tracking_and_default_pii()
        {
            SentryOptions options = SentryOptionsBuilder.Build(
                "https://test@sentry.io/1",
                "1.6.0",
                () => "Valid");

            options.AutoSessionTracking.Should().BeFalse();
            options.SendDefaultPii.Should().BeFalse();
        }

        // ---------- ScrubAndTag(...) ----------

        [Fact]
        public void ScrubAndTag_adds_five_required_tags()
        {
            SentryEvent evt = new SentryEvent();
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", "Trial");

            evt.Tags.Should().ContainKey("version").WhoseValue.Should().Be("1.6.0");
            evt.Tags.Should().ContainKey("revit_version").WhoseValue.Should().Be("2025");
            evt.Tags.Should().ContainKey("os");
            evt.Tags["os"].Should().NotBeNullOrWhiteSpace();
            evt.Tags.Should().ContainKey("culture");
            evt.Tags["culture"].Should().NotBeNullOrWhiteSpace();
            evt.Tags.Should().ContainKey("license_state").WhoseValue.Should().Be("Trial");
        }

        [Fact]
        public void ScrubAndTag_substitutes_email_in_message()
        {
            SentryEvent evt = new SentryEvent { Message = "User joao@empresa.com falhou" };
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", "Valid");

            evt.Message.Message.Should().Contain("<EMAIL>");
            evt.Message.Message.Should().NotContain("joao@empresa.com");
        }

        [Fact]
        public void ScrubAndTag_substitutes_windows_path_in_message()
        {
            SentryEvent evt = new SentryEvent
            {
                Message = "Falha em C:\\Users\\joao\\Desktop\\modelo.rvt",
            };
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", "Valid");

            evt.Message.Message.Should().Contain(@"<USER>\Desktop\modelo.rvt");
            evt.Message.Message.Should().NotContain("joao");
        }

        [Fact]
        public void ScrubAndTag_returns_null_when_event_is_null()
        {
            SentryEvent result = SentryOptionsBuilder.ScrubAndTag(null, "1.6.0", "Valid");
            result.Should().BeNull();
        }

        [Fact]
        public void ScrubAndTag_uses_Unknown_when_license_state_is_null()
        {
            SentryEvent evt = new SentryEvent();
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", null);

            evt.Tags.Should().ContainKey("license_state").WhoseValue.Should().Be("Unknown");
        }

        [Fact]
        public void ScrubAndTag_uses_Unknown_when_license_state_is_empty()
        {
            SentryEvent evt = new SentryEvent();
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", string.Empty);

            evt.Tags.Should().ContainKey("license_state").WhoseValue.Should().Be("Unknown");
        }

        [Fact]
        public void ScrubAndTag_falls_back_to_unknown_when_release_is_null()
        {
            SentryEvent evt = new SentryEvent();
            SentryOptionsBuilder.ScrubAndTag(evt, null, "Valid");

            evt.Tags.Should().ContainKey("version").WhoseValue.Should().Be("unknown");
        }

        [Fact]
        public void ScrubAndTag_combined_email_and_path_both_scrubbed()
        {
            SentryEvent evt = new SentryEvent
            {
                Message = "From maria@x.com em C:\\Users\\maria\\AppData\\file.log",
            };
            SentryOptionsBuilder.ScrubAndTag(evt, "1.6.0", "Valid");

            string scrubbed = evt.Message.Message;
            scrubbed.Should().Contain("<EMAIL>");
            scrubbed.Should().Contain(@"<USER>\AppData\file.log");
            scrubbed.Should().NotContain("maria");
        }
    }
}
