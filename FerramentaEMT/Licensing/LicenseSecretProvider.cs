using System;
using System.IO;
using System.Threading;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Resolve o segredo HMAC usado pelo KeySigner em tempo de execucao.
    ///
    /// Ordem de prioridade (primeiro que responder ganha):
    ///   1. Variavel de ambiente EMT_LICENSE_SECRET
    ///   2. Arquivo local %LOCALAPPDATA%\FerramentaEMT\license.secret
    ///   3. Arquivo local junto ao assembly: license.secret (ao lado do DLL)
    ///   4. Fallback hardcoded DEV_ONLY (emite warning no log).
    ///
    /// O fallback existe para garantir compatibilidade com licencas ja emitidas
    /// e permitir builds de desenvolvimento sem setup. PRODUCAO deve sempre
    /// fornecer o segredo via uma das fontes externas acima.
    ///
    /// IMPORTANTE: trocar o segredo invalida TODAS as licencas em uso. Nao trocar
    /// sem plano de migracao.
    /// </summary>
    public static class LicenseSecretProvider
    {
        public const string EnvVarName = "EMT_LICENSE_SECRET";
        public const string SecretFileName = "license.secret";

        // Fallback DEV_ONLY. Mantido identico ao que foi usado em releases anteriores
        // para nao invalidar licencas emitidas durante o periodo de transicao.
        // TODO [RELEASE-1.3]: remover apos externalizar em todos os ambientes de build.
        private const string DevOnlyFallback = "EMT-PROD-SECRET-CHANGE-BEFORE-FIRST-SALE-2026-ALEF";

        private static string _cached;
        private static SecretSource _cachedSource;

        /// <summary>Fonte resolvida do segredo, para diagnostico.</summary>
        public enum SecretSource
        {
            NotResolved,
            EnvironmentVariable,
            LocalAppDataFile,
            AssemblyAdjacentFile,
            DevOnlyFallback
        }

        public static string GetSecret()
        {
            string cached = Volatile.Read(ref _cached);
            if (cached != null)
                return cached;

            SecretSource source;
            string resolved = ResolveFromSources(out source);
            Volatile.Write(ref _cached, resolved);
            _cachedSource = source;
            return resolved;
        }

        /// <summary>
        /// Indica se o segredo atual veio do fallback hardcoded (DEV_ONLY).
        /// Uso: testes, startup logging e diagnostico.
        /// </summary>
        public static bool IsUsingDevOnlyFallback()
        {
            // garante que GetSecret() foi chamado pelo menos uma vez
            GetSecret();
            return _cachedSource == SecretSource.DevOnlyFallback;
        }

        /// <summary>Fonte resolvida do segredo ativo. Util para logs de startup.</summary>
        public static SecretSource GetResolvedSource()
        {
            GetSecret();
            return _cachedSource;
        }

        /// <summary>
        /// Limpa o cache de resolucao. Uso exclusivo em testes.
        /// </summary>
        internal static void ResetCacheForTests()
        {
            Volatile.Write(ref _cached, null);
            _cachedSource = SecretSource.NotResolved;
        }

        private static string ResolveFromSources(out SecretSource source)
        {
            // 1. variavel de ambiente
            string envValue = SafeReadEnvVar();
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                source = SecretSource.EnvironmentVariable;
                return envValue.Trim();
            }

            // 2. arquivo em %LOCALAPPDATA%\FerramentaEMT
            string localAppDataPath = TryBuildLocalAppDataPath();
            string fromLocalAppData = SafeReadFile(localAppDataPath);
            if (!string.IsNullOrWhiteSpace(fromLocalAppData))
            {
                source = SecretSource.LocalAppDataFile;
                return fromLocalAppData.Trim();
            }

            // 3. arquivo ao lado do assembly
            string assemblyPath = TryBuildAssemblyAdjacentPath();
            string fromAssemblyDir = SafeReadFile(assemblyPath);
            if (!string.IsNullOrWhiteSpace(fromAssemblyDir))
            {
                source = SecretSource.AssemblyAdjacentFile;
                return fromAssemblyDir.Trim();
            }

            // 4. fallback DEV_ONLY
            source = SecretSource.DevOnlyFallback;
            return DevOnlyFallback;
        }

        private static string SafeReadEnvVar()
        {
            try
            {
                return Environment.GetEnvironmentVariable(EnvVarName);
            }
            catch
            {
                return null;
            }
        }

        private static string TryBuildLocalAppDataPath()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root))
                    return null;
                return Path.Combine(root, "FerramentaEMT", SecretFileName);
            }
            catch
            {
                return null;
            }
        }

        private static string TryBuildAssemblyAdjacentPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(LicenseSecretProvider).Assembly.Location);
                if (string.IsNullOrWhiteSpace(dir))
                    return null;
                return Path.Combine(dir, SecretFileName);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                if (!File.Exists(path))
                    return null;
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

    }
}
