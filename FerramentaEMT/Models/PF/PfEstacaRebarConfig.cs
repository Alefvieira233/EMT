namespace FerramentaEMT.Models.PF
{
    /// <summary>
    /// Config para o comando "PF - Aços Estaca": lança barras longitudinais
    /// distribuídas em anel circular dentro da estaca, opcionalmente com
    /// estribos circulares e traspasse.
    /// </summary>
    public sealed class PfEstacaRebarConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public int QuantidadeBarras { get; set; } = 6;
        public bool InserirEstribos { get; set; }
        public PfColumnStirrupsConfig Estribos { get; } = new PfColumnStirrupsConfig();
        public PfLapSpliceConfig Traspasse { get; } = new PfLapSpliceConfig();
    }
}
