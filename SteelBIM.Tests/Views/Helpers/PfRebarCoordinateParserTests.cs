using FluentAssertions;
using SteelBIM.Views.Helpers;
using Xunit;

namespace SteelBIM.Tests.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3): testes do PfRebarCoordinateParser extraido
    /// do code-behind das Pf*BarsWindow. Antes intestavel (private static
    /// na Window com dependencia transitiva de WPF). Agora puro.
    /// </summary>
    public class PfRebarCoordinateParserTests
    {
        [Fact]
        public void Input_null_retorna_lista_vazia_sem_error()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates(null, out string error);
            coords.Should().BeEmpty();
            error.Should().BeEmpty();
        }

        [Fact]
        public void Input_vazio_retorna_lista_vazia_sem_error()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates(string.Empty, out string error);
            coords.Should().BeEmpty();
            error.Should().BeEmpty();
        }

        [Fact]
        public void Input_so_whitespace_retorna_lista_vazia_sem_error()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("   \n\t\r\n", out string error);
            coords.Should().BeEmpty();
            error.Should().BeEmpty();
        }

        [Fact]
        public void Linha_unica_valida_retorna_um_ponto()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("10;20", out string error);
            coords.Should().HaveCount(1);
            coords[0].XCm.Should().Be(10);
            coords[0].YCm.Should().Be(20);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Multiplas_linhas_validas_retornam_lista_completa()
        {
            string input = "10;20\n30;40\n50;60";
            var coords = PfRebarCoordinateParser.ParseCoordinates(input, out string error);
            coords.Should().HaveCount(3);
            coords[2].XCm.Should().Be(50);
            coords[2].YCm.Should().Be(60);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Aceita_separador_tab()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("15\t25", out string error);
            coords.Should().HaveCount(1);
            coords[0].XCm.Should().Be(15);
            coords[0].YCm.Should().Be(25);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Aceita_decimal_brasileiro_com_virgula()
        {
            // NumberParsing.TryParseDouble aceita "10,5" como 10.5 em pt-BR
            var coords = PfRebarCoordinateParser.ParseCoordinates("10,5;20,75", out string error);
            coords.Should().HaveCount(1);
            coords[0].XCm.Should().BeApproximately(10.5, 0.001);
            coords[0].YCm.Should().BeApproximately(20.75, 0.001);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Aceita_decimal_com_ponto()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("10.5;20.75", out string error);
            coords.Should().HaveCount(1);
            coords[0].XCm.Should().BeApproximately(10.5, 0.001);
        }

        [Fact]
        public void Linhas_em_branco_misturadas_sao_ignoradas()
        {
            string input = "10;20\n\n30;40\n\n\n50;60\n";
            var coords = PfRebarCoordinateParser.ParseCoordinates(input, out string error);
            coords.Should().HaveCount(3);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Linha_com_um_valor_gera_error_e_retorna_vazio()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("10\n20;30", out string error);
            coords.Should().BeEmpty();
            error.Should().StartWith("Linha 1");
            error.Should().Contain("formato");
        }

        [Fact]
        public void Linha_com_tres_valores_gera_error_e_retorna_vazio()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("10;20;30", out string error);
            coords.Should().BeEmpty();
            error.Should().StartWith("Linha 1");
        }

        [Fact]
        public void Linha_com_valor_nao_numerico_gera_error_e_retorna_vazio()
        {
            var coords = PfRebarCoordinateParser.ParseCoordinates("abc;20", out string error);
            coords.Should().BeEmpty();
            error.Should().StartWith("Linha 1");
            error.Should().Contain("numeros");
        }

        [Fact]
        public void Error_indica_linha_correta_quando_segunda_linha_invalida()
        {
            string input = "10;20\nabc;def";
            var coords = PfRebarCoordinateParser.ParseCoordinates(input, out string error);
            coords.Should().BeEmpty();
            error.Should().StartWith("Linha 2");
        }

        [Fact]
        public void Aceita_quebra_de_linha_windows_crlf()
        {
            string input = "10;20\r\n30;40";
            var coords = PfRebarCoordinateParser.ParseCoordinates(input, out string error);
            coords.Should().HaveCount(2);
            error.Should().BeEmpty();
        }

        [Fact]
        public void Coordenadas_negativas_aceitas()
        {
            // Pilar pode ter eixo de simetria deslocado da origem
            var coords = PfRebarCoordinateParser.ParseCoordinates("-15;-25", out string error);
            coords.Should().HaveCount(1);
            coords[0].XCm.Should().Be(-15);
            coords[0].YCm.Should().Be(-25);
        }
    }
}
