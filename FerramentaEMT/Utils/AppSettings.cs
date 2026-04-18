using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Utils
{
    /// <summary>
    /// Persistencia de preferencias do usuario (perfil padrao, niveis ultima sessao, etc).
    /// Salvo em JSON em %AppData%\FerramentaEMT\settings.json.
    /// </summary>
    /// <remarks>
    /// THREAD-SAFE (Sprint 1):
    /// Usa <see cref="ReaderWriterLockSlim"/> para permitir multiplas leituras
    /// simultaneas mas apenas uma escrita por vez.
    /// </remarks>
    public class AppSettings
    {
        // ---------- Perfis genericos ----------
        public string LastSelectedProfileName { get; set; } = string.Empty;
        public string LastSelectedProfileFamilyName { get; set; } = string.Empty;
        public string LastSelectedTiranteName { get; set; } = string.Empty;
        public string LastSelectedTiranteFamilyName { get; set; } = string.Empty;
        public string LastSelectedFrechalName { get; set; } = string.Empty;
        public string LastSelectedFrechalFamilyName { get; set; } = string.Empty;
        public string LastSelectedTrelicaMontanteName { get; set; } = string.Empty;
        public string LastSelectedTrelicaMontanteFamilyName { get; set; } = string.Empty;
        public string LastSelectedTrelicaDiagonalName { get; set; } = string.Empty;
        public string LastSelectedTrelicaDiagonalFamilyName { get; set; } = string.Empty;

        // ---------- Guarda-corpo ----------
        public string LastSelectedGuardaCorpoProfileName { get; set; } = string.Empty;
        public string LastSelectedGuardaCorpoFamilyName { get; set; } = string.Empty;
        public string LastSelectedGuardaCorpoLevelName { get; set; } = string.Empty;

        // ---------- Pipe Rack ----------
        public string LastSelectedPipeRackPilarName { get; set; } = string.Empty;
        public string LastSelectedPipeRackPilarFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackVigaName { get; set; } = string.Empty;
        public string LastSelectedPipeRackVigaFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackMontanteName { get; set; } = string.Empty;
        public string LastSelectedPipeRackMontanteFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackDiagonalName { get; set; } = string.Empty;
        public string LastSelectedPipeRackDiagonalFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackPenteName { get; set; } = string.Empty;
        public string LastSelectedPipeRackPenteFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackContraventamentoName { get; set; } = string.Empty;
        public string LastSelectedPipeRackContraventamentoFamilyName { get; set; } = string.Empty;
        public string LastSelectedPipeRackLevelName { get; set; } = string.Empty;
        public string LastSelectedPipeRackTopLevelName { get; set; } = string.Empty;

        // ---------- Escada ----------
        public string LastSelectedEscadaLongarinaNome { get; set; } = string.Empty;
        public string LastSelectedEscadaLongarinaFamilyName { get; set; } = string.Empty;
        public string LastSelectedEscadaDegrauNome { get; set; } = string.Empty;
        public string LastSelectedEscadaDegrauFamilyName { get; set; } = string.Empty;
        public string LastSelectedEscadaLevelName { get; set; } = string.Empty;

        // ---------- Numeracao ----------
        public string LastNumeracaoScope { get; set; } = "VistaAtiva";
        public string LastNumeracaoCategoryName { get; set; } = string.Empty;
        public string LastNumeracaoFamilyName { get; set; } = string.Empty;
        public string LastNumeracaoTypeName { get; set; } = string.Empty;
        public string LastNumeracaoParameterKey { get; set; } = string.Empty;
        public string LastNumeracaoPrefix { get; set; } = string.Empty;
        public int LastNumeracaoStart { get; set; } = 1;
        public int LastNumeracaoStep { get; set; } = 1;
        public string LastNumeracaoSuffix { get; set; } = string.Empty;
        public bool LastNumeracaoKeepHighlight { get; set; } = true;

        // ---------- PF Nomeacao ----------
        public string LastPfNamingTarget { get; set; } = "Pilares";
        public string LastPfNamingScope { get; set; } = "VistaAtiva";
        public string LastPfNamingFamilyName { get; set; } = string.Empty;
        public string LastPfNamingTypeName { get; set; } = string.Empty;
        public string LastPfNamingParameterKey { get; set; } = string.Empty;
        public string LastPfNamingPrefix { get; set; } = string.Empty;
        public int LastPfNamingStart { get; set; } = 1;
        public int LastPfNamingStep { get; set; } = 1;
        public string LastPfNamingSuffix { get; set; } = string.Empty;

        // =====================================================================
        // PERSISTENCIA (thread-safe)
        // =====================================================================

        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FerramentaEMT",
            "settings.json");

        /// <summary>
        /// Carrega as configuracoes do arquivo. Retorna instancia padrao se nao existir
        /// ou se houver erro de leitura (logado em Logger).
        /// </summary>
        public static AppSettings Load()
        {
            _lock.EnterReadLock();
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return new AppSettings();

                string json = File.ReadAllText(SettingsFilePath);
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (IOException ioEx)
            {
                Logger.Warn(ioEx, "AppSettings.Load: erro de I/O lendo {Path}", SettingsFilePath);
                return new AppSettings();
            }
            catch (JsonException jsonEx)
            {
                Logger.Warn(jsonEx, "AppSettings.Load: JSON corrompido em {Path}", SettingsFilePath);
                return new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AppSettings.Load: erro inesperado lendo {Path}", SettingsFilePath);
                return new AppSettings();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Sprint 4: helper conveniente para "load + mutar + save" em uma chamada.
        /// Evita boilerplate em windows/services que precisam persistir uma alteracao.
        /// Se a acao lancar excecao, ela e logada mas nao propagada.
        /// </summary>
        /// <example>
        /// <code>
        /// AppSettings.Update(s => s.LastSelectedProfileName = "W12X26");
        /// </code>
        /// </example>
        public static void Update(Action<AppSettings> mutate)
        {
            if (mutate == null)
                return;

            try
            {
                AppSettings settings = Load();
                mutate(settings);
                settings.Save();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "AppSettings.Update: erro mutando settings");
            }
        }

        /// <summary>
        /// Salva as configuracoes no arquivo. Erros sao logados mas nao propagados
        /// (preferencias nao devem quebrar o fluxo do usuario).
        /// </summary>
        public void Save()
        {
            _lock.EnterWriteLock();
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Escrita atomica: escreve em .tmp e renomeia
                // (evita arquivo corrompido se o processo morre durante escrita)
                string tmpPath = SettingsFilePath + ".tmp";
                File.WriteAllText(tmpPath, json);

                if (File.Exists(SettingsFilePath))
                    File.Delete(SettingsFilePath);
                File.Move(tmpPath, SettingsFilePath);
            }
            catch (IOException ioEx)
            {
                Logger.Warn(ioEx, "AppSettings.Save: erro de I/O salvando {Path}", SettingsFilePath);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Logger.Warn(uaEx, "AppSettings.Save: sem permissao em {Path}", SettingsFilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AppSettings.Save: erro inesperado salvando {Path}", SettingsFilePath);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
