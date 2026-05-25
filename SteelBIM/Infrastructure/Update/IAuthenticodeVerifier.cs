namespace SteelBIM.Infrastructure.Update
{
    /// <summary>
    /// Abstracao do verifier Authenticode pra permitir teste sem WinVerifyTrust real.
    /// Implementacao de producao: <see cref="WinTrustAuthenticodeVerifier"/>.
    /// Implementacao de teste: <see cref="UpdateApplier"/> injection com fake.
    ///
    /// <para>v2.7.10 (auditoria 2026-05-25 §5.3).</para>
    /// </summary>
    public interface IAuthenticodeVerifier
    {
        /// <summary>
        /// Verifica a assinatura Authenticode de um arquivo no caminho dado.
        /// Implementacao deve ser TRY/CATCH defensiva — qualquer exception
        /// nao tratada vira <see cref="AuthenticodeVerifyResult.Error"/>.
        /// </summary>
        AuthenticodeVerifyResult VerifyFile(string filePath);
    }
}
