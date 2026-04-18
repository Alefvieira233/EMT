using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.Montagem;

namespace FerramentaEMT.Tests.Services.Montagem
{
    public class PlanoMontagemReportTests
    {
        [Fact]
        public void Constructor_Default_InitializesWithDefaults()
        {
            // Act
            var report = new PlanoMontagemReport();

            // Assert
            report.TotalElementos.Should().Be(0);
            report.TotalEtapas.Should().Be(0);
            report.Etapas.Should().NotBeNull();
            report.Etapas.Should().BeEmpty();
            report.Duracao.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void Constructor_WithParameters_InitializesCorrectly()
        {
            // Arrange
            var duracao = TimeSpan.FromSeconds(5);

            // Act
            var report = new PlanoMontagemReport(100, 5, duracao);

            // Assert
            report.TotalElementos.Should().Be(100);
            report.TotalEtapas.Should().Be(5);
            report.Duracao.Should().Be(duracao);
            report.Etapas.Should().BeEmpty();
        }

        [Fact]
        public void Etapas_CanAddMultipleEtapas()
        {
            // Arrange
            var report = new PlanoMontagemReport();
            var etapa1 = new EtapaMontagem(1, "Fase 1");
            var etapa2 = new EtapaMontagem(2, "Fase 2");

            // Act
            report.Etapas.Add(etapa1);
            report.Etapas.Add(etapa2);

            // Assert
            report.Etapas.Should().HaveCount(2);
            report.Etapas[0].Numero.Should().Be(1);
            report.Etapas[1].Numero.Should().Be(2);
        }

        [Fact]
        public void Report_WithPopulatedEtapas_CountsElementsCorrectly()
        {
            // Arrange
            var report = new PlanoMontagemReport();
            var etapa1 = new EtapaMontagem(1);
            etapa1.ElementIds.AddRange(new[] { 1L, 2L, 3L });
            var etapa2 = new EtapaMontagem(2);
            etapa2.ElementIds.AddRange(new[] { 4L, 5L });

            // Act
            report.Etapas.Add(etapa1);
            report.Etapas.Add(etapa2);
            report.TotalElementos = etapa1.ElementIds.Count + etapa2.ElementIds.Count;
            report.TotalEtapas = 2;

            // Assert
            report.TotalElementos.Should().Be(5);
            report.TotalEtapas.Should().Be(2);
            report.Etapas.Should().HaveCount(2);
        }

        [Fact]
        public void Report_EmptyReport_HasZeroTotals()
        {
            // Arrange & Act
            var report = new PlanoMontagemReport();

            // Assert
            report.TotalElementos.Should().Be(0);
            report.TotalEtapas.Should().Be(0);
            report.Etapas.Count.Should().Be(0);
        }
    }
}
