using Autodesk.Revit.DB;

namespace FerramentaEMT.Models.Conexoes
{
    public sealed class PlacaBaseConfig
    {
        public string FamilyName { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public ElementId FamilySymbolId { get; set; } = ElementId.InvalidElementId;

        public double ComprimentoMm { get; set; } = 300;

        public double LarguraMm { get; set; } = 300;

        public double EspessuraMm { get; set; } = 20;

        public double FuroDiametroMm { get; set; } = 22;

        public double FuroOffsetXmm { get; set; } = 80;

        public double FuroOffsetYmm { get; set; } = 80;

        public double ChumbadorDiametroMm { get; set; } = 19;

        public double GrauteEspessuraMm { get; set; } = 30;

        public string Solda { get; set; } = "6 mm";

        public bool SobrescreverParametros { get; set; } = true;
    }
}
