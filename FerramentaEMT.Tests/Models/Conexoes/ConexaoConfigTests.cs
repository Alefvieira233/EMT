using System;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.Conexoes;

namespace FerramentaEMT.Tests.Models.Conexoes
{
    public class ConexaoConfigTests
    {
        [Fact]
        public void Constructor_Default_InitializesWithDefaults()
        {
            // Act
            var config = new ConexaoConfig();

            // Assert
            config.Tipo.Should().Be(TipoConexao.ChapaDePonta);
            config.GerarFurosDstv.Should().BeTrue();
            config.ChapaPonta.Should().NotBeNull();
            config.Cantoneira.Should().NotBeNull();
            config.Gusset.Should().NotBeNull();
        }

        [Fact]
        public void ChapaPontaDefaults_AreInitialized()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act & Assert
            config.ChapaPonta.EspessuraMm.Should().Be(12.7);
            config.ChapaPonta.LarguraMm.Should().Be(150);
            config.ChapaPonta.AlturaMm.Should().Be(250);
            config.ChapaPonta.NumParafusos.Should().Be(4);
            config.ChapaPonta.DiamParafusoMm.Should().Be(19.05);
            config.ChapaPonta.EspacamentoXMm.Should().Be(75);
            config.ChapaPonta.EspacamentoYMm.Should().Be(75);
        }

        [Fact]
        public void CantonadeiraDefaults_AreInitialized()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act & Assert
            config.Cantoneira.EspessuraMm.Should().Be(12.7);
            config.Cantoneira.LarguraMm.Should().Be(100);
            config.Cantoneira.AlturaMm.Should().Be(200);
            config.Cantoneira.NumParafusosPorCantoneira.Should().Be(3);
            config.Cantoneira.DiamParafusoMm.Should().Be(19.05);
            config.Cantoneira.EspacamentoFurosMm.Should().Be(65);
            config.Cantoneira.DistanciaABordaMm.Should().Be(35);
        }

        [Fact]
        public void GussetDefaults_AreInitialized()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act & Assert
            config.Gusset.EspessuraMm.Should().Be(9.53);
            config.Gusset.LarguraMm.Should().Be(300);
            config.Gusset.AlturaMm.Should().Be(300);
            config.Gusset.AnguloDiagonalDeg.Should().Be(45.0);
            config.Gusset.NumParafusos.Should().Be(6);
            config.Gusset.DiamParafusoMm.Should().Be(19.05);
        }

        [Fact]
        public void Config_CanChangeType()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act
            config.Tipo = TipoConexao.DuplaCantoneira;

            // Assert
            config.Tipo.Should().Be(TipoConexao.DuplaCantoneira);
        }

        [Fact]
        public void Config_CanModifyChapaPontaParameters()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act
            config.ChapaPonta.EspessuraMm = 15.0;
            config.ChapaPonta.NumParafusos = 8;

            // Assert
            config.ChapaPonta.EspessuraMm.Should().Be(15.0);
            config.ChapaPonta.NumParafusos.Should().Be(8);
        }

        [Fact]
        public void Config_GerarFurosDstv_DefaultIsTrue()
        {
            // Arrange & Act
            var config = new ConexaoConfig();

            // Assert
            config.GerarFurosDstv.Should().BeTrue();
        }

        [Fact]
        public void Config_GerarFurosDstv_CanBeModified()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act
            config.GerarFurosDstv = false;

            // Assert
            config.GerarFurosDstv.Should().BeFalse();
        }
    }
}
