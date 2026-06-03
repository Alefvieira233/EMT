using System.Linq;
using FluentAssertions;
using SteelBIM.Services.Portico;
using Xunit;

namespace SteelBIM.Tests.Services.Portico
{
    /// <summary>
    /// Matriz de testes do nucleo puro do gerador de portico (galpao). Numeros do template EMT:
    /// 7 porticos x 5000 mm, vao 15010 mm, beiral 4000 mm, treliça H=600 / B=1600.
    /// </summary>
    public class PorticoGeometriaCalculatorTests
    {
        private static GerarPorticoEntrada BaseTrelica() => new GerarPorticoEntrada
        {
            NumeroPorticos = 7,
            EspacamentoPorticosMm = 5000.0,
            VaoGalpaoMm = 15010.0,
            AlturaPilarMm = 4000.0,
            UsarTrelica = true,
            AlturaExtremidadeMm = 600.0,
            AlturaCentralMm = 1600.0,
            LancarTercas = true,
            EspacamentoTercasMm = 1500.0,
            ContravCobertura = false,
            ContravPilares = false,
            LancarLinhaCorrente = false
        };

        [Fact]
        public void Calcular_EstacoesDosPorticos()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            r.XPorticosMm.Should().Equal(new[] { 0.0, 5000.0, 10000.0, 15000.0, 20000.0, 25000.0, 30000.0 });
            r.YEixosMm.Should().Equal(new[] { 0.0, 15010.0 });
        }

        [Fact]
        public void Calcular_DoisPilaresPorPortico_DaBaseAoBeiral()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            r.Pilares.Should().HaveCount(14); // 7 porticos x 2
            r.Pilares.Should().OnlyContain(p => p.A.ZMm == 0.0 && p.B.ZMm == 4000.0);
            r.Pilares.Should().Contain(p => p.A.YMm == 0.0);
            r.Pilares.Should().Contain(p => p.A.YMm == 15010.0);
        }

        [Fact]
        public void Calcular_Trelica_EixoInferiorPorPortico_NoBeiral()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            r.EixosInferioresTrelica.Should().HaveCount(7);
            r.EixosInferioresTrelica.Should().OnlyContain(s => s.A.ZMm == 4000.0 && s.B.ZMm == 4000.0);
            r.EixosInferioresTrelica.Should().OnlyContain(s => s.A.YMm == 0.0 && s.B.YMm == 15010.0);
            r.Vigas.Should().BeEmpty();
        }

        [Fact]
        public void Calcular_Viga_DuasAguasPorPortico_ComApiceNaCumeeira()
        {
            var e = BaseTrelica();
            e.UsarTrelica = false;
            e.AlturaCumeeiraMm = 1500.0;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.Vigas.Should().HaveCount(14); // 7 porticos x 2 aguas
            r.EixosInferioresTrelica.Should().BeEmpty();
            r.Vigas.Should().Contain(s => s.B.YMm == 15010.0 / 2.0 && s.B.ZMm == 5500.0);
        }

        [Fact]
        public void Calcular_Tercas_SimetricasEmTornoDaCumeeira_Longitudinais()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            r.Tercas.Should().NotBeEmpty();
            // 6 posicoes na meia-agua (j=0..5) + 5 espelhos (a cumeeira nao duplica) = 11.
            r.Tercas.Should().HaveCount(11);
            r.Tercas.Should().OnlyContain(s => s.A.XMm == 0.0 && s.B.XMm == 30000.0);
            r.Tercas.Should().OnlyContain(s => s.A.YMm == s.B.YMm && s.A.ZMm == s.B.ZMm);

            double meia = 15010.0 / 2.0;
            r.Tercas.Count(s => s.A.YMm == meia).Should().Be(1); // cumeeira aparece uma vez
            foreach (var s in r.Tercas.Where(t => t.A.YMm < meia))
                r.Tercas.Should().Contain(o => o.A.YMm == 15010.0 - s.A.YMm); // espelho
        }

        [Fact]
        public void Calcular_Tercas_NaCumeeira_AtingemAlturaCentral()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            double meia = 15010.0 / 2.0;
            var cumeeira = r.Tercas.Single(s => s.A.YMm == meia);
            cumeeira.A.ZMm.Should().BeApproximately(4000.0 + 1600.0, 1e-6); // beiral + B
        }

        [Fact]
        public void Calcular_ContravEListasDesligadas_Vazias()
        {
            var r = PorticoGeometriaCalculator.Calcular(BaseTrelica());
            r.ContravCobertura.Should().BeEmpty();
            r.ContravPilares.Should().BeEmpty();
            r.LinhasCorrente.Should().BeEmpty();
        }

        [Fact]
        public void Calcular_ContravCobertura_SoNosVaosDeExtremidade()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.ContravCobertura.Should().HaveCount(8); // 2 vaos x 2 aguas x 2 diagonais
            r.ContravCobertura.Should().OnlyContain(s =>
                s.A.XMm == 0.0 || s.A.XMm == 5000.0 || s.A.XMm == 25000.0 || s.A.XMm == 30000.0);
        }

        [Fact]
        public void Calcular_ContravPilares_XVerticalNasParedes()
        {
            var e = BaseTrelica();
            e.ContravPilares = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.ContravPilares.Should().HaveCount(8); // 2 vaos x 2 paredes x 2 diagonais
            r.ContravPilares.Should().OnlyContain(s => s.A.ZMm == 0.0);
        }

        [Fact]
        public void Calcular_LinhaCorrente_TresTirantesLongitudinais()
        {
            var e = BaseTrelica();
            e.LancarLinhaCorrente = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.LinhasCorrente.Should().HaveCount(3);
            r.LinhasCorrente.Should().OnlyContain(s => s.A.XMm == 0.0 && s.B.XMm == 30000.0);
        }

        [Fact]
        public void Calcular_DoisPorticos_UmVaoUnico()
        {
            var e = BaseTrelica();
            e.NumeroPorticos = 2;
            e.ContravCobertura = true;
            e.ContravPilares = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.XPorticosMm.Should().Equal(new[] { 0.0, 5000.0 });
            r.Pilares.Should().HaveCount(4);
            r.ContravCobertura.Should().HaveCount(4); // 1 vao x 2 aguas x 2
            r.ContravPilares.Should().HaveCount(4);   // 1 vao x 2 paredes x 2
        }

        [Fact]
        public void Calcular_MenosDeDoisPorticos_RetornaVazio()
        {
            var e = BaseTrelica();
            e.NumeroPorticos = 1;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.Pilares.Should().BeEmpty();
            r.EixosInferioresTrelica.Should().BeEmpty();
            r.XPorticosMm.Should().BeEmpty();
        }

        [Fact]
        public void Calcular_BanzosParalelos_QuandoBIgualH()
        {
            var e = BaseTrelica();
            e.AlturaCentralMm = e.AlturaExtremidadeMm; // 600 == 600 => agua plana
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.Tercas.Should().HaveCount(11);
            r.Tercas.Should().OnlyContain(s => s.A.ZMm == 4600.0); // beiral + H, sem pico
        }

        [Fact]
        public void Calcular_TercasDesligadas_ListaVazia()
        {
            var e = BaseTrelica();
            e.LancarTercas = false;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.Tercas.Should().BeEmpty();
        }
    }
}
