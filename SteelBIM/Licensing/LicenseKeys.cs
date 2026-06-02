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
        /// SubjectPublicKeyInfo (DER) da chave publica de PRODUCAO (ECDsa P-256), em Base64.
        /// Par regenerado em 2026-06-01 (o par anterior nao tinha a privada acessivel ao
        /// produtor). A chave PRIVADA correspondente fica SO com o produtor
        /// (env STEELBIM_LICENSE_PRIVATE_KEY ou %LOCALAPPDATA%\SteelBIM\license.private.key
        /// no EmtKeyGen) e NUNCA entra no repositorio.
        /// Validada: 91 bytes, curva prime256v1, importavel por ImportSubjectPublicKeyInfo.
        /// </summary>
        public const string PublicKeySpkiBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE5DN7rWkaDnyYGT+VKvSALIne47owx8eAgsc33AQIU5W//K5LIKkepQrJnRlF2mlN0icSoaNbxDgdU/bAlWtssQ==";

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

        /// <summary>
        /// True se a chave publica de PRODUCAO ainda for o placeholder (a chave real do
        /// `genkeypair` nao foi colada). Nesse estado nenhuma licenca paga valida — so trial.
        /// Usado no startup para avisar alto (evita o sintoma confuso de "trial silencioso").
        /// </summary>
        public static bool ChavePublicaEhPlaceholder =>
            string.IsNullOrWhiteSpace(PublicKeySpkiBase64)
            || PublicKeySpkiBase64.StartsWith("COLE_AQUI", StringComparison.Ordinal);
    }
}
