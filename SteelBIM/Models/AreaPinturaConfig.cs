#nullable enable
namespace SteelBIM.Models
{
    /// <summary>
    /// Configuracao do comando "Area de Pintura": escopo, categorias, desconto e geracao da tabela.
    /// </summary>
    public sealed class AreaPinturaConfig
    {
        public bool SomenteSelecao { get; set; }            // false = todo o modelo
        public bool IncluirVigas { get; set; } = true;       // OST_StructuralFraming
        public bool IncluirPilares { get; set; } = true;     // OST_StructuralColumns
        public bool DescontarTopos { get; set; } = true;     // desconta as faces de corte das barras
        public double PercentualDesconto { get; set; } = 5.0; // % (parafusos/folgas)
        public bool CriarTabela { get; set; } = true;        // cria/atualiza a ViewSchedule
    }
}
