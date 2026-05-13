using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using FerramentaEMT.Infrastructure;
using Microsoft.Win32;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Persiste o estado de licenca/trial em disco, criptografado com DPAPI (CurrentUser).
    /// </summary>
    /// <remarks>
    /// DPAPI = Data Protection API do Windows. A chave de criptografia eh derivada do
    /// usuario logado — outro usuario na mesma maquina nao consegue ler o arquivo.
    /// Isso evita que o usuario simplesmente edite o arquivo para "estender" a licenca:
    /// alterar o conteudo invalida a descriptografia.
    ///
    /// Local: %LocalAppData%\FerramentaEMT\license\
    ///   - emt.lic   = token assinado + fingerprint (apenas se ATIVADO)
    ///   - emt.trl   = data de inicio do trial (apenas se TRIAL nunca foi convertido)
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class LicenseStore
    {
        private const string FolderName = "license";
        private const string LicenseFileName = "emt.lic";
        private const string TrialFileName = "emt.trl";

        // ===== Caminhos =====
        private static string FolderPath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(baseDir, "FerramentaEMT", FolderName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string LicensePath() => Path.Combine(FolderPath(), LicenseFileName);
        private static string TrialPath() => Path.Combine(FolderPath(), TrialFileName);

        // ===== Licenca paga =====

        /// <summary>
        /// Salva a licenca paga (token + fingerprint da maquina onde foi ativada).
        /// </summary>
        public static void SaveLicense(string token, string machineFingerprint)
        {
            // Formato interno: "token|fingerprint"
            string raw = $"{token}|{machineFingerprint}";
            byte[] data = Encoding.UTF8.GetBytes(raw);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(LicensePath(), encrypted);
            Logger.Info("[License] Licenca salva em disco");
        }

        /// <summary>
        /// Le a licenca paga. Retorna (token, fingerprint) ou null se nao existe / corrompido.
        /// </summary>
        public static (string Token, string Fingerprint)? LoadLicense()
        {
            string path = LicensePath();
            if (!File.Exists(path)) return null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string raw = Encoding.UTF8.GetString(data);

                int sep = raw.IndexOf('|');
                if (sep <= 0 || sep >= raw.Length - 1)
                {
                    Logger.Warn("[License] Arquivo de licenca com formato invalido");
                    return null;
                }

                return (raw.Substring(0, sep), raw.Substring(sep + 1));
            }
            catch (CryptographicException ex)
            {
                Logger.Warn(ex, "[License] Falha ao descriptografar licenca (possivel adulteracao)");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[License] Erro lendo arquivo de licenca");
                return null;
            }
        }

        /// <summary>Apaga o arquivo de licenca (usado para reset/desativacao).</summary>
        public static void DeleteLicense()
        {
            try
            {
                string path = LicensePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[License] Erro ao apagar licenca");
            }
        }

        // ===== Trial =====
        //
        // Estrategia anti-reset: gravamos o timestamp do trial em DOIS lugares:
        //   (a) %LocalAppData%\FerramentaEMT\license\emt.trl  (DPAPI)
        //   (b) HKCU\Software\FerramentaEMT\Trial             (DPAPI)
        // Sempre lemos o MAIS ANTIGO entre os dois (MIN). Se o usuario apaga 1
        // dos lados, o outro lembra a data verdadeira. Se apagar os dois,
        // perde tambem o historico de comandos no log e isso fica obvio em suporte.
        // ====================================================================

        private const string TrialRegPath = @"Software\FerramentaEMT\Trial";
        private const string TrialRegValueName = "InitialUnix";

        /// <summary>
        /// Retorna a data UTC em que o trial foi iniciado, ou null se nunca rodou.
        /// Le DOIS sentinels (arquivo + registro) e devolve o MAIS ANTIGO.
        /// </summary>
        public static DateTime? GetTrialStartUtc()
        {
            DateTime? fromFile = ReadTrialFromFile();
            DateTime? fromReg = ReadTrialFromRegistry();

            if (fromFile.HasValue && fromReg.HasValue)
                return fromFile.Value < fromReg.Value ? fromFile : fromReg;

            return fromFile ?? fromReg;
        }

        /// <summary>Define a data de inicio do trial como agora (so se ainda nao existe nenhum sentinel).</summary>
        public static void StartTrialIfNotStarted()
        {
            if (GetTrialStartUtc().HasValue) return;

            long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteTrialToFile(unix);
            WriteTrialToRegistry(unix);
            Logger.Info("[License] Trial iniciado (sentinel arquivo + registro)");
        }

        // ---- arquivo ----
        private static DateTime? ReadTrialFromFile()
        {
            string path = TrialPath();
            if (!File.Exists(path)) return null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string raw = Encoding.UTF8.GetString(data);

                if (long.TryParse(raw, out long unix))
                    return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

                Logger.Warn("[License] Arquivo de trial com formato invalido");
                return null;
            }
            catch (CryptographicException ex)
            {
                Logger.Warn(ex, "[License] Trial em arquivo adulterado");
                return DateTime.UtcNow.AddYears(-10); // forca TrialExpired
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[License] Erro lendo arquivo de trial");
                return null;
            }
        }

        private static void WriteTrialToFile(long unix)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(unix.ToString(System.Globalization.CultureInfo.InvariantCulture));
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(TrialPath(), encrypted);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[License] Nao foi possivel gravar trial em arquivo");
            }
        }

        // ---- registro ----
        private static DateTime? ReadTrialFromRegistry()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(TrialRegPath);
                if (key == null) return null;

                if (key.GetValue(TrialRegValueName) is not byte[] encrypted || encrypted.Length == 0)
                    return null;

                byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string raw = Encoding.UTF8.GetString(data);

                if (long.TryParse(raw, out long unix))
                    return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

                return null;
            }
            catch (CryptographicException ex)
            {
                Logger.Warn(ex, "[License] Trial em registro adulterado");
                return DateTime.UtcNow.AddYears(-10);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[License] Erro lendo trial do registro");
                return null;
            }
        }

        private static void WriteTrialToRegistry(long unix)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(TrialRegPath, writable: true);
                if (key == null) return;

                byte[] data = Encoding.UTF8.GetBytes(unix.ToString(System.Globalization.CultureInfo.InvariantCulture));
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                key.SetValue(TrialRegValueName, encrypted, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[License] Nao foi possivel gravar trial no registro");
            }
        }
    }
}
