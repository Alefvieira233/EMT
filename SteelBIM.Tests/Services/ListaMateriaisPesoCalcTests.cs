using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    /// <summary>
    /// v2.8.12 (Onda 1): testes do peso/inferencia de base da lista de materiais.
    /// Cobre o fix da densidade do concreto (era 25, agora 2500) e a inferencia por texto.
    /// </summary>
    public class ListaMateriaisPesoCalcTests
    {
        [Fact]
        public void PesoKg_ConcretoEAco()
        {
            ListaMateriaisPesoCalc.PesoKg(2.0, 2500.0).Should().Be(5000.0);   // concreto
            ListaMateriaisPesoCalc.PesoKg(0.01, 7850.0).Should().Be(78.5);    // aco
        }

        [Theory]
        [InlineData(0.0, 2500.0)]
        [InlineData(2.0, 0.0)]
        [InlineData(-1.0, 2500.0)]
        public void PesoKg_NaoPositivo_RetornaZero(double v, double d)
        {
            ListaMateriaisPesoCalc.PesoKg(v, d).Should().Be(0.0);
        }

        [Fact]
        public void InferirBase_PorNomeDoMaterial()
        {
            ListaMateriaisPesoCalc.InferirBase("Concreto fck=30 MPa", null, false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
            ListaMateriaisPesoCalc.InferirBase("Aço ASTM A36", null, false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Metalico);
        }

        [Fact]
        public void InferirBase_PorNomeDaFamiliaQuandoSemMaterial()
        {
            // pilar de concreto sem material atribuido — antes dava peso 0
            ListaMateriaisPesoCalc.InferirBase(null, "Pilar - Seção retangular de concreto moldado in loco", false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
            ListaMateriaisPesoCalc.InferirBase(null, "Viga estrutural perfil formado a frio de aço", false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Metalico);
        }

        [Fact]
        public void InferirBase_FundacaoSemTexto_AssumeConcreto()
        {
            ListaMateriaisPesoCalc.InferirBase(null, null, isFundacao: true)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
        }

        [Fact]
        public void InferirBase_ConcretoTemPrioridadeSobreAco()
        {
            ListaMateriaisPesoCalc.InferirBase("Concreto", "perfil de aço", false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
        }

        [Fact]
        public void InferirBase_SemPista_RetornaOutro()
        {
            ListaMateriaisPesoCalc.InferirBase("Genérico", "Familia X", false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Outro);
        }
    }
}
