using FerramentaEMT.Models.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Models.PF
{
    /// <summary>
    /// Testes do DTO <see cref="PfTwoPileCapResultado"/> que materializa o
    /// padrao ADR-003 ("service mudo") no PfTwoPileCapRebarService.
    /// O caller (Cmd) le este DTO e decide a UX — sem este contrato
    /// estavel, a janela de confirmacao do comando some/duplica.
    /// </summary>
    public class PfTwoPileCapResultadoTests
    {
        [Fact]
        public void Defaults_SaoSeguros()
        {
            var r = new PfTwoPileCapResultado();

            r.SelecaoVazia.Should().BeFalse();
            r.HostsProcessados.Should().Be(0);
            r.HostsComSucesso.Should().Be(0);
            r.ArmadurasCriadas.Should().Be(0);
            r.Avisos.Should().BeEmpty();
        }

        [Fact]
        public void ToResumo_SemAvisos_TresLinhas()
        {
            var r = new PfTwoPileCapResultado
            {
                HostsProcessados = 5,
                HostsComSucesso = 4,
                ArmadurasCriadas = 56
            };

            string resumo = r.ToResumo();

            resumo.Should().Contain("Hospedeiros processados: 5");
            resumo.Should().Contain("Hospedeiros com sucesso: 4");
            resumo.Should().Contain("Armaduras criadas: 56");
            resumo.Should().NotContain("Ocorrencias");
        }

        [Fact]
        public void ToResumo_ComAvisos_AdicionaSecaoOcorrencias()
        {
            var r = new PfTwoPileCapResultado
            {
                HostsProcessados = 3,
                HostsComSucesso = 1,
                ArmadurasCriadas = 14
            };
            r.Avisos.Add("Id 100: nenhuma barra foi gerada.");
            r.Avisos.Add("Id 101: familia nao habilitada para hospedar armadura.");

            string resumo = r.ToResumo();

            resumo.Should().Contain("Ocorrencias:");
            resumo.Should().Contain("- Id 100:");
            resumo.Should().Contain("- Id 101:");
        }

        [Fact]
        public void ToResumo_LimitaAvisosA10NaUI()
        {
            // Regressao: o resumo so mostra os primeiros 10 avisos pra nao
            // escalar pra 1000 linhas no MessageBox e quebrar a UX.
            var r = new PfTwoPileCapResultado
            {
                HostsProcessados = 50,
                HostsComSucesso = 30,
                ArmadurasCriadas = 420
            };
            for (int i = 1; i <= 25; i++)
                r.Avisos.Add($"Id {i}: motivo {i}");

            string resumo = r.ToResumo();
            int countDashes = 0;
            int idx = 0;
            while ((idx = resumo.IndexOf("\n- ", idx)) >= 0) { countDashes++; idx++; }

            countDashes.Should().Be(10);
        }

        [Fact]
        public void SelecaoVazia_PodeSerSetada_SemDispararResumo()
        {
            // Quando selecao vazia, o caller mostra warning especifico — nao chama ToResumo.
            // Este teste so garante que a flag e setavel e ToResumo continua funcionando.
            var r = new PfTwoPileCapResultado { SelecaoVazia = true };

            r.SelecaoVazia.Should().BeTrue();
            r.ToResumo().Should().NotBeNullOrWhiteSpace();
        }
    }
}
