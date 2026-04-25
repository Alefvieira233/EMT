namespace FerramentaEMT.Models.PF
{
    public enum PfTwoPileCapBarShape
    {
        Reta,
        U,
        RetanguloFechado,
        EstriboVertical,
        CaliceVertical,
        FormaEspecial
    }

    public sealed class PfTwoPileCapBarPosition
    {
        public int Posicao { get; set; }
        public double DiametroMm { get; set; }
        public int QuantidadeTotalPdf { get; set; }
        public int QuantidadePorBloco { get; set; }
        public double ComprimentoCm { get; set; }
        public double EspacamentoCm { get; set; }
        public PfTwoPileCapBarShape Forma { get; set; }
        public string DescricaoForma { get; set; } = string.Empty;

        public string Nome => "N" + Posicao;

        public string ToComment()
        {
            string spacing = EspacamentoCm > 0.0
                ? $" - C/{EspacamentoCm:0.##}"
                : string.Empty;

            return $"{Nome} - POS {Posicao} - diam. {DiametroMm:0.###}{spacing} - C={ComprimentoCm:0.##} - {DescricaoForma}";
        }
    }
}
