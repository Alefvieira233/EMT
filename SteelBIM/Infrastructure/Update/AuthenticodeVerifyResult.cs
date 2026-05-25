namespace SteelBIM.Infrastructure.Update
{
    /// <summary>
    /// Resultado da verificacao Authenticode de um arquivo (.dll ou .exe).
    /// Usado pelo <see cref="IAuthenticodeVerifier"/> + <see cref="UpdateApplier"/>
    /// pra decidir se o binario extraido eh confiavel.
    ///
    /// <para>v2.7.10 (auditoria 2026-05-25 §5.3) — defense-in-depth supply chain:
    /// SHA256 do .zip ja eh validado contra o manifesto, mas se o repo do
    /// GitHub for comprometido (ou houver MITM que troque ZIP+manifesto
    /// juntos), o usuario fica owned. Authenticode adiciona uma 2a camada
    /// independente (cert do publisher, chain ate root trustado).</para>
    /// </summary>
    public enum AuthenticodeVerifyResult
    {
        /// <summary>Assinatura presente, hash bate, chain valida ate root trustado, dentro da validade.</summary>
        Verified = 0,

        /// <summary>Arquivo nao tem assinatura nenhuma. Estado atual pre-cert.</summary>
        NoSignature = 1,

        /// <summary>Assinatura existe mas o issuer/root nao esta no trust store local.</summary>
        UntrustedRoot = 2,

        /// <summary>Certificado expirado SEM counter-signature de timestamp valida.</summary>
        Expired = 3,

        /// <summary>Hash do arquivo nao bate com o que esta na assinatura — binario adulterado pos-sign.</summary>
        SignatureBroken = 4,

        /// <summary>
        /// WinVerifyTrust nao esta disponivel (ex: rodando em CI Linux,
        /// ou wintrust.dll ausente). Caller deve tratar como soft-pass
        /// (skip verify + log warning) pra nao quebrar test runners.
        /// </summary>
        NotSupported = 5,

        /// <summary>Outro erro nao mapeado pelo verifier (HResult desconhecido).</summary>
        Error = 6,
    }
}
