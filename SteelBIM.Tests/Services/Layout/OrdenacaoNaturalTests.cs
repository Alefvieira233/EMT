using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SteelBIM.Services.Layout;
using Xunit;

namespace SteelBIM.Tests.Services.Layout
{
    /// <summary>v2.8.13: ordenacao natural (TR-2 antes de TR-10) usada ao pranchar/ordenar vistas.</summary>
    public class OrdenacaoNaturalTests
    {
        [Fact]
        public void TR2_AntesDe_TR10()
        {
            OrdenacaoNatural.Comparar("TR-2", "TR-10").Should().BeLessThan(0);
            OrdenacaoNatural.Comparar("TR-10", "TR-2").Should().BeGreaterThan(0);
        }

        [Fact]
        public void Numerico_PorValor_NaoPorTexto()
        {
            OrdenacaoNatural.Comparar("A9", "A10").Should().BeLessThan(0);
            OrdenacaoNatural.Comparar("Corte 100", "Corte 20").Should().BeGreaterThan(0);
        }

        [Fact]
        public void CaseInsensitive()
        {
            OrdenacaoNatural.Comparar("tr-2", "TR-2").Should().Be(0);
        }

        [Fact]
        public void Iguais_RetornaZero()
        {
            OrdenacaoNatural.Comparar("Planta", "Planta").Should().Be(0);
        }

        [Fact]
        public void Sort_OrdemHumana()
        {
            var lista = new List<string> { "TR-10", "TR-1", "TR-2", "TR-20", "TR-3" };
            lista.Sort(OrdenacaoNatural.Comparer);
            lista.Should().Equal("TR-1", "TR-2", "TR-3", "TR-10", "TR-20");
        }

        [Fact]
        public void ZerosAEsquerda_Equivalentes()
        {
            OrdenacaoNatural.Comparar("V-007", "V-7").Should().Be(0);
        }
    }
}
