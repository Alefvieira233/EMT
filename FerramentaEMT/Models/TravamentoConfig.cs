using Autodesk.Revit.DB;

namespace FerramentaEMT.Models
{
    public class TravamentoConfig
    {
        public FamilySymbol SymbolTirante { get; set; }
        public FamilySymbol SymbolFrechal { get; set; }
        public bool LancarTirante { get; set; }
        public bool LancarFrechal { get; set; }
        public int Quantidade { get; set; }
        public int ZJustificationValue { get; set; }
        public double ZOffsetMm { get; set; }
        public bool InverterSentido { get; set; }
    }
}
