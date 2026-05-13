using Autodesk.Revit.DB;

namespace FerramentaEMT.Models
{
    public class GuardaCorpoConfig
    {
        public FamilySymbol SymbolSelecionado { get; set; }
        public Level NivelReferencia { get; set; }
        public double AlturaCorrimaoCm { get; set; }
        public double OffsetLateralCm { get; set; }
        public int ZJustificationValue { get; set; }
        public bool UnirGeometrias { get; set; }
        public bool CriarPostes { get; set; }
        public bool CriarTravessasIntermediarias { get; set; }
        public int QuantidadeTravessasIntermediarias { get; set; } = 6;
        public double EspacamentoMaximoPostesCm { get; set; } = 150.0;
    }
}
