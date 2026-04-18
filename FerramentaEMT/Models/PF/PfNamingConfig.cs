using Autodesk.Revit.DB;
using FerramentaEMT.Models;
using FerramentaEMT.Services.PF;

namespace FerramentaEMT.Models.PF
{
    public enum PfNamingTarget
    {
        Pilares = 0,
        Vigas = 1,
        LajesPf = 2
    }

    public sealed class PfNamingConfig
    {
        public PfNamingTarget Alvo { get; set; } = PfNamingTarget.Pilares;
        public NumeracaoEscopo Escopo { get; set; } = NumeracaoEscopo.VistaAtiva;
        public string FamiliaNome { get; set; } = string.Empty;
        public string TipoNome { get; set; } = string.Empty;
        public string ParametroChave { get; set; } = string.Empty;
        public string ParametroNome { get; set; } = string.Empty;
        public StorageType ParametroStorageType { get; set; } = StorageType.String;
        public string Prefixo { get; set; } = string.Empty;
        public int Inicio { get; set; } = 1;
        public int Degrau { get; set; } = 1;
        public string Sufixo { get; set; } = string.Empty;

        public string MontarValor(int numero)
        {
            return PfNamingFormatter.Formatar(Prefixo, numero, Sufixo);
        }
    }
}
