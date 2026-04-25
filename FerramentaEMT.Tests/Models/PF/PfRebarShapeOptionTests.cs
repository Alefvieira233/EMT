using FerramentaEMT.Models.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Models.PF
{
    /// <summary>
    /// Testes da DTO <see cref="PfRebarShapeOption"/> usada pelo catalogo de RebarShape
    /// do projeto Revit (aba de estribos). Garantem estabilidade dos defaults —
    /// se mudarem, a UI que mostra "Automatico" no topo da lista quebra.
    /// </summary>
    public class PfRebarShapeOptionTests
    {
        [Fact]
        public void Defaults_SaoSeguros_ParaVinculacaoUI()
        {
            var option = new PfRebarShapeOption();

            option.ElementIdValue.Should().Be(0);
            option.Name.Should().Be(string.Empty);
            option.DisplayName.Should().Be(string.Empty);
            option.IsAutomatic.Should().BeFalse();
        }

        [Fact]
        public void Automatico_PodeSerConstruidoComFlag()
        {
            var option = new PfRebarShapeOption
            {
                Name = string.Empty,
                DisplayName = "Automatico",
                IsAutomatic = true
            };

            option.IsAutomatic.Should().BeTrue();
            option.DisplayName.Should().Be("Automatico");
        }

        [Fact]
        public void ShapeDoProjeto_PreencheTodosCampos()
        {
            var option = new PfRebarShapeOption
            {
                ElementIdValue = 123456,
                Name = "M-01_EstriboFechado",
                DisplayName = "M-01 EstriboFechado"
            };

            option.ElementIdValue.Should().Be(123456);
            option.IsAutomatic.Should().BeFalse();
            option.Name.Should().Contain("EstriboFechado");
        }

        [Fact]
        public void ToString_DisplayNamePreferidoSobreName()
        {
            var option = new PfRebarShapeOption
            {
                Name = "M-01_EstriboFechado",
                DisplayName = "M-01 EstriboFechado"
            };

            option.ToString().Should().Be("M-01 EstriboFechado");
        }

        [Fact]
        public void ToString_FallbackParaName_QuandoDisplayNameEhVazio()
        {
            var option = new PfRebarShapeOption
            {
                Name = "M-01_EstriboFechado",
                DisplayName = string.Empty
            };

            option.ToString().Should().Be("M-01_EstriboFechado");
        }
    }
}
