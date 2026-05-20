using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    public class MarcarPecasSignatureBuilderTests
    {
        // ============================================================
        // BuildTypeKey
        // ============================================================

        [Fact]
        public void BuildTypeKey_FamilyNamePlusName()
        {
            Assert.Equal("W12X26|Default",
                MarcarPecasSignatureBuilder.BuildTypeKey("W12X26", "Default"));
        }

        [Theory]
        [InlineData(null, null, "?|?")]
        [InlineData("", "", "?|?")]
        [InlineData("  ", "  ", "?|?")]
        [InlineData(null, "Default", "?|Default")]
        [InlineData("W12X26", null, "W12X26|?")]
        [InlineData("  W12X26  ", "  Default  ", "W12X26|Default")]
        public void BuildTypeKey_TratasNulosEWhitespace(string fam, string nome, string esperado)
        {
            Assert.Equal(esperado, MarcarPecasSignatureBuilder.BuildTypeKey(fam, nome));
        }

        // ============================================================
        // BuildMaterialKey
        // ============================================================

        [Theory]
        [InlineData(null, "<sem>")]
        [InlineData("", "<sem>")]
        [InlineData("  ", "<sem>")]
        [InlineData("Aco A36", "Aco A36")]
        [InlineData("  Aco A36  ", "Aco A36")]
        public void BuildMaterialKey_TratasNulosEWhitespace(string mat, string esperado)
        {
            Assert.Equal(esperado, MarcarPecasSignatureBuilder.BuildMaterialKey(mat));
        }

        // ============================================================
        // BuildParameterSection
        // ============================================================

        [Fact]
        public void BuildParameterSection_OrdemAlfabetica()
        {
            var p = new[] { ("largura", "50"), ("altura", "100"), ("comprimento", "200") };
            Assert.Equal(
                "T:altura=100|T:comprimento=200|T:largura=50|",
                MarcarPecasSignatureBuilder.BuildParameterSection("T", p));
        }

        [Fact]
        public void BuildParameterSection_IndependeDaOrdemEntrada()
        {
            var p1 = new[] { ("z", "1"), ("a", "2"), ("m", "3") };
            var p2 = new[] { ("a", "2"), ("m", "3"), ("z", "1") };
            var p3 = new[] { ("m", "3"), ("z", "1"), ("a", "2") };
            string s1 = MarcarPecasSignatureBuilder.BuildParameterSection("X", p1);
            string s2 = MarcarPecasSignatureBuilder.BuildParameterSection("X", p2);
            string s3 = MarcarPecasSignatureBuilder.BuildParameterSection("X", p3);
            Assert.Equal(s1, s2);
            Assert.Equal(s2, s3);
        }

        [Fact]
        public void BuildParameterSection_IgnoraNomesEValoresVaziosOuWhitespace()
        {
            var p = new (string, string)[]
            {
                ("altura", "100"),
                ("", "x"),
                ("largura", ""),
                ("  ", "y"),
                ("espessura", "  "),
            };
            Assert.Equal(
                "T:altura=100|",
                MarcarPecasSignatureBuilder.BuildParameterSection("T", p));
        }

        [Fact]
        public void BuildParameterSection_RetornaVazioParaNull()
        {
            Assert.Equal("", MarcarPecasSignatureBuilder.BuildParameterSection("T", null));
        }

        [Fact]
        public void BuildParameterSection_RetornaVazioParaListaVazia()
        {
            Assert.Equal("",
                MarcarPecasSignatureBuilder.BuildParameterSection("T", new (string, string)[0]));
        }

        [Fact]
        public void BuildParameterSection_PrefixoCustomizavel()
        {
            var p = new[] { ("a", "1") };
            Assert.Equal("Z:a=1|", MarcarPecasSignatureBuilder.BuildParameterSection("Z", p));
            Assert.Equal("I:a=1|", MarcarPecasSignatureBuilder.BuildParameterSection("I", p));
        }

        // ============================================================
        // Regression guards P0 DETERMINISMO (v2.6.1)
        // ============================================================

        [Fact]
        public void BuildParameterSection_NaoRegredeV261_DETERMINISMO_OrdemEstavel()
        {
            // Guard P0 DETERMINISMO: o bug original (v2.6.0-) era que
            // Element.Parameters iteration tinha ordem nao-deterministica
            // gerando signatures variantes a cada execucao. Este teste
            // prova que duas chamadas com MESMO INPUT em ORDEM DIFERENTE
            // produzem MESMA OUTPUT — provando que OrderBy(Name) funciona.
            var inputA = new[] { ("z", "1"), ("a", "2"), ("m", "3") };
            var inputB = new[] { ("a", "2"), ("m", "3"), ("z", "1") };
            Assert.Equal(
                MarcarPecasSignatureBuilder.BuildParameterSection("X", inputA),
                MarcarPecasSignatureBuilder.BuildParameterSection("X", inputB)
            );
        }

        [Fact]
        public void BuildTypeKey_NaoRegredeV261_DETERMINISMO_NaoUsaElementId()
        {
            // Guard P0 DETERMINISMO: o bug v2.6.0- era usar tipo.Id.Value
            // (numerico, per-document). Duas peças idênticas em projetos
            // diferentes recebiam IDs diferentes — quebrando dedup
            // inter-projeto. BuildTypeKey usa STRING (FamilyName + Name)
            // que e estavel cross-document. Este teste prova que a chave
            // gerada nao contem nada numerico nem o substring "Id".
            string key = MarcarPecasSignatureBuilder.BuildTypeKey("W12X26", "Default");
            Assert.Equal("W12X26|Default", key);
            Assert.DoesNotContain("Id", key);
            Assert.DoesNotContain("=", key);
            // Confirmar idempotencia simples (mesmo input -> mesmo output, sempre)
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(key, MarcarPecasSignatureBuilder.BuildTypeKey("W12X26", "Default"));
            }
        }
    }
}
