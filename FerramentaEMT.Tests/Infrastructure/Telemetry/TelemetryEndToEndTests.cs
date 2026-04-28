using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Infrastructure.Telemetry;
using FerramentaEMT.Models.Privacy;
using FluentAssertions;
using Moq;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Smoke test end-to-end (espelha o SentryEndToEndTests da PR-3):
    /// pipeline completo TelemetryStartupWiring.InitializeServices →
    /// TelemetryReporter.Initialize → Track → POST capturado pelo
    /// CapturingHandler. NAO toca rede (HttpMessageHandler mockado).
    /// </summary>
    [Collection("PostHogApiKeySerial")]
    public class TelemetryEndToEndTests : IDisposable
    {
        private readonly string _origApiKeyEnv;

        public TelemetryEndToEndTests()
        {
            _origApiKeyEnv = Environment.GetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName);
            TelemetryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, null);
            PostHogApiKeyProvider.ResetCacheForTests();
        }

        public void Dispose()
        {
            TelemetryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, _origApiKeyEnv);
            PostHogApiKeyProvider.ResetCacheForTests();
        }

        // Recicla CapturingHandler do PostHogHttpTelemetryClientTests
        // (mesma assembly de testes — duplicacao deliberada pra independencia
        // do teste e2e nao depender da estrutura interna do unit test).
        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly object _lock = new object();
            public string LastBody { get; private set; }
            public List<string> AllBodies { get; } = new List<string>();
            public ManualResetEventSlim Signal { get; } = new ManualResetEventSlim(false);
            public int RequestCount;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage req, CancellationToken ct)
            {
                string body = req.Content != null
                    ? await req.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                    : null;
                lock (_lock)
                {
                    LastBody = body;
                    AllBodies.Add(body);
                    Interlocked.Increment(ref RequestCount);
                }
                Signal.Set();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        private static IPrivacySettingsStore StoreReturning(PrivacySettings settings)
        {
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            store.Setup(s => s.Load()).Returns(settings);
            return store.Object;
        }

        [Fact]
        public void Wiring_then_track_sends_post_via_capturing_handler()
        {
            CapturingHandler handler = new CapturingHandler();
            HttpClient http = new HttpClient(handler, disposeHandler: false);

            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { Telemetry = ConsentState.Granted });
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_e2e");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryStartupWiring.InitializeServices(
                privacyStore: store,
                clientFactory: () => new PostHogHttpTelemetryClient(
                    http, "phc_e2e", "https://eu.posthog.com",
                    "550e8400-e29b-41d4-a716-446655440000",
                    "1.7.0", () => "Trial",
                    logWarnException: null,
                    logWarnTemplate: null),
                licenseStateResolver: () => "Trial",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            TelemetryReporter.IsEnabled.Should().BeTrue();

            // Init ja emitiu license.state_checked sintetico — aguardamos.
            handler.Signal.Wait(3000).Should().BeTrue("Init deve disparar license.state_checked");

            using JsonDocument doc = JsonDocument.Parse(handler.LastBody);
            doc.RootElement.GetProperty("event").GetString()
                .Should().Be("license.state_checked");
            doc.RootElement.GetProperty("api_key").GetString()
                .Should().Be("phc_e2e");
            doc.RootElement.GetProperty("distinct_id").GetString()
                .Should().Be("550e8400-e29b-41d4-a716-446655440000");
            doc.RootElement.GetProperty("properties").GetProperty("status")
                .GetString().Should().Be("Trial");
        }

        [Fact]
        public void End_to_end_skips_post_when_consent_denied()
        {
            CapturingHandler handler = new CapturingHandler();
            HttpClient http = new HttpClient(handler, disposeHandler: false);
            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { Telemetry = ConsentState.Denied });
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_e2e");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryStartupWiring.InitializeServices(
                privacyStore: store,
                clientFactory: () => new PostHogHttpTelemetryClient(
                    http, "phc_e2e", "https://eu.posthog.com",
                    "session", "1.7.0", () => "Trial"),
                licenseStateResolver: () => "Trial",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            TelemetryReporter.IsEnabled.Should().BeFalse();
            // Track depois do init negado: zero POSTs.
            TelemetryReporter.Track(new TelemetryEvent("custom.event", null));

            handler.Signal.Wait(200).Should().BeFalse();
            handler.RequestCount.Should().Be(0);
        }

        [Fact]
        public void End_to_end_command_executed_failure_posts_with_six_super_properties()
        {
            CapturingHandler handler = new CapturingHandler();
            HttpClient http = new HttpClient(handler, disposeHandler: false);
            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { Telemetry = ConsentState.Granted });
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_e2e");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryStartupWiring.InitializeServices(
                privacyStore: store,
                clientFactory: () => new PostHogHttpTelemetryClient(
                    http, "phc_e2e", "https://eu.posthog.com",
                    "550e8400-e29b-41d4-a716-446655440000",
                    "1.7.0", () => "Valid"),
                licenseStateResolver: () => "Valid",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            // Aguarda o Init's license.state_checked completar
            handler.Signal.Wait(3000).Should().BeTrue();
            handler.Signal.Reset();

            // Track manual: command.failed
            TelemetryReporter.Track(new TelemetryEvent(
                SamplingDecider.EventCommandFailed,
                new Dictionary<string, object>
                {
                    { "command_name", "CmdGerarTrelica" },
                    { "exception_type", "InvalidOperationException" },
                    { "duration_ms", 4321 },
                }));

            handler.Signal.Wait(3000).Should().BeTrue("command.failed deve postar");

            string body = handler.LastBody;
            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("event").GetString()
                .Should().Be("command.failed");
            JsonElement props = doc.RootElement.GetProperty("properties");
            props.GetProperty("version").GetString().Should().Be("1.7.0");
            props.GetProperty("revit_version").GetString().Should().Be("2025");
            props.GetProperty("license_state").GetString().Should().Be("Valid");
            props.GetProperty("session_id").GetString()
                .Should().Be("550e8400-e29b-41d4-a716-446655440000");
            props.GetProperty("command_name").GetString().Should().Be("CmdGerarTrelica");
        }
    }
}
