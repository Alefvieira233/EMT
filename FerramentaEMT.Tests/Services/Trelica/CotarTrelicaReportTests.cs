#nullable enable
using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Services.Trelica;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class CotarTrelicaReportTests
    {
        [Fact]
        public void Resumo_SemAvisos_ContainsContagens()
        {
            // Arrange
            var report = new CotarTrelicaReport(
                CotasCriadas: 10,
                TagsCriadas: 8,
                TextosCriados: 2,
                WarningsCount: 0,
                Warnings: Array.Empty<string>(),
                TempoMs: 1234);

            // Act
            string resumo = report.Resumo;

            // Assert
            Assert.Contains("10", resumo);
            Assert.Contains("8", resumo);
            Assert.Contains("2", resumo);
            Assert.Contains("1234", resumo);
            Assert.DoesNotContain("Avisos", resumo);
        }

        [Fact]
        public void Resumo_ComUmAviso_ExibeTituloEAviso()
        {
            // Arrange
            var warnings = new List<string> { "Topologia desconhecida" };
            var report = new CotarTrelicaReport(
                CotasCriadas: 5,
                TagsCriadas: 5,
                TextosCriados: 1,
                WarningsCount: 1,
                Warnings: warnings,
                TempoMs: 500);

            // Act
            string resumo = report.Resumo;

            // Assert
            Assert.Contains("Avisos (1)", resumo);
            Assert.Contains("Topologia desconhecida", resumo);
        }

        [Fact]
        public void Resumo_ComMultiplosAvisos_ExibeTodasAsMensagens()
        {
            // Arrange
            var warnings = new List<string>
            {
                "Primeira falha",
                "Segunda falha",
                "Terceira falha"
            };
            var report = new CotarTrelicaReport(
                CotasCriadas: 0,
                TagsCriadas: 0,
                TextosCriados: 0,
                WarningsCount: 3,
                Warnings: warnings,
                TempoMs: 100);

            // Act
            string resumo = report.Resumo;

            // Assert
            Assert.Contains("Avisos (3)", resumo);
            Assert.Contains("Primeira falha", resumo);
            Assert.Contains("Segunda falha", resumo);
            Assert.Contains("Terceira falha", resumo);
        }

        [Fact]
        public void Resumo_ComZeroElementos_ExibeZeros()
        {
            // Arrange
            var report = new CotarTrelicaReport(
                CotasCriadas: 0,
                TagsCriadas: 0,
                TextosCriados: 0,
                WarningsCount: 0,
                Warnings: Array.Empty<string>(),
                TempoMs: 50);

            // Act
            string resumo = report.Resumo;

            // Assert
            Assert.Contains("Cotas criadas:        0", resumo);
            Assert.Contains("Tags de perfil:       0", resumo);
            Assert.Contains("Textos de banzo:      0", resumo);
            Assert.Contains("50", resumo);
        }

        [Fact]
        public void CotarTrelicaReport_IsRecord_Immutable()
        {
            // Arrange & Act
            var r1 = new CotarTrelicaReport(1, 1, 1, 0, Array.Empty<string>(), 100);
            var r2 = new CotarTrelicaReport(1, 1, 1, 0, Array.Empty<string>(), 100);
            var r3 = new CotarTrelicaReport(2, 1, 1, 0, Array.Empty<string>(), 100);

            // Assert
            Assert.Equal(r1, r2);
            Assert.NotEqual(r1, r3);
        }

        [Fact]
        public void Report_AllCountersZero_ResumoIndicaSemElementos()
        {
            // Arrange
            var report = new CotarTrelicaReport(0, 0, 0, 0, new List<string>(), 100);

            // Act
            string resumo = report.Resumo;

            // Assert
            resumo.Should().Contain("0");
            report.CotasCriadas.Should().Be(0);
            report.TagsCriadas.Should().Be(0);
            report.TextosCriados.Should().Be(0);
        }

        [Fact]
        public void Report_WithTextosCreated_ResumoIncludesTextos()
        {
            // Arrange
            var report = new CotarTrelicaReport(5, 3, 2, 0, new List<string>(), 250);

            // Act
            string resumo = report.Resumo;

            // Assert
            report.CotasCriadas.Should().Be(5);
            report.TagsCriadas.Should().Be(3);
            report.TextosCriados.Should().Be(2);
            report.TempoMs.Should().Be(250);
            resumo.Should().Contain("5");
            resumo.Should().Contain("3");
            resumo.Should().Contain("2");
            resumo.Should().Contain("250");
        }

        [Fact]
        public void Report_HighWarningCount_ResumoMencionaAvisos()
        {
            // Arrange
            var warnings = new List<string> { "aviso 1", "aviso 2", "aviso 3" };
            var report = new CotarTrelicaReport(10, 5, 2, 3, warnings, 500);

            // Act
            string resumo = report.Resumo;

            // Assert
            report.WarningsCount.Should().Be(3);
            report.Warnings.Should().HaveCount(3);
            resumo.Should().Contain("Avisos (3)");
            resumo.Should().Contain("aviso 1");
            resumo.Should().Contain("aviso 2");
            resumo.Should().Contain("aviso 3");
        }
    }
}
