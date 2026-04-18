#nullable enable
using System;
using FerramentaEMT.Services.Trelica;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.Trelica
{
    public class TrelicaClassificadorTests
    {
        [Theory]
        [InlineData(0.0)]            // horizontal perfeito
        [InlineData(0.17)]           // 10 graus
        [InlineData(0.26)]           // 14.9 graus (limite)
        public void Classificar_BanzoHorizontal_RetornaIndefinido(double incRad)
        {
            var t = TrelicaClassificador.ClassificarPorInclinacao(incRad);
            t.Should().Be(TrelicaClassificador.TipoMembro.BanzoIndefinido);
        }

        [Theory]
        [InlineData(Math.PI / 2.0)]  // 90 graus
        [InlineData(1.40)]           // 80 graus
        [InlineData(1.31)]           // 75 graus (limite)
        public void Classificar_Montante(double incRad)
        {
            var t = TrelicaClassificador.ClassificarPorInclinacao(incRad);
            t.Should().Be(TrelicaClassificador.TipoMembro.Montante);
        }

        [Theory]
        [InlineData(0.5)]            // 28 graus
        [InlineData(0.8)]            // 45 graus
        [InlineData(1.0)]            // 57 graus
        public void Classificar_Diagonal(double incRad)
        {
            var t = TrelicaClassificador.ClassificarPorInclinacao(incRad);
            t.Should().Be(TrelicaClassificador.TipoMembro.Diagonal);
        }

        [Fact]
        public void Classificar_NaN_Indefinido()
        {
            TrelicaClassificador.ClassificarPorInclinacao(double.NaN)
                .Should().Be(TrelicaClassificador.TipoMembro.Indefinido);
        }

        [Fact]
        public void ClassificarBanzoPorAltura_Superior()
        {
            TrelicaClassificador.ClassificarBanzoPorAltura(zMedioBarra: 10.0, zMedioTrelica: 5.0)
                .Should().Be(TrelicaClassificador.TipoMembro.BanzoSuperior);
        }

        [Fact]
        public void ClassificarBanzoPorAltura_Inferior()
        {
            TrelicaClassificador.ClassificarBanzoPorAltura(zMedioBarra: 2.0, zMedioTrelica: 5.0)
                .Should().Be(TrelicaClassificador.TipoMembro.BanzoInferior);
        }

        [Fact]
        public void ClassificarBanzoPorAltura_DentroDaTolerancia_Indefinido()
        {
            // zMedioBarra e zMedioTrelica praticamente iguais -> ambiguo
            TrelicaClassificador.ClassificarBanzoPorAltura(5.0, 5.0, tolZPes: 0.01)
                .Should().Be(TrelicaClassificador.TipoMembro.BanzoIndefinido);
        }
    }
}
