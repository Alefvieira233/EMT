using FluentAssertions;
using SteelBIM.Models.Bloco;
using Xunit;

namespace SteelBIM.Tests.Models.Bloco
{
    /// <summary>
    /// Cobertura dos DTOs da F3 Bloco Fundacao Armaduras (Onda 3).
    /// Onda 6 (Victor Final): valida defaults, round-trip e composicao das 9+ classes
    /// que formam o config completo do editor de armaduras de bloco.
    /// </summary>
    public class BlocoFundacaoRebarConfigTests
    {
        // ---------- BlocoFundacaoRebarConfig (top-level) ----------

        [Fact]
        public void BlocoFundacaoRebarConfig_Defaults()
        {
            var config = new BlocoFundacaoRebarConfig();
            config.LancarArmaduraInferior.Should().BeTrue();
            config.LancarArmaduraSuperior.Should().BeFalse();
            config.LancarArmaduraLateral.Should().BeFalse();
            config.LancarEstriboVertical.Should().BeFalse();
            config.LancarEstriboHorizontal.Should().BeFalse();
            config.LancarFaixaTransversal.Should().BeFalse();

            config.ArmaduraInferior.Should().NotBeNull();
            config.ArmaduraSuperior.Should().NotBeNull();
            config.ArmaduraLateral.Should().NotBeNull();
            config.EstriboVertical.Should().NotBeNull();
            config.EstriboHorizontal.Should().NotBeNull();
            config.FaixaTransversal.Should().NotBeNull();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarArmaduraInferior_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarArmaduraInferior = false };
            c.LancarArmaduraInferior.Should().BeFalse();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarArmaduraSuperior_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarArmaduraSuperior = true };
            c.LancarArmaduraSuperior.Should().BeTrue();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarArmaduraLateral_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarArmaduraLateral = true };
            c.LancarArmaduraLateral.Should().BeTrue();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarEstriboVertical_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarEstriboVertical = true };
            c.LancarEstriboVertical.Should().BeTrue();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarEstriboHorizontal_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarEstriboHorizontal = true };
            c.LancarEstriboHorizontal.Should().BeTrue();
        }

        [Fact]
        public void BlocoFundacaoRebarConfig_LancarFaixaTransversal_RoundTrip()
        {
            var c = new BlocoFundacaoRebarConfig { LancarFaixaTransversal = true };
            c.LancarFaixaTransversal.Should().BeTrue();
        }

        // ---------- Enums ----------

        [Theory]
        [InlineData(BlocoDirecao.ApenasX, 0)]
        [InlineData(BlocoDirecao.ApenasY, 1)]
        [InlineData(BlocoDirecao.AmbosXeY, 2)]
        public void BlocoDirecao_NumericValues(BlocoDirecao d, int expected)
        {
            ((int)d).Should().Be(expected);
        }

        [Theory]
        [InlineData(BlocoAnguloGancho.Graus90, 90)]
        [InlineData(BlocoAnguloGancho.Graus135, 135)]
        [InlineData(BlocoAnguloGancho.Graus180, 180)]
        public void BlocoAnguloGancho_NumericValues(BlocoAnguloGancho a, int expected)
        {
            ((int)a).Should().Be(expected);
        }

        [Theory]
        [InlineData(BlocoModoQuantidade.PorQuantidade, 0)]
        [InlineData(BlocoModoQuantidade.PorEspacamento, 1)]
        public void BlocoModoQuantidade_NumericValues(BlocoModoQuantidade m, int expected)
        {
            ((int)m).Should().Be(expected);
        }

        // ---------- BlocoRebarBendConfig ----------

        [Fact]
        public void BlocoRebarBendConfig_Defaults()
        {
            var b = new BlocoRebarBendConfig();
            b.HaDobraInicial.Should().BeFalse();
            b.HaDobraFinal.Should().BeFalse();
            b.ComprimentoDobraInicialCm.Should().Be(10.0);
            b.ComprimentoDobraFinalCm.Should().Be(10.0);
            b.AnguloGancho.Should().Be(BlocoAnguloGancho.Graus90);
            b.DobraInicialParaCima.Should().BeTrue();
            b.DobraFinalParaCima.Should().BeTrue();
        }

        [Fact]
        public void BlocoRebarBendConfig_AllPropertiesRoundTrip()
        {
            var b = new BlocoRebarBendConfig
            {
                HaDobraInicial = true,
                HaDobraFinal = true,
                ComprimentoDobraInicialCm = 15.5,
                ComprimentoDobraFinalCm = 20.0,
                AnguloGancho = BlocoAnguloGancho.Graus135,
                DobraInicialParaCima = false,
                DobraFinalParaCima = false
            };

            b.HaDobraInicial.Should().BeTrue();
            b.HaDobraFinal.Should().BeTrue();
            b.ComprimentoDobraInicialCm.Should().Be(15.5);
            b.ComprimentoDobraFinalCm.Should().Be(20.0);
            b.AnguloGancho.Should().Be(BlocoAnguloGancho.Graus135);
            b.DobraInicialParaCima.Should().BeFalse();
            b.DobraFinalParaCima.Should().BeFalse();
        }

        // ---------- BlocoBarraDirecaoConfig ----------

        [Fact]
        public void BlocoBarraDirecaoConfig_Defaults()
        {
            var b = new BlocoBarraDirecaoConfig();
            b.Ativo.Should().BeTrue();
            b.BarTypeName.Should().Be(string.Empty);
            b.CobrimentoCm.Should().Be(5.0);
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorQuantidade);
            b.Quantidade.Should().Be(4);
            b.EspacamentoCm.Should().Be(15.0);
            b.OffsetZCm.Should().Be(0.0);
            b.Dobra.Should().NotBeNull();
        }

        [Fact]
        public void BlocoBarraDirecaoConfig_AllPropertiesRoundTrip()
        {
            var b = new BlocoBarraDirecaoConfig
            {
                Ativo = false,
                BarTypeName = "10 mm",
                CobrimentoCm = 3.0,
                ModoQuantidade = BlocoModoQuantidade.PorEspacamento,
                Quantidade = 8,
                EspacamentoCm = 10.0,
                OffsetZCm = 2.5
            };
            b.Ativo.Should().BeFalse();
            b.BarTypeName.Should().Be("10 mm");
            b.CobrimentoCm.Should().Be(3.0);
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorEspacamento);
            b.Quantidade.Should().Be(8);
            b.EspacamentoCm.Should().Be(10.0);
            b.OffsetZCm.Should().Be(2.5);
        }

        // ---------- BlocoBarraInferiorConfig / BlocoBarraSuperiorConfig ----------

        [Fact]
        public void BlocoBarraInferiorConfig_NestedBarsXY_Defaults()
        {
            var b = new BlocoBarraInferiorConfig();
            b.BarsX.Should().NotBeNull();
            b.BarsY.Should().NotBeNull();
            b.BarsX.Ativo.Should().BeTrue();
            b.BarsY.Ativo.Should().BeTrue();
        }

        [Fact]
        public void BlocoBarraSuperiorConfig_NestedBarsXY_Defaults()
        {
            var b = new BlocoBarraSuperiorConfig();
            b.BarsX.Should().NotBeNull();
            b.BarsY.Should().NotBeNull();
        }

        // ---------- BlocoArmaduraLateralConfig ----------

        [Fact]
        public void BlocoArmaduraLateralConfig_Defaults()
        {
            var b = new BlocoArmaduraLateralConfig();
            b.BarTypeName.Should().Be(string.Empty);
            b.CobrimentoCm.Should().Be(5.0);
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorQuantidade);
            b.Quantidade.Should().Be(2);
            b.EspacamentoCm.Should().Be(20.0);
            b.LancarNasFacesX.Should().BeTrue();
            b.LancarNasFacesY.Should().BeTrue();
            b.Dobra.Should().NotBeNull();
        }

        [Fact]
        public void BlocoArmaduraLateralConfig_FaceFlags_RoundTrip()
        {
            var b = new BlocoArmaduraLateralConfig
            {
                LancarNasFacesX = false,
                LancarNasFacesY = false
            };
            b.LancarNasFacesX.Should().BeFalse();
            b.LancarNasFacesY.Should().BeFalse();
        }

        // ---------- BlocoEstriboVerticalDirConfig ----------

        [Fact]
        public void BlocoEstriboVerticalDirConfig_Defaults()
        {
            var b = new BlocoEstriboVerticalDirConfig();
            b.Ativo.Should().BeTrue();
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorQuantidade);
            b.Quantidade.Should().Be(2);
            b.EspacamentoCm.Should().Be(20.0);
        }

        // ---------- BlocoEstriboVerticalConfig ----------

        [Fact]
        public void BlocoEstriboVerticalConfig_Defaults()
        {
            var b = new BlocoEstriboVerticalConfig();
            b.BarTypeName.Should().Be(string.Empty);
            b.CobrimentoCm.Should().Be(5.0);
            b.DirX.Should().NotBeNull();
            b.DirY.Should().NotBeNull();
            b.AnguloGarras.Should().Be(BlocoAnguloGancho.Graus135);
        }

        [Fact]
        public void BlocoEstriboVerticalConfig_AnguloGarras_RoundTrip()
        {
            var b = new BlocoEstriboVerticalConfig { AnguloGarras = BlocoAnguloGancho.Graus180 };
            b.AnguloGarras.Should().Be(BlocoAnguloGancho.Graus180);
        }

        // ---------- BlocoEstriboHorizontalConfig ----------

        [Fact]
        public void BlocoEstriboHorizontalConfig_Defaults()
        {
            var b = new BlocoEstriboHorizontalConfig();
            b.BarTypeName.Should().Be(string.Empty);
            b.CobrimentoCm.Should().Be(5.0);
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorQuantidade);
            b.Quantidade.Should().Be(2);
            b.EspacamentoCm.Should().Be(20.0);
            b.AnguloGarras.Should().Be(BlocoAnguloGancho.Graus135);
        }

        [Fact]
        public void BlocoEstriboHorizontalConfig_AllPropertiesRoundTrip()
        {
            var b = new BlocoEstriboHorizontalConfig
            {
                BarTypeName = "6.3 mm",
                CobrimentoCm = 4.5,
                ModoQuantidade = BlocoModoQuantidade.PorEspacamento,
                Quantidade = 3,
                EspacamentoCm = 25.0,
                AnguloGarras = BlocoAnguloGancho.Graus180
            };
            b.BarTypeName.Should().Be("6.3 mm");
            b.CobrimentoCm.Should().Be(4.5);
            b.ModoQuantidade.Should().Be(BlocoModoQuantidade.PorEspacamento);
            b.Quantidade.Should().Be(3);
            b.EspacamentoCm.Should().Be(25.0);
            b.AnguloGarras.Should().Be(BlocoAnguloGancho.Graus180);
        }

        // ---------- BlocoFaixaTransversalConfig ----------

        [Fact]
        public void BlocoFaixaTransversalConfig_Defaults()
        {
            var b = new BlocoFaixaTransversalConfig();
            b.BarTypeName.Should().Be(string.Empty);
            b.CobrimentoCm.Should().Be(5.0);
            b.EspacamentoCm.Should().Be(15.0);
            b.PosicaoZCm.Should().Be(10.0);
            b.Direcao.Should().Be(BlocoDirecao.AmbosXeY);
            b.Dobra.Should().NotBeNull();
        }

        [Fact]
        public void BlocoFaixaTransversalConfig_Direcao_RoundTrip()
        {
            var b = new BlocoFaixaTransversalConfig { Direcao = BlocoDirecao.ApenasX };
            b.Direcao.Should().Be(BlocoDirecao.ApenasX);

            b.Direcao = BlocoDirecao.ApenasY;
            b.Direcao.Should().Be(BlocoDirecao.ApenasY);
        }

        [Fact]
        public void BlocoFaixaTransversalConfig_NumericProperties_RoundTrip()
        {
            var b = new BlocoFaixaTransversalConfig
            {
                BarTypeName = "8 mm",
                CobrimentoCm = 4.0,
                EspacamentoCm = 18.0,
                PosicaoZCm = 5.0
            };
            b.BarTypeName.Should().Be("8 mm");
            b.CobrimentoCm.Should().Be(4.0);
            b.EspacamentoCm.Should().Be(18.0);
            b.PosicaoZCm.Should().Be(5.0);
        }
    }
}
