using System;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Decide se um evento de telemetria deve ser enviado, em funcao
    /// do nome do evento e da flag de sucesso. Pure: sem deps externas
    /// alem de Random.Shared (CLR-managed).
    ///
    /// Sample rates documentados (ADR-008 §Sampling rationale):
    ///   - command.executed (success=true):  10%  (volume alto — cada clique)
    ///   - command.executed (success=false): 100% (falhas raras + valiosas)
    ///   - command.failed:                   100% (catch raiz)
    ///   - license.state_checked:            100% (boot, baixo volume)
    ///   - update.detected / update.applied: 100% (volume baixissimo)
    ///   - desconhecido (defesa):            100% (nao silenciar dados)
    ///
    /// Constantes publicas para ajuste sem PR — basta alterar e re-deploy.
    /// </summary>
    public static class SamplingDecider
    {
        public const double CommandExecutedSuccessRate = 0.10;
        public const double FullSampleRate = 1.0;

        public const string EventCommandExecuted = "command.executed";
        public const string EventCommandFailed = "command.failed";
        public const string EventLicenseStateChecked = "license.state_checked";
        public const string EventUpdateDetected = "update.detected";
        public const string EventUpdateApplied = "update.applied";

        public const string SuccessProperty = "success";

        /// <summary>
        /// Retorna true se o evento deve ser enviado pro PostHog.
        /// <paramref name="rng"/> opcional — para testes deterministicos.
        /// </summary>
        public static bool ShouldSend(TelemetryEvent evt, Random rng = null)
        {
            if (evt == null) return false;
            double rate = ResolveRate(evt);
            if (rate >= 1.0) return true;
            if (rate <= 0.0) return false;
            double dice = (rng ?? Random.Shared).NextDouble();
            return dice < rate;
        }

        /// <summary>Sample rate aplicavel ao evento. Util para inspecao em log.</summary>
        public static double ResolveRate(TelemetryEvent evt)
        {
            if (evt == null) return 0.0;
            if (evt.Name == EventCommandExecuted)
            {
                bool isSuccess = ExtractSuccess(evt);
                return isSuccess ? CommandExecutedSuccessRate : FullSampleRate;
            }
            // command.failed / license.state_checked / update.* / unknown
            return FullSampleRate;
        }

        private static bool ExtractSuccess(TelemetryEvent evt)
        {
            // Default: assume success quando ausente — semantica "command.executed"
            // sem property eh inconsistente, mas defensivo: 10% sample em vez de 100%.
            if (evt.Properties == null) return true;
            if (!evt.Properties.TryGetValue(SuccessProperty, out object raw) || raw == null)
                return true;
            if (raw is bool b) return b;
            if (raw is string s && bool.TryParse(s, out bool parsed)) return parsed;
            return true;
        }
    }
}
