#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using SteelBIM.Services.Trelica;
using Xunit;

namespace SteelBIM.Tests.Services.Trelica
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

        // =====================================================================
        // v2.8.9 — regressao do bug P0 "Cotar Treliça nunca detecta banzos".
        // Antes, o passo 4 re-classificava por inclinacao (que nunca devolve
        // BanzoSuperior/Inferior) e a deteccao saia SEMPRE vazia. Estes testes
        // pinam a coleta de nos a partir da classificacao ja desambiguada.
        // =====================================================================

        [Fact]
        public void ColetarNosDoBanzo_TrelicaPlana_SeparaSuperiorEInferior()
        {
            // 2 banzos horizontais (sup Z=3, inf Z=0) + 1 montante.
            var barras = new (TrelicaClassificador.TipoMembro, (double, double), (double, double))[]
            {
                (TrelicaClassificador.TipoMembro.BanzoSuperior, (0.0, 3.0), (5.0, 3.0)),
                (TrelicaClassificador.TipoMembro.BanzoInferior, (0.0, 0.0), (5.0, 0.0)),
                (TrelicaClassificador.TipoMembro.Montante,      (5.0, 0.0), (5.0, 3.0)),
            };

            var sup = TrelicaGeometria.ColetarNosDoBanzo(barras, TrelicaClassificador.TipoMembro.BanzoSuperior);
            var inf = TrelicaGeometria.ColetarNosDoBanzo(barras, TrelicaClassificador.TipoMembro.BanzoInferior);

            sup.Should().NotBeEmpty();   // o bug original deixava VAZIO -> pipeline abortava
            inf.Should().NotBeEmpty();
            sup.Should().OnlyContain(n => n.Z == 3.0);
            inf.Should().OnlyContain(n => n.Z == 0.0);
        }

        [Fact]
        public void ColetarNosDoBanzo_UnificaNosCoincidentes()
        {
            // Duas barras do banzo superior compartilham o no (5,3) -> deve aparecer 1x.
            var barras = new (TrelicaClassificador.TipoMembro, (double, double), (double, double))[]
            {
                (TrelicaClassificador.TipoMembro.BanzoSuperior, (0.0, 3.0), (5.0, 3.0)),
                (TrelicaClassificador.TipoMembro.BanzoSuperior, (5.0, 3.0), (10.0, 3.0)),
            };

            var sup = TrelicaGeometria.ColetarNosDoBanzo(barras, TrelicaClassificador.TipoMembro.BanzoSuperior);

            sup.Should().HaveCount(3); // 0, 5, 10 (o 5 compartilhado e unificado)
            sup.Select(n => n.X).Should().BeInAscendingOrder();
        }

        [Fact]
        public void ColetarNosDoBanzo_SemBarrasDoTipo_RetornaVazio()
        {
            var barras = new (TrelicaClassificador.TipoMembro, (double, double), (double, double))[]
            {
                (TrelicaClassificador.TipoMembro.Diagonal, (0.0, 0.0), (5.0, 3.0)),
            };
            TrelicaGeometria.ColetarNosDoBanzo(barras, TrelicaClassificador.TipoMembro.BanzoSuperior)
                .Should().BeEmpty();
        }

        [Fact]
        public void IndiceNoMaisProximo_AchaDentroDaTolerancia()
        {
            var xs = new[] { 0.0, 5.0, 10.0 };
            TrelicaGeometria.IndiceNoMaisProximo(xs, 5.02, 0.05).Should().Be(1);
        }

        [Fact]
        public void IndiceNoMaisProximo_ForaDaTolerancia_RetornaMenosUm()
        {
            var xs = new[] { 0.0, 5.0, 10.0 };
            TrelicaGeometria.IndiceNoMaisProximo(xs, 7.0, 0.05).Should().Be(-1);
        }

        [Fact]
        public void IndiceNoMaisProximo_NoEmZero_NaoEhDescartado()
        {
            // Regressao do sentinela antigo "noSup.X == 0": no legitimo em X=0 deve ser achavel.
            var xs = new[] { 0.0, 5.0 };
            TrelicaGeometria.IndiceNoMaisProximo(xs, 0.0, 0.05).Should().Be(0);
        }
    }
}
