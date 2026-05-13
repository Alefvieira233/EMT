using System;

namespace FerramentaEMT.Models.Privacy
{
    /// <summary>
    /// Persistencia de consentimento do usuario para 3 features:
    /// auto-update (PR-2), crash reporting (PR-3), telemetria (PR-4).
    ///
    /// Persistido em %LocalAppData%\FerramentaEMT\privacy.json — machine-local
    /// (nao-roaming), compartilhado entre as 3 features.
    ///
    /// Pure DTO — sem dependencia de Revit, IO ou WPF.
    /// </summary>
    public sealed class PrivacySettings
    {
        /// <summary>
        /// Versao do modelo de consentimento. Incrementar quando adicionar
        /// uma feature nova que exija nova pergunta. PrivacyConsentWindow
        /// reabre automaticamente quando ConsentVersion local &lt; codigo.
        ///
        /// PR-2: 1 (auto-update apenas).
        /// PR-3: 2 (auto-update + crash reports).
        /// PR-4: 3 (auto-update + crash + telemetry).
        /// </summary>
        public int ConsentVersion { get; set; }

        // ---------- Auto-update (PR-2) ----------

        /// <summary>Permissao para consultar GitHub Releases API + baixar atualizacoes.</summary>
        public ConsentState AutoUpdate { get; set; }

        /// <summary>Ultima verificacao bem-sucedida (UTC). Cache 24h baseado neste timestamp.</summary>
        public DateTime LastUpdateCheckUtc { get; set; }

        /// <summary>
        /// Versao que o usuario clicou "pular" no dialog de update.
        /// String vazia = nada pulado.
        /// </summary>
        public string SkippedUpdateVersion { get; set; }

        // ---------- Crash reports (PR-3, dormente nesta PR) ----------

        /// <summary>Permissao para enviar stack traces ao Sentry. Sera usado em PR-3.</summary>
        public ConsentState CrashReports { get; set; }

        // ---------- Telemetry (PR-4, dormente nesta PR) ----------

        /// <summary>Permissao para enviar eventos de uso ao PostHog. Sera usado em PR-4.</summary>
        public ConsentState Telemetry { get; set; }

        public PrivacySettings()
        {
            ConsentVersion = 0;
            AutoUpdate = ConsentState.Unset;
            CrashReports = ConsentState.Unset;
            Telemetry = ConsentState.Unset;
            LastUpdateCheckUtc = DateTime.MinValue;
            SkippedUpdateVersion = string.Empty;
        }
    }
}
