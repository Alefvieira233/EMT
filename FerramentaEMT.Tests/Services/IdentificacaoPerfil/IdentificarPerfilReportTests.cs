#nullable enable
using System.Collections.Generic;
using Xunit;
using FerramentaEMT.Services.IdentificacaoPerfil;

namespace FerramentaEMT.Tests.Services.IdentificacaoPerfil
{
    /// <summary>
    /// Testes unitarios do relatorio de identificacao de perfil.
    /// Verifica formatacao do resumo e consistencia de contadores.
    /// </summary>
    public class IdentificarPerfilReportTests
    {
        [Fact]
        public void ObterResumo_SemErros_FormatacaoPadrao()
        {
            // Arrange
            var relatorio = new IdentificarPerfilService.IdentificarPerfilReport
            {
                TotalElementosSelecionados = 10,
                TotalTagsCriadas = 10,
                ElementosPuladosTagExistente = 0,
                ElementosComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("10", resumo);
            Assert.DoesNotContain("pulados", resumo.ToLowerInvariant());
            Assert.DoesNotContain("erro", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_ComTagsPuladas_MencionalizesPulados()
        {
            // Arrange
            var relatorio = new IdentificarPerfilService.IdentificarPerfilReport
            {
                TotalElementosSelecionados = 15,
                TotalTagsCriadas = 10,
                ElementosPuladosTagExistente = 5,
                ElementosComErro = 0,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("15", resumo);
            Assert.Contains("10", resumo);
            Assert.Contains("5", resumo);
            Assert.Contains("pulados", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_ComErros_MencionalizeErrors()
        {
            // Arrange
            var relatorio = new IdentificarPerfilService.IdentificarPerfilReport
            {
                TotalElementosSelecionados = 10,
                TotalTagsCriadas = 8,
                ElementosPuladosTagExistente = 0,
                ElementosComErro = 2,
                Erros = new List<string> { "Erro 1", "Erro 2" }
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("2", resumo);
            Assert.Contains("erro", resumo.ToLowerInvariant());
        }

        [Fact]
        public void ObterResumo_TodosZeros_FormatacaoValida()
        {
            // Arrange
            var relatorio = new IdentificarPerfilService.IdentificarPerfilReport
            {
                TotalElementosSelecionados = 0,
                TotalTagsCriadas = 0,
                ElementosPuladosTagExistente = 0,
                ElementosComErro = 0,
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
            var relatorio = new IdentificarPerfilService.IdentificarPerfilReport
            {
                TotalElementosSelecionados = 1000,
                TotalTagsCriadas = 950,
                ElementosPuladosTagExistente = 30,
                ElementosComErro = 20,
                Erros = new()
            };

            // Act
            string resumo = relatorio.ObterResumo();

            // Assert
            Assert.NotEmpty(resumo);
            Assert.Contains("1000", resumo);
            Assert.Contains("950", resumo);
            Assert.Contains("30", resumo);
            Assert.Contains("20", resumo);
        }
    }
}
