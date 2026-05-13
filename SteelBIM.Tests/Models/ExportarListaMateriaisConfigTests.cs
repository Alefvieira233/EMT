using Xunit;
using FluentAssertions;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    public class ExportarListaMateriaisConfigTests
    {
        [Fact]
        public void Constructor_Default_EscopoIsVistaAtiva()
        {
            var config = new ExportarListaMateriaisConfig();

            config.Escopo.Should().Be(ListaMateriaisEscopo.VistaAtiva);
        }

        [Fact]
        public void Constructor_Default_AllCategoriasTrue()
        {
            var config = new ExportarListaMateriaisConfig();

            config.IncluirVigas.Should().BeTrue();
            config.IncluirPilares.Should().BeTrue();
            config.IncluirFundacoes.Should().BeTrue();
            config.IncluirContraventamentos.Should().BeTrue();
            config.IncluirChapasConexoes.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_AllAbasTrue()
        {
            var config = new ExportarListaMateriaisConfig();

            config.ExportarPerfisLineares.Should().BeTrue();
            config.ExportarChapas.Should().BeTrue();
            config.ExportarResumo.Should().BeTrue();
        }

        [Fact]
        public void TemCategoriaSelecionada_AllTrue_ReturnsTrue()
        {
            var config = new ExportarListaMateriaisConfig();

            config.TemCategoriaSelecionada().Should().BeTrue();
        }

        [Fact]
        public void TemCategoriaSelecionada_AllFalse_ReturnsFalse()
        {
            var config = new ExportarListaMateriaisConfig
            {
                IncluirVigas = false,
                IncluirPilares = false,
                IncluirFundacoes = false,
                IncluirContraventamentos = false,
                IncluirChapasConexoes = false
            };

            config.TemCategoriaSelecionada().Should().BeFalse();
        }

        [Fact]
        public void TemAbaSelecionada_AllTrue_ReturnsTrue()
        {
            var config = new ExportarListaMateriaisConfig();

            config.TemAbaSelecionada().Should().BeTrue();
        }

        [Fact]
        public void TemAbaSelecionada_AllFalse_ReturnsFalse()
        {
            var config = new ExportarListaMateriaisConfig
            {
                ExportarPerfisLineares = false,
                ExportarChapas = false,
                ExportarResumo = false
            };

            config.TemAbaSelecionada().Should().BeFalse();
        }

        [Fact]
        public void SugerirNomeArquivo_ContainsListaMateriais()
        {
            var config = new ExportarListaMateriaisConfig();

            config.SugerirNomeArquivo().Should().StartWith("ListaMateriais_");
            config.SugerirNomeArquivo().Should().EndWith(".xlsx");
        }
    }
}
