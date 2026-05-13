using System;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.Montagem;

namespace FerramentaEMT.Tests.Models.Montagem
{
    public class EtapaMontagemTests
    {
        [Fact]
        public void Constructor_Default_InitializesWithDefaults()
        {
            // Act
            var etapa = new EtapaMontagem();

            // Assert
            etapa.Numero.Should().Be(0);
            etapa.Descricao.Should().BeEmpty();
            etapa.DataPlanejada.Should().BeNull();
            etapa.ElementIds.Should().NotBeNull();
            etapa.ElementIds.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_WithNumberAndDescription_InitializesCorrectly()
        {
            // Act
            var etapa = new EtapaMontagem(1, "Fundações");

            // Assert
            etapa.Numero.Should().Be(1);
            etapa.Descricao.Should().Be("Fundações");
            etapa.DataPlanejada.Should().BeNull();
            etapa.ElementIds.Should().BeEmpty();
        }

        [Fact]
        public void ElementIds_CanAddIds()
        {
            // Arrange
            var etapa = new EtapaMontagem(1);

            // Act
            etapa.ElementIds.Add(100L);
            etapa.ElementIds.Add(200L);

            // Assert
            etapa.ElementIds.Should().HaveCount(2);
            etapa.ElementIds.Should().Contain(new[] { 100L, 200L });
        }

        [Fact]
        public void DataPlanejada_CanBeSet()
        {
            // Arrange
            var etapa = new EtapaMontagem();
            var data = new DateTime(2026, 4, 15);

            // Act
            etapa.DataPlanejada = data;

            // Assert
            etapa.DataPlanejada.Should().Be(data);
        }

        [Fact]
        public void Constructor_WithNullDescription_UsesEmptyString()
        {
            // Act
            var etapa = new EtapaMontagem(2, null);

            // Assert
            etapa.Numero.Should().Be(2);
            etapa.Descricao.Should().Be(string.Empty);
        }
    }
}
