namespace FerramentaEMT.Models.Conexoes
{
    /// <summary>Configuração de uma conexão do tipo Chapa de Ponta.</summary>
    public sealed class ConfiguracaoChapaPonta
    {
        /// <summary>Espessura da chapa em mm.</summary>
        public double EspessuraMm { get; set; } = 12.7;

        /// <summary>Largura da chapa em mm.</summary>
        public double LarguraMm { get; set; } = 150;

        /// <summary>Altura da chapa em mm.</summary>
        public double AlturaMm { get; set; } = 250;

        /// <summary>Número total de parafusos na chapa.</summary>
        public int NumParafusos { get; set; } = 4;

        /// <summary>Diâmetro do parafuso em mm (ex: 19.05 para M3/4").</summary>
        public double DiamParafusoMm { get; set; } = 19.05;

        /// <summary>Espaçamento entre parafusos na direção X (horizontal) em mm.</summary>
        public double EspacamentoXMm { get; set; } = 75;

        /// <summary>Espaçamento entre parafusos na direção Y (vertical) em mm.</summary>
        public double EspacamentoYMm { get; set; } = 75;
    }
}
