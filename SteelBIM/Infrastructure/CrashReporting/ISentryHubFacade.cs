using System;
using System.Threading.Tasks;
using Sentry;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Abstracao mockavel sobre o subset do Sentry SDK que o
    /// SentryReporter usa em runtime. Existe pelo mesmo motivo que
    /// IGitHubReleaseProvider em PR-2: testes precisam mockar o lado
    /// "fala com servico externo" sem subir HTTP de verdade.
    ///
    /// Mantemos a superficie minima — quanto menos metodos, menos
    /// stubs precisamos manter sincronizados com o SDK.
    /// </summary>
    public interface ISentryHubFacade
    {
        /// <summary>
        /// Inicializa a SDK com as options dadas. Idempotente do lado
        /// do SDK (Sentry.SentrySdk.Init) — chamadas subsequentes
        /// substituem o client. SentryReporter garante "uma vez so"
        /// ao chamar isso.
        /// </summary>
        void Init(SentryOptions options);

        /// <summary>
        /// Captura uma excecao no Sentry. Retorna o EventId atribuido
        /// (ou Guid.Empty se descartado pelo BeforeSend).
        /// </summary>
        SentryId CaptureException(Exception exception);

        /// <summary>
        /// Aplica um tag adicional ao evento corrente do scope ativo.
        /// Usado em CaptureCrash para anexar 'kind' ao evento que
        /// vai ser enviado em seguida.
        /// </summary>
        void SetTag(string key, string value);

        /// <summary>
        /// Drena eventos pendentes ate timeout. Usado em OnShutdown.
        /// </summary>
        Task FlushAsync(TimeSpan timeout);

        /// <summary>
        /// Indica se a SDK esta ativa. False antes de Init ou apos
        /// Close. Util pra duplicate-init guard.
        /// </summary>
        bool IsEnabled { get; }
    }
}
