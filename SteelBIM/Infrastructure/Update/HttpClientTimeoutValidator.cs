using System;
using System.Net.Http;

namespace SteelBIM.Infrastructure.Update
{
    /// <summary>
    /// Validador puro do HttpClient.Timeout injetado em UpdateDownloader.
    /// Adicionado em v2.6.1 (hotfix P0 SECURITY-1) apos auditoria senior
    /// 2026-05-19 PHASE 6.5 identificar que UpdateDownloader aceitava
    /// qualquer HttpClient — incluindo o default (Timeout=100s) ou
    /// Timeout=InfiniteTimeSpan. Sob ataque slowloris, download de 50MB
    /// congelaria a UI do Revit por minutos.
    ///
    /// Regra: HttpClient.Timeout deve estar em (0, 60s]. Caller que injetar
    /// um HttpClient sem cuidar do Timeout recebe ArgumentException no
    /// construtor — fail-fast em vez de freeze silencioso.
    ///
    /// Helper puro (sem deps de Logger, sem deps de Revit) — testavel via
    /// xUnit sem precisar linkar UpdateDownloader (que arrasta SteelBIM.
    /// Infrastructure.Logger -> Serilog).
    /// </summary>
    public static class HttpClientTimeoutValidator
    {
        /// <summary>Limite superior aceito (60s).</summary>
        public static readonly TimeSpan MaxAllowed = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Valida que o HttpClient.Timeout informado esta em (0, MaxAllowed].
        /// Lanca <see cref="ArgumentException"/> caso contrario, com paramName
        /// igual a <paramref name="paramName"/> (default "httpClient") para
        /// melhor mensagem no stack do caller.
        /// </summary>
        public static void Validate(TimeSpan timeout, string paramName = "httpClient")
        {
            if (timeout == System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentException(
                    "UpdateDownloader requires HttpClient with explicit Timeout (got Infinite). " +
                    "Caller must inject an HttpClient with Timeout in (0s, 60s].",
                    paramName);
            }
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "UpdateDownloader requires HttpClient.Timeout > 0 (got " + timeout + ").",
                    paramName);
            }
            if (timeout > MaxAllowed)
            {
                throw new ArgumentException(
                    "UpdateDownloader requires HttpClient.Timeout <= 60s (got " + timeout +
                    "). Default HttpClient.Timeout is 100s — caller must set it explicitly.",
                    paramName);
            }
        }

        /// <summary>
        /// Sugar pra validar diretamente de um HttpClient injetado.
        /// </summary>
        public static void Validate(HttpClient httpClient, string paramName = "httpClient")
        {
            if (httpClient == null)
                throw new ArgumentNullException(paramName);
            Validate(httpClient.Timeout, paramName);
        }
    }
}
