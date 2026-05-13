using System.IO;
using System.IO.Compression;
using System.Text;
using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    public class ZipSlipValidatorTests
    {
        // ---------- IsSafeEntryName ----------

        [Theory]
        [InlineData("FerramentaEMT.dll")]
        [InlineData("Resources/icon.png")]
        [InlineData("Resources\\icon.png")]
        [InlineData("a/b/c/file.txt")]
        [InlineData("nested/deep/folder/file.dll")]
        public void IsSafeEntryName_aceita_paths_relativos_normais(string name)
        {
            ZipSlipValidator.IsSafeEntryName(name).Should().BeTrue();
        }

        [Theory]
        [InlineData("../etc/passwd")]
        [InlineData("..\\Windows\\System32\\evil.dll")]
        [InlineData("a/../../../etc/passwd")]
        [InlineData("a/b/../c")]
        [InlineData("..")]
        public void IsSafeEntryName_rejeita_dotdot_em_qualquer_segmento(string name)
        {
            ZipSlipValidator.IsSafeEntryName(name).Should().BeFalse();
        }

        [Theory]
        [InlineData("/etc/passwd")]
        [InlineData("\\Windows\\evil.dll")]
        [InlineData("C:\\Windows\\evil.dll")]
        [InlineData("D:/data/x")]
        public void IsSafeEntryName_rejeita_paths_absolutos(string name)
        {
            ZipSlipValidator.IsSafeEntryName(name).Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void IsSafeEntryName_rejeita_nome_vazio_ou_null(string name)
        {
            ZipSlipValidator.IsSafeEntryName(name).Should().BeFalse();
        }

        // ---------- AllEntriesSafe (em ZipArchive real montado em memoria) ----------

        [Fact]
        public void AllEntriesSafe_zip_normal_retorna_true()
        {
            byte[] zipBytes = BuildZipWithEntries("file1.dll", "Resources/icon.png", "deep/folder/file.txt");
            using (MemoryStream ms = new MemoryStream(zipBytes))
            using (ZipArchive arc = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                ZipSlipValidator.AllEntriesSafe(arc).Should().BeTrue();
            }
        }

        [Fact]
        public void AllEntriesSafe_zip_com_dotdot_retorna_false()
        {
            byte[] zipBytes = BuildZipWithEntries("file1.dll", "../etc/passwd");
            using (MemoryStream ms = new MemoryStream(zipBytes))
            using (ZipArchive arc = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                ZipSlipValidator.AllEntriesSafe(arc).Should().BeFalse();
            }
        }

        // Helper: monta um zip em memoria com as entries dadas (conteudo dummy)
        private static byte[] BuildZipWithEntries(params string[] entryNames)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (string name in entryNames)
                    {
                        ZipArchiveEntry entry = zip.CreateEntry(name);
                        using (Stream stream = entry.Open())
                        {
                            byte[] payload = Encoding.UTF8.GetBytes("dummy");
                            stream.Write(payload, 0, payload.Length);
                        }
                    }
                }
                return ms.ToArray();
            }
        }
    }
}
