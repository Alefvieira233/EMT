using System;
using System.Threading;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Estado in-memory do subsistema de update durante UMA sessao do Revit.
    /// Reseta a cada boot — nao persiste em disco.
    ///
    /// Razao: se antivirus, disco cheio ou outro problema de IO bloqueia
    /// o subsistema de update repetidamente, queremos parar de tentar para:
    ///   1. Nao spammar o log com a mesma falha
    ///   2. Nao gastar requests no GitHub API rate limit (60/h sem PAT)
    ///   3. Dar feedback acionavel ao usuario / suporte
    ///
    /// Apos N=2 falhas consecutivas de IO, marca <see cref="IsDisabledForSession"/>
    /// como true. UpdateCheckService respeita esse flag e retorna Unknown direto.
    /// Reset apenas via reboot do Revit (intencional — forca usuario a resolver
    /// o problema de IO antes de re-tentar).
    /// </summary>
    public static class UpdateSession
    {
        public const int IoFailureThreshold = 2;

        // int em vez de bool para permitir Interlocked com tipos de valor
        private static int _consecutiveIoFailures;
        private static int _disabledForSession; // 0/1 como int

        /// <summary>Quantas falhas consecutivas de IO foram registradas nesta sessao.</summary>
        public static int ConsecutiveIoFailures => Volatile.Read(ref _consecutiveIoFailures);

        /// <summary>
        /// true se o subsistema de update foi desabilitado pelo resto desta sessao
        /// devido a falhas repetidas de IO. UpdateCheckService respeita isso.
        /// </summary>
        public static bool IsDisabledForSession => Volatile.Read(ref _disabledForSession) != 0;

        /// <summary>
        /// Registra uma falha de IO. Apos <see cref="IoFailureThreshold"/> falhas
        /// consecutivas, marca a sessao como desabilitada e loga uma mensagem
        /// acionavel (orientando whitelist em antivirus / liberar disco).
        ///
        /// <paramref name="context"/> identifica onde foi a falha (ex: "download",
        /// "swap", "privacy-save") para o log.
        /// </summary>
        public static void RecordIoFailure(string context)
        {
            int n = Interlocked.Increment(ref _consecutiveIoFailures);

            if (n >= IoFailureThreshold && Volatile.Read(ref _disabledForSession) == 0)
            {
                Interlocked.Exchange(ref _disabledForSession, 1);
                UpdateLog.Warn(
                    "[Update] {0} falhas de IO consecutivas em '{1}' — desabilitando update por esta sessao. " +
                    "Verifique antivirus (whitelist em %LocalAppData%\\FerramentaEMT\\) " +
                    "ou disco cheio. Reinicie o Revit apos resolver.",
                    new object[] { n, context ?? string.Empty });
            }
            else
            {
                UpdateLog.Debug("[Update] falha de IO em '{0}' (consecutivas={1})",
                    new object[] { context ?? string.Empty, n });
            }
        }

        /// <summary>
        /// Registra que uma operacao de IO foi bem-sucedida — zera o contador
        /// de falhas consecutivas (mas NAO desbloqueia a sessao se ja foi
        /// marcada como desabilitada — desbloqueio so via reboot).
        /// </summary>
        public static void RecordIoSuccess()
        {
            Interlocked.Exchange(ref _consecutiveIoFailures, 0);
        }

        /// <summary>
        /// Reseta o estado completamente. Util em testes — em producao,
        /// chamar isso defeats the propose deste mecanismo.
        /// </summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _consecutiveIoFailures, 0);
            Interlocked.Exchange(ref _disabledForSession, 0);
        }
    }
}
