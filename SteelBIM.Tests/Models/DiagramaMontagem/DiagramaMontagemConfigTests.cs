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

        [Fact]
        public void Defaults_NovosCamposV240()
        {
            var c = new DiagramaMontagemConfig();
            Assert.True(c.AdicionarCotasVerticais);
            Assert.Equal(100.0, c.ToleranciaClusterizacaoMm);
            Assert.True(c.AdicionarCotaTotalConjunto);
            Assert.True(c.MostrarSimboloDeNivel);
            Assert.False(c.ColocarEmFolha);
            Assert.Equal("EM-XX", c.NumeroFolha);
            Assert.Equal("", c.NomeFolha);
            Assert.False(c.AdicionarComprimentosIndividuais);
        }

        [Fact]
        public void NovosCampos_RoundTrip()
        {
            var c = new DiagramaMontagemConfig
            {
                AdicionarCotasVerticais = false,
                ToleranciaClusterizacaoMm = 200.0,
                AdicionarCotaTotalConjunto = false,
                MostrarSimboloDeNivel = false,
                ColocarEmFolha = true,
                NumeroFolha = "EM-05",
                NomeFolha = "Elevacao Eixo 5",
                AdicionarComprimentosIndividuais = true
            };
            Assert.False(c.AdicionarCotasVerticais);
            Assert.Equal(200.0, c.ToleranciaClusterizacaoMm);
            Assert.False(c.AdicionarCotaTotalConjunto);
            Assert.False(c.MostrarSimboloDeNivel);
            Assert.True(c.ColocarEmFolha);
            Assert.Equal("EM-05", c.NumeroFolha);
            Assert.Equal("Elevacao Eixo 5", c.NomeFolha);
            Assert.True(c.AdicionarComprimentosIndividuais);
        }
    }
}
