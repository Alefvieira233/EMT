using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SteelBIM.Services.Layout;
using Xunit;

namespace SteelBIM.Tests.Services.Layout
{
    /// <summary>
    /// v2.8.13: matriz de testes do nucleo puro de layout de vistas (alinhar/distribuir/grade).
    /// Numeros concretos conforme o projeto do algoritmo.
    /// </summary>
    public class LayoutVistasCalculatorTests
    {
        private static CaixaVista Caixa(string id, double w, double h, double cx, double cy) => new(id, w, h, cx, cy);

        private static PosicaoVista Por(IReadOnlyList<PosicaoVista> r, string id) => r.First(p => p.Id == id);

        // ---------- ALINHAR ----------
        [Fact]
        public void Alinhar_Esquerda()
        {
            var cx = new[] { Caixa("a", 100, 50, 200, 400), Caixa("b", 100, 50, 500, 400), Caixa("c", 100, 50, 800, 400) };
            var r = LayoutVistasCalculator.Alinhar(cx, ModoAlinhamento.Esquerda);
            r.Should().OnlyContain(p => p.CentroXMm == 200.0);
            r.Should().OnlyContain(p => p.CentroYMm == 400.0);
        }

        [Fact]
        public void Alinhar_Direita()
        {
            var cx = new[] { Caixa("a", 100, 50, 200, 400), Caixa("b", 100, 50, 500, 400), Caixa("c", 100, 50, 800, 400) };
            var r = LayoutVistasCalculator.Alinhar(cx, ModoAlinhamento.Direita);
            r.Should().OnlyContain(p => p.CentroXMm == 800.0);
        }

        [Fact]
        public void Alinhar_CentroH()
        {
            var cx = new[] { Caixa("a", 100, 50, 100, 0), Caixa("b", 100, 50, 300, 0) };
            var r = LayoutVistasCalculator.Alinhar(cx, ModoAlinhamento.CentroH);
            r.Should().OnlyContain(p => p.CentroXMm == 200.0);
        }

        [Fact]
        public void Alinhar_Topo()
        {
            var cx = new[] { Caixa("a", 100, 50, 0, 300), Caixa("b", 100, 50, 0, 600) };
            var r = LayoutVistasCalculator.Alinhar(cx, ModoAlinhamento.Topo);
            r.Should().OnlyContain(p => p.CentroYMm == 600.0);
        }

        [Fact]
        public void Alinhar_UmItem_NoOp()
        {
            var cx = new[] { Caixa("a", 100, 50, 200, 400) };
            var r = LayoutVistasCalculator.Alinhar(cx, ModoAlinhamento.Esquerda);
            Por(r, "a").CentroXMm.Should().Be(200.0);
            Por(r, "a").CentroYMm.Should().Be(400.0);
        }

        // ---------- DISTRIBUIR ----------
        [Fact]
        public void Distribuir_Horizontal_GapIgual()
        {
            var cx = new[] { Caixa("a", 100, 50, 100, 0), Caixa("b", 100, 50, 500, 0), Caixa("c", 100, 50, 1000, 0) };
            var r = LayoutVistasCalculator.Distribuir(cx, Eixo.Horizontal);
            Por(r, "a").CentroXMm.Should().Be(100.0);    // extremo fixo
            Por(r, "c").CentroXMm.Should().Be(1000.0);   // extremo fixo
            Por(r, "b").CentroXMm.Should().BeApproximately(550.0, 1e-6); // meio recalculado
        }

        [Fact]
        public void Distribuir_MenosDe3_NoOp()
        {
            var cx = new[] { Caixa("a", 100, 50, 100, 0), Caixa("b", 100, 50, 900, 0) };
            var r = LayoutVistasCalculator.Distribuir(cx, Eixo.Horizontal);
            Por(r, "a").CentroXMm.Should().Be(100.0);
            Por(r, "b").CentroXMm.Should().Be(900.0);
        }

        [Fact]
        public void Distribuir_GapNegativo_NaoQuebra()
        {
            var cx = new[] { Caixa("a", 500, 50, 0, 0), Caixa("b", 500, 50, 100, 0), Caixa("c", 500, 50, 200, 0) };
            var r = LayoutVistasCalculator.Distribuir(cx, Eixo.Horizontal);
            r.Should().HaveCount(3);
            Por(r, "a").CentroXMm.Should().Be(0.0);
            Por(r, "c").CentroXMm.Should().Be(200.0);
        }

        // ---------- GRADE ----------
        [Fact]
        public void Grade_DuasColunas()
        {
            var cx = new[] { Caixa("a", 100, 50, 0, 0), Caixa("b", 100, 50, 0, 0), Caixa("c", 100, 50, 0, 0) };
            var r = LayoutVistasCalculator.Grade(cx, new AreaUtil(0, 0, 1000, 800), 2, 0, 0);
            r.Colunas.Should().Be(2);
            r.Linhas.Should().Be(2);
            r.NaoCoube.Should().BeEmpty();
            Por(r.Posicionados, "a").Should().Be(new PosicaoVista("a", 50, 775));
            Por(r.Posicionados, "b").Should().Be(new PosicaoVista("b", 150, 775));
            Por(r.Posicionados, "c").Should().Be(new PosicaoVista("c", 50, 725));
        }

        [Fact]
        public void Grade_AutoColunas()
        {
            var cx = Enumerable.Range(0, 4).Select(i => Caixa($"v{i}", 200, 100, 0, 0)).ToArray();
            var r = LayoutVistasCalculator.Grade(cx, new AreaUtil(0, 0, 1000, 800), null, 0, 0);
            r.Colunas.Should().Be(4);
            r.Linhas.Should().Be(1);
            Por(r.Posicionados, "v0").CentroXMm.Should().Be(100);
            Por(r.Posicionados, "v3").CentroXMm.Should().Be(700);
            r.Posicionados.Should().OnlyContain(p => p.CentroYMm == 750);
        }

        [Fact]
        public void Grade_CaixaGigante_VaiParaNaoCoube()
        {
            var cx = new[] { Caixa("A", 100, 50, 0, 0), Caixa("B", 2000, 50, 0, 0), Caixa("C", 100, 50, 0, 0) };
            var r = LayoutVistasCalculator.Grade(cx, new AreaUtil(0, 0, 1000, 800), 2, 0, 0);
            r.NaoCoube.Should().ContainSingle().Which.Should().Be("B");
            r.Posicionados.Should().HaveCount(2);
            r.Colunas.Should().Be(2);
            r.Linhas.Should().Be(1);
        }

        [Fact]
        public void Grade_AreaDegenerada_TudoNaoCoube()
        {
            var cx = new[] { Caixa("a", 100, 50, 0, 0) };
            var r = LayoutVistasCalculator.Grade(cx, new AreaUtil(0, 0, 0, 800), 2, 0, 0);
            r.NaoCoube.Should().ContainSingle();
            r.Posicionados.Should().BeEmpty();
            r.Colunas.Should().Be(0);
            r.Linhas.Should().Be(0);
        }

        [Fact]
        public void Grade_OverflowVertical()
        {
            var cx = new[] { Caixa("a", 100, 500, 0, 0), Caixa("b", 100, 500, 0, 0), Caixa("c", 100, 500, 0, 0) };
            var r = LayoutVistasCalculator.Grade(cx, new AreaUtil(0, 0, 1000, 800), 1, 0, 0);
            r.Posicionados.Should().ContainSingle().Which.Id.Should().Be("a");
            Por(r.Posicionados, "a").CentroYMm.Should().Be(550);
            r.NaoCoube.Should().BeEquivalentTo(new[] { "b", "c" });
            r.Linhas.Should().Be(1);
        }
    }
}
