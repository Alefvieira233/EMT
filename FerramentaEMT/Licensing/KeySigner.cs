using System;
using System.Security.Cryptography;
using System.Text;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Assina e valida chaves de licenca usando HMAC-SHA256.
    /// </summary>
    /// <remarks>
    /// Modelo do token (formato compacto, sem dependencia de System.Text.Json no Revit):
    ///
    ///   <payload-base64url>.<hmac-base64url>
    ///
    /// Onde:
    /// - payload-base64url e o JSON do <see cref="LicensePayload"/> codificado em Base64URL
    /// - hmac-base64url e o HMAC-SHA256(payload-bytes, SECRET) tambem em Base64URL
    ///
    /// O SECRET FICA HARDCODED. SO O ALEF (que tem o codigo) consegue gerar chaves validas.
    /// O usuario nao consegue forjar porque nao conhece o SECRET — qualquer alteracao no payload
    /// invalida o HMAC.
    ///
    /// IMPORTANTE: este SECRET tem que ser o MESMO no projeto principal (validacao) e no
    /// EmtKeyGen (geracao). Trocar o SECRET invalida TODAS as licencas em uso.
    /// </remarks>
    public static class KeySigner
    {
        // ---------------------------------------------------------------------
        // SECRET DE PRODUCAO — TROCAR ANTES DA PRIMEIRA VENDA, DEPOIS NUNCA MAIS.
        // ---------------------------------------------------------------------
        // Use uma string aleatoria de 32+ caracteres. Para gerar:
        //   PowerShell:  [System.Convert]::ToBase64String((1..32 | %%{ Get-Random -Min 0 -Max 256 } | %%{ [byte]$_ }))
        //
        // Trocar este valor invalida TODAS as licencas previamente emitidas.
        // ---------------------------------------------------------------------
        private const string Secret = "EMT-PROD-SECRET-CHANGE-BEFORE-FIRST-SALE-2026-ALEF";

        /// <summary>
        /// Gera o token assinado a partir de um payload.
        /// Usado pelo EmtKeyGen.
        /// </summary>
        public static string Sign(LicensePayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            string payloadJson = SimpleJson.Serialize(payload);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            string payloadB64 = Base64Url.Encode(payloadBytes);

            byte[] sigBytes = ComputeHmac(payloadBytes);
            string sigB64 = Base64Url.Encode(sigBytes);

            return $"{payloadB64}.{sigB64}";
        }

        /// <summary>
        /// Valida o token e devolve o payload se OK.
        /// Retorna null se a assinatura nao bater ou o token estiver mal formado.
        /// </summary>
        public static LicensePayload Verify(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            string trimmed = token.Trim();
            int dotIndex = trimmed.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= trimmed.Length - 1) return null;

            string payloadB64 = trimmed.Substring(0, dotIndex);
            string sigB64 = trimmed.Substring(dotIndex + 1);

            byte[] payloadBytes;
            byte[] expectedSig;
            try
            {
                payloadBytes = Base64Url.Decode(payloadB64);
                expectedSig = Base64Url.Decode(sigB64);
            }
            catch
            {
                return null;
            }

            byte[] actualSig = ComputeHmac(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig))
                return null;

            try
            {
                string json = Encoding.UTF8.GetString(payloadBytes);
                return SimpleJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ComputeHmac(byte[] payload)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(Secret);
            using HMACSHA256 hmac = new HMACSHA256(keyBytes);
            return hmac.ComputeHash(payload);
        }
    }
}
