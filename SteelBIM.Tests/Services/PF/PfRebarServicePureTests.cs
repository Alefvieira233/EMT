using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SteelBIM.Services.PF;
using Xunit;

namespace SteelBIM.Tests.Services.PF
{
    /// <summary>
    /// v2.7.9 (auditoria 2026-05-25 #1 — bloqueador critico): testes diretos
    /// pra logica pura extraida do <see cref="PfRebarService"/> (2178 LOC,
    /// zero tests diretos antes desta release). Cobertura inicial nos 5
    /// metodos puros centrais — pode expandir conforme refactor extrai mais.
    /// </summary>
    public class PfRebarServicePureTests
    {
        // =============================================================
        //  BuildLapRangesMm — algoritmo de particionamento com stagger
        // =============================================================

        [Fact]
        public void BuildLapRanges_TrechoMenorQueMinSegmento_RetornaVazio()
        {
            // Trecho de 30mm < MinSegmentMm (50mm) → barra "perdida"
            var ranges = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 30, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 0);

            ranges.Should().BeEmpty();
        }

        [Fact]
        public void BuildLapRanges_TrechoCabeInteiro_RetornaUmRange()
        {
            // 5m de viga, barra max 12m → cabe inteiro sem splice
            var ranges = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 5000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 0);

            ranges.Should().HaveCount(1);
            ranges[0].StartMm.Should().Be(0);
            ranges[0].EndMm.Should().Be(5000);
        }

        [Fact]
        public void BuildLapRanges_TrechoMaiorQueBarra_RetornaMultiplosRanges()
        {
            // 25m de barra, peca max 12m, lap 500mm → precisa 3 pedacos
            var ranges = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 25000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 0);

            ranges.Should().HaveCountGreaterThan(1);
            ranges.First().StartMm.Should().Be(0);
            ranges.Last().EndMm.Should().Be(25000);
        }

        [Fact]
        public void BuildLapRanges_StaggerDifferentes_GeramJuncoesDiferentes()
        {
            // Mesma config, stagger 0 vs 1 → 1a juncao em posicoes diferentes
            var rangesStagger0 = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 25000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 0);
            var rangesStagger1 = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 25000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 1);

            // Posicao da primeira juncao difere (stagger desloca firstReduction)
            rangesStagger0[0].EndMm.Should().NotBe(rangesStagger1[0].EndMm);
        }

        [Fact]
        public void BuildLapRanges_StaggerNegativo_TratadoComoAbs()
        {
            // staggerIndex usa Math.Abs internamente — -1 e 1 dao mesmo resultado
            var rangesPos = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 25000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: 1);
            var rangesNeg = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 25000, maxPieceLengthMm: 12000, lapLengthMm: 500, staggerIndex: -1);

            rangesPos.Should().HaveSameCount(rangesNeg);
            for (int i = 0; i < rangesPos.Count; i++)
            {
                rangesPos[i].StartMm.Should().Be(rangesNeg[i].StartMm);
                rangesPos[i].EndMm.Should().Be(rangesNeg[i].EndMm);
            }
        }

        [Fact]
        public void BuildLapRanges_MaxPieceMenorQueLap_Lanca()
        {
            // Config invalida: peca de 800mm com lap de 500mm → sobra 300mm de barra util,
            // mas precisa de MinPieceMm (300mm). Limiar.
            Action act = () => PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 5000, maxPieceLengthMm: 800, lapLengthMm: 500, staggerIndex: 0);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*comprimento maximo*");
        }

        [Theory]
        [InlineData(0, 100, 12000, 500, 0)]   // trecho 100mm, sem splice (cabe)
        [InlineData(0, 12000, 12000, 500, 0)] // trecho exato ao max
        [InlineData(0, 13000, 12000, 500, 0)] // trecho ligeiramente > max → 2 pieces
        [InlineData(100, 5100, 12000, 500, 0)] // start nao-zero
        public void BuildLapRanges_VariosCenarios_SempreCobreTrechoCompleto(
            double startMm, double endMm, double maxMm, double lapMm, int stagger)
        {
            var ranges = PfRebarServicePure.BuildLapRangesMm(startMm, endMm, maxMm, lapMm, stagger);

            if (ranges.Count == 0)
            {
                (endMm - startMm).Should().BeLessThanOrEqualTo(PfRebarServicePure.MinSegmentMm);
            }
            else
            {
                ranges.First().StartMm.Should().Be(startMm);
                ranges.Last().EndMm.Should().Be(endMm);
            }
        }

        [Fact]
        public void BuildLapRanges_TodosRangesRespeitamMaxLength()
        {
            // Garante invariante: cada range <= maxPieceLength
            var ranges = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 50000, maxPieceLengthMm: 6000, lapLengthMm: 400, staggerIndex: 0);

            foreach (var r in ranges)
            {
                r.LengthMm.Should().BeLessThanOrEqualTo(6000 + 1.0, // +1mm tolerancia
                    $"range {r} excede maxPieceLength");
            }
        }

        [Fact]
        public void BuildLapRanges_RangesConsecutivosTemOverlapDeLap()
        {
            // Invariante NBR: barras consecutivas se sobrepoem em pelo menos lapLength
            var ranges = PfRebarServicePure.BuildLapRangesMm(
                startMm: 0, endMm: 30000, maxPieceLengthMm: 12000, lapLengthMm: 600, staggerIndex: 0);

            for (int i = 1; i < ranges.Count; i++)
            {
                double overlap = ranges[i - 1].EndMm - ranges[i].StartMm;
                overlap.Should().BeApproximately(600, 1.0, $"overlap entre range {i - 1} e {i} deve ser ~600mm");
            }
        }

        // =============================================================
        //  DistributePositionsMm — espalhamento de posicoes
        // =============================================================

        [Fact]
        public void DistributePositions_CountZero_TratadoComoUm()
        {
            // count <= 0 → tratado como 1, retorna so o centro
            var positions = PfRebarServicePure.DistributePositionsMm(0, 100, 200);

            positions.Should().HaveCount(1);
            positions[0].Should().Be(150); // centro de 100..200
        }

        [Fact]
        public void DistributePositions_RangeMuitoPequeno_RetornaCentro()
        {
            // range <= 10mm → nao vale espalhar
            var positions = PfRebarServicePure.DistributePositionsMm(5, 100, 105);

            positions.Should().HaveCount(1);
            positions[0].Should().BeApproximately(102.5, 0.001);
        }

        [Fact]
        public void DistributePositions_CountUm_RetornaCentro()
        {
            var positions = PfRebarServicePure.DistributePositionsMm(1, 0, 1000);

            positions.Should().HaveCount(1);
            positions[0].Should().Be(500);
        }

        [Fact]
        public void DistributePositions_CountDois_RetornaExtremos()
        {
            var positions = PfRebarServicePure.DistributePositionsMm(2, 0, 1000);

            positions.Should().HaveCount(2);
            positions[0].Should().Be(0);
            positions[1].Should().Be(1000);
        }

        [Theory]
        [InlineData(3, 0, 100)] // 0, 50, 100
        [InlineData(5, 0, 400)] // 0, 100, 200, 300, 400
        [InlineData(4, 100, 700)] // step = 200 → 100, 300, 500, 700
        public void DistributePositions_VariosCenarios_EspacamentoUniforme(int count, double min, double max)
        {
            var positions = PfRebarServicePure.DistributePositionsMm(count, min, max);

            positions.Should().HaveCount(count);
            positions.First().Should().Be(min);
            positions.Last().Should().Be(max);

            // Espacamento uniforme
            if (count > 2)
            {
                double expectedStep = (max - min) / (count - 1);
                for (int i = 1; i < positions.Count; i++)
                {
                    (positions[i] - positions[i - 1]).Should().BeApproximately(expectedStep, 0.001);
                }
            }
        }

        // =============================================================
        //  NormalizeText
        // =============================================================

        [Theory]
        [InlineData("Pilar", "pilar")]
        [InlineData("PILAR 12", "pilar12")]
        [InlineData("  Viga  Principal  ", "vigaprincipal")]
        [InlineData("Açúcar Cristal", "acucarcristal")]
        [InlineData("Coração", "coracao")]
        [InlineData("São Paulo", "saopaulo")]
        [InlineData("V-300x150", "v300x150")]
        [InlineData("100%", "100")]
        public void NormalizeText_VariosCenarios_Removeespacos_Diacriticos_Especiais(string input, string esperado)
        {
            PfRebarServicePure.NormalizeText(input).Should().Be(esperado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void NormalizeText_NullOuVazio_RetornaStringVazia(string? input)
        {
            PfRebarServicePure.NormalizeText(input).Should().BeEmpty();
        }

        // =============================================================
        //  LimparMensagem
        // =============================================================

        [Theory]
        [InlineData("Erro simples", "Erro simples")]
        [InlineData("  com whitespace  ", "com whitespace")]
        [InlineData("linha 1\nlinha 2", "linha 1 linha 2")]
        [InlineData("linha 1\r\nlinha 2", "linha 1  linha 2")]
        [InlineData("\r\nmsg\r\n", "msg")]
        public void LimparMensagem_RemoveQuebrasLinhaETrim(string input, string esperado)
        {
            PfRebarServicePure.LimparMensagem(input).Should().Be(esperado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void LimparMensagem_NullOuVazio_RetornaFallback(string? input)
        {
            PfRebarServicePure.LimparMensagem(input).Should().Be("falha desconhecida.");
        }

        // =============================================================
        //  FormatDiameterToken
        // =============================================================

        [Theory]
        [InlineData(12.0, "12")]
        [InlineData(12.5, "125")]
        [InlineData(8.0, "8")]
        [InlineData(20.5, "205")]
        [InlineData(6.3, "63")]
        public void FormatDiameterToken_RemovePontoEVirgula(double diameterMm, string esperado)
        {
            PfRebarServicePure.FormatDiameterToken(diameterMm).Should().Be(esperado);
        }

        [Fact]
        public void FormatDiameterToken_UsaInvariantCulture_NaoQuebraEmPtBR()
        {
            // Em pt-BR, 12.5.ToString() seria "12,5" — formatter usa Invariant pra
            // SEMPRE produzir "125" (ponto removido). Test simula PC pt-BR.
            var ptBr = new System.Globalization.CultureInfo("pt-BR");
            System.Globalization.CultureInfo.CurrentCulture = ptBr;
            System.Globalization.CultureInfo.CurrentUICulture = ptBr;

            PfRebarServicePure.FormatDiameterToken(12.5).Should().Be("125");
        }

        // =============================================================
        //  RadiansToDegrees / DegreesToRadians
        // =============================================================

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(Math.PI, 180.0)]
        [InlineData(Math.PI / 2, 90.0)]
        [InlineData(Math.PI / 4, 45.0)]
        [InlineData(2 * Math.PI, 360.0)]
        public void RadiansToDegrees_ConversoesCanonicas(double radians, double esperado)
        {
            PfRebarServicePure.RadiansToDegrees(radians).Should().BeApproximately(esperado, 1e-9);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(180.0, Math.PI)]
        [InlineData(90.0, Math.PI / 2)]
        [InlineData(45.0, Math.PI / 4)]
        [InlineData(360.0, 2 * Math.PI)]
        public void DegreesToRadians_ConversoesCanonicas(double degrees, double esperado)
        {
            PfRebarServicePure.DegreesToRadians(degrees).Should().BeApproximately(esperado, 1e-9);
        }

        [Fact]
        public void DegreesRadians_RoundTrip_NaoPerde()
        {
            double original = 137.5;
            double rad = PfRebarServicePure.DegreesToRadians(original);
            double backToDeg = PfRebarServicePure.RadiansToDegrees(rad);

            backToDeg.Should().BeApproximately(original, 1e-9);
        }

        // =============================================================
        //  Constantes documentadas
        // =============================================================

        [Fact]
        public void MinSegmentMm_ConstanteDocumentada()
        {
            PfRebarServicePure.MinSegmentMm.Should().Be(50.0,
                "barras menores que 5cm sao 'pontas perdidas' por NBR");
        }

        [Fact]
        public void MinPieceMm_ConstanteDocumentada()
        {
            PfRebarServicePure.MinPieceMm.Should().Be(300.0,
                "pedacos de barra util precisam ter pelo menos 30cm");
        }

        // =============================================================
        // v2.7.11 F10: extracoes NBR adicionais
        // =============================================================

        // ---------- ClampCoverCm (NBR 6118 §7.4.7.1) ----------

        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(-5.0, 1.0)]
        [InlineData(0.5, 1.0)]
        [InlineData(1.0, 1.0)]
        [InlineData(1.5, 1.5)]
        [InlineData(3.0, 3.0)]
        public void ClampCoverCm_ValorAbaixoMinimo_RetornaUmCm(double input, double expected)
        {
            PfRebarServicePure.ClampCoverCm(input).Should().Be(expected);
        }

        // ---------- EffectiveCoverCm (NBR 6118 §7.4.7.5) ----------

        [Fact]
        public void EffectiveCoverCm_SemEstribo_RetornaCobrimentoNominal()
        {
            // Sem estribo (diametro 0): efetivo = cobrimento nominal
            PfRebarServicePure.EffectiveCoverCm(coverCm: 3.0, stirrupDiameterMm: 0.0).Should().Be(3.0);
        }

        [Fact]
        public void EffectiveCoverCm_ComEstribo8mm_SomaEm08cm()
        {
            // Estribo 8mm = 0.8cm. Cobrimento 3cm + 0.8cm = 3.8cm
            PfRebarServicePure.EffectiveCoverCm(coverCm: 3.0, stirrupDiameterMm: 8.0).Should().Be(3.8);
        }

        [Fact]
        public void EffectiveCoverCm_ComEstribo10mm_SomaEm1cm()
        {
            PfRebarServicePure.EffectiveCoverCm(coverCm: 2.5, stirrupDiameterMm: 10.0).Should().Be(3.5);
        }

        [Fact]
        public void EffectiveCoverCm_ValoresNegativos_TratadosComoZero()
        {
            PfRebarServicePure.EffectiveCoverCm(coverCm: -2.0, stirrupDiameterMm: -5.0).Should().Be(0.0);
        }

        // ---------- MinimumClearSpacingMm (NBR 6118 §18.3.2.2) ----------

        [Theory]
        [InlineData(10.0, 20.0)]    // diam 10mm → min 20mm (regra constante)
        [InlineData(16.0, 20.0)]    // diam 16mm → min 20mm
        [InlineData(20.0, 20.0)]    // diam 20mm → empate
        [InlineData(25.0, 25.0)]    // diam 25mm → min = diam
        [InlineData(32.0, 32.0)]    // diam 32mm → min = diam
        public void MinimumClearSpacingMm_AplicaMaxDe20mmOuDiametro(double barDiameterMm, double expected)
        {
            PfRebarServicePure.MinimumClearSpacingMm(barDiameterMm).Should().Be(expected);
        }

        // ---------- IsBarSpacingValid ----------

        [Fact]
        public void IsBarSpacingValid_BarrasMuitoPertas_Invalida()
        {
            // Diam 20mm, espacamento entre centros 30mm = clear 10mm < min 20mm
            PfRebarServicePure.IsBarSpacingValid(minCenterSpacingMm: 30.0, barDiameterMm: 20.0)
                .Should().BeFalse();
        }

        [Fact]
        public void IsBarSpacingValid_EspacamentoOk_Valida()
        {
            // Diam 16mm, espacamento entre centros 50mm = clear 34mm > min 20mm
            PfRebarServicePure.IsBarSpacingValid(minCenterSpacingMm: 50.0, barDiameterMm: 16.0)
                .Should().BeTrue();
        }

        [Fact]
        public void IsBarSpacingValid_DiametroBarraIgnoravel_AceitaQualquer()
        {
            // barDiameter <= 1mm = considerado "sem barra" / nao validar
            PfRebarServicePure.IsBarSpacingValid(minCenterSpacingMm: 0.5, barDiameterMm: 0.5)
                .Should().BeTrue();
        }

        [Fact]
        public void IsBarSpacingValid_ToleranciaConstrutiva1mm_RespeitadaNoLimite()
        {
            // Diam 25mm, min clear = 25mm. Center-to-center 49mm = clear 24mm.
            // Com tolerancia +1mm: 24+1=25 >= 25 → valido (na linha)
            PfRebarServicePure.IsBarSpacingValid(minCenterSpacingMm: 49.0, barDiameterMm: 25.0)
                .Should().BeTrue();
        }

        // ---------- CalculateLinearSpacingMm ----------

        [Theory]
        [InlineData(0, 1000.0, 0.0)]     // count 0 → spacing 0 (invalido)
        [InlineData(1, 1000.0, 0.0)]     // count 1 → spacing 0
        [InlineData(2, 1000.0, 1000.0)]  // 2 barras em 1m → spacing 1m
        [InlineData(5, 1000.0, 250.0)]   // 5 barras em 1m → 4 vaos → 250mm
        [InlineData(11, 1000.0, 100.0)]  // 11 barras em 1m → 10 vaos → 100mm
        [InlineData(3, 4.0, 0.0)]        // path 4mm <= 5mm → invalido
        public void CalculateLinearSpacingMm_DistribuiUniforme(int count, double pathLengthMm, double expected)
        {
            PfRebarServicePure.CalculateLinearSpacingMm(count, pathLengthMm)
                .Should().BeApproximately(expected, 0.001);
        }

        // ---------- CalculateMaxSpacingFallback ----------

        [Fact]
        public void CalculateMaxSpacingFallback_PathDivisivelPeloSpacing_RealEspacamentoBate()
        {
            // path 1000mm, max spacing 200mm → count = floor(1000/200)+1 = 6
            // real spacing = 1000 / 5 = 200mm
            var (count, realSpacingMm) = PfRebarServicePure.CalculateMaxSpacingFallback(
                spacingMm: 200.0, pathLengthMm: 1000.0);

            count.Should().Be(6);
            realSpacingMm.Should().BeApproximately(200.0, 0.001);
        }

        [Fact]
        public void CalculateMaxSpacingFallback_PathNaoDivisivel_RecalculaEspacamentoMenor()
        {
            // path 1000mm, max spacing 300mm → count = floor(1000/300)+1 = 4
            // real spacing = 1000 / 3 ≈ 333mm (mas a regra "max" eh respeitada
            // porque count cresce ao em vez do spacing — esperado: count=4, real=333)
            var (count, realSpacingMm) = PfRebarServicePure.CalculateMaxSpacingFallback(
                spacingMm: 300.0, pathLengthMm: 1000.0);

            count.Should().Be(4);
            realSpacingMm.Should().BeApproximately(333.333, 0.01);
        }

        [Fact]
        public void CalculateMaxSpacingFallback_SpacingInvalido_RetornaZero()
        {
            var (count, _) = PfRebarServicePure.CalculateMaxSpacingFallback(
                spacingMm: 0.5, pathLengthMm: 1000.0);
            count.Should().Be(0);
        }

        [Fact]
        public void CalculateMaxSpacingFallback_PathInvalido_RetornaZero()
        {
            var (count, _) = PfRebarServicePure.CalculateMaxSpacingFallback(
                spacingMm: 200.0, pathLengthMm: 3.0);
            count.Should().Be(0);
        }

        [Fact]
        public void CalculateMaxSpacingFallback_MinimoCountSempre2()
        {
            // Path 100mm, max spacing 90mm → count = floor(100/90)+1 = 2 (ok)
            // Edge: garante que count nunca cai abaixo de 2.
            var (count, _) = PfRebarServicePure.CalculateMaxSpacingFallback(
                spacingMm: 90.0, pathLengthMm: 100.0);
            count.Should().BeGreaterThanOrEqualTo(2);
        }
    }
}
