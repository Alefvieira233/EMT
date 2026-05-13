namespace FerramentaEMT.Models
{
    public enum EscopoCotagem
    {
        SelecaoManual = 0,
        VistaAtiva = 1
    }

    public sealed class CotarPecaFabricacaoConfig
    {
        /// <summary>Escopo: selecao manual ou todos da vista ativa.</summary>
        public EscopoCotagem Escopo { get; set; } = EscopoCotagem.SelecaoManual;

        /// <summary>Cotar o comprimento total da peca (entre faces de extremidade).</summary>
        public bool CotarComprimento { get; set; } = true;

        /// <summary>Cotar a altura da secao (d).</summary>
        public bool CotarAlturaPerfil { get; set; } = true;

        /// <summary>Cotar a largura da mesa (bf).</summary>
        public bool CotarLarguraMesa { get; set; } = false;

        /// <summary>Cotar furos (distancias entre centros).</summary>
        public bool CotarFuros { get; set; } = true;

        /// <summary>Cotar distancia de borda ao primeiro furo.</summary>
        public bool CotarDistanciaBorda { get; set; } = true;

        /// <summary>Offset da linha de cota em mm.</summary>
        public double OffsetCotaMm { get; set; } = 250;

        /// <summary>Se true, agrupa cotas iguais em running dimension.</summary>
        public bool UsarCotasCorridas { get; set; } = true;

        public bool TemCotaSelecionada()
        {
            return CotarComprimento || CotarAlturaPerfil || CotarLarguraMesa
                || CotarFuros || CotarDistanciaBorda;
        }
    }
}
