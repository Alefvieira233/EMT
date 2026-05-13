using System;
using System.IO;
using System.Text;
using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    public class Sha256CalculatorTests
    {
        // ---------- ComputeHex ----------

        [Fact]
        public void ComputeHex_array_vazio_retorna_hash_canonico()
        {
            // SHA256 do array vazio eh constante conhecida
            string hash = Sha256Calculator.ComputeHex(new byte[0]);
            hash.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        }

        [Fact]
        public void ComputeHex_de_string_abc_retorna_hash_canonico()
        {
            byte[] data = Encoding.ASCII.GetBytes("abc");
            string hash = Sha256Calculator.ComputeHex(data);
            hash.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        }

        [Fact]
        public void ComputeHex_stream_e_array_retornam_mesmo_hash()
        {
            byte[] data = Encoding.UTF8.GetBytes("teste de hash");
            string fromArray = Sha256Calculator.ComputeHex(data);

            using (MemoryStream ms = new MemoryStream(data))
            {
                string fromStream = Sha256Calculator.ComputeHex(ms);
                fromStream.Should().Be(fromArray);
            }
        }

        [Fact]
        public void ComputeHex_retorna_hex_lowercase()
        {
            byte[] data = Encoding.ASCII.GetBytes("abc");
            string hash = Sha256Calculator.ComputeHex(data);
            hash.Should().Be(hash.ToLowerInvariant());
            hash.Should().HaveLength(64);
        }

        [Fact]
        public void ComputeHex_array_null_lanca()
        {
            Action act = () => Sha256Calculator.ComputeHex((byte[])null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ComputeHex_stream_null_lanca()
        {
            Action act = () => Sha256Calculator.ComputeHex((Stream)null);
            act.Should().Throw<ArgumentNullException>();
        }

        // ---------- FindHashForFile ----------

        [Fact]
        public void FindHashForFile_formato_canonico_dois_espacos()
        {
            string content = "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789  arquivo.zip";
            string result = Sha256Calculator.FindHashForFile(content, "arquivo.zip");
            result.Should().Be("abc1234567890abc1234567890abc1234567890abc1234567890abc123456789");
        }

        [Fact]
        public void FindHashForFile_modo_binario_com_asterisco()
        {
            string content = "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789 *arquivo.zip";
            string result = Sha256Calculator.FindHashForFile(content, "arquivo.zip");
            result.Should().NotBeNull();
        }

        [Fact]
        public void FindHashForFile_nome_diferente_retorna_null()
        {
            string content = "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789  outro.zip";
            string result = Sha256Calculator.FindHashForFile(content, "arquivo.zip");
            result.Should().BeNull();
        }

        [Fact]
        public void FindHashForFile_multiplas_entries_acha_a_correta()
        {
            string content = string.Join("\n",
                "1111111111111111111111111111111111111111111111111111111111111111  arquivo1.zip",
                "2222222222222222222222222222222222222222222222222222222222222222  arquivo2.zip",
                "3333333333333333333333333333333333333333333333333333333333333333  arquivo3.zip");
            string result = Sha256Calculator.FindHashForFile(content, "arquivo2.zip");
            result.Should().Be("2222222222222222222222222222222222222222222222222222222222222222");
        }

        [Fact]
        public void FindHashForFile_hash_invalido_ignora_linha()
        {
            string content = string.Join("\n",
                "tooshort  arquivo.zip",
                "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ  arquivo.zip", // nao-hex
                "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789  arquivo.zip");
            string result = Sha256Calculator.FindHashForFile(content, "arquivo.zip");
            result.Should().Be("abc1234567890abc1234567890abc1234567890abc1234567890abc123456789");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void FindHashForFile_conteudo_vazio_retorna_null(string content)
        {
            Sha256Calculator.FindHashForFile(content, "arquivo.zip").Should().BeNull();
        }

        [Fact]
        public void FindHashForFile_filename_vazio_retorna_null()
        {
            string content = "abc1234567890abc1234567890abc1234567890abc1234567890abc123456789  arquivo.zip";
            Sha256Calculator.FindHashForFile(content, "").Should().BeNull();
        }

        [Fact]
        public void FindHashForFile_normaliza_hash_para_lowercase()
        {
            string content = "ABC1234567890ABC1234567890ABC1234567890ABC1234567890ABC123456789  arquivo.zip";
            string result = Sha256Calculator.FindHashForFile(content, "arquivo.zip");
            result.Should().Be("abc1234567890abc1234567890abc1234567890abc1234567890abc123456789");
        }
    }
}
