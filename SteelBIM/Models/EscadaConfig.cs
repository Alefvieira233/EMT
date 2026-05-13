using Autodesk.Revit.DB;

namespace FerramentaEMT.Models
{
    public enum EscadaLadoInsercao
    {
        Centro = 0,
        Esquerda = 1,
        Direita = 2
    }

    public enum EscadaTipoDegrau
    {
        PerfilLinear = 0,
        Chapa = 1
    }

    public class EscadaConfig
    {
        public FamilySymbol SymbolLongarina { get; set; }
        public FamilySymbol SymbolDegrau { get; set; }
        public Level NivelReferencia { get; set; }
        public double LarguraCm { get; set; } = 100.0;
        public double AlturaEspelhoCm { get; set; } = 19.0;
        public double PisadaCm { get; set; } = 30.0;
        public int QuantidadeDegraus { get; set; }
        public EscadaLadoInsercao LadoInsercao { get; set; } = EscadaLadoInsercao.Centro;
        public EscadaTipoDegrau TipoDegrau { get; set; } = EscadaTipoDegrau.PerfilLinear;
        public bool PossuiExtensaoInicio { get; set; }
        public double ExtensaoInicioCm { get; set; } = 20.0;
        public bool PossuiExtensaoFim { get; set; }
        public double ExtensaoFimCm { get; set; } = 20.0;
        public double EspessuraChapaDegrauCm { get; set; } = 0.5;
        public int ZJustificationValue { get; set; } = 2; // ZJustification.Top = 2 no Revit API
        public bool UnirGeometrias { get; set; } = false;
        public bool CriarDegraus { get; set; } = true;
    }
}
