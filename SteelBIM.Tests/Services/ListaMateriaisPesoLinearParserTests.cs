using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    /// <summary>
    /// v2.8.10 (Etapa C do plano Onda 3): testes do helper puro extraido de
    /// <c>ListaMateriaisExportService</c>. Valida triagem de nomes e parsing
    /// de strings em pt-BR + en-US sem precisar de Revit Document.
    /// </summary>
    public class ListaMateriaisPesoLinearParserTests
    {
        // ============================================
        // NormalizarToken
        // ============================================

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("ABC", "abc")]
        [InlineData("  Kg/m  ", "kg/m")]
        [InlineData("Peso Linear", "peso linear")]
        public void NormalizarToken_NormalizaCorretamente(string? input, string esperado)
        {
            Assert.Equal(esperado, ListaMateriaisPesoLinearParser.NormalizarToken(input));
        }

        // ============================================
        // ParametroNomeIndicaKgPorMetro
        // ============================================

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("Mark", false)]
        [InlineData("Volume", false)]
        [InlineData("kg/m", true)]
        [InlineData("Kg/M", true)]
        [InlineData("Peso Linear", true)]
        [InlineData("MASSA LINEAR", true)]
        [InlineData("Mass per Unit Length", true)]
        [InlineData("weight per length", true)]
        [InlineData("Unit Mass", true)]
        [InlineData("peso/m", false)] // nao esta na lista
        public void ParametroNomeIndicaKgPorMetro_DetectaTokens(string? nome, bool esperado)
        {
            Assert.Equal(esperado, ListaMateriaisPesoLinearParser.ParametroNomeIndicaKgPorMetro(nome));
        }

        // ============================================
        // TryParsePesoLinearStringKgM
        // ============================================

        [Fact]
        public void TryParse_Null_RetornaFalse()
        {
            bool ok = ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM(null, out double v);
            Assert.False(ok);
            Assert.Equal(0.0, v);
        }

        [Fact]
        public void TryParse_Vazio_RetornaFalse()
        {
            Assert.False(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("   ", out _));
        }

        [Fact]
        public void TryParse_SemNumero_RetornaFalse()
        {
            Assert.False(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("kg/m", out _));
        }

        [Theory]
        [InlineData("59.42 kg/m", 59.42)]
        [InlineData("59.42", 59.42)]
        [InlineData("100 kg/m", 100.0)]
        [InlineData("12.5", 12.5)]
        public void TryParse_FormatoEnUS_Funciona(string texto, double esperado)
        {
            Assert.True(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM(texto, out double v));
            Assert.Equal(esperado, v, precision: 4);
        }

        [Theory]
        [InlineData("59,42 kg/m", 59.42)]
        [InlineData("12,5", 12.5)]
        [InlineData("100,75 Kg/M", 100.75)]
        public void TryParse_FormatoPtBR_Funciona(string texto, double esperado)
        {
            Assert.True(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM(texto, out double v));
            Assert.Equal(esperado, v, precision: 4);
        }

        [Fact]
        public void TryParse_RegexCapturaPrimeiroGrupoNumerico()
        {
            // O Regex captura apenas o PRIMEIRO grupo numerico (\d+[.,]\d+)?.
            // Comportamento herdado pre-refator: "1,234.56" → casa "1,234" → 1.234
            // "1.234,56" → casa "1.234" → 1.234.
            // Documenta o limite atual do parser — peso linear esperado e' < 1000
            // na pratica (vigas comuns 5-200 kg/m), entao milhar e' raro.
            Assert.True(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("1,234.56 kg/m", out double v1));
            Assert.Equal(1.234, v1, precision: 4);

            Assert.True(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("1.234,56 kg/m", out double v2));
            Assert.Equal(1.234, v2, precision: 4);
        }

        [Fact]
        public void TryParse_ZeroOuNegativo_RetornaFalse()
        {
            // Implementacao exige peso > 0
            Assert.False(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("0 kg/m", out _));
            // Sinal negativo nao representa peso fisico — descartado
            Assert.False(ListaMateriaisPesoLinearParser.TryParsePesoLinearStringKgM("-5 kg/m", out _));
        }

        [Fact]
        public void TokensPesoLinear_ListaImutavel()
        {
            // Validacao defensiva: garante que a lista exposta nao colide com nomes triviais
            Assert.Contains("kg/m", ListaMateriaisPesoLinearParser.TokensPesoLinear);
            Assert.Contains("peso linear", ListaMateriaisPesoLinearParser.TokensPesoLinear);
            Assert.DoesNotContain("mark", ListaMateriaisPesoLinearParser.TokensPesoLinear);
        }
    }
}
