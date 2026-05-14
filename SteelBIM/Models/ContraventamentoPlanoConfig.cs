using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    public class ContraventamentoPlanoConfig
    {
        public FamilySymbol SymbolSelecionado { get; set; }
        public int ZJustificationValue { get; set; }
        public double OffsetSegundaDiagonalMm { get; set; }
        public bool DesabilitarUniao { get; set; }
    }
}
