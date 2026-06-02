using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SteelBIM.Models;
using SteelBIM.Services.Trelica;
using Xunit;

namespace SteelBIM.Tests.Services.Trelica
{
    /// <summary>
    /// v2.8.11 (Onda 2 — Treliça): matriz de testes do helper puro que decide os membros
    /// da treliça por padrao. Cobre contagem de montantes/diagonais/banzos, sentido das
    /// diagonais por padrao, e as flags (banzos, diagonais de extremidade, espelhar).
    /// </summary>
    public class TrelicaPatternBuilderTests
    {
        private static int Contar(List<TrussSegment> s, TrussMemberKind tipo) => s.Count(x => x.Tipo == tipo);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Construir_MenosDeDuasEstacoes_RetornaVazio(int nEstacoes)
        {
            TrelicaPatternBuilder.Construir(nEstacoes, new TrussBuildOptions()).Should().BeEmpty();
        }

        [Fact]
        public void Construir_OpcoesNull_RetornaVazio()
        {
            TrelicaPatternBuilder.Construir(5, null!).Should().BeEmpty();
        }

        [Fact]
        public void Warren_4Estacoes_DuasMontantesExtremidade_TresDiagonaisAlternadas()
        {
            var segs = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.Warren });

            Contar(segs, TrussMemberKind.Banzo).Should().Be(0);       // modo "preencher entre banzos"
            Contar(segs, TrussMemberKind.Montante).Should().Be(2);    // so extremidades
            Contar(segs, TrussMemberKind.Diagonal).Should().Be(3);    // uma por painel

            // 1o painel sobe (Inferior0 -> Superior1).
            var d0 = segs.First(s => s.Tipo == TrussMemberKind.Diagonal);
            d0.De.Chord.Should().Be(TrussChord.Inferior);
            d0.De.Estacao.Should().Be(0);
            d0.Para.Chord.Should().Be(TrussChord.Superior);
            d0.Para.Estacao.Should().Be(1);
        }

        [Fact]
        public void SoMontantes_NaoCriaDiagonais_EMontantesEmTodasEstacoes()
        {
            var segs = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.SoMontantes });
            Contar(segs, TrussMemberKind.Diagonal).Should().Be(0);
            Contar(segs, TrussMemberKind.Montante).Should().Be(4); // todas as estacoes
        }

        [Fact]
        public void EmX_DuasDiagonaisPorPainel()
        {
            var segs = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.EmX });
            Contar(segs, TrussMemberKind.Diagonal).Should().Be(6); // 2 x 3 paineis
        }

        [Fact]
        public void Pratt_MontantesEmTodasEstacoes_EUmaDiagonalPorPainel()
        {
            var segs = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.Pratt });
            Contar(segs, TrussMemberKind.Montante).Should().Be(4);
            Contar(segs, TrussMemberKind.Diagonal).Should().Be(3);
        }

        [Fact]
        public void IncluirBanzos_AdicionaDoisBanzosContinuos()
        {
            var segs = TrelicaPatternBuilder.Construir(
                5, new TrussBuildOptions { Padrao = TrussPattern.Warren, IncluirBanzos = true });

            var banzos = segs.Where(s => s.Tipo == TrussMemberKind.Banzo).ToList();
            banzos.Should().HaveCount(2);
            banzos.Should().Contain(b => b.De.Chord == TrussChord.Superior && b.De.Estacao == 0 && b.Para.Estacao == 4);
            banzos.Should().Contain(b => b.De.Chord == TrussChord.Inferior && b.De.Estacao == 0 && b.Para.Estacao == 4);
        }

        [Fact]
        public void DiagonaisExtremidadeFalse_PulaPaineisDasPontas()
        {
            var segs = TrelicaPatternBuilder.Construir(
                4, new TrussBuildOptions { Padrao = TrussPattern.Warren, DiagonaisExtremidade = false });
            // paineis = 3; p=0 e p=2 sao ponta -> so o painel central (p=1) gera diagonal.
            Contar(segs, TrussMemberKind.Diagonal).Should().Be(1);
        }

        [Fact]
        public void MontantesExtremidadeFalse_NaoCriaVerticaisNasPontas()
        {
            var segs = TrelicaPatternBuilder.Construir(
                4, new TrussBuildOptions { Padrao = TrussPattern.Warren, MontantesExtremidade = false });
            Contar(segs, TrussMemberKind.Montante).Should().Be(0);
        }

        [Fact]
        public void MontantesIntermediarios_AdicionaVerticaisInternasNoWarren()
        {
            var segs = TrelicaPatternBuilder.Construir(
                4, new TrussBuildOptions { Padrao = TrussPattern.Warren, MontantesIntermediarios = true });
            Contar(segs, TrussMemberKind.Montante).Should().Be(4); // 2 pontas + 2 internas
        }

        [Fact]
        public void Espelhar_InverteSentidoDaPrimeiraDiagonal()
        {
            var normal = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.Warren });
            var espelhado = TrelicaPatternBuilder.Construir(4, new TrussBuildOptions { Padrao = TrussPattern.Warren, Espelhar = true });

            var d0n = normal.First(s => s.Tipo == TrussMemberKind.Diagonal);
            var d0e = espelhado.First(s => s.Tipo == TrussMemberKind.Diagonal);

            d0n.De.Chord.Should().Be(TrussChord.Inferior);  // sobe
            d0e.De.Chord.Should().Be(TrussChord.Superior);  // espelhado: desce
        }

        [Fact]
        public void DiagonalDireita_TodasNoMesmoSentido()
        {
            var segs = TrelicaPatternBuilder.Construir(5, new TrussBuildOptions { Padrao = TrussPattern.DiagonalDireita });
            var diagonais = segs.Where(s => s.Tipo == TrussMemberKind.Diagonal).ToList();
            diagonais.Should().OnlyContain(d => d.De.Chord == TrussChord.Inferior && d.Para.Chord == TrussChord.Superior);
        }

        // =============================================================
        //  AlturaNaPosicao — altura variavel H (extremidade) -> B (centro), duas aguas
        // =============================================================

        [Theory]
        [InlineData(0.0, 100.0)]   // extremidade esquerda -> H
        [InlineData(1.0, 100.0)]   // extremidade direita  -> H
        [InlineData(0.5, 300.0)]   // centro                -> B
        [InlineData(0.25, 200.0)]  // meio caminho          -> H + (B-H)*0.5
        [InlineData(0.75, 200.0)]
        public void AlturaNaPosicao_DuasAguas_InterpolaHaB(double t, double esperado)
        {
            // H=100, B=300
            PfArredonda(TrelicaPatternBuilder.AlturaNaPosicao(t, 100.0, 300.0)).Should().Be(esperado);
        }

        [Fact]
        public void AlturaNaPosicao_HIgualB_AlturaConstante()
        {
            TrelicaPatternBuilder.AlturaNaPosicao(0.0, 150.0, 150.0).Should().Be(150.0);
            TrelicaPatternBuilder.AlturaNaPosicao(0.5, 150.0, 150.0).Should().Be(150.0);
            TrelicaPatternBuilder.AlturaNaPosicao(0.83, 150.0, 150.0).Should().Be(150.0);
        }

        [Theory]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void AlturaNaPosicao_ForaDoIntervalo_ClampaNaExtremidade(double t)
        {
            // t clampado para [0,1] -> extremidade -> H
            TrelicaPatternBuilder.AlturaNaPosicao(t, 100.0, 300.0).Should().Be(100.0);
        }

        private static double PfArredonda(double v) => System.Math.Round(v, 6);
    }
}
