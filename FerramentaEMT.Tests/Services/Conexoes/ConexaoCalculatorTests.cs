using System;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.Conexoes;
using FerramentaEMT.Services.Conexoes;

namespace FerramentaEMT.Tests.Services.Conexoes
{
    public class ConexaoCalculatorTests
    {
        [Fact]
        public void ContarParafusosTotal_ChapaPonta_CalculatesCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.ChapaDePonta,
                ChapaPonta = new ConfiguracaoChapaPonta { NumParafusos = 4 }
            };

            // Act
            int total = ConexaoCalculator.ContarParafusosTotal(config, 3);

            // Assert
            total.Should().Be(12); // 4 parafusos * 3 conexões
        }

        [Fact]
        public void ContarParafusosTotal_DuplaCantoneira_CalculatesCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.DuplaCantoneira,
                Cantoneira = new ConfiguracaoCantoneira { NumParafusosPorCantoneira = 3 }
            };

            // Act
            int total = ConexaoCalculator.ContarParafusosTotal(config, 2);

            // Assert
            total.Should().Be(12); // (3 * 2 cantoneiras) * 2 conexões
        }

        [Fact]
        public void ContarParafusosTotal_ChapaGusset_CalculatesCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.ChapaGusset,
                Gusset = new ConfiguracaoGusset { NumParafusos = 6 }
            };

            // Act
            int total = ConexaoCalculator.ContarParafusosTotal(config, 5);

            // Assert
            total.Should().Be(30); // 6 parafusos * 5 conexões
        }

        [Fact]
        public void ContarParafusosTotal_ZeroConexoes_ReturnsZero()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act
            int total = ConexaoCalculator.ContarParafusosTotal(config, 0);

            // Assert
            total.Should().Be(0);
        }

        [Fact]
        public void ContarParafusosTotal_NegativeConexoes_ReturnsZero()
        {
            // Arrange
            var config = new ConexaoConfig();

            // Act
            int total = ConexaoCalculator.ContarParafusosTotal(config, -1);

            // Assert
            total.Should().Be(0);
        }

        [Fact]
        public void ContarParafusosTotal_NullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                ConexaoCalculator.ContarParafusosTotal(null!, 5));
            ex.ParamName.Should().Be("config");
        }

        [Fact]
        public void GerarMarcadorConexao_ChapaDePonta_FormatsCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.ChapaDePonta,
                ChapaPonta = new ConfiguracaoChapaPonta
                {
                    EspessuraMm = 12.7,
                    LarguraMm = 150,
                    AlturaMm = 250,
                    NumParafusos = 4,
                    DiamParafusoMm = 19.05
                }
            };

            // Act
            string marcador = ConexaoCalculator.GerarMarcadorConexao(config);

            // Assert
            marcador.Should().StartWith("CP-");
            marcador.Should().Contain("12.7");
            marcador.Should().Contain("150");
            marcador.Should().Contain("250");
            marcador.Should().Contain("4x");
            marcador.Should().Contain("M19");
        }

        [Fact]
        public void GerarMarcadorConexao_DuplaCantoneira_FormatsCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.DuplaCantoneira,
                Cantoneira = new ConfiguracaoCantoneira
                {
                    LarguraMm = 100,
                    EspessuraMm = 12.7,
                    NumParafusosPorCantoneira = 3,
                    DiamParafusoMm = 19.05
                }
            };

            // Act
            string marcador = ConexaoCalculator.GerarMarcadorConexao(config);

            // Assert
            marcador.Should().StartWith("DC-");
            marcador.Should().Contain("100");
            marcador.Should().Contain("12.7");
            marcador.Should().Contain("3x");
        }

        [Fact]
        public void GerarMarcadorConexao_ChapaGusset_FormatsCorrectly()
        {
            // Arrange
            var config = new ConexaoConfig
            {
                Tipo = TipoConexao.ChapaGusset,
                Gusset = new ConfiguracaoGusset
                {
                    LarguraMm = 300,
                    AlturaMm = 300,
                    AnguloDiagonalDeg = 45.0,
                    NumParafusos = 6,
                    DiamParafusoMm = 19.05
                }
            };

            // Act
            string marcador = ConexaoCalculator.GerarMarcadorConexao(config);

            // Assert
            marcador.Should().StartWith("GS-");
            marcador.Should().Contain("300");
            marcador.Should().Contain("45");
            marcador.Should().Contain("6x");
        }

        [Fact]
        public void GerarMarcadorConexao_NullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                ConexaoCalculator.GerarMarcadorConexao(null!));
            ex.ParamName.Should().Be("config");
        }

        [Fact]
        public void GerarMarcadorConexao_AllTypes_ProduceUniqueMarkers()
        {
            // Arrange
            var configCP = new ConexaoConfig { Tipo = TipoConexao.ChapaDePonta };
            var configDC = new ConexaoConfig { Tipo = TipoConexao.DuplaCantoneira };
            var configGS = new ConexaoConfig { Tipo = TipoConexao.ChapaGusset };

            // Act
            string marcadorCP = ConexaoCalculator.GerarMarcadorConexao(configCP);
            string marcadorDC = ConexaoCalculator.GerarMarcadorConexao(configDC);
            string marcadorGS = ConexaoCalculator.GerarMarcadorConexao(configGS);

            // Assert
            marcadorCP.Should().NotBe(marcadorDC);
            marcadorCP.Should().NotBe(marcadorGS);
            marcadorDC.Should().NotBe(marcadorGS);
        }
    }
}
