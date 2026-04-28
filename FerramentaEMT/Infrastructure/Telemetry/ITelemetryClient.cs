using System;
using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Abstracao mockavel sobre o subset do PostHog SDK que o
    /// TelemetryReporter usa em runtime. Mesma motivacao do
    /// ISentryHubFacade (PR-3): testes mockam via Moq sem subir
    /// HttpClient real do PostHog.
    /// </summary>
    public interface ITelemetryClient
    {
        /// <summary>
        /// Configura a SDK com a API key + host + super properties.
        /// Idempotente do lado do impl (PostHogTelemetryClient).
        /// </summary>
        void Init(string apiKey, string host);

        /// <summary>
        /// Captura um evento. PostHog SDK enfileira async — retorna
        /// rapido. Deve ser fire-and-forget do ponto de vista do caller.
        /// </summary>
        void Capture(TelemetryEvent evt, string distinctId);

        /// <summary>
        /// Define uma super property que vai junto em todo evento subsequente.
        /// Usado pra version, revit_version, os, culture, license_state.
        /// </summary>
        void SetSuperProperty(string key, object value);

        /// <summary>
        /// Drena eventos pendentes ate timeout. Usado em OnShutdown.
        /// </summary>
        Task FlushAsync(TimeSpan timeout);

        /// <summary>True apos Init bem-sucedido.</summary>
        bool IsEnabled { get; }
    }
}
