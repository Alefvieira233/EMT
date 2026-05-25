using System;
using System.Collections.Generic;
using FluentAssertions;
using Sentry;
using SteelBIM.Infrastructure.CrashReporting;
using Xunit;

namespace SteelBIM.Tests.Infrastructure.CrashReporting
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

            // v2.6.1 (hotfix P0 SECURITY-2): .rvt filename agora tambem scrubed.
            evt.Message.Message.Should().Contain(@"<USER>\Desktop\<REVIT_FILE>.rvt");
            evt.Message.Message.Should().NotContain("joao");
            evt.Message.Message.Should().NotContain("modelo.rvt");
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

        // ---------- ScrubBreadcrumb(...) — v2.7.10 LGPD ----------

        [Fact]
        public void ScrubBreadcrumb_returns_null_when_input_is_null()
        {
            Breadcrumb result = SentryOptionsBuilder.ScrubBreadcrumb(null);
            result.Should().BeNull();
        }

        [Fact]
        public void ScrubBreadcrumb_substitutes_email_in_message()
        {
            Breadcrumb original = new Breadcrumb(
                message: "Usuario joao@empresa.com abriu modelo",
                type: "default");

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Message.Should().Contain("<EMAIL>");
            scrubbed.Message.Should().NotContain("joao@empresa.com");
        }

        [Fact]
        public void ScrubBreadcrumb_substitutes_windows_path_and_rvt_filename_in_message()
        {
            Breadcrumb original = new Breadcrumb(
                message: "Abriu C:\\Users\\joao\\Desktop\\ProjetoCliente.rvt",
                type: "default");

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Message.Should().Contain(@"<USER>\Desktop\<REVIT_FILE>.rvt");
            scrubbed.Message.Should().NotContain("joao");
            scrubbed.Message.Should().NotContain("ProjetoCliente");
        }

        [Fact]
        public void ScrubBreadcrumb_preserves_type_category_and_level()
        {
            Breadcrumb original = new Breadcrumb(
                message: "msg sem PII",
                type: "navigation",
                data: null,
                category: "ui.click",
                level: BreadcrumbLevel.Warning);

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Type.Should().Be("navigation");
            scrubbed.Category.Should().Be("ui.click");
            scrubbed.Level.Should().Be(BreadcrumbLevel.Warning);
        }

        [Fact]
        public void ScrubBreadcrumb_scrubs_pii_in_data_values()
        {
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { "user", "alef@emt.com.br" },
                { "path", "C:\\Users\\alef\\AppData\\Local\\app.log" },
                { "level", "warning" },
            };
            Breadcrumb original = new Breadcrumb(
                message: "no pii",
                type: "default",
                data: data);

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Data.Should().NotBeNull();
            scrubbed.Data["user"].Should().Be("<EMAIL>");
            scrubbed.Data["path"].Should().Be(@"<USER>\AppData\Local\app.log");
            scrubbed.Data["level"].Should().Be("warning"); // sem PII, intocado
        }

        [Fact]
        public void ScrubBreadcrumb_preserves_data_keys()
        {
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { "email_address", "joao@x.com" },
                { "user_path", "C:\\Users\\joao\\file.log" },
            };
            Breadcrumb original = new Breadcrumb(
                message: "msg",
                type: "default",
                data: data);

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            // Keys sao identificadores (nao PII) — preservados intactos.
            scrubbed.Data.Keys.Should().Contain("email_address");
            scrubbed.Data.Keys.Should().Contain("user_path");
        }

        [Fact]
        public void ScrubBreadcrumb_handles_null_data_dictionary()
        {
            Breadcrumb original = new Breadcrumb(
                message: "joao@x.com falhou",
                type: "default",
                data: null);

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Message.Should().Contain("<EMAIL>");
            scrubbed.Data.Should().BeNull();
        }

        [Fact]
        public void ScrubBreadcrumb_handles_null_message()
        {
            Breadcrumb original = new Breadcrumb(
                message: null,
                type: "default");

            Breadcrumb scrubbed = SentryOptionsBuilder.ScrubBreadcrumb(original);

            scrubbed.Should().NotBeNull();
            scrubbed.Message.Should().BeNull();
        }
    }
}
