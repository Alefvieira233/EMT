#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Resolve a chave PRIVADA de assinatura (ECDsa P-256, PKCS#8 base64). Usado SO pelo
    /// EmtKeyGen. Ordem: env STEELBIM_LICENSE_PRIVATE_KEY -> %LOCALAPPDATA%\SteelBIM\
    /// license.private.key -> arquivo ao lado do executavel. NUNCA commitar a privada.
    /// </summary>
    public static class LicensePrivateKeyProvider
    {
        public const string EnvVarName = "STEELBIM_LICENSE_PRIVATE_KEY";
        public const string KeyFileName = "license.private.key";

        // Seam de teste: setado por LicenseTestKeys.InstallEphemeral no projeto de testes.
        // CS0649 suprimido porque no SteelBIM.dll/EmtKeyGen.exe o campo nunca eh
        // atribuido em codigo de producao — assembly de testes seta via Test seam.
#pragma warning disable CS0649
        internal static string? TestOverridePrivateKeyPkcs8Base64;
#pragma warning restore CS0649

        public static ECDsa CreatePrivateKey()
        {
            string? pkcs8B64 = TestOverridePrivateKeyPkcs8Base64 ?? ResolvePkcs8Base64();
            if (string.IsNullOrWhiteSpace(pkcs8B64))
            {
                throw new InvalidOperationException(
                    "Chave privada de licenca nao configurada. Defina '" + EnvVarName
                    + "' (PKCS#8 base64) ou coloque '" + KeyFileName + "' em '%LOCALAPPDATA%\\SteelBIM\\' "
                    + "ou ao lado do executavel. Gere com 'EmtKeyGen genkeypair'.");
            }
            ECDsa ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(pkcs8B64!.Trim()), out _);
                return ecdsa;
            }
            catch { ecdsa.Dispose(); throw; }
        }

        private static string? ResolvePkcs8Base64()
        {
            string? env = SafeEnv(EnvVarName);
            if (!string.IsNullOrWhiteSpace(env))
                return env!.Trim();
            foreach (string? path in new[] { LocalAppDataPath(), AssemblyAdjacentPath() })
            {
                string? content = SafeRead(path);
                if (!string.IsNullOrWhiteSpace(content))
                    return content!.Trim();
            }
            return null;
        }

        private static string? SafeEnv(string name)
        { try { return Environment.GetEnvironmentVariable(name); } catch { return null; } }

        private static string? LocalAppDataPath()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "SteelBIM", KeyFileName);
            }
            catch { return null; }
        }

        private static string? AssemblyAdjacentPath()
        {
            try
            {
                string? dir = Path.GetDirectoryName(typeof(LicensePrivateKeyProvider).Assembly.Location);
                return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir!, KeyFileName);
            }
            catch { return null; }
        }

        private static string? SafeRead(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            { return File.ReadAllText(path!); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch { return null; }
        }
    }
}
