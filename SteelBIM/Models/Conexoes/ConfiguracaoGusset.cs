namespace FerramentaEMT.Models.Conexoes
{
    /// <summary>Configuração de uma conexão do tipo Chapa Gusset (placa de reforço diagonal).</summary>
    public sealed class ConfiguracaoGusset
    {
        /// <summary>Espessura da chapa gusset em mm.</summary>
        public double EspessuraMm { get; set; } = 9.53;

        /// <summary>Largura da chapa gusset em mm.</summary>
        public double LarguraMm { get; set; } = 300;

        /// <summary>Altura da chapa gusset em mm.</summary>
        public double AlturaMm { get; set; } = 300;

        /// <summary>Ângulo de inclinação diagonal da chapa em graus.</summary>
        public double AnguloDiagonalDeg { get; set; } = 45.0;

        /// <summary>Número de parafusos de fixação.</summary>
        public int NumParafusos { get; set; } = 6;

        /// <summary>Diâmetro do parafuso em mm.</summary>
        public double DiamParafusoMm { get; set; } = 19.05;
    }
}
