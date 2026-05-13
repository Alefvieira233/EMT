using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace FerramentaEMT.Models
{
    public class PipeRackConfig
    {
        public Level NivelBase { get; set; }
        public Level NivelTopoPilares { get; set; }
        public FamilySymbol SymbolPilar { get; set; }
        public FamilySymbol SymbolViga { get; set; }
        public FamilySymbol SymbolMontante { get; set; }
        public FamilySymbol SymbolDiagonal { get; set; }
        public List<double> VaosMm { get; set; } = new List<double>();
        public double AlturaModuloMm { get; set; }
        public int QuantidadeModulos { get; set; } = 1;
        public double LarguraEstruturaMm { get; set; }
        public int NumeroModulosLargura { get; set; }
        public string TipoTrelica { get; set; } = "Pratt";
        public string PadraoDiagonais { get; set; } = "Alternadas";
        public bool DesabilitarUniaoMembros { get; set; } = true;
    }
}
