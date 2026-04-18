using Autodesk.Revit.DB;

namespace FerramentaEMT.Models
{
    public class TrelicaConfig
    {
        public FamilySymbol SymbolMontante { get; set; }
        public FamilySymbol SymbolDiagonal { get; set; }
        public bool LancarMontante { get; set; }
        public bool LancarDiagonal { get; set; }
        public int Quantidade { get; set; }
        public int ZJustificationValue { get; set; }
        public double ZOffsetMm { get; set; }
        public bool InverterSentido { get; set; }
    }
}
