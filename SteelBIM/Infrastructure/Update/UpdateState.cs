namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Resultado de uma verificacao de update.
    /// </summary>
    public enum UpdateCheckOutcome
    {
        /// <summary>
        /// Nao foi possivel verificar (sem internet, rate limit, JSON malformado, etc).
        /// Caller deve apenas logar e seguir boot normal.
        /// </summary>
        Unknown = 0,

        /// <summary>Versao local eh a mais recente disponivel.</summary>
        NoUpdate = 1,

        /// <summary>Ha versao nova com asset .zip valido — pode prosseguir para download.</summary>
        UpdateAvailable = 2,

        /// <summary>
        /// Usuario ainda nao deu consentimento (PrivacyConsent.Unset).
        /// Caller deve mostrar PrivacyConsentWindow antes de qualquer chamada de rede.
        /// </summary>
        ConsentRequired = 3,

        /// <summary>Usuario optou por nao verificar atualizacoes (PrivacyConsent.Denied).</summary>
        ConsentDenied = 4,
    }

    /// <summary>
    /// Resultado completo de uma verificacao. Se Outcome == UpdateAvailable,
    /// os campos LatestVersion, ReleaseUrl e Release sao garantidos.
    /// </summary>
    public sealed class UpdateCheckResult
    {
        public UpdateCheckOutcome Outcome { get; set; }

        /// <summary>Versao remota parseada (so quando UpdateAvailable ou NoUpdate).</summary>
        public string LatestVersion { get; set; }

        /// <summary>URL HTML do release (so quando UpdateAvailable, para fallback manual).</summary>
        public string ReleaseUrl { get; set; }

        /// <summary>Release completo do GitHub (so quando UpdateAvailable, para o downloader).</summary>
        public GitHubRelease Release { get; set; }

        public static UpdateCheckResult Unknown() =>
            new UpdateCheckResult { Outcome = UpdateCheckOutcome.Unknown };

        public static UpdateCheckResult NoUpdate(string latestVersion) =>
            new UpdateCheckResult { Outcome = UpdateCheckOutcome.NoUpdate, LatestVersion = latestVersion };

        public static UpdateCheckResult Available(string latestVersion, string releaseUrl, GitHubRelease release) =>
            new UpdateCheckResult
            {
                Outcome = UpdateCheckOutcome.UpdateAvailable,
                LatestVersion = latestVersion,
                ReleaseUrl = releaseUrl,
                Release = release,
            };

        public static UpdateCheckResult ConsentRequired() =>
            new UpdateCheckResult { Outcome = UpdateCheckOutcome.ConsentRequired };

        public static UpdateCheckResult ConsentDenied() =>
            new UpdateCheckResult { Outcome = UpdateCheckOutcome.ConsentDenied };
    }
}
