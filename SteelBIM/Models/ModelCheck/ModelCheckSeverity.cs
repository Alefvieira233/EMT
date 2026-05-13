namespace FerramentaEMT.Models.ModelCheck
{
    /// <summary>
    /// Severidade de um problema encontrado pela verificacao de modelo.
    /// </summary>
    public enum ModelCheckSeverity
    {
        /// <summary>Informacao, nao e um problema.</summary>
        Info = 0,

        /// <summary>Aviso — deveria ser corrigido.</summary>
        Warning = 1,

        /// <summary>Erro — requer correcao obrigatoria.</summary>
        Error = 2
    }
}
