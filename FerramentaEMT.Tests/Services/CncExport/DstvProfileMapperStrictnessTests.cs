using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Services.CncExport;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.CncExport
{
    /// <summary>
    /// Regression tests: MapByDesignation antes usava StartsWith(letra) + HasDigit(string),
    /// o que classificava qualquer string comecando com letra e contendo digito em qualquer
    /// posicao como perfil. "CUSTOM-001" virava U-channel porque comeca com 'C' e tem digito.
    ///
    /// Bug corrigido em v1.0.4: introduzido helper StartsDigit que exige digito IMEDIATAMENTE
    /// apos o prefixo (tolerando '-' ou espaco opcional).
    /// </summary>
    public class DstvProfileMapperStrictnessTests
    {
        [Theory]
        [InlineData("CUSTOM-001")]        // Antes virava U (comeca com C + tem digito)
        [InlineData("CustomType-99")]
        [InlineData("UNKNOWN-1")]         // Antes virava U
        [InlineData("LABEL-5")]           // Antes virava L (angle)
        [InlineData("TEST-7")]            // Antes virava T (tee)
        [InlineData("SOMETHING-3")]       // Antes virava I (S-shape)
        [InlineData("MYSTERY-8")]         // Antes virava I (M-shape) OU U (MC-prefix check)
        [InlineData("WILDCARD-2")]        // Antes virava I (W-shape)
        public void Map_TipoNaoReconhecido_CaiParaSO_QuandoFamiliaTambemEhDesconhecida(string typeName)
        {
            DstvProfileMapper.Map("UnknownFamily", typeName)
                .Should().Be(DstvProfileType.SO,
                    $"'{typeName}' nao e perfil de verdade — digito nao esta imediatamente apos a letra");
        }

        [Theory]
        [InlineData("W12X26", DstvProfileType.I)]
        [InlineData("W-250X33", DstvProfileType.I)]  // separador '-' tolerado
        [InlineData("C310X45", DstvProfileType.U)]
        [InlineData("U200", DstvProfileType.U)]
        [InlineData("L4X4X1/2", DstvProfileType.L)]
        [InlineData("T75X75", DstvProfileType.T)]
        [InlineData("S12X31.8", DstvProfileType.I)]
        [InlineData("WT9X30", DstvProfileType.T)]
        public void Map_TiposReaisContinuamSendoReconhecidos(string typeName, DstvProfileType esperado)
        {
            DstvProfileMapper.Map("", typeName).Should().Be(esperado);
        }

        [Fact]
        public void Map_FamiliaWideFlange_ComTipoDesconhecido_CaiNaHeuristicaFamilia()
        {
            // Tipo nao reconhecido MAS familia clara -> usa heuristica de familia
            DstvProfileMapper.Map("Wide Flange Beam", "Custom-001")
                .Should().Be(DstvProfileType.I);
        }

        [Fact]
        public void Map_FamiliaChannel_ComTipoDesconhecido_CaiNaHeuristicaFamilia()
        {
            DstvProfileMapper.Map("Channel", "CustomType-99")
                .Should().Be(DstvProfileType.U);
        }
    }
}
