#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    /// <summary>
    /// Configuracao do comando "Inserir Conexao de Terca" (v2.8.1, Victor).
    /// Construida pela <c>ConexaoTercasWindow</c>, consumida pelo
    /// <c>ConexaoTercasService</c>.
    /// </summary>
    public class ConexaoTercasConfig
    {
        public FamilySymbol? SymbolSelecionado { get; set; }
        public bool ColocarExtremidades { get; set; } = true;
        public bool ColocarMeio { get; set; } = false;
        public double OffsetRotacaoGraus { get; set; } = 0.0;
        public double OffsetVerticalAdicionalMm { get; set; } = 0.0;

        // Valores de parametros de tipo (em unidades internas Revit: pes, radianos).
        // Aplicados ao FamilySymbol antes do lancamento das instancias.
        public Dictionary<string, double> ParametrosInternos { get; set; } = new Dictionary<string, double>();
    }
}
