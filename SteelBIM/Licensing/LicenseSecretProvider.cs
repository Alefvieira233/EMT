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
    ///   4. InvalidOperationException (nenhuma fonte configurada).
    ///
    /// IMPORTANTE: trocar o segredo invalida TODAS as licencas em uso. Nao trocar
    /// sem plano de migracao.
    /// </summary>
    public static class LicenseSecretProvider
    {
        public const string EnvVarName = "EMT_LICENSE_SECRET";
        public const string SecretFileName = "license.secret";

        // Snapshot atomico (secret, source). Escrito de uma vez so via Lazy
        // (ExecutionAndPublication) — elimina a janela em que outro thread
        // poderia ler secret != null mas source == NotResolved.
        private static Lazy<ResolvedSecret> _lazy = NewLazy();

        private static Lazy<ResolvedSecret> NewLazy() =>
            new Lazy<ResolvedSecret>(ResolveSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>Fonte resolvida do segredo, para diagnostico.</summary>
        public enum SecretSource
        {
            NotResolved,
            EnvironmentVariable,
            LocalAppDataFile,
            AssemblyAdjacentFile
        }

        private readonly struct ResolvedSecret
        {
            public ResolvedSecret(string secret, SecretSource source) { Secret = secret; Source = source; }
            public string Secret { get; }
            public SecretSource Source { get; }
        }

        public static string GetSecret() => _lazy.Value.Secret;

        /// <summary>Fonte resolvida do segredo ativo. Util para logs de startup.</summary>
        public static SecretSource GetResolvedSource() => _lazy.Value.Source;

        /// <summary>
        /// Limpa o cache de resolucao. Uso exclusivo em testes.
        /// </summary>
        internal static void ResetCacheForTests()
        {
            // Substituir o Lazy inteiro e o jeito limpo de "resetar" sem
            // precisar de reflection ou campos mutaveis dentro da snapshot.
            Interlocked.Exchange(ref _lazy, NewLazy());
        }

        private static ResolvedSecret ResolveSnapshot()
        {
            SecretSource source;
            string secret = ResolveFromSources(out source);
            return new ResolvedSecret(secret, source);
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

            // 4. nenhuma fonte configurada -- erro fatal
            throw new InvalidOperationException(
                "HMAC secret not configured. Set the environment variable '"
                + EnvVarName + "' or place a '" + SecretFileName + "' file in "
                + "'%LOCALAPPDATA%\\FerramentaEMT\\' or next to the assembly.");
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
                // Eliminar File.Exists + ReadAllText TOCTOU num unico try/catch.
                // Se o arquivo foi deletado entre check e read, FileNotFound e tratado
                // como "nao existe" (fallback para proxima fonte) — comportamento identico
                // ao File.Exists mas sem syscalls duplicadas.
                return File.ReadAllText(path);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch
            {
                // Se houver permissao negada ou IO error num arquivo que existe,
                // a surface superior (logger em App.OnStartup) ja reporta que o
                // segredo caiu no fallback.
                return null;
            }
        }

        /// <summary>
        /// Indica se o arquivo de segredo existe mas esta vazio/whitespace-only.
        /// Util para o startup logger distinguir "arquivo nao configurado" de
        /// "arquivo configurado errado" — um arquivo vazio resulta em
        /// InvalidOperationException e provavelmente indica bug de deploy.
        /// </summary>
        public static bool HasMalformedSecretFile(out string offendingPath)
        {
            offendingPath = null;
            foreach (string candidate in new[] { TryBuildLocalAppDataPath(), TryBuildAssemblyAdjacentPath() })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string raw;
                try { raw = File.Exists(candidate) ? File.ReadAllText(candidate) : null; }
                catch { continue; }
                if (raw != null && string.IsNullOrWhiteSpace(raw))
                {
                    offendingPath = candidate;
                    return true;
                }
            }
            return false;
        }

    }
}
