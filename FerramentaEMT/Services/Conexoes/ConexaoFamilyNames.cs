using FerramentaEMT.Models.Conexoes;

namespace FerramentaEMT.Services.Conexoes
{
    /// <summary>
    /// Nomes canonicos das familias Revit utilizadas por cada <see cref="TipoConexao"/>.
    /// Pure C# — sem dependencia Revit API, permite teste unitario.
    /// </summary>
    /// <remarks>
    /// Convencao: prefixo "EMT_" seguido do tipo, com underscores.
    /// Retorna string vazia para tipo desconhecido (nao lanca).
    /// </remarks>
    public static class ConexaoFamilyNames
    {
        public const string ChapaDePonta = "EMT_Chapa_Ponta";
        public const string DuplaCantoneira = "EMT_Dupla_Cantoneira";
        public const string ChapaGusset = "EMT_Chapa_Gusset";

        public static string For(TipoConexao tipo) => tipo switch
        {
            TipoConexao.ChapaDePonta => ChapaDePonta,
            TipoConexao.DuplaCantoneira => DuplaCantoneira,
            TipoConexao.ChapaGusset => ChapaGusset,
            _ => string.Empty
        };
    }
}
