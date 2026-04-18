namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Estado atual da licenca.
    /// Determina se o plugin pode rodar e o que mostrar para o usuario.
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>Licenca paga, valida, dentro do prazo. Plugin libera tudo.</summary>
        Valid = 0,

        /// <summary>Licenca paga mas expirada. Bloqueia ribbon ate renovar.</summary>
        Expired = 1,

        /// <summary>Periodo gratuito ativo. Plugin libera tudo + mostra "Trial: X dias".</summary>
        Trial = 2,

        /// <summary>Periodo gratuito acabou. Bloqueia ribbon ate ativar licenca paga.</summary>
        TrialExpired = 3,

        /// <summary>Nunca ativou nada. Mostra tela de boas-vindas / inicia trial.</summary>
        NotActivated = 4,

        /// <summary>Arquivo de licenca foi adulterado ou esta corrompido.</summary>
        Tampered = 5,

        /// <summary>Licenca foi ativada em outra maquina (fingerprint nao bate).</summary>
        WrongMachine = 6,
    }
}
