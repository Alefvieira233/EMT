#nullable enable
using System;
using FerramentaEMT.Services.Trelica;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class TrelicaTopologiaTests
    {
        [Fact]
        public void Detectar_Plana()
        {
            var nos = new[] { (0.0, 10.0), (5.0, 10.0), (10.0, 10.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.Plana);
        }

        [Fact]
        public void Detectar_DuasAguas_PicoCentral()
        {
            var nos = new[] { (0.0, 5.0), (5.0, 10.0), (10.0, 5.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.DuasAguas);
        }

        [Fact]
        public void Detectar_DuasAguas_PicoAssimetrico()
        {
            var nos = new[] { (0.0, 5.0), (3.0, 8.0), (7.0, 10.0), (12.0, 6.0), (15.0, 5.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.DuasAguas);
        }

        [Fact]
        public void Detectar_Shed_Monotonico()
        {
            var nos = new[] { (0.0, 5.0), (5.0, 7.5), (10.0, 10.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.Shed);
        }

        [Fact]
        public void Detectar_MenosDeDoisNos_Desconhecida()
        {
            TrelicaTopologia.Detectar(Array.Empty<(double, double)>())
                .Should().Be(TrelicaTopologia.Topologia.Desconhecida);
        }

        [Fact]
        public void Detectar_DuasAguas_ComRuidoNoPico_AindaDuasAguas()
        {
            // pico com pequena oscilacao dentro da tolerancia de 1% do vao
            var nos = new[] { (0.0, 5.0), (5.0, 10.0), (7.5, 9.99), (10.0, 5.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.DuasAguas);
        }

        [Fact]
        public void Detectar_DoisNosIdenticos_Desconhecida()
        {
            // vao = 0 -> nao tem como classificar
            var nos = new[] { (5.0, 10.0), (5.0, 10.0) };
            TrelicaTopologia.Detectar(nos).Should().Be(TrelicaTopologia.Topologia.Desconhecida);
        }
    }
}
