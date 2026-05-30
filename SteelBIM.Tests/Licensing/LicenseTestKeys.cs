#nullable enable
using System;
using System.Security.Cryptography;
using SteelBIM.Licensing;

namespace SteelBIM.Tests.Licensing
{
    /// <summary>Instala um par ECDsa P-256 efemero nos seams de teste, para os testes
    /// assinarem/verificarem sem a chave de producao.</summary>
    internal static class LicenseTestKeys
    {
        public static void InstallEphemeral()
        {
            using ECDsa ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            LicenseKeys.TestOverridePublicKeySpkiBase64 =
                Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
            LicensePrivateKeyProvider.TestOverridePrivateKeyPkcs8Base64 =
                Convert.ToBase64String(ec.ExportPkcs8PrivateKey());
        }
    }
}
