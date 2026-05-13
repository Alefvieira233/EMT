using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre o cliente HTTP-direct: serializacao JSON no formato esperado
    /// pela API /capture do PostHog, fire-and-forget seguro, scrubbing de
    /// PII, snake_case, super properties, sampling, host customizavel,
    /// e swallow de excecoes.
    /// </summary>
    public class PostHogHttpTelemetryClientTests
    {
        // ----------- Test infra -----------

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly object _lock = new object();
            public HttpRequestMessage LastRequest { get; private set; }
            public string LastBody { get; private set; }
            public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;
            public bool ThrowOnSend { get; set; }
            public List<string> AllBodies { get; } = new List<string>();
            public ManualResetEventSlim Signal { get; } = new ManualResetEventSlim(false);
            public int RequestCount;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage req, CancellationToken ct)
            {
                if (ThrowOnSend) throw new HttpRequestException("simulated network down");

                string body = req.Content != null
                    ? await req.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                    : null;

                lock (_lock)
                {
                    LastRequest = req;
                    LastBody = body;
                    AllBodies.Add(body);
                    Interlocked.Increment(ref RequestCount);
                }
                Signal.Set();
                return new HttpResponseMessage(ResponseStatus);
            }
        }

        private static PostHogHttpTelemetryClient NewClient(
            CapturingHandler handler,
            string apiKey = "phc_test",
            string host = "https://eu.posthog.com",
            string sessionId = "session-123",
            string release = "1.7.0",
            string licenseState = "Trial",
            Action<Exception, string> logWarnException = null,
            Action<string, object[]> logWarnTemplate = null)
        {
            HttpClient http = new HttpClient(handler, disposeHandler: false);
            return new PostHogHttpTelemetryClient(
                http, apiKey, host, sessionId, release,
                () => licenseState, logWarnException, logWarnTemplate);
        }

        private static TelemetryEvent FailureEvent() =>
            new TelemetryEvent(SamplingDecider.EventCommandFailed,
                new Dictionary<string, object>
                {
                    { "command_name", "CmdGerarTrelica" },
                    { "exception_type", "InvalidOperationException" },
                    { "duration_ms", 1234 },
                });

        // Helper: aguarda o POST fire-and-forget completar (até timeout).
        private static bool WaitForPost(CapturingHandler handler, int timeoutMs = 3000)
        {
            return handler.Signal.Wait(timeoutMs);
        }

        // ----------- Endpoint + body -----------

        [Fact]
        public void Track_posts_to_capture_endpoint_with_normalized_host()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(
                handler, host: "https://eu.posthog.com");

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue("POST deve completar dentro do timeout");

            handler.LastRequest.RequestUri.ToString().Should()
                .Be("https://eu.posthog.com/capture/");
            handler.LastRequest.Method.Should().Be(HttpMethod.Post);
        }

        [Fact]
        public void Track_normalizes_host_without_schema()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(
                handler, host: "eu.posthog.com");

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            handler.LastRequest.RequestUri.ToString().Should()
                .StartWith("https://eu.posthog.com");
        }

        [Fact]
        public void Track_normalizes_host_with_trailing_slash()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(
                handler, host: "https://custom.posthog.io/");

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            handler.LastRequest.RequestUri.ToString().Should()
                .Be("https://custom.posthog.io/capture/");
        }

        [Fact]
        public void Track_body_contains_required_fields()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            using JsonDocument doc = JsonDocument.Parse(handler.LastBody);
            doc.RootElement.TryGetProperty("api_key", out _).Should().BeTrue();
            doc.RootElement.TryGetProperty("event", out _).Should().BeTrue();
            doc.RootElement.TryGetProperty("distinct_id", out _).Should().BeTrue();
            doc.RootElement.TryGetProperty("properties", out _).Should().BeTrue();
            doc.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
        }

        [Fact]
        public void Track_body_uses_correct_field_values()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(
                handler, apiKey: "phc_xyz", sessionId: "sess-789");

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            using JsonDocument doc = JsonDocument.Parse(handler.LastBody);
            doc.RootElement.GetProperty("api_key").GetString().Should().Be("phc_xyz");
            doc.RootElement.GetProperty("event").GetString().Should().Be("command.failed");
            doc.RootElement.GetProperty("distinct_id").GetString().Should().Be("sess-789");
        }

        [Fact]
        public void Track_content_type_is_application_json_utf8()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            handler.LastRequest.Content.Headers.ContentType.MediaType
                .Should().Be("application/json");
            handler.LastRequest.Content.Headers.ContentType.CharSet
                .Should().Be("utf-8");
        }

        // ----------- Super properties -----------

        [Fact]
        public void Track_body_includes_six_super_properties()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(
                handler, release: "1.7.0", licenseState: "Valid",
                sessionId: "550e8400-e29b-41d4-a716-446655440000");

            client.Track(FailureEvent());
            WaitForPost(handler).Should().BeTrue();

            using JsonDocument doc = JsonDocument.Parse(handler.LastBody);
            JsonElement props = doc.RootElement.GetProperty("properties");
            props.GetProperty("version").GetString().Should().Be("1.7.0");
            props.GetProperty("revit_version").GetString().Should().Be("2025");
            props.GetProperty("license_state").GetString().Should().Be("Valid");
            props.GetProperty("session_id").GetString()
                .Should().Be("550e8400-e29b-41d4-a716-446655440000");
            props.TryGetProperty("os", out _).Should().BeTrue();
            props.TryGetProperty("culture", out _).Should().BeTrue();
        }

        // ----------- PII scrubbing -----------

        [Fact]
        public void Track_scrubs_email_in_property_values_before_post()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            TelemetryEvent evt = new TelemetryEvent(
                SamplingDecider.EventCommandFailed,
                new Dictionary<string, object>
                {
                    { "user_input", "joao@empresa.com falhou" },
                });
            client.Track(evt);
            WaitForPost(handler).Should().BeTrue();

            handler.LastBody.Should().Contain("\\u003CEMAIL\\u003E", "JSON escapa < e > em \\u003C / \\u003E");
            handler.LastBody.Should().NotContain("joao@empresa.com");
        }

        [Fact]
        public void Track_scrubs_windows_path_with_username()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            TelemetryEvent evt = new TelemetryEvent(
                SamplingDecider.EventCommandFailed,
                new Dictionary<string, object>
                {
                    { "rvt_path", @"C:\Users\maria\Desktop\projeto.rvt" },
                });
            client.Track(evt);
            WaitForPost(handler).Should().BeTrue();

            handler.LastBody.Should().NotContain("maria");
            handler.LastBody.Should().Contain("Desktop");
        }

        // ----------- IsEnabled gate -----------

        [Fact]
        public void Track_is_noop_when_api_key_is_empty()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler, apiKey: "");

            client.IsEnabled.Should().BeFalse();
            client.Track(FailureEvent());

            // Sem POST. Aguarda um pouco pra ter certeza.
            handler.Signal.Wait(200).Should().BeFalse();
            handler.RequestCount.Should().Be(0);
        }

        [Fact]
        public void Track_is_noop_when_api_key_is_whitespace()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler, apiKey: "   ");

            client.IsEnabled.Should().BeFalse();
            client.Track(FailureEvent());

            handler.Signal.Wait(200).Should().BeFalse();
        }

        [Fact]
        public void Track_with_null_event_does_not_post_or_throw()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            Action act = () => client.Track(null);

            act.Should().NotThrow();
            handler.Signal.Wait(200).Should().BeFalse();
        }

        // ----------- Sampling -----------

        [Fact]
        public void Track_with_command_failed_always_posts_100_percent()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            // command.failed sempre 100%. Vai postar.
            for (int i = 0; i < 5; i++) client.Track(FailureEvent());

            // Aguarda alguns serem processados (sao em background)
            for (int i = 0; i < 50; i++)
            {
                if (handler.RequestCount >= 5) break;
                Thread.Sleep(50);
            }

            handler.RequestCount.Should().Be(5,
                "command.failed eh 100% sample rate");
        }

        // ----------- Failures (network down + non-2xx) -----------

        [Fact]
        public void HttpException_is_swallowed_without_throw()
        {
            CapturingHandler handler = new CapturingHandler { ThrowOnSend = true };
            int warnCount = 0;
            PostHogHttpTelemetryClient client = NewClient(
                handler,
                logWarnException: (_, _) => Interlocked.Increment(ref warnCount));

            Action act = () => client.Track(FailureEvent());
            act.Should().NotThrow();

            // Aguarda fire-and-forget completar
            for (int i = 0; i < 50; i++)
            {
                if (warnCount > 0) break;
                Thread.Sleep(50);
            }
            warnCount.Should().Be(1, "exception deve ser logada via warn");
        }

        [Fact]
        public void Non_2xx_response_is_logged_and_not_thrown()
        {
            CapturingHandler handler = new CapturingHandler
            {
                ResponseStatus = HttpStatusCode.Unauthorized
            };
            int warnTemplateCount = 0;
            PostHogHttpTelemetryClient client = NewClient(
                handler,
                logWarnTemplate: (_, _) => Interlocked.Increment(ref warnTemplateCount));

            Action act = () => client.Track(FailureEvent());
            act.Should().NotThrow();
            WaitForPost(handler).Should().BeTrue();

            // Aguarda o warn template ser chamado
            for (int i = 0; i < 50; i++)
            {
                if (warnTemplateCount > 0) break;
                Thread.Sleep(50);
            }
            warnTemplateCount.Should().Be(1, "401 deve ser logada via warn template");
        }

        // ----------- FlushAsync -----------

        [Fact]
        public async Task FlushAsync_returns_completed_task_immediately()
        {
            CapturingHandler handler = new CapturingHandler();
            PostHogHttpTelemetryClient client = NewClient(handler);

            Task t = client.FlushAsync(2000);

            t.IsCompleted.Should().BeTrue("HTTP-direct nao tem batch buffer");
            await t;
        }
    }
}
