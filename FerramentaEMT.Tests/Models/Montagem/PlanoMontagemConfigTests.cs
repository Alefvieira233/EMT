using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.Montagem;

namespace FerramentaEMT.Tests.Models.Montagem
{
    public class PlanoMontagemConfigTests
    {
        [Fact]
        public void Constructor_Default_ParametroEtapaCorreto()
        {
            var config = new PlanoMontagemConfig();

            config.NomeParametroEtapa.Should().Be("EMT_Etapa_Montagem");
        }

        [Fact]
        public void Constructor_Default_DestaqueVisualIsTrue()
        {
            var config = new PlanoMontagemConfig();

            config.AplicarDestaqueVisual.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_ExportarRelatorioIsFalse()
        {
            var config = new PlanoMontagemConfig();

            config.ExportarRelatorio.Should().BeFalse();
        }

        [Fact]
        public void Constructor_Default_CaminhoRelatorioIsEmpty()
        {
            var config = new PlanoMontagemConfig();

            config.CaminhoRelatorio.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_Default_EscopoIsVistaAtiva()
        {
            var config = new PlanoMontagemConfig();

            config.Escopo.Should().Be(EscopoMontagem.VistaAtiva);
        }

        [Fact]
        public void Properties_CanBeModified()
        {
            var config = new PlanoMontagemConfig
            {
                NomeParametroEtapa = "Custom_Param",
                AplicarDestaqueVisual = false,
                ExportarRelatorio = true,
                CaminhoRelatorio = @"C:\temp\report.xlsx",
                Escopo = EscopoMontagem.ModeloInteiro
            };

            config.NomeParametroEtapa.Should().Be("Custom_Param");
            config.AplicarDestaqueVisual.Should().BeFalse();
            config.ExportarRelatorio.Should().BeTrue();
            config.CaminhoRelatorio.Should().Be(@"C:\temp\report.xlsx");
            config.Escopo.Should().Be(EscopoMontagem.ModeloInteiro);
        }
    }
}
