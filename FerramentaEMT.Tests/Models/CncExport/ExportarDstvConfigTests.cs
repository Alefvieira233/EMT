using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.CncExport;

namespace FerramentaEMT.Tests.Models.CncExport
{
    public class ExportarDstvConfigTests
    {
        [Fact]
        public void Constructor_Default_EscopoIsSelecaoManual()
        {
            var config = new ExportarDstvConfig();

            config.Escopo.Should().Be(EscopoExportacaoDstv.SelecaoManual);
        }

        [Fact]
        public void Constructor_Default_AgrupamentoIsUmPorMarca()
        {
            var config = new ExportarDstvConfig();

            config.Agrupamento.Should().Be(AgrupamentoArquivosDstv.UmPorMarca);
        }

        [Fact]
        public void Constructor_Default_AllCategoriasTrue()
        {
            var config = new ExportarDstvConfig();

            config.ExportarVigas.Should().BeTrue();
            config.ExportarPilares.Should().BeTrue();
            config.ExportarContraventamentos.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_GerarRelatorioIsTrue()
        {
            var config = new ExportarDstvConfig();

            config.GerarRelatorio.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_AbrirPastaIsTrue()
        {
            var config = new ExportarDstvConfig();

            config.AbrirPastaAposExportar.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_SobrescreverExistentesIsTrue()
        {
            var config = new ExportarDstvConfig();

            config.SobrescreverExistentes.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_FaseIs1()
        {
            var config = new ExportarDstvConfig();

            config.Fase.Should().Be("1");
        }

        [Fact]
        public void Constructor_Default_StringsAreEmpty()
        {
            var config = new ExportarDstvConfig();

            config.PastaDestino.Should().BeEmpty();
            config.CodigoProjeto.Should().BeEmpty();
            config.TratamentoSuperficiePadrao.Should().BeEmpty();
            config.NomeParametroMarca.Should().BeEmpty();
        }
    }
}
