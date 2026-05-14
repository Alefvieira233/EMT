using System;
using System.IO;
using SteelBIM.Infrastructure;
using SteelBIM.Models.Privacy;

namespace SteelBIM.Infrastructure.Privacy
{
    /// <summary>
    /// Persiste <see cref="PrivacySettings"/> em
    /// <c>%LocalAppData%\SteelBIM\privacy.json</c>.
    /// Machine-local (nao-roaming) — escolha consciente: o consentimento eh
    /// para esta maquina especifica.
    ///
    /// JSON (de)serializacao delegado para <see cref="PrivacyJson"/> (pure,
    /// testavel). Esta classe so faz IO + log de erros.
    /// </summary>
    public sealed class PrivacySettingsStore : IPrivacySettingsStore
    {
        private readonly string _filePath;

        /// <summary>Construtor padrao — usa caminho real do filesystem.</summary>
        public PrivacySettingsStore()
            : this(GetDefaultPath())
        {
        }

        /// <summary>Construtor para testes — permite redirecionar pra um diretorio temporario.</summary>
        public PrivacySettingsStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath obrigatorio", "filePath");
            _filePath = filePath;
        }

        public static string GetDefaultPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "SteelBIM", "privacy.json");
        }

        /// <summary>
        /// Migration v1.x -> v2.0 rebrand: caminho legado em
        /// %LocalAppData%\FerramentaEMT\privacy.json. Lido como fallback
        /// somente se o caminho novo nao existe; migrado (copiado + deletado)
        /// na primeira execucao.
        /// </summary>
        private static string GetLegacyPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FerramentaEMT", "privacy.json");
        }

        public PrivacySettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    // Migration v1.x -> v2.0: se nao existe no path novo, tenta
                    // migrar do legado (so quando _filePath aponta para o default).
                    if (string.Equals(_filePath, GetDefaultPath(), StringComparison.OrdinalIgnoreCase))
                    {
                        string legacy = GetLegacyPath();
                        if (File.Exists(legacy))
                        {
                            try
                            {
                                string newDir = Path.GetDirectoryName(_filePath);
                                if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
                                    Directory.CreateDirectory(newDir);
                                File.Copy(legacy, _filePath, overwrite: false);
                                try
                                { File.Delete(legacy); }
                                catch { /* nao fatal */ }
                                Logger.Info("[Privacy] Migrated privacy.json from FerramentaEMT legacy path");
                            }
                            catch (Exception mEx)
                            {
                                Logger.Warn(mEx, "[Privacy] Migration falhou (mantendo legado, lendo fallback)");
                                // Le diretamente do legado nesta sessao
                                try
                                {
                                    string legacyJson = File.ReadAllText(legacy);
                                    return PrivacyJson.DeserializeOrDefault(legacyJson);
                                }
                                catch { /* cai no default abaixo */ }
                            }
                        }
                    }

                    if (!File.Exists(_filePath))
                        return new PrivacySettings();
                }

                string json = File.ReadAllText(_filePath);
                return PrivacyJson.DeserializeOrDefault(json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Privacy] Falha ao ler privacy.json");
                return new PrivacySettings();
            }
        }

        public void Save(PrivacySettings settings)
        {
            if (settings == null)
                return;

            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = PrivacyJson.Serialize(settings);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Privacy] Falha ao salvar privacy.json");
            }
        }
    }
}
