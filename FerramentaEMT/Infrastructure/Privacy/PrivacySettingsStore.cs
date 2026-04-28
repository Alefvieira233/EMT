using System;
using System.IO;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Privacy;

namespace FerramentaEMT.Infrastructure.Privacy
{
    /// <summary>
    /// Persiste <see cref="PrivacySettings"/> em
    /// <c>%LocalAppData%\FerramentaEMT\privacy.json</c>.
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
            return Path.Combine(localAppData, "FerramentaEMT", "privacy.json");
        }

        public PrivacySettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
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
            if (settings == null) return;

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
