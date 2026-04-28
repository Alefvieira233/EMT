namespace FerramentaEMT.Models.Privacy
{
    /// <summary>
    /// Estado de consentimento de uma feature individual de coleta/envio de dados.
    /// Usado por auto-update (PR-2), crash reporting (PR-3) e telemetria (PR-4).
    /// </summary>
    public enum ConsentState
    {
        /// <summary>Usuario ainda nao foi perguntado. Nao ativar a feature.</summary>
        Unset = 0,

        /// <summary>Usuario marcou o checkbox e clicou Salvar — ativar.</summary>
        Granted = 1,

        /// <summary>Usuario explicitamente desmarcou ou clicou Negar tudo — nao ativar.</summary>
        Denied = 2,
    }
}
