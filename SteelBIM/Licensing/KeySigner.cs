using System;
using System.Security.Cryptography;
using System.Text;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Assina e valida chaves de licenca com assinatura ASSIMETRICA ECDsa P-256 / SHA-256.
    /// Token: &lt;payload-base64url&gt;.&lt;signature-base64url&gt;. Assinatura usa a chave PRIVADA
    /// (EmtKeyGen, via LicensePrivateKeyProvider); verificacao usa a PUBLICA embarcada
    /// (LicenseKeys). Ver docs/ADR/ADR-011-licenca-assimetrica.md.
    /// </summary>
    public static class KeySigner
    {
        /// <summary>Assina um payload com a chave privada resolvida (EmtKeyGen).</summary>
        public static string Sign(LicensePayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            using (ECDsa priv = LicensePrivateKeyProvider.CreatePrivateKey())
                return Sign(payload, priv);
        }

        /// <summary>Assina com uma chave privada explicita (testes).</summary>
        public static string Sign(LicensePayload payload, ECDsa privateKey)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));
            byte[] payloadBytes = Encoding.UTF8.GetBytes(SimpleJson.Serialize(payload));
            string payloadB64 = Base64Url.Encode(payloadBytes);
            byte[] sig = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256);
            return payloadB64 + "." + Base64Url.Encode(sig);
        }

        /// <summary>Valida o token com a chave PUBLICA embarcada (plugin). Null se invalido.</summary>
        public static LicensePayload Verify(string token)
        {
            ECDsa pub = SafeCreatePublicKey();
            if (pub == null)
                return null;
            using (pub)
                return Verify(token, pub);
        }

        /// <summary>Valida com uma chave publica explicita (testes). Null se invalido.</summary>
        public static LicensePayload Verify(string token, ECDsa publicKey)
        {
            if (string.IsNullOrWhiteSpace(token) || publicKey == null)
                return null;
            string trimmed = token.Trim();
            int dot = trimmed.IndexOf('.');
            if (dot <= 0 || dot >= trimmed.Length - 1)
                return null;

            byte[] payloadBytes;
            byte[] sig;
            try
            {
                payloadBytes = Base64Url.Decode(trimmed.Substring(0, dot));
                sig = Base64Url.Decode(trimmed.Substring(dot + 1));
            }
            catch { return null; }

            bool ok;
            try
            { ok = publicKey.VerifyData(payloadBytes, sig, HashAlgorithmName.SHA256); }
            catch { return null; }
            if (!ok)
                return null;

            try
            { return SimpleJson.Deserialize(Encoding.UTF8.GetString(payloadBytes)); }
            catch { return null; }
        }

        private static ECDsa SafeCreatePublicKey()
        {
            // Placeholder/chave invalida -> null -> fail-closed (nenhuma licenca valida).
            try
            { return LicenseKeys.CreatePublicKey(); }
            catch { return null; }
        }
    }
}
