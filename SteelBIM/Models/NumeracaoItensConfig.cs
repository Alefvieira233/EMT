using Autodesk.Revit.DB;

namespace FerramentaEMT.Models
{
    public enum NumeracaoEscopo
    {
        ModeloInteiro = 0,
        VistaAtiva = 1,
        SelecaoAtual = 2
    }

    public class NumeracaoItensConfig
    {
        public NumeracaoEscopo Escopo { get; set; } = NumeracaoEscopo.VistaAtiva;
        public long CategoriaIdValor { get; set; }
        public string CategoriaNome { get; set; } = string.Empty;
        public string FamiliaNome { get; set; } = string.Empty;
        public string TipoNome { get; set; } = string.Empty;
        public string ParametroChave { get; set; } = string.Empty;
        public string ParametroNome { get; set; } = string.Empty;
        public StorageType ParametroStorageType { get; set; } = StorageType.String;
        public string Prefixo { get; set; } = string.Empty;
        public int Inicio { get; set; } = 1;
        public int Degrau { get; set; } = 1;
        public string Sufixo { get; set; } = string.Empty;
        public bool ManterDestaqueAoConcluir { get; set; } = true;

        public ElementId CategoriaId => new ElementId(CategoriaIdValor);

        public string MontarValor(int numero)
        {
            return $"{Prefixo}{numero}{Sufixo}";
        }
    }
}
