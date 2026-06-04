using System.Linq;
using FluentAssertions;
using SteelBIM.Models;
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
            ElevacaoTercasMm = 150.0,
            ContravCobertura = false,
            TercasPorXCobertura = 2,
            ContravPilares = false,
            NumeroXPilares = 2,
            LancarLinhaCorrente = false,
            NumeroLinhasCorrente = 3
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
        public void Calcular_PilarCentral_AdicionaColunaNoMeioDoVao()
        {
            var e = BaseTrelica();
            e.PilarCentral = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.Pilares.Should().HaveCount(21); // 7 porticos x 3 (y=0, y=w, y=w/2)
            r.Pilares.Should().Contain(p => p.A.YMm == 15010.0 / 2.0 && p.A.ZMm == 0.0 && p.B.ZMm == 4000.0);
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
            cumeeira.A.ZMm.Should().BeApproximately(4000.0 + 1600.0 + 150.0, 1e-6); // beiral + B + elevacao
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
        public void Calcular_ContravCobertura_XACadaNTercas_NosVaosDeExtremidade()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true; // TercasPorXCobertura = 2 (default)
            var r = PorticoGeometriaCalculator.Calcular(e);
            // 6 posicoes de terça na meia-agua -> passo 2 -> 3 X por agua;
            // 2 vaos de extremidade x 2 aguas x 3 X x 2 diagonais = 24.
            r.ContravCobertura.Should().HaveCount(24);
            r.ContravCobertura.Should().OnlyContain(s =>
                s.A.XMm == 0.0 || s.A.XMm == 5000.0 || s.A.XMm == 25000.0 || s.A.XMm == 30000.0);
            // nao e' mais 1 X gigante: varios Y distintos subindo a agua.
            r.ContravCobertura.Select(s => s.A.YMm).Distinct().Count().Should().BeGreaterThan(2);
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
        public void Calcular_ContravCobertura_PassoMaior_GeraMenosXs()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true;
            e.TercasPorXCobertura = 3; // 1 X a cada 3 terças -> menos X
            var r = PorticoGeometriaCalculator.Calcular(e);
            // 6 posicoes -> passo 3 -> 2 X por agua; 2 vaos x 2 aguas x 2 X x 2 diag = 16.
            r.ContravCobertura.Should().HaveCount(16);
        }

        [Fact]
        public void Calcular_ContravCobertura_ExtremidadesECentro_GeraMaisVaos()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true;
            e.DistribuicaoContravCobertura = DistribuicaoContravCobertura.ExtremidadesECentro;
            var r = PorticoGeometriaCalculator.Calcular(e);
            // 4 vaos x 2 aguas x 3 X x 2 diag = 48 (vs 24 das extremidades).
            r.ContravCobertura.Should().HaveCount(48);
        }

        [Fact]
        public void Calcular_ContravCobertura_Todos_ContraventaTodosOsVaos()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true;
            e.DistribuicaoContravCobertura = DistribuicaoContravCobertura.Todos;
            var r = PorticoGeometriaCalculator.Calcular(e);
            // 6 vaos x 2 aguas x 3 X x 2 diag = 72.
            r.ContravCobertura.Should().HaveCount(72);
        }

        [Fact]
        public void Calcular_PilarCentral_ModoViga_AlcancaACumeeira()
        {
            var e = BaseTrelica();
            e.PilarCentral = true;
            e.UsarTrelica = false;
            e.AlturaCumeeiraMm = 1500.0;
            var r = PorticoGeometriaCalculator.Calcular(e);
            double meia = 15010.0 / 2.0;
            // pilar central (y=w/2) vai do piso ao ápice da viga (beiral + cumeeira = 5500).
            r.Pilares.Should().Contain(p => p.A.YMm == meia && p.A.ZMm == 0.0 && p.B.ZMm == 5500.0);
        }

        [Fact]
        public void Calcular_LinhaCorrente_NoNivelDasTercas()
        {
            var e = BaseTrelica();
            e.LancarLinhaCorrente = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            double meia = 15010.0 / 2.0;
            // na cumeeira, a linha de corrente fica em ZTopo + elevacao = 4000+1600+150 = 5750.
            r.LinhasCorrente.Should().Contain(s =>
                (s.A.YMm == meia && s.A.ZMm == 5750.0) || (s.B.YMm == meia && s.B.ZMm == 5750.0));
        }

        [Fact]
        public void Calcular_ContravCobertura_NoPlanoDoBanzo_TercasElevadas()
        {
            var e = BaseTrelica();
            e.ContravCobertura = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            // contrav de cobertura no plano do banzo: no apoio (y=0) z = beiral + H = 4600 (sem elevacao).
            r.ContravCobertura.Should().Contain(s => s.A.ZMm == 4600.0 || s.B.ZMm == 4600.0);
            // a terça no mesmo apoio fica 150 mm acima (sobre o banzo): 4750.
            r.Tercas.Should().Contain(t => t.A.YMm == 0.0 && t.A.ZMm == 4750.0);
        }

        [Fact]
        public void Calcular_NumeroXPilares_Configuravel()
        {
            var e = BaseTrelica();
            e.ContravPilares = true;
            e.NumeroXPilares = 1;
            var r = PorticoGeometriaCalculator.Calcular(e);
            r.ContravPilares.Should().HaveCount(4); // 1 vao x 2 paredes x 2 diagonais
        }

        [Fact]
        public void Calcular_LinhaCorrente_SobeAAgua_NoMeioDoVao()
        {
            var e = BaseTrelica();
            e.LancarLinhaCorrente = true;
            var r = PorticoGeometriaCalculator.Calcular(e);
            // NumeroLinhasCorrente=3 (default) -> 3 fileiras x 2 aguas = 6 sag-rods.
            r.LinhasCorrente.Should().HaveCount(6);
            // sobe a agua: X constante (meio do vao) e Y varia (do beiral a cumeeira).
            r.LinhasCorrente.Should().OnlyContain(s => s.A.XMm == s.B.XMm && s.A.YMm != s.B.YMm);
            // no meio do primeiro vao (entre x=0 e x=5000 -> 2500).
            r.LinhasCorrente.Should().Contain(s => s.A.XMm == 2500.0);
            // uma das aguas vai do beiral (y=0) ate a cumeeira (y=w/2).
            r.LinhasCorrente.Should().Contain(s => s.A.YMm == 0.0 && s.B.YMm == 15010.0 / 2.0);
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
            r.ContravCobertura.Should().HaveCount(12); // 1 vao x 2 aguas x 3 X x 2
            r.ContravPilares.Should().HaveCount(4);    // 1 vao x 2 paredes x 2
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
            r.Tercas.Should().OnlyContain(s => s.A.ZMm == 4750.0); // beiral + H + elevacao, sem pico
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
