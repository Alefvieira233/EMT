using System.Globalization;

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
            // Culture-invariant: o Comment vira parametro do Revit e parser de
            // schedule/CSV downstream pode interpretar virgula como separador
            // de campos. Bug descoberto por testes na Wave 2 — em pt-BR sem
            // CultureInfo.InvariantCulture, "{x:0.##}" gerava "6,3" em vez de
            // "6.3". Forcar invariante em TODOS os formatadores numericos.
            CultureInfo c = CultureInfo.InvariantCulture;

            string spacing = EspacamentoCm > 0.0
                ? string.Format(c, " - C/{0:0.##}", EspacamentoCm)
                : string.Empty;

            return string.Format(
                c,
                "{0} - POS {1} - diam. {2:0.###}{3} - C={4:0.##} - {5}",
                Nome,
                Posicao,
                DiametroMm,
                spacing,
                ComprimentoCm,
                DescricaoForma);
        }
    }
}
