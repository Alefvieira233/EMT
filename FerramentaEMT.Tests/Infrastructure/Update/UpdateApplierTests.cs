using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    /// <summary>
    /// Tests usam diretorio temporario por instancia — limpos no Dispose
    /// (xUnit cria nova instancia da classe por teste, entao nao ha
    /// race entre testes paralelos do mesmo runner).
    /// </summary>
    public class UpdateApplierTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _pendingDir;
        private readonly string _installDir;

        public UpdateApplierTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ferramentaemt-tests-" + Guid.NewGuid().ToString("N"));
            _pendingDir = Path.Combine(_tempRoot, "pending");
            _installDir = Path.Combine(_tempRoot, "install");
            Directory.CreateDirectory(_pendingDir);
            Directory.CreateDirectory(_installDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best effort */ }
        }

        // ---------- NoPending ----------

        [Fact]
        public void Sem_pending_retorna_NoPending()
        {
            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.NoPending);
            applier.LastVersionAttempted.Should().BeEmpty();
        }

        [Fact]
        public void Pending_dir_inexistente_retorna_NoPending()
        {
            string ghost = Path.Combine(_tempRoot, "no-such-dir");
            UpdateApplier applier = new UpdateApplier(ghost, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.NoPending);
        }

        // ---------- InvalidMarker ----------

        [Fact]
        public void Marker_corrompido_retorna_NoPending_e_deleta()
        {
            // Marker corrompido eh deletado durante a varredura. Se ele eh o unico,
            // o resultado eh NoPending (nada validado).
            string mp = Path.Combine(_pendingDir, "broken.marker");
            File.WriteAllText(mp, "{ broken !!!");

            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.NoPending);
            File.Exists(mp).Should().BeFalse();
        }

        [Fact]
        public void Marker_aponta_para_zip_inexistente_retorna_InvalidMarker()
        {
            UpdateMarker marker = new UpdateMarker
            {
                Version = "v1.7.0",
                ZipPath = Path.Combine(_tempRoot, "ghost.zip"),
                Sha256 = "abc",
                DownloadedAtUtc = DateTime.UtcNow,
            };
            string mp = Path.Combine(_pendingDir, "marker.marker");
            File.WriteAllText(mp, UpdateMarkerJson.Serialize(marker));

            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.InvalidMarker);
            File.Exists(mp).Should().BeFalse();
        }

        [Fact]
        public void SHA256_do_zip_diferente_do_marker_retorna_InvalidMarker_e_deleta()
        {
            string zipPath = CreateValidZip(Path.Combine(_tempRoot, "test.zip"), "FerramentaEMT.dll", "dummy");
            UpdateMarker marker = new UpdateMarker
            {
                Version = "v1.7.0",
                ZipPath = zipPath,
                Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
                DownloadedAtUtc = DateTime.UtcNow,
            };
            string mp = Path.Combine(_pendingDir, "marker.marker");
            File.WriteAllText(mp, UpdateMarkerJson.Serialize(marker));

            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.InvalidMarker);
            File.Exists(mp).Should().BeFalse();
            File.Exists(zipPath).Should().BeFalse();
        }

        // ---------- Applied ----------

        [Fact]
        public void Marker_valido_aplica_e_limpa_pending()
        {
            string zipPath = CreateValidZip(Path.Combine(_tempRoot, "test.zip"), "FerramentaEMT.dll", "stub-dll-content");
            string sha = ComputeSha256(zipPath);

            UpdateMarker marker = new UpdateMarker
            {
                Version = "v1.7.0",
                ZipPath = zipPath,
                Sha256 = sha,
                DownloadedAtUtc = DateTime.UtcNow,
            };
            string mp = Path.Combine(_pendingDir, "marker.marker");
            File.WriteAllText(mp, UpdateMarkerJson.Serialize(marker));

            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            ApplyResult result = applier.ApplyPendingIfAny();

            result.Should().Be(ApplyResult.Applied);
            applier.LastVersionAttempted.Should().Be("v1.7.0");
            File.Exists(mp).Should().BeFalse();
            File.Exists(zipPath).Should().BeFalse();
            File.Exists(Path.Combine(_installDir, "FerramentaEMT.dll")).Should().BeTrue();
            // backup deletado tambem
            Directory.Exists(_installDir + ".bak").Should().BeFalse();
        }

        // ---------- multiplos markers ----------

        [Fact]
        public void Multiplos_markers_aplica_a_maior_versao_e_deleta_outros()
        {
            string zip16 = CreateValidZip(Path.Combine(_tempRoot, "v16.zip"), "FerramentaEMT.dll", "v1.6");
            string zip17 = CreateValidZip(Path.Combine(_tempRoot, "v17.zip"), "FerramentaEMT.dll", "v1.7");

            UpdateMarker m16 = new UpdateMarker { Version = "v1.6.0", ZipPath = zip16, Sha256 = ComputeSha256(zip16), DownloadedAtUtc = DateTime.UtcNow };
            UpdateMarker m17 = new UpdateMarker { Version = "v1.7.0", ZipPath = zip17, Sha256 = ComputeSha256(zip17), DownloadedAtUtc = DateTime.UtcNow };

            string mp16 = Path.Combine(_pendingDir, "v1.6.0.marker");
            string mp17 = Path.Combine(_pendingDir, "v1.7.0.marker");
            File.WriteAllText(mp16, UpdateMarkerJson.Serialize(m16));
            File.WriteAllText(mp17, UpdateMarkerJson.Serialize(m17));

            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.Applied);
            applier.LastVersionAttempted.Should().Be("v1.7.0");

            File.Exists(mp16).Should().BeFalse();
            File.Exists(mp17).Should().BeFalse();
        }

        // ---------- empty pending dir ----------

        [Fact]
        public void Pending_dir_existe_mas_vazio_retorna_NoPending()
        {
            UpdateApplier applier = new UpdateApplier(_pendingDir, _installDir);
            applier.ApplyPendingIfAny().Should().Be(ApplyResult.NoPending);
        }

        // ---------- IsFileInUseException ----------

        [Fact]
        public void IsFileInUseException_detecta_HResult_32_e_33()
        {
            IOException sharing = new IOException("sharing violation") { HResult = unchecked((int)0x80070020) }; // 32
            IOException locking = new IOException("lock violation") { HResult = unchecked((int)0x80070021) }; // 33
            IOException other = new IOException("other") { HResult = unchecked((int)0x80070005) }; // access denied

            UpdateApplier.IsFileInUseException(sharing).Should().BeTrue();
            UpdateApplier.IsFileInUseException(locking).Should().BeTrue();
            UpdateApplier.IsFileInUseException(other).Should().BeFalse();
            UpdateApplier.IsFileInUseException(null).Should().BeFalse();
        }

        // ---------- helpers ----------

        private static string CreateValidZip(string path, string entryName, string content)
        {
            using (FileStream fs = File.Create(path))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = zip.CreateEntry(entryName);
                using (Stream s = entry.Open())
                {
                    byte[] payload = Encoding.UTF8.GetBytes(content);
                    s.Write(payload, 0, payload.Length);
                }
            }
            return path;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                return Sha256Calculator.ComputeHex(fs);
            }
        }
    }
}
