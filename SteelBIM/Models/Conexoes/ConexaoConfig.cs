namespace FerramentaEMT.Models.Conexoes
{
    /// <summary>
    /// Configuração completa de uma conexão estrutural, incluindo tipo e parâmetros específicos.
    /// </summary>
    public sealed class ConexaoConfig
    {
        /// <summary>Tipo de conexão a ser gerada.</summary>
        public TipoConexao Tipo { get; set; } = TipoConexao.ChapaDePonta;

        /// <summary>Configuração para conexão tipo Chapa de Ponta.</summary>
        public ConfiguracaoChapaPonta ChapaPonta { get; set; } = new();

        /// <summary>Configuração para conexão tipo Dupla Cantoneira.</summary>
        public ConfiguracaoCantoneira Cantoneira { get; set; } = new();

        /// <summary>Configuração para conexão tipo Chapa Gusset.</summary>
        public ConfiguracaoGusset Gusset { get; set; } = new();

        /// <summary>
        /// Se true, gera furos DSTV (para exportação CNC).
        /// Se false, apenas marca visualmente a conexão.
        /// </summary>
        public bool GerarFurosDstv { get; set; } = true;
    }
}
