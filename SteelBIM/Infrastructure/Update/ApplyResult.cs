namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Resultado de uma chamada a <see cref="UpdateApplier.ApplyPendingIfAny"/>.
    /// Caller (App.OnStartup) usa isso pra decidir o que mostrar pro usuario.
    /// </summary>
    public enum ApplyResult
    {
        /// <summary>Nao havia marker em pending/. Boot continua normal.</summary>
        NoPending = 0,

        /// <summary>Update aplicado com sucesso. Caller pode logar mas nada visivel ao usuario.</summary>
        Applied = 1,

        /// <summary>
        /// Marker presente mas swap bloqueado por arquivo em uso (CLR ja carregou).
        /// AttemptCount incrementado. Mostrar dialog "Reinicie o Revit pra completar."
        /// </summary>
        RetryNextStartup = 2,

        /// <summary>
        /// 3 tentativas falharam. Marker deletado. Mostrar dialog
        /// "Atualize manualmente em github.com/.../releases".
        /// </summary>
        AbortedAfterRetries = 3,

        /// <summary>
        /// Marker corrompido / re-validacao SHA256 falhou / .zip ausente.
        /// Marker + zip deletados. Caller pode logar warn e seguir boot.
        /// </summary>
        InvalidMarker = 4,
    }
}
