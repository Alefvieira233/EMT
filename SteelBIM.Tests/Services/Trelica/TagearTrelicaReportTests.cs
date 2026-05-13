#nullable enable
using System.Collections.Generic;
using Xunit;
using FerramentaEMT.Services.Trelica;

namespace FerramentaEMT.Tests.Services.Trelica
{
    /// <summary>
    /// Testes unitarios do relatorio de tagging de trelica.
    /// Verifica formatacao do resumo e consistencia de contadores.
    /// </summary>
    public class TagearTrelicaReportTests
    {
        [Fact]
        public void ObterResumo_SemErros_FormatacaoPadrao()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 25,
                TotalTagsCriadas = 25,
                TotalRotulosBanzos = 0,
                BarrasComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("25", resumo);
            Assert.DoesNotContain("rótulos", resumo.ToLowerInvariant());
            Assert.DoesNotContain("erro", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_ComRotulos_MencionalizeRotulos()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 25,
                TotalTagsCriadas = 25,
                TotalRotulosBanzos = 2,
                BarrasComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("25", resumo);
            Assert.Contains("2", resumo);
            Assert.Contains("rótulo", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_ComErros_MencionalizeErrors()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 30,
                TotalTagsCriadas = 28,
                TotalRotulosBanzos = 2,
                BarrasComErro = 2,
                Erros = new List<string> { "Erro 1", "Erro 2" }
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("30", resumo);
            Assert.Contains("28", resumo);
            Assert.Contains("2", resumo);
            Assert.Contains("erro", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_TodosZeros_FormatacaoValida()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 0,
                TotalTagsCriadas = 0,
                TotalRotulosBanzos = 0,
                BarrasComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("0", resumo);
        }

        [Fact]
        public void ObterResumo_NumerosGrandes_FormatacaoCorreta()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 150,
                TotalTagsCriadas = 145,
                TotalRotulosBanzos = 5,
                BarrasComErro = 5,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("150", resumo);
            Assert.Contains("145", resumo);
            Assert.Contains("5", resumo);
        }

        [Fact]
        public void ObterResumo_TrelicaCompleta_TodosOsDados()
        {
            // Arrange
            var relatorio = new TagearTrelicaService.TagearTrelicaReport
            {
                TotalBarrasProcessadas = 50,
                TotalTagsCriadas = 48,
                TotalRotulosBanzos = 2,
                BarrasComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("50", resumo); // Total processadas
            Assert.Contains("48", resumo); // Tags criadas
            Assert.Contains("2", resumo);  // Rotulos
        }
    }
}
