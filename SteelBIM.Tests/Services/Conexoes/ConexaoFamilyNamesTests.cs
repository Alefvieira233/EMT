using FerramentaEMT.Models.Conexoes;
using FerramentaEMT.Services.Conexoes;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Conexoes
{
    public class ConexaoFamilyNamesTests
    {
        [Theory]
        [InlineData(TipoConexao.ChapaDePonta, "EMT_Chapa_Ponta")]
        [InlineData(TipoConexao.DuplaCantoneira, "EMT_Dupla_Cantoneira")]
        [InlineData(TipoConexao.ChapaGusset, "EMT_Chapa_Gusset")]
        public void For_KnownTypes_ReturnsExpectedName(TipoConexao tipo, string expected)
        {
            ConexaoFamilyNames.For(tipo).Should().Be(expected);
        }

        [Fact]
        public void For_UnknownType_ReturnsEmpty()
        {
            ConexaoFamilyNames.For((TipoConexao)9999).Should().Be(string.Empty);
        }

        [Fact]
        public void Constants_MatchForMethod()
        {
            ConexaoFamilyNames.For(TipoConexao.ChapaDePonta).Should().Be(ConexaoFamilyNames.ChapaDePonta);
            ConexaoFamilyNames.For(TipoConexao.DuplaCantoneira).Should().Be(ConexaoFamilyNames.DuplaCantoneira);
            ConexaoFamilyNames.For(TipoConexao.ChapaGusset).Should().Be(ConexaoFamilyNames.ChapaGusset);
        }

        [Theory]
        [InlineData(TipoConexao.ChapaDePonta)]
        [InlineData(TipoConexao.DuplaCantoneira)]
        [InlineData(TipoConexao.ChapaGusset)]
        public void For_KnownTypes_StartsWithEmtPrefix(TipoConexao tipo)
        {
            ConexaoFamilyNames.For(tipo).Should().StartWith("EMT_");
        }
    }
}
