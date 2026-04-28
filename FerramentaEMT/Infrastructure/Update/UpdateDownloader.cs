using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Resultado de uma tentativa de download de update.
    /// </summary>
    public enum DownloadResult
    {
        /// <summary>Download + 6 validacoes OK; .zip salvo em pending/.</summary>
        Ready = 0,

        /// <summary>Asset .zip nao encontrado no release.</summary>
        ZipAssetMissing = 1,

        /// <summary>Asset checksums.txt nao encontrado no release.</summary>
        ChecksumsAssetMissing = 2,

        /// <summary>Tamanho do .zip fora dos limites (1MB, 50MB).</summary>
        SizeOutOfBounds = 3,

        /// <summary>SHA256 do .zip baixado nao bate com o publicado.</summary>
        HashMismatch = 4,

        /// <summary>.zip nao abre como arquivo zip valido.</summary>
        InvalidZipArchive = 5,

        /// <summary>Zip-slip detectado: entry com .. ou path absoluto.</summary>
        ZipSlipDetected = 6,

        /// <summary>.zip nao contem FerramentaEMT.dll no top-level.</summary>
        DllMissing = 7,

        /// <summary>FerramentaEMT.dll extraido tem versao diferente da tag_name do release.</summary>
        VersionMismatch = 8,

        /// <summary>Falha de rede / IO durante download.</summary>
        IoError = 9,

        /// <summary>Cancellation token disparado.</summary>
        Canceled = 10,
    }

    /// <summary>
    /// Baixa o asset .zip de um GitHubRelease, valida 6 invariantes,
    /// e grava em <c>%LocalAppData%\FerramentaEMT\Updates\{version}.zip</c>.
    ///
    /// As 6 validacoes (rejeicao = abort + delete + log):
    ///   1. Tamanho do .zip em (1 MB, 50 MB) — sanity check
    ///   2. ZipArchive abre sem excecao
    ///   3. Nenhuma entry com .. ou path absoluto (zip-slip)
    ///   4. SHA256 bate com o asset checksums.txt
    ///   5. Top-level contem FerramentaEMT.dll
    ///   6. AssemblyInformationalVersion do .dll == tag_name do release
    /// </summary>
    public sealed class UpdateDownloader
    {
        private const long MinSizeBytes = 1L * 1024 * 1024;       // 1 MB
        private const long MaxSizeBytes = 50L * 1024 * 1024;      // 50 MB
        private const string DllAssetName = "FerramentaEMT.dll";
        private const string ChecksumsFileName = "checksums.txt";

        private readonly HttpClient _http;
        private readonly string _updatesDirectory;

        /// <summary>Construtor para producao (HttpClient compartilhado).</summary>
        public UpdateDownloader(HttpClient httpClient)
            : this(httpClient, GetDefaultUpdatesDirectory())
        {
        }

        /// <summary>Construtor para testes (permite redirecionar diretorio).</summary>
        public UpdateDownloader(HttpClient httpClient, string updatesDirectory)
        {
            if (httpClient == null) throw new ArgumentNullException("httpClient");
            if (string.IsNullOrWhiteSpace(updatesDirectory))
                throw new ArgumentException("updatesDirectory obrigatorio", "updatesDirectory");
            _http = httpClient;
            _updatesDirectory = updatesDirectory;
        }

        public static string GetDefaultUpdatesDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FerramentaEMT", "Updates");
        }

        /// <summary>
        /// Caminho do .zip baixado (vazio em caso de falha).
        /// Util para logging e para o UpdateApplier consumir.
        /// </summary>
        public string DownloadedZipPath { get; private set; }

        public async Task<DownloadResult> DownloadAndValidateAsync(GitHubRelease release, CancellationToken ct)
        {
            if (release == null) throw new ArgumentNullException("release");
            if (string.IsNullOrWhiteSpace(release.TagName))
                throw new ArgumentException("release.TagName obrigatorio", "release");

            DownloadedZipPath = string.Empty;

            GitHubAsset zipAsset = release.Assets.FirstOrDefault(a =>
                a != null && !string.IsNullOrEmpty(a.Name)
                && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zipAsset == null || string.IsNullOrEmpty(zipAsset.BrowserDownloadUrl))
            {
                Logger.Warn("[Update] release {Tag} sem asset .zip", release.TagName);
                return DownloadResult.ZipAssetMissing;
            }

            GitHubAsset checksumsAsset = release.Assets.FirstOrDefault(a =>
                a != null && string.Equals(a.Name, ChecksumsFileName, StringComparison.OrdinalIgnoreCase));
            if (checksumsAsset == null || string.IsNullOrEmpty(checksumsAsset.BrowserDownloadUrl))
            {
                Logger.Warn("[Update] release {Tag} sem asset {Name}", release.TagName, ChecksumsFileName);
                return DownloadResult.ChecksumsAssetMissing;
            }

            // Validacao 1: tamanho declarado pelo metadata do release
            if (zipAsset.Size > 0 && (zipAsset.Size < MinSizeBytes || zipAsset.Size > MaxSizeBytes))
            {
                Logger.Warn("[Update] tamanho declarado {Size}B fora dos limites (1MB-50MB)", zipAsset.Size);
                return DownloadResult.SizeOutOfBounds;
            }

            // Limpar diretorio Updates/ antes do novo download
            try
            {
                if (Directory.Exists(_updatesDirectory))
                {
                    foreach (string oldFile in Directory.GetFiles(_updatesDirectory, "*.zip"))
                    {
                        try { File.Delete(oldFile); } catch { /* best effort */ }
                    }
                }
                else
                {
                    Directory.CreateDirectory(_updatesDirectory);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Update] Falha ao limpar Updates/");
                return DownloadResult.IoError;
            }

            string zipPath = Path.Combine(_updatesDirectory, ReleaseTagToFileName(release.TagName));
            string checksumsContent;

            try
            {
                // Baixar checksums.txt (pequeno, em memoria)
                using (HttpResponseMessage resp = await _http.GetAsync(
                    checksumsAsset.BrowserDownloadUrl, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        Logger.Warn("[Update] falha ao baixar checksums.txt (status {Status})", (int)resp.StatusCode);
                        return DownloadResult.IoError;
                    }
                    checksumsContent = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                // Baixar o .zip pra disco (streaming)
                using (HttpResponseMessage resp = await _http.GetAsync(
                    zipAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        Logger.Warn("[Update] falha ao baixar .zip (status {Status})", (int)resp.StatusCode);
                        return DownloadResult.IoError;
                    }

                    using (Stream input = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream output = File.Create(zipPath))
                    {
                        await input.CopyToAsync(output, 81920, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                TryDelete(zipPath);
                return DownloadResult.Canceled;
            }
            catch (HttpRequestException ex)
            {
                TryDelete(zipPath);
                Logger.Warn(ex, "[Update] falha de rede durante download");
                return DownloadResult.IoError;
            }
            catch (IOException ex)
            {
                TryDelete(zipPath);
                Logger.Warn(ex, "[Update] falha de IO durante download");
                return DownloadResult.IoError;
            }
            catch (Exception ex)
            {
                TryDelete(zipPath);
                Logger.Warn(ex, "[Update] falha inesperada durante download");
                return DownloadResult.IoError;
            }

            // Validacao 1 (re-check com tamanho real do disco)
            FileInfo info = new FileInfo(zipPath);
            if (info.Length < MinSizeBytes || info.Length > MaxSizeBytes)
            {
                Logger.Warn("[Update] tamanho real {Size}B fora dos limites", info.Length);
                TryDelete(zipPath);
                return DownloadResult.SizeOutOfBounds;
            }

            // Validacao 4: SHA256 hash bate com checksums.txt
            string expectedHash = Sha256Calculator.FindHashForFile(checksumsContent, zipAsset.Name);
            if (string.IsNullOrEmpty(expectedHash))
            {
                Logger.Warn("[Update] hash do {Name} nao encontrado em checksums.txt", zipAsset.Name);
                TryDelete(zipPath);
                return DownloadResult.HashMismatch;
            }

            string actualHash;
            try
            {
                using (FileStream fs = File.OpenRead(zipPath))
                {
                    actualHash = Sha256Calculator.ComputeHex(fs);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Update] falha ao calcular SHA256");
                TryDelete(zipPath);
                return DownloadResult.IoError;
            }

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn("[Update] SHA256 nao bate (esperado={Expected} obtido={Actual})",
                    expectedHash, actualHash);
                TryDelete(zipPath);
                return DownloadResult.HashMismatch;
            }

            // Validacoes 2, 3, 5, 6: abrir zip, zip-slip, dll presente, versao da dll
            DownloadResult zipValidation = ValidateZipContents(zipPath, release.TagName);
            if (zipValidation != DownloadResult.Ready)
            {
                TryDelete(zipPath);
                return zipValidation;
            }

            DownloadedZipPath = zipPath;
            Logger.Info("[Update] download de {Tag} validado em {Path}", release.TagName, zipPath);
            return DownloadResult.Ready;
        }

        private DownloadResult ValidateZipContents(string zipPath, string expectedTag)
        {
            ZipArchive archive = null;
            string tempDllPath = null;
            try
            {
                try
                {
                    archive = ZipFile.OpenRead(zipPath);
                }
                catch (InvalidDataException)
                {
                    return DownloadResult.InvalidZipArchive;
                }

                // Validacao 3: zip-slip
                if (!ZipSlipValidator.AllEntriesSafe(archive))
                {
                    Logger.Warn("[Update] zip-slip detectado em {Path}", zipPath);
                    return DownloadResult.ZipSlipDetected;
                }

                // Validacao 5: top-level contem FerramentaEMT.dll
                ZipArchiveEntry dllEntry = archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName, DllAssetName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(e.FullName), DllAssetName, StringComparison.OrdinalIgnoreCase));
                if (dllEntry == null)
                {
                    Logger.Warn("[Update] zip {Path} nao contem {Dll}", zipPath, DllAssetName);
                    return DownloadResult.DllMissing;
                }

                // Validacao 6: versao do .dll bate com tag_name
                tempDllPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
                dllEntry.ExtractToFile(tempDllPath, overwrite: true);

                AssemblyName name = AssemblyName.GetAssemblyName(tempDllPath);
                int? cmp = SemVerComparer.CompareStrings(name.Version?.ToString() ?? "", expectedTag);
                if (cmp == null || cmp.Value != 0)
                {
                    Logger.Warn("[Update] versao do .dll ({DllVer}) nao bate com tag_name ({Tag})",
                        name.Version, expectedTag);
                    return DownloadResult.VersionMismatch;
                }

                return DownloadResult.Ready;
            }
            finally
            {
                if (archive != null) archive.Dispose();
                if (tempDllPath != null) TryDelete(tempDllPath);
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        /// <summary>
        /// Converte tag_name (ex: "v1.7.0") em nome de arquivo seguro
        /// (ex: "1.7.0.zip"). Tira o "v" prefixo e troca chars invalidos.
        /// </summary>
        internal static string ReleaseTagToFileName(string tagName)
        {
            string s = tagName ?? "";
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid) s = s.Replace(c, '_');
            return s + ".zip";
        }
    }
}
