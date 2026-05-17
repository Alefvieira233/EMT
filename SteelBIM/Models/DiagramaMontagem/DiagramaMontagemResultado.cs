using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models.DiagramaMontagem
{
    public sealed class DiagramaMontagemResultado
    {
        public bool Sucesso { get; set; }
        public ElementId VistaCriadaId { get; set; }
        public string NomeVistaCriada { get; set; } = string.Empty;
        public int EixosVisiveis { get; set; }
        public int CotasCriadas { get; set; }
        public int TagsCriadas { get; set; }
        public int TagsSemMark { get; set; }
        public List<string> Avisos { get; set; } = new();
        public string Mensagem { get; set; } = string.Empty;

        // Novos v2.4.0
        public int CotasVerticais { get; set; }
        public int NiveisVisiveis { get; set; }
        public int ComprimentosCriados { get; set; }
        public bool FolhaCriada { get; set; }
        public ElementId FolhaCriadaId { get; set; }
        public string NomeFolhaCriada { get; set; } = string.Empty;
        public bool CotaTotalConjunto { get; set; }
    }
}
