#nullable enable
using System;
using System.Security.Cryptography;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Chave PUBLICA de verificacao de licenca (ECDsa P-256 / SHA-256). Segura para
    /// versionar e embarcar: so VERIFICA, nao ASSINA. A privada fica so com o produtor
    /// (LicensePrivateKeyProvider) e NUNCA entra no repo. Ver ADR-011.
    /// </summary>
    public static class LicenseKeys
    {
        /// <summary>
        /// SubjectPublicKeyInfo (DER) da chave publica de PRODUCAO, em Base64.
        /// >>> SUBSTITUIR pelo output de `EmtKeyGen genkeypair` (secao 7). Enquanto for o
        ///     placeholder, Verify retorna null para qualquer chave (fail-closed). <<<
        /// </summary>
        public const string PublicKeySpkiBase64 = "COLE_AQUI_A_CHAVE_PUBLICA_DO_genkeypair";

        // Seam de teste: o projeto de testes COMPILA este arquivo no proprio assembly,
        // entao pode setar este campo internal. NAO e' nova superficie de ataque (um
        // atacante com reflection in-process ja venceria qualquer cheque). Producao nunca seta.
        // CS0649 e' suprimido porque no SteelBIM.dll/EmtKeyGen.exe o campo de fato
        // nunca eh atribuido — so o assembly de testes o seta via InstallEphemeral.
#pragma warning disable CS0649
        internal static string? TestOverridePublicKeySpkiBase64;
#pragma warning restore CS0649

        public static ECDsa CreatePublicKey()
        {
            string spki = TestOverridePublicKeySpkiBase64 ?? PublicKeySpkiBase64;
            ECDsa ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _);
                return ecdsa;
            }
            catch { ecdsa.Dispose(); throw; }
        }
    }
}
