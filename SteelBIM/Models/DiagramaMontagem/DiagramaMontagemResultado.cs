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
    }
}
