namespace SteelBIM.Infrastructure.Update
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

        /// <summary>
        /// v2.7.10 (auditoria 2026-05-25 §5.3): assinatura Authenticode do DLL extraido
        /// nao bate com publisher esperado (UntrustedRoot, SignatureBroken, Expired
        /// sem timestamp valido, NoSignature). Backup foi restaurado, ZIP + marker
        /// deletados. Caller deve logar error + mostrar dialog "Atualizacao rejeitada
        /// por motivo de seguranca — instale manualmente de github.com/.../releases".
        /// So eh retornado se <see cref="UpdateApplier"/> foi construido com
        /// <see cref="IAuthenticodeVerifier"/> nao-nulo.
        /// </summary>
        SignatureInvalid = 5,
    }
}
