using System;
using System.Collections.Generic;

namespace FerramentaEMT.Models.ModelCheck
{
    /// <summary>
    /// Resultado da execucao de uma regra de verificacao individual.
    /// </summary>
    public sealed class ModelCheckRuleResult
    {
        /// <summary>Nome da regra.</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>Descricao da regra.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Tempo de execucao em milissegundos.</summary>
        public long ElapsedMs { get; set; }

        /// <summary>Total de problemas encontrados por esta regra.</summary>
        public int IssuesCount => Issues?.Count ?? 0;

        /// <summary>Lista de problemas encontrados.</summary>
        public List<ModelCheckIssue> Issues { get; set; } = new();
    }
}
