using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Aplica updates pendentes no proximo App.OnStartup.
    ///
    /// Estrategia:
    /// 1. Se nao houver marker em pending/, retorna NoPending.
    /// 2. Le o marker, re-valida SHA256 do .zip referenciado.
    /// 3. Backup do diretorio de instalacao atual (para rollback).
    /// 4. Tenta extrair .zip por cima do diretorio de instalacao.
    /// 5. Se IOException por arquivo em uso (CLR ja carregou):
    ///    - Incrementa AttemptCount no marker
    ///    - Retorna RetryNextStartup (1a-2a tentativa) ou AbortedAfterRetries (3a)
    /// 6. Se sucesso: deleta backup + marker + zip, retorna Applied.
    /// 7. Se outro erro: restaura backup, deleta marker, retorna InvalidMarker.
    ///
    /// Suporta multiplos markers em pending/ (aplica o de maior versao,
    /// deleta os outros). Sem dep de Logger — usa UpdateLog facade
    /// pra ser testavel em xUnit (App.cs wira o real Logger no boot).
    /// </summary>
    public sealed class UpdateApplier
    {
        public const int MaxAttempts = 3;

        private readonly string _pendingDirectory;
        private readonly string _installDirectory;

        /// <summary>Construtor para producao.</summary>
        public UpdateApplier()
            : this(GetDefaultPendingDirectory(), GetDefaultInstallDirectory())
        {
        }

        /// <summary>Construtor para testes (permite redirecionar diretorios).</summary>
        public UpdateApplier(string pendingDirectory, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(pendingDirectory))
                throw new ArgumentException("pendingDirectory obrigatorio", "pendingDirectory");
            if (string.IsNullOrWhiteSpace(installDirectory))
                throw new ArgumentException("installDirectory obrigatorio", "installDirectory");

            _pendingDirectory = pendingDirectory;
            _installDirectory = installDirectory;
        }

        public static string GetDefaultPendingDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FerramentaEMT", "Updates", "pending");
        }

        public static string GetDefaultInstallDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autodesk", "Revit", "Addins", "2025", "FerramentaEMT");
        }

        /// <summary>Versao aplicada/tentada na ultima chamada (vazia se NoPending).</summary>
        public string LastVersionAttempted { get; private set; }

        /// <summary>AttemptCount apos a chamada (significativo se RetryNextStartup ou AbortedAfterRetries).</summary>
        public int LastAttemptCount { get; private set; }

        public ApplyResult ApplyPendingIfAny()
        {
            LastVersionAttempted = string.Empty;
            LastAttemptCount = 0;

            if (!Directory.Exists(_pendingDirectory))
            {
                return ApplyResult.NoPending;
            }

            string[] markerFiles;
            try
            {
                markerFiles = Directory.GetFiles(_pendingDirectory, "*.marker");
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex, "[Update] falha ao listar pending/", EmptyArgs);
                return ApplyResult.NoPending;
            }

            if (markerFiles.Length == 0)
            {
                return ApplyResult.NoPending;
            }

            UpdateMarker chosen = null;
            string chosenPath = null;
            foreach (string mp in markerFiles)
            {
                UpdateMarker m;
                try
                {
                    m = UpdateMarkerJson.DeserializeOrNull(File.ReadAllText(mp));
                }
                catch
                {
                    m = null;
                }

                if (m == null)
                {
                    TryDelete(mp);
                    continue;
                }

                if (chosen == null)
                {
                    chosen = m;
                    chosenPath = mp;
                    continue;
                }

                int? cmp = SemVerComparer.CompareStrings(m.Version, chosen.Version);
                if (cmp.HasValue && cmp.Value > 0)
                {
                    TryDelete(chosenPath);
                    chosen = m;
                    chosenPath = mp;
                }
                else
                {
                    TryDelete(mp);
                }
            }

            if (chosen == null)
            {
                return ApplyResult.NoPending;
            }

            LastVersionAttempted = chosen.Version;
            LastAttemptCount = chosen.AttemptCount;

            if (!File.Exists(chosen.ZipPath))
            {
                UpdateLog.Warn("[Update] .zip referenciado pelo marker nao existe: {0}",
                    new object[] { chosen.ZipPath });
                TryDelete(chosenPath);
                return ApplyResult.InvalidMarker;
            }

            string actualHash;
            try
            {
                using (FileStream fs = File.OpenRead(chosen.ZipPath))
                {
                    actualHash = Sha256Calculator.ComputeHex(fs);
                }
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex, "[Update] falha ao re-validar SHA256", EmptyArgs);
                if (ex is IOException) UpdateSession.RecordIoFailure("apply-revalidate-hash");
                TryDelete(chosenPath);
                return ApplyResult.InvalidMarker;
            }

            if (!string.Equals(actualHash, chosen.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                UpdateLog.Warn("[Update] SHA256 do .zip pendente nao bate (esperado {0} obtido {1})",
                    new object[] { chosen.Sha256, actualHash });
                TryDelete(chosen.ZipPath);
                TryDelete(chosenPath);
                return ApplyResult.InvalidMarker;
            }

            string backupDir = _installDirectory + ".bak";
            try { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, recursive: true); }
            catch { /* best effort */ }

            try
            {
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Move(_installDirectory, backupDir);
                }
            }
            catch (IOException ex) when (IsFileInUseException(ex))
            {
                return IncrementAttemptOrAbort(chosen, chosenPath);
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex, "[Update] falha ao fazer backup do install dir", EmptyArgs);
                if (ex is IOException) UpdateSession.RecordIoFailure("apply-backup");
                TryDelete(chosenPath);
                return ApplyResult.InvalidMarker;
            }

            try
            {
                Directory.CreateDirectory(_installDirectory);
                ZipFile.ExtractToDirectory(chosen.ZipPath, _installDirectory, overwriteFiles: true);
            }
            catch (IOException ex) when (IsFileInUseException(ex))
            {
                RestoreBackup(backupDir);
                return IncrementAttemptOrAbort(chosen, chosenPath);
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex, "[Update] falha ao extrair .zip — restaurando backup", EmptyArgs);
                if (ex is IOException) UpdateSession.RecordIoFailure("apply-extract");
                RestoreBackup(backupDir);
                TryDelete(chosenPath);
                return ApplyResult.InvalidMarker;
            }

            try { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, recursive: true); }
            catch { /* best effort */ }
            TryDelete(chosen.ZipPath);
            TryDelete(chosenPath);

            UpdateSession.RecordIoSuccess();
            UpdateLog.Info("[Update] aplicado com sucesso para versao {0}",
                new object[] { chosen.Version });
            return ApplyResult.Applied;
        }

        private ApplyResult IncrementAttemptOrAbort(UpdateMarker chosen, string markerPath)
        {
            chosen.AttemptCount += 1;
            LastAttemptCount = chosen.AttemptCount;
            UpdateLog.Info("[Update] swap bloqueado — DLL em uso. Tentativa {0}/{1}",
                new object[] { chosen.AttemptCount, MaxAttempts });

            if (chosen.AttemptCount >= MaxAttempts)
            {
                UpdateLog.Warn("[Update] {0} tentativas falharam — descartando marker {1}",
                    new object[] { MaxAttempts, chosen.Version });
                TryDelete(chosen.ZipPath);
                TryDelete(markerPath);
                return ApplyResult.AbortedAfterRetries;
            }

            try
            {
                File.WriteAllText(markerPath, UpdateMarkerJson.Serialize(chosen));
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex,
                    "[Update] falha ao persistir AttemptCount — proxima tentativa zera o contador",
                    EmptyArgs);
            }

            return ApplyResult.RetryNextStartup;
        }

        private void RestoreBackup(string backupDir)
        {
            try
            {
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Delete(_installDirectory, recursive: true);
                }
                if (Directory.Exists(backupDir))
                {
                    Directory.Move(backupDir, _installDirectory);
                }
            }
            catch (Exception ex)
            {
                UpdateLog.ErrorException(ex,
                    "[Update] falha critica ao restaurar backup — instalacao pode estar inconsistente",
                    EmptyArgs);
            }
        }

        /// <summary>
        /// Detecta se uma <see cref="IOException"/> indica "arquivo em uso por outro processo"
        /// (HResult ERROR_SHARING_VIOLATION 0x20 ou ERROR_LOCK_VIOLATION 0x21 no Win32).
        /// </summary>
        public static bool IsFileInUseException(IOException ex)
        {
            if (ex == null) return false;
            int hr = ex.HResult & 0xFFFF;
            return hr == 32 || hr == 33;
        }

        private static readonly object[] EmptyArgs = new object[0];

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }
}
