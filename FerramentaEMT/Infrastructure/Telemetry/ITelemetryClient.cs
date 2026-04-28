using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Abstracao mockavel sobre o cliente de telemetria. PR-4 implementa
    /// como HTTP-direct (PostHogHttpTelemetryClient) — POST direto pro
    /// endpoint /capture do PostHog. NAO usamos NuGet PostHog (pre-release
    /// com warning de breaking changes — ver ADR-008).
    ///
    /// API minima: caller chama <see cref="Track"/> e nada mais. Toda a
    /// configuracao (api key, host, session id, super properties) eh
    /// resolvida pelo construtor da impl. Caller eh agnostico de
    /// transport — pode ser HTTP-direct, SDK futura, ou mock em testes.
    /// </summary>
    public interface ITelemetryClient
    {
        /// <summary>True se a configuracao permite envio (api key presente).</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Captura um evento de telemetria. Fire-and-forget do ponto de
        /// vista do caller — implementacoes podem postar async em background.
        /// NUNCA lanca; falhas sao logadas internamente e descartadas
        /// (telemetry-loss eh tolerado, conforme ADR-008).
        /// </summary>
        void Track(TelemetryEvent evt);

        /// <summary>
        /// Drena eventos pendentes ate timeout (em ms). PR-4 HTTP-direct
        /// retorna <see cref="Task.CompletedTask"/> imediatamente — sem
        /// batch buffer. Reservado para futuras impls com queue.
        /// </summary>
        Task FlushAsync(int timeoutMs);
    }
}
