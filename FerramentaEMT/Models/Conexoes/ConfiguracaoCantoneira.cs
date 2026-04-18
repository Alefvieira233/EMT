namespace FerramentaEMT.Models.Conexoes
{
    /// <summary>Configuração de uma conexão do tipo Dupla Cantoneira.</summary>
    public sealed class ConfiguracaoCantoneira
    {
        /// <summary>Espessura da cantoneira em mm.</summary>
        public double EspessuraMm { get; set; } = 12.7;

        /// <summary>Largura da abinha da cantoneira em mm.</summary>
        public double LarguraMm { get; set; } = 100;

        /// <summary>Altura da cantoneira em mm (comprimento da abinha).</summary>
        public double AlturaMm { get; set; } = 200;

        /// <summary>Número de parafusos por cantoneira.</summary>
        public int NumParafusosPorCantoneira { get; set; } = 3;

        /// <summary>Diâmetro do parafuso em mm.</summary>
        public double DiamParafusoMm { get; set; } = 19.05;

        /// <summary>Espaçamento entre furos em mm.</summary>
        public double EspacamentoFurosMm { get; set; } = 65;

        /// <summary>Distância da primeira linha de furos à borda em mm.</summary>
        public double DistanciaABordaMm { get; set; } = 35;
    }
}
