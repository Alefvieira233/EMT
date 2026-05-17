using SteelBIM.Models.DiagramaMontagem;
using Xunit;

namespace SteelBIM.Tests.Models.DiagramaMontagem
{
    public class DiagramaMontagemConfigTests
    {
        [Fact]
        public void Defaults_OrientacaoAuto_MargemEixosCotasTags()
        {
            var c = new DiagramaMontagemConfig();
            Assert.Equal(OrientacaoDiagrama.Auto, c.Orientacao);
            Assert.Equal(500.0, c.MargemMm);
            Assert.True(c.MostrarEixos);
            Assert.True(c.AdicionarCotasEntreEixos);
            Assert.True(c.AdicionarTagsMarca);
            Assert.Equal("Diagrama de Montagem", c.NomeVista);
        }

        [Fact]
        public void Properties_RoundTrip()
        {
            var c = new DiagramaMontagemConfig
            {
                Orientacao = OrientacaoDiagrama.ParaleloEixoX,
                MargemMm = 1000.0,
                MostrarEixos = false,
                AdicionarCotasEntreEixos = false,
                AdicionarTagsMarca = false,
                NomeVista = "Eixo 5"
            };
            Assert.Equal(OrientacaoDiagrama.ParaleloEixoX, c.Orientacao);
            Assert.Equal(1000.0, c.MargemMm);
            Assert.False(c.MostrarEixos);
            Assert.False(c.AdicionarCotasEntreEixos);
            Assert.False(c.AdicionarTagsMarca);
            Assert.Equal("Eixo 5", c.NomeVista);
        }
    }
}
