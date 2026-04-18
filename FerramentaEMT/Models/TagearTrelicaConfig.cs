#nullable enable
namespace FerramentaEMT.Models
{
    /// <summary>
    /// Configuracao para comando "Tagear Treliça" (identificacao leve de perfis).
    /// Subconjunto de CotarTrelicaConfig — so coloca tags de perfil, sem cotas.
    /// </summary>
    public sealed class TagearTrelicaConfig
    {
        /// <summary>Colocar tags de perfil no banzo superior.</summary>
        public bool TagearBanzoSuperior { get; set; } = true;

        /// <summary>Colocar tags de perfil no banzo inferior.</summary>
        public bool TagearBanzoInferior { get; set; } = true;

        /// <summary>Colocar tags de perfil nos montantes.</summary>
        public bool TagearMontantes { get; set; } = true;

        /// <summary>Colocar tags de perfil nas diagonais.</summary>
        public bool TagearDiagonais { get; set; } = true;

        /// <summary>Se verdadeiro, formata cantoneiras duplas com prefixo "2x".</summary>
        public bool CantoneiraDupla { get; set; } = true;

        /// <summary>
        /// Se verdadeiro, cria TextNote com rotulos "BANZO SUPERIOR <perfil>"
        /// e "BANZO INFERIOR <perfil>" acima/abaixo dos banzos.
        /// </summary>
        public bool CriarRotuloBanzos { get; set; } = true;

        /// <summary>Offset perpendicular da tag em relacao ao elemento (milimetros).</summary>
        public double OffsetTagMm { get; set; } = 300.0;

        /// <summary>Verifica se ao menos um tipo de membro foi selecionado para tagging.</summary>
        public bool TemTipoSelecionado() =>
            TagearBanzoSuperior || TagearBanzoInferior || TagearMontantes || TagearDiagonais;
    }
}
