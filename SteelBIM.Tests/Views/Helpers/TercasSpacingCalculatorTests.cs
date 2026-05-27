#nullable enable
using System.Collections.Generic;
using FluentAssertions;
using SteelBIM.Views.Helpers;
using Xunit;

namespace SteelBIM.Tests.Views.Helpers
{
    /// <summary>
    /// v2.8.1 (Victor): tests do math puro da janela TercasSpacingWindow.
    /// Cobre escala proporcional, recalculo do ultimo como remainder, e
    /// validacao de total matching (label verde/vermelho).
    /// </summary>
    public class TercasSpacingCalculatorTests
    {
        // ---------- ScaleProportionally ----------

        [Fact]
        public void Escala_600_para_800_aumenta_25_porcento()
        {
            var values = new List<double> { 150, 150, 150, 150 };
            var scaled = TercasSpacingCalculator.ScaleProportionally(600, 800, values);

            scaled.Should().Equal(200, 200, 200, 200);
        }

        [Fact]
        public void Escala_para_metade()
        {
            var values = new List<double> { 200, 200, 200 };
            var scaled = TercasSpacingCalculator.ScaleProportionally(600, 300, values);

            scaled.Should().Equal(100, 100, 100);
        }

        [Fact]
        public void Escala_oldTotal_zero_retorna_copia_inalterada()
        {
            var values = new List<double> { 100, 100, 100 };
            var scaled = TercasSpacingCalculator.ScaleProportionally(0, 800, values);

            scaled.Should().Equal(100, 100, 100);
        }

        [Fact]
        public void Escala_oldTotal_negativo_retorna_copia_inalterada()
        {
            var values = new List<double> { 100, 100 };
            var scaled = TercasSpacingCalculator.ScaleProportionally(-10, 200, values);

            scaled.Should().Equal(100, 100);
        }

        [Fact]
        public void Escala_newTotal_zero_retorna_lista_de_zeros()
        {
            var values = new List<double> { 150, 200, 100 };
            var scaled = TercasSpacingCalculator.ScaleProportionally(450, 0, values);

            scaled.Should().Equal(0, 0, 0);
        }

        [Fact]
        public void Escala_values_null_retorna_lista_vazia()
        {
            var scaled = TercasSpacingCalculator.ScaleProportionally(100, 200, null!);
            scaled.Should().BeEmpty();
        }

        [Fact]
        public void Escala_preserva_proporcao_relativa()
        {
            // Valores nao-uniformes mantem proporcao quando escalados.
            var values = new List<double> { 100, 200, 300, 400 }; // total 1000
            var scaled = TercasSpacingCalculator.ScaleProportionally(1000, 500, values);

            scaled.Should().Equal(50, 100, 150, 200);
        }

        // ---------- RecalculateLastAsRemainder ----------

        [Fact]
        public void Remainder_ultimo_recalcula_pra_fechar_o_total()
        {
            var values = new List<double> { 100, 100, 100, 999 };
            var result = TercasSpacingCalculator.RecalculateLastAsRemainder(500, values);

            result.Should().Equal(100, 100, 100, 200);
        }

        [Fact]
        public void Remainder_input_nao_eh_mutado()
        {
            var input = new List<double> { 100, 100, 100, 999 };
            TercasSpacingCalculator.RecalculateLastAsRemainder(500, input);

            input.Should().Equal(100, 100, 100, 999); // intocado
        }

        [Fact]
        public void Remainder_soma_dos_outros_maior_que_total_clampa_em_zero()
        {
            var values = new List<double> { 300, 300, 300, 999 };
            var result = TercasSpacingCalculator.RecalculateLastAsRemainder(500, values);

            // soma_outros = 900, total = 500 → remainder seria -400, clampa 0
            result.Should().Equal(300, 300, 300, 0);
        }

        [Fact]
        public void Remainder_lista_de_1_elemento_ultimo_vira_total()
        {
            var values = new List<double> { 999 };
            var result = TercasSpacingCalculator.RecalculateLastAsRemainder(150, values);

            result.Should().Equal(150);
        }

        [Fact]
        public void Remainder_lista_vazia_retorna_vazia()
        {
            var result = TercasSpacingCalculator.RecalculateLastAsRemainder(500, new List<double>());
            result.Should().BeEmpty();
        }

        [Fact]
        public void Remainder_null_retorna_lista_vazia()
        {
            var result = TercasSpacingCalculator.RecalculateLastAsRemainder(500, null!);
            result.Should().BeEmpty();
        }

        // ---------- IsTotalMatching ----------

        [Fact]
        public void Total_bate_dentro_da_tolerancia_padrao_01cm()
        {
            var values = new List<double> { 150, 150, 150, 150 };
            TercasSpacingCalculator.IsTotalMatching(600, values).Should().BeTrue();
        }

        [Fact]
        public void Total_diff_005_cm_eh_match()
        {
            // diff 0.05 < tolerancia padrao 0.1 -> match
            var values = new List<double> { 100, 100, 100.05 };
            TercasSpacingCalculator.IsTotalMatching(300, values).Should().BeTrue();
        }

        [Fact]
        public void Total_diff_02_cm_NAO_eh_match()
        {
            var values = new List<double> { 100, 100, 100.2 };
            TercasSpacingCalculator.IsTotalMatching(300, values).Should().BeFalse();
        }

        [Fact]
        public void Total_values_null_compara_zero()
        {
            TercasSpacingCalculator.IsTotalMatching(0, null).Should().BeTrue();
            TercasSpacingCalculator.IsTotalMatching(10, null).Should().BeFalse();
        }

        [Fact]
        public void Total_tolerancia_custom_respeitada()
        {
            var values = new List<double> { 100, 100, 100 };
            // total real 300, expectativa 305, diff 5
            TercasSpacingCalculator.IsTotalMatching(305, values, toleranceCm: 10).Should().BeTrue();
            TercasSpacingCalculator.IsTotalMatching(305, values, toleranceCm: 1).Should().BeFalse();
        }

        [Fact]
        public void Total_lista_vazia_soma_zero()
        {
            TercasSpacingCalculator.IsTotalMatching(0, new List<double>()).Should().BeTrue();
            TercasSpacingCalculator.IsTotalMatching(100, new List<double>()).Should().BeFalse();
        }

        // ---------- CalcularParametrosPosicao ----------

        [Fact]
        public void Posicao_uniforme_5_tercas_em_vao_600cm()
        {
            // 5 tercas + 6 espacamentos iguais → params 1/6, 2/6, ..., 5/6
            double vaoFt = 600.0 * TercasSpacingCalculator.FtPerCm;
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 5, usarEspacamentoManual: false, espacamentosCm: null, vaoLineLengthFt: vaoFt);

            paras.Should().HaveCount(5);
            paras[0].Should().BeApproximately(1.0 / 6, 1e-9);
            paras[1].Should().BeApproximately(2.0 / 6, 1e-9);
            paras[2].Should().BeApproximately(3.0 / 6, 1e-9);
            paras[3].Should().BeApproximately(4.0 / 6, 1e-9);
            paras[4].Should().BeApproximately(5.0 / 6, 1e-9);
        }

        [Fact]
        public void Posicao_uniforme_quantidade_1_retorna_meio()
        {
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(1, false, null, 10.0);
            paras.Should().Equal(0.5);
        }

        [Fact]
        public void Posicao_quantidade_zero_retorna_vazia()
        {
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(0, false, null, 10.0);
            paras.Should().BeEmpty();
        }

        [Fact]
        public void Posicao_manual_5_espacamentos_iguais_resulta_em_uniforme()
        {
            // 4 tercas + 5 espacamentos de 100cm → vao = 500cm = 16.4042 ft.
            // Acumulado: 100, 200, 300, 400 cm.
            double vaoFt = 500.0 * TercasSpacingCalculator.FtPerCm;
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 4, usarEspacamentoManual: true,
                espacamentosCm: new List<double> { 100, 100, 100, 100, 100 },
                vaoLineLengthFt: vaoFt);

            paras.Should().HaveCount(4);
            paras[0].Should().BeApproximately(0.2, 1e-9);
            paras[1].Should().BeApproximately(0.4, 1e-9);
            paras[2].Should().BeApproximately(0.6, 1e-9);
            paras[3].Should().BeApproximately(0.8, 1e-9);
        }

        [Fact]
        public void Posicao_manual_espacamentos_nao_uniformes()
        {
            // 3 tercas + 4 espacamentos: 100, 200, 300, 200 cm → vao 800cm.
            // Acumulado: 100, 300, 600 cm.
            double vaoFt = 800.0 * TercasSpacingCalculator.FtPerCm;
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 3, usarEspacamentoManual: true,
                espacamentosCm: new List<double> { 100, 200, 300, 200 },
                vaoLineLengthFt: vaoFt);

            paras.Should().HaveCount(3);
            paras[0].Should().BeApproximately(100.0 / 800.0, 1e-9);
            paras[1].Should().BeApproximately(300.0 / 800.0, 1e-9);
            paras[2].Should().BeApproximately(600.0 / 800.0, 1e-9);
        }

        [Fact]
        public void Posicao_manual_count_invalido_cai_para_uniforme()
        {
            // 4 tercas mas só 3 espacamentos (deveria ter 5) → fallback uniforme.
            double vaoFt = 500.0 * TercasSpacingCalculator.FtPerCm;
            var parasManual = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 4, usarEspacamentoManual: true,
                espacamentosCm: new List<double> { 100, 100, 100 },
                vaoLineLengthFt: vaoFt);

            var parasUniforme = TercasSpacingCalculator.CalcularParametrosPosicao(
                4, false, null, vaoFt);

            parasManual.Should().Equal(parasUniforme);
        }

        [Fact]
        public void Posicao_manual_espacamentos_null_cai_para_uniforme()
        {
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 3, usarEspacamentoManual: true, espacamentosCm: null, vaoLineLengthFt: 10.0);

            paras.Should().HaveCount(3);
            paras[0].Should().BeApproximately(0.25, 1e-9);
            paras[1].Should().BeApproximately(0.50, 1e-9);
            paras[2].Should().BeApproximately(0.75, 1e-9);
        }

        [Fact]
        public void Posicao_manual_vao_zero_cai_para_uniforme()
        {
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 2, usarEspacamentoManual: true,
                espacamentosCm: new List<double> { 100, 100, 100 },
                vaoLineLengthFt: 0);

            paras.Should().Equal(1.0 / 3, 2.0 / 3);
        }

        [Fact]
        public void Posicao_manual_extrapolacao_clampada_em_um()
        {
            // Soma > vao → ultimo parametro acumulado deve ser clampado em 1.
            // 2 tercas, espacamentos [500, 500, 500] cm = 1500cm acc, mas vao = 800cm.
            // Acc: 500cm = 0.625, 1000cm clamp 1.0.
            double vaoFt = 800.0 * TercasSpacingCalculator.FtPerCm;
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                quantidade: 2, usarEspacamentoManual: true,
                espacamentosCm: new List<double> { 500, 500, 500 },
                vaoLineLengthFt: vaoFt);

            paras.Should().HaveCount(2);
            paras[0].Should().BeApproximately(0.625, 1e-9);
            paras[1].Should().Be(1.0);
        }

        [Fact]
        public void Posicao_manual_quantidade_negativa_retorna_vazia()
        {
            var paras = TercasSpacingCalculator.CalcularParametrosPosicao(
                -1, true, new List<double> { 100, 100 }, 10.0);
            paras.Should().BeEmpty();
        }

        [Fact]
        public void FtPerCm_constante_corresponde_ao_padrao_Revit()
        {
            // 1 ft = 30.48 cm → 1 cm = 1/30.48 ft
            TercasSpacingCalculator.FtPerCm.Should().BeApproximately(1.0 / 30.48, 1e-12);
        }
    }
}
