using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Implementacao HTTP-direct do <see cref="ITelemetryClient"/> usando
    /// HttpClient para postar diretamente no endpoint /capture do PostHog.
    ///
    /// Decisao registrada no ADR-008 §"Decisao: HTTP-direct vs SDK oficial":
    /// o NuGet oficial PostHog 2.5.0 eh pre-release com warning de breaking
    /// changes. /capture eh REST estavel ha anos — mesma API consumida por
    /// todas as SDKs PostHog.
    ///
    /// Caracteristicas:
    ///   - Fire-and-forget: cada Track posta async em background sem
    ///     bloquear o caller. Telemetry-loss eh tolerado (Sentry cobre o
    ///     critico via crashes locais).
    ///   - Sem batch buffer / sem retry queue (escopo PR-4 — 5 eventos,
    ///     baixo volume).
    ///   - JSON snake_case (PostHog convention).
    ///   - Logger desacoplado via delegates (mesmo padrao do SentryReporter)
    ///     — torna a classe linkavel ao test csproj sem puxar Serilog.
    ///   - Sample rate respeitado via SamplingDecider antes do POST.
    ///   - PII scrubbing aplicado via TelemetryOptionsBuilder.ScrubAndTag
    ///     antes da serializacao.
    /// </summary>
    public sealed class PostHogHttpTelemetryClient : ITelemetryClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _host;
        private readonly string _sessionId;
        private readonly string _release;
        private readonly Func<string> _licenseStateResolver;
        private readonly Action<Exception, string> _logWarnException;
        private readonly Action<string, object[]> _logWarnTemplate;
        private readonly JsonSerializerOptions _jsonOpts;

        public bool IsEnabled { get; }

        public PostHogHttpTelemetryClient(
            HttpClient http,
            string apiKey,
            string host,
            string sessionId,
            string release,
            Func<string> licenseStateResolver,
            Action<Exception, string> logWarnException = null,
            Action<string, object[]> logWarnTemplate = null)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            _http = http;
            _apiKey = apiKey ?? string.Empty;
            _host = NormalizeHost(host);
            _sessionId = string.IsNullOrEmpty(sessionId) ? "unknown" : sessionId;
            _release = string.IsNullOrEmpty(release) ? "unknown" : release;
            _licenseStateResolver = licenseStateResolver ?? (() => "Unknown");
            _logWarnException = logWarnException ?? ((_, _) => { });
            _logWarnTemplate = logWarnTemplate ?? ((_, _) => { });

            // Instancia local — NUNCA usar JsonSerializerOptions estatico
            // global state.
            _jsonOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            IsEnabled = !string.IsNullOrWhiteSpace(_apiKey);
        }

        public void Track(TelemetryEvent evt)
        {
            if (!IsEnabled) return;
            if (evt == null) return;

            // Sample rate: command.executed:success vai a 10%, resto 100%.
            if (!SamplingDecider.ShouldSend(evt)) return;

            string licenseState = SafeResolveLicenseState();
            TelemetryEvent enriched = TelemetryOptionsBuilder.ScrubAndTag(
                evt, _release, licenseState, _sessionId);
            if (enriched == null) return;

            // Anonymous type: fields ja em snake_case porque
            // JsonNamingPolicy.SnakeCaseLower esta ativo, MAS escolhi
            // escrever direto em snake_case pra deixar explicito o
            // contrato com a API PostHog (e nao depender da policy).
            object body = new
            {
                api_key = _apiKey,
                @event = enriched.Name,
                distinct_id = _sessionId,
                properties = enriched.Properties,
                timestamp = DateTime.UtcNow.ToString(
                    "o", CultureInfo.InvariantCulture),
            };

            string url = _host + "/capture/";

            // Fire-and-forget. Falha → log Warn, NUNCA propaga.
            _ = Task.Run(async () =>
            {
                try
                {
                    string json = JsonSerializer.Serialize(body, _jsonOpts);
                    using StringContent content = new StringContent(
                        json, Encoding.UTF8, "application/json");
                    using HttpResponseMessage resp = await _http.PostAsync(url, content)
                        .ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logWarnTemplate(
                            "[Telemetry] capture POST returned {Status} (event: {Name})",
                            new object[] { (int)resp.StatusCode, enriched.Name });
                    }
                }
                catch (Exception ex)
                {
                    _logWarnException(ex, "[Telemetry] capture POST falhou");
                }
            });
        }

        /// <summary>
        /// HTTP-direct PR-4 NAO mantem batch buffer — Track posta direto.
        /// Flush eh no-op imediato. Reservado para futuras impls com queue.
        /// </summary>
        public Task FlushAsync(int timeoutMs) => Task.CompletedTask;

        private string SafeResolveLicenseState()
        {
            try { return _licenseStateResolver() ?? "Unknown"; }
            catch { return "Unknown"; }
        }

        private static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return "https://eu.posthog.com";
            string trimmed = host.Trim().TrimEnd('/');
            // Aceita "eu.posthog.com" sem schema, vira "https://...".
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "https://" + trimmed;
            }
            return trimmed;
        }
    }
}
