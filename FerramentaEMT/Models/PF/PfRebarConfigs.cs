namespace FerramentaEMT.Models.PF
{
    public enum PfBeamBarEndMode
    {
        Reta = 0,
        DobraInterna = 1
    }

    public sealed class PfColumnStirrupsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public double EspacamentoInferiorCm { get; set; } = 12.0;
        public double EspacamentoCentralCm { get; set; } = 20.0;
        public double EspacamentoSuperiorCm { get; set; } = 12.0;
        public double AlturaZonaExtremidadeCm { get; set; } = 60.0;
    }

    public sealed class PfColumnBarsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public int QuantidadeLargura { get; set; } = 2;
        public int QuantidadeProfundidade { get; set; } = 2;
    }

    public sealed class PfBeamStirrupsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public double EspacamentoApoioCm { get; set; } = 12.0;
        public double EspacamentoCentralCm { get; set; } = 20.0;
        public double ComprimentoZonaApoioCm { get; set; } = 60.0;
    }

    public sealed class PfBeamBarsConfig
    {
        public string BarTypeSuperiorName { get; set; } = string.Empty;
        public string BarTypeInferiorName { get; set; } = string.Empty;
        public string BarTypeLateralName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public int QuantidadeSuperior { get; set; } = 2;
        public int QuantidadeInferior { get; set; } = 2;
        public int QuantidadeLateral { get; set; } = 0;
        public double ComprimentoGanchoCm { get; set; } = 10.0;
        public PfBeamBarEndMode ModoPonta { get; set; } = PfBeamBarEndMode.DobraInterna;
    }

    public sealed class PfConsoloRebarConfig
    {
        public string BarTypeTiranteName { get; set; } = string.Empty;
        public int NumeroTirantes { get; set; } = 4;
        public double ComprimentoTiranteCm { get; set; } = 100.0;
        public string BarTypeSuspensaoName { get; set; } = string.Empty;
        public int NumeroSuspensoes { get; set; } = 4;
        public double ComprimentoSuspensaoCm { get; set; } = 60.0;
        public string BarTypeEstriboVerticalName { get; set; } = string.Empty;
        public int QuantidadeEstribosVerticais { get; set; } = 5;
        public string BarTypeEstriboHorizontalName { get; set; } = string.Empty;
        public int QuantidadeEstribosHorizontais { get; set; } = 5;
    }
}
