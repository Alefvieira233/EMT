using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    public class ConverterPerfilIfcConfig
    {
        public List<ConversaoElementoIfc> Conversoes { get; set; } = new List<ConversaoElementoIfc>();
        public bool DeletarOriginal { get; set; } = true;
        public Level NivelPadrao { get; set; }
    }

    public class ConversaoElementoIfc
    {
        public ElementId ElementoOrigem { get; set; }
        public FamilySymbol PerfilDestino { get; set; }
    }
}
