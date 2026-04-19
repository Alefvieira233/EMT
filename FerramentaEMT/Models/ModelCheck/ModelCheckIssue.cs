namespace FerramentaEMT.Models.ModelCheck
{
    /// <summary>
    /// Um problema individual encontrado por uma regra de verificacao.
    /// </summary>
    public sealed class ModelCheckIssue
    {
        /// <summary>Nome da regra que detectou este problema.</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>Severidade do problema.</summary>
        public ModelCheckSeverity Severity { get; set; } = ModelCheckSeverity.Warning;

        /// <summary>
        /// ID do elemento Revit afetado.
        /// Pode ser nulo se o problema nao estiver associado a um elemento especifico.
        /// </summary>
        public long? ElementId { get; set; }

        /// <summary>
        /// Indica se o problema esta associado a uma folha/carimbo (true)
        /// ou a um elemento de modelo 3D (false).
        /// Usado pela janela de relatorio para decidir se navega para a vista 3D
        /// ou para a folha correspondente.
        /// </summary>
        public bool IsSheetIssue { get; set; }

        /// <summary>Descricao detalhada do problema.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Sugestao de correcao (opcional).</summary>
        public string Suggestion { get; set; } = string.Empty;
    }
}
