#nullable enable
using System.Collections.Generic;

namespace SteelBIM.Models.Bloco
{
    public enum BlocoDirecao
    {
        ApenasX = 0,
        ApenasY = 1,
        AmbosXeY = 2
    }

    public enum BlocoAnguloGancho
    {
        Graus90 = 90,
        Graus135 = 135,
        Graus180 = 180
    }

    public enum BlocoModoQuantidade
    {
        PorQuantidade = 0,
        PorEspacamento = 1
    }

    public sealed class BlocoRebarBendConfig
    {
        public bool HaDobraInicial { get; set; }
        public bool HaDobraFinal { get; set; }
        public double ComprimentoDobraInicialCm { get; set; } = 10.0;
        public double ComprimentoDobraFinalCm { get; set; } = 10.0;
        public BlocoAnguloGancho AnguloGancho { get; set; } = BlocoAnguloGancho.Graus90;
        public bool DobraInicialParaCima { get; set; } = true;
        public bool DobraFinalParaCima { get; set; } = true;
    }

    public sealed class BlocoBarraDirecaoConfig
    {
        public bool Ativo { get; set; } = true;
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public BlocoModoQuantidade ModoQuantidade { get; set; } = BlocoModoQuantidade.PorQuantidade;
        public int Quantidade { get; set; } = 4;
        public double EspacamentoCm { get; set; } = 15.0;
        public double OffsetZCm { get; set; }
        public BlocoRebarBendConfig Dobra { get; } = new BlocoRebarBendConfig();
    }

    public sealed class BlocoBarraInferiorConfig
    {
        public BlocoBarraDirecaoConfig BarsX { get; } = new BlocoBarraDirecaoConfig();
        public BlocoBarraDirecaoConfig BarsY { get; } = new BlocoBarraDirecaoConfig();
    }

    public sealed class BlocoBarraSuperiorConfig
    {
        public BlocoBarraDirecaoConfig BarsX { get; } = new BlocoBarraDirecaoConfig();
        public BlocoBarraDirecaoConfig BarsY { get; } = new BlocoBarraDirecaoConfig();
    }

    public sealed class BlocoArmaduraLateralConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public BlocoModoQuantidade ModoQuantidade { get; set; } = BlocoModoQuantidade.PorQuantidade;
        public int Quantidade { get; set; } = 2;
        public double EspacamentoCm { get; set; } = 20.0;
        // LancarNasFacesX: barras nas faces minX e maxX, correndo ao longo de Y
        public bool LancarNasFacesX { get; set; } = true;
        // LancarNasFacesY: barras nas faces minY e maxY, correndo ao longo de X
        public bool LancarNasFacesY { get; set; } = true;
        public BlocoRebarBendConfig Dobra { get; } = new BlocoRebarBendConfig();
    }

    public sealed class BlocoEstriboVerticalDirConfig
    {
        public bool Ativo { get; set; } = true;
        public BlocoModoQuantidade ModoQuantidade { get; set; } = BlocoModoQuantidade.PorQuantidade;
        public int Quantidade { get; set; } = 2;
        public double EspacamentoCm { get; set; } = 20.0;
    }

    public sealed class BlocoEstriboVerticalConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        // DirX: quadros no plano YZ, distribuidos ao longo de X
        public BlocoEstriboVerticalDirConfig DirX { get; } = new BlocoEstriboVerticalDirConfig();
        // DirY: quadros no plano XZ, distribuidos ao longo de Y
        public BlocoEstriboVerticalDirConfig DirY { get; } = new BlocoEstriboVerticalDirConfig();
        public BlocoAnguloGancho AnguloGarras { get; set; } = BlocoAnguloGancho.Graus135;
    }

    public sealed class BlocoEstriboHorizontalConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public BlocoModoQuantidade ModoQuantidade { get; set; } = BlocoModoQuantidade.PorQuantidade;
        public int Quantidade { get; set; } = 2;
        public double EspacamentoCm { get; set; } = 20.0;
        public BlocoAnguloGancho AnguloGarras { get; set; } = BlocoAnguloGancho.Graus135;
    }

    public sealed class BlocoFaixaTransversalConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public double EspacamentoCm { get; set; } = 15.0;
        public double PosicaoZCm { get; set; } = 10.0;
        public BlocoDirecao Direcao { get; set; } = BlocoDirecao.AmbosXeY;
        public BlocoRebarBendConfig Dobra { get; } = new BlocoRebarBendConfig();
    }

    public sealed class BlocoFundacaoRebarConfig
    {
        public bool LancarArmaduraInferior { get; set; } = true;
        public bool LancarArmaduraSuperior { get; set; }
        public bool LancarArmaduraLateral { get; set; }
        public bool LancarEstriboVertical { get; set; }
        public bool LancarEstriboHorizontal { get; set; }
        public bool LancarFaixaTransversal { get; set; }

        public BlocoBarraInferiorConfig ArmaduraInferior { get; } = new BlocoBarraInferiorConfig();
        public BlocoBarraSuperiorConfig ArmaduraSuperior { get; } = new BlocoBarraSuperiorConfig();
        public BlocoArmaduraLateralConfig ArmaduraLateral { get; } = new BlocoArmaduraLateralConfig();
        public BlocoEstriboVerticalConfig EstriboVertical { get; } = new BlocoEstriboVerticalConfig();
        public BlocoEstriboHorizontalConfig EstriboHorizontal { get; } = new BlocoEstriboHorizontalConfig();
        public BlocoFaixaTransversalConfig FaixaTransversal { get; } = new BlocoFaixaTransversalConfig();
    }
}
