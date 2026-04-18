using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Models.ModelCheck
{
    /// <summary>
    /// Relatorio consolidado da verificacao de modelo.
    /// Agrega resultados de todas as regras executadas.
    /// </summary>
    public sealed class ModelCheckReport
    {
        /// <summary>Total de elementos analisados.</summary>
        public int TotalElementsAnalyzed { get; set; }

        /// <summary>Total de problemas encontrados em todas as regras.</summary>
        public int TotalIssues => Results?.Sum(r => r.IssuesCount) ?? 0;

        /// <summary>Tempo total de execucao em milissegundos.</summary>
        public long Duration { get; set; }

        /// <summary>Data/hora da execucao do relatorio.</summary>
        public DateTime ExecutionTime { get; set; } = DateTime.Now;

        /// <summary>Lista de resultados por regra.</summary>
        public List<ModelCheckRuleResult> Results { get; set; } = new();

        /// <summary>
        /// Conta o total de problemas por severidade.
        /// </summary>
        /// <param name="severity">Severidade a contar.</param>
        /// <returns>Total de problemas com essa severidade.</returns>
        public int CountBySeverity(ModelCheckSeverity severity)
        {
            if (Results == null || Results.Count == 0)
                return 0;

            return Results
                .SelectMany(r => r.Issues)
                .Count(issue => issue.Severity == severity);
        }

        /// <summary>
        /// Obtem todos os problemas do relatorio, opcionalmente filtrados por severidade.
        /// </summary>
        public IEnumerable<ModelCheckIssue> GetAllIssues(ModelCheckSeverity? filterBySeverity = null)
        {
            var allIssues = Results?.SelectMany(r => r.Issues) ?? Enumerable.Empty<ModelCheckIssue>();

            if (filterBySeverity.HasValue)
                return allIssues.Where(i => i.Severity == filterBySeverity.Value);

            return allIssues;
        }
    }
}
