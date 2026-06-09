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

        [Fact]
        public void InferirBase_PerfilEstruturalSemMaterial_AssumeAco()
        {
            // terça/contrav "U150x65" sem material e sem "aço" no nome: antes virava Outro (peso 0)
            // e SUMIA da lista. Agora, como perfil estrutural, assume aço por padrao.
            ListaMateriaisPesoCalc.InferirBase(null, "U150x65x4,76", isFundacao: false, isPerfilEstrutural: true)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Metalico);
        }

        [Fact]
        public void InferirBase_PilarSemPista_NaoViraAco()
        {
            // Regressao corrigida: pilar de concreto sem material e sem "concreto" no nome
            // NAO deve virar aco. Pilares nao sao tratados como perfil estrutural (isPerfilEstrutural
            // = false), entao "P1 40x40" sem pista permanece Outro (nao entra como aco na lista).
            ListaMateriaisPesoCalc.InferirBase(null, "P1 40x40", isFundacao: false, isPerfilEstrutural: false)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Outro);
        }

        [Fact]
        public void InferirBase_PerfilEstrutural_NaoSobrepoeConcretoNemFundacao()
        {
            // concreto continua tendo prioridade mesmo marcando perfil estrutural
            ListaMateriaisPesoCalc.InferirBase("Concreto", "Pilar", isFundacao: false, isPerfilEstrutural: true)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
            // fundacao tambem (concreto antes do fallback de perfil)
            ListaMateriaisPesoCalc.InferirBase(null, "Sapata", isFundacao: true, isPerfilEstrutural: true)
                .Should().Be(ListaMateriaisPesoCalc.BaseMaterial.Concreto);
        }
    }
}
