namespace FerramentaEMT.Models.Conexoes
{
    /// <summary>Tipo de conexão estrutural entre elementos metálicos.</summary>
    public enum TipoConexao
    {
        /// <summary>Chapa de ponta (end plate connection) - soldada ou parafusada na extremidade da viga.</summary>
        ChapaDePonta = 0,

        /// <summary>Dupla cantoneira (double angle connection) - cantoneiras em ambos os lados.</summary>
        DuplaCantoneira = 1,

        /// <summary>Chapa gusset (gusset plate) - chapa diagonal em contraventamento.</summary>
        ChapaGusset = 2
    }
}
