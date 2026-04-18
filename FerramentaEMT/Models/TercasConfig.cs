using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace FerramentaEMT.Models
{
    public class TercasConfig
    {
        public FamilySymbol SymbolSelecionado { get; set; }
        public int Quantidade { get; set; }
        public double BeiralInicialCm { get; set; }
        public double BeiralFinalCm { get; set; }
        public double OffsetMm { get; set; }
        public double RotacaoSecaoGraus { get; set; }
        public bool InverterSentido { get; set; }
        public int ZJustificationValue { get; set; }
        public bool DividirNosBanzos { get; set; }
    }
}