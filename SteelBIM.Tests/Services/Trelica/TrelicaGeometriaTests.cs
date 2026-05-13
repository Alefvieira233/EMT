#nullable enable
using System;
using FerramentaEMT.Services.Trelica;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class TrelicaGeometriaTests
    {
        [Fact]
        public void LarguraDosPaineis_TresNos_RetornaDoisPaineis()
        {
            var r = TrelicaGeometria.LarguraDosPaineis(new[] { 0.0, 5.0, 12.0 });
            r.Should().Equal(new[] { 5.0, 7.0 });
        }

        [Fact]
        public void LarguraDosPaineis_Desordenado_ReordenaECalcula()
        {
            var r = TrelicaGeometria.LarguraDosPaineis(new[] { 5.0, 0.0, 12.0 });
            r.Should().Equal(new[] { 5.0, 7.0 });
        }

        [Fact]
        public void LarguraDosPaineis_RemoveDuplicados()
        {
            var r = TrelicaGeometria.LarguraDosPaineis(new[] { 0.0, 5.0, 5.0005, 12.0 });
            r.Should().HaveCount(2);
        }

        [Fact]
        public void LarguraDosPaineis_VazioOuUm_RetornaVazio()
        {
            TrelicaGeometria.LarguraDosPaineis(Array.Empty<double>()).Should().BeEmpty();
            TrelicaGeometria.LarguraDosPaineis(new[] { 1.0 }).Should().BeEmpty();
        }

        [Fact]
        public void VaoTotal_RetornaDiferenca()
        {
            TrelicaGeometria.VaoTotal(new[] { 0.0, 5.0, 12.0 }).Should().Be(12.0);
        }

        [Fact]
        public void AlturasPorEstacao_CalculaCorretamente()
        {
            // banzo superior constante Z=5, banzo inferior constante Z=0 -> altura 5 em todos
            var alturas = TrelicaGeometria.AlturasPorEstacao(
                new[] { 0.0, 1.0, 2.0 },
                x => 5.0,
                x => 0.0);
            alturas.Should().Equal(new[] { 5.0, 5.0, 5.0 });
        }

        [Fact]
        public void AlturasPorEstacao_InferiorAcimaDoSuperior_RetornaZero()
        {
            // banzo inferior acima do superior (modelo invertido) -> Max(0, -) = 0
            var alturas = TrelicaGeometria.AlturasPorEstacao(
                new[] { 0.0 },
                x => 0.0,
                x => 3.0);
            alturas.Should().Equal(new[] { 0.0 });
        }

        [Fact]
        public void AlturasPorEstacao_NuncaRetornaNegativa()
        {
            // Contrato: altura e sempre >= 0 (cota vertical entre banzos)
            var alturas = TrelicaGeometria.AlturasPorEstacao(
                new[] { 0.0, 5.0, 10.0 },
                x => 2.0,   // superior
                x => 5.0);  // inferior acima -> -3
            alturas.Should().OnlyContain(h => h >= 0.0);
        }

        [Fact]
        public void ExtremosApoio_RetornaMinEMax()
        {
            var (e, d) = TrelicaGeometria.ExtremosApoio(new[] { 2.0, 0.0, 10.0, 7.0 });
            e.Should().Be(0.0);
            d.Should().Be(10.0);
        }

        [Fact]
        public void ExtremosApoio_MenosDeDoisPontos_Lanca()
        {
            var act = () => TrelicaGeometria.ExtremosApoio(new[] { 1.0 });
            act.Should().Throw<ArgumentException>();
        }
    }
}
