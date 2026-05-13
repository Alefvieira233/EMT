#nullable enable
namespace FerramentaEMT.Models
{
    /// <summary>
    /// Configuracao para comando "Identificar Perfil em Massa".
    /// Controla quais categorias de elementos serao identificadas,
    /// se tags existentes serao substituidas, e parametros de formatacao.
    /// </summary>
    public sealed class IdentificarPerfilConfig
    {
        /// <summary>Incluir vigas estruturais (Structural Framing).</summary>
        public bool IncluirVigas { get; set; } = true;

        /// <summary>Incluir pilares estruturais (Structural Columns).</summary>
        public bool IncluirPilares { get; set; } = true;

        /// <summary>Incluir contraventos/bracing (Structural Framing tipo brace).</summary>
        public bool IncluirContraventos { get; set; } = true;

        /// <summary>Se verdadeiro, substitui tags existentes no elemento. Se falso, pula elementos que ja tem tag.</summary>
        public bool SubstituirTagsExistentes { get; set; } = false;

        /// <summary>Se verdadeiro, formata cantoneiras duplas com prefixo "2x" (ex: "2x L 76x76x6.3").</summary>
        public bool CantoneiraDupla { get; set; } = true;

        /// <summary>Offset perpendicular da tag em relacao ao elemento (milimetros).</summary>
        public double OffsetTagMm { get; set; } = 300.0;

        /// <summary>Verifica se ao menos uma categoria foi selecionada para identificacao.</summary>
        public bool TemCategoriaSelecionada() =>
            IncluirVigas || IncluirPilares || IncluirContraventos;
    }
}
