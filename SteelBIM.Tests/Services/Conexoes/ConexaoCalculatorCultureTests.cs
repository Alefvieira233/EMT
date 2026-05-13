using System.Globalization;
using System.Threading;
using FerramentaEMT.Models.Conexoes;
using FerramentaEMT.Services.Conexoes;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Conexoes
{
    /// <summary>
    /// Regression tests: marcadores de conexao PRECISAM ser culture-invariant
    /// (ponto como separador decimal) mesmo em maquinas com locale pt-BR/DE/FR
    /// que usam virgula. Marcadores viajam em nomes de arquivo, CNC, DSTV, shop
    /// drawings — locale nao pode influenciar.
    ///
    /// Bug corrigido em v1.0.4: string interpolation `$"{x:F1}"` usava CurrentCulture.
    /// </summary>
    public class ConexaoCalculatorCultureTests
    {
        [Theory]
        [InlineData("pt-BR")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        [InlineData("en-US")]
        [InlineData("")]  // InvariantCulture
        public void Marcador_ChapaDePonta_UsaPontoDecimalEmQualquerLocale(string cultureName)
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture =
                    string.IsNullOrEmpty(cultureName)
                        ? CultureInfo.InvariantCulture
                        : CultureInfo.GetCultureInfo(cultureName);

                var config = new ConexaoConfig
                {
                    Tipo = TipoConexao.ChapaDePonta,
                    ChapaPonta = new ConfiguracaoChapaPonta
                    {
                        EspessuraMm = 12.7,
                        LarguraMm = 150,
                        AlturaMm = 250,
                        NumParafusos = 4,
                        DiamParafusoMm = 19
                    }
                };

                string marcador = ConexaoCalculator.GerarMarcadorConexao(config);

                marcador.Should().Contain("12.7", $"locale {cultureName} nao deveria mudar separador decimal");
                marcador.Should().NotContain("12,7");
                marcador.Should().Be("CP-12.7-150x250-4xM19");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("de-DE")]
        public void Marcador_DuplaCantoneira_UsaPontoDecimal(string cultureName)
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

                var config = new ConexaoConfig
                {
                    Tipo = TipoConexao.DuplaCantoneira,
                    Cantoneira = new ConfiguracaoCantoneira
                    {
                        LarguraMm = 100,
                        EspessuraMm = 12.7,
                        NumParafusosPorCantoneira = 3,
                        DiamParafusoMm = 19
                    }
                };

                string marcador = ConexaoCalculator.GerarMarcadorConexao(config);

                marcador.Should().Contain("12.7");
                marcador.Should().NotContain("12,7");
                marcador.Should().Be("DC-100-12.7-3xM19");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Marcador_Gusset_InteirosNaoAfetadosPorLocale()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                var config = new ConexaoConfig
                {
                    Tipo = TipoConexao.ChapaGusset,
                    Gusset = new ConfiguracaoGusset
                    {
                        LarguraMm = 300,
                        AlturaMm = 300,
                        AnguloDiagonalDeg = 45,
                        NumParafusos = 6,
                        DiamParafusoMm = 19
                    }
                };

                string marcador = ConexaoCalculator.GerarMarcadorConexao(config);
                marcador.Should().Be("GS-300x300-45d-6xM19");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
