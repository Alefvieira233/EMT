#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Tests.Services.ModelCheck
{
    /// <summary>
    /// Testes de logica pura para a verificacao de carimbo (TitleBlock) do Miniciclo 4.
    /// Cobrem early returns do config, agregacao de issues e propriedades dos modelos.
    /// A logica Revit-bound (LookupParameter, FilteredElementCollector) e validada
    /// via smoke test manual no Miniciclo 7.
    /// </summary>
    public class TitleBlockVerificationTests
    {
        // ---------------------------------------------------------------
        // Testes de config — early return paths
        // ---------------------------------------------------------------

        [Fact]
        public void Config_RunTitleBlockDisabled_GetEnabledRulesCount_ExcludesParams()
        {
            // Arrange
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = false,
                TitleBlockParameters = new List<string> { "Projetista", "Revisao", "Data" }
            };

            // Act & Assert — com RunTitleBlockParameters = false, parametros nao contam
            config.GetEnabledRulesCount().Should().Be(9, "9 regras padrao ativas, carimbo desabilitado");
        }

        [Fact]
        public void Config_TitleBlockParametersEmpty_GetEnabledRulesCount_DoesNotAdd()
        {
            // Arrange
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = true,
                TitleBlockParameters = new List<string>()
            };

            // Act & Assert — habilitado mas sem parametros = +0
            config.GetEnabledRulesCount().Should().Be(9);
        }

        [Fact]
        public void Config_TitleBlockParametersNull_GetEnabledRulesCount_DoesNotAdd()
        {
            // Arrange
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = true,
                TitleBlockParameters = null!
            };

            // Act & Assert — null guard no GetEnabledRulesCount
            config.GetEnabledRulesCount().Should().Be(9);
        }

        // ---------------------------------------------------------------
        // Testes de ModelCheckIssue — propriedades de carimbo
        // ---------------------------------------------------------------

        [Fact]
        public void TitleBlockIssue_HasCorrectProperties()
        {
            // Arrange & Act
            var issue = new ModelCheckIssue
            {
                RuleName = "Carimbo: Projetista",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 98765,
                IsSheetIssue = true,
                Description = "A folha '001 - Planta' esta sem valor para 'Projetista'.",
                Suggestion = "Preencha o parametro 'Projetista' no carimbo da folha 001 - Planta."
            };

            // Assert
            issue.RuleName.Should().Be("Carimbo: Projetista");
            issue.Severity.Should().Be(ModelCheckSeverity.Warning);
            issue.ElementId.Should().Be(98765);
            issue.IsSheetIssue.Should().BeTrue();
            issue.Description.Should().Contain("Projetista");
            issue.Suggestion.Should().Contain("Projetista");
        }

        [Fact]
        public void TitleBlockIssue_IsSheetIssue_DefaultFalse()
        {
            // Arrange & Act
            var issue = new ModelCheckIssue();

            // Assert — default e false (problemas de modelo 3D)
            issue.IsSheetIssue.Should().BeFalse();
        }

        // ---------------------------------------------------------------
        // Testes de ModelCheckRuleResult — agregacao de carimbo
        // ---------------------------------------------------------------

        [Fact]
        public void TitleBlockRuleResult_WithIssues_AggregatesCorrectly()
        {
            // Arrange
            var result = new ModelCheckRuleResult
            {
                RuleName = "Carimbo: Revisao",
                Description = "Verifica o preenchimento do atributo 'Revisao' no carimbo da folha."
            };

            result.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Carimbo: Revisao",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 100,
                IsSheetIssue = true,
                Description = "A folha '001 - Planta' esta sem valor para 'Revisao'."
            });

            result.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Carimbo: Revisao",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 200,
                IsSheetIssue = true,
                Description = "A folha '002 - Corte' esta sem valor para 'Revisao'."
            });

            // Act & Assert
            result.IssuesCount.Should().Be(2);
            result.Issues.Should().AllSatisfy(i =>
            {
                i.Severity.Should().Be(ModelCheckSeverity.Warning);
                i.IsSheetIssue.Should().BeTrue();
                i.RuleName.Should().StartWith("Carimbo:");
            });
        }

        [Fact]
        public void TitleBlockRuleResult_EmptyIssues_HasZeroCount()
        {
            // Arrange — parametro verificado, todas as folhas preenchidas
            var result = new ModelCheckRuleResult
            {
                RuleName = "Carimbo: Projetista",
                Description = "Verifica o preenchimento do atributo 'Projetista' no carimbo da folha.",
                Issues = new List<ModelCheckIssue>()
            };

            // Act & Assert
            result.IssuesCount.Should().Be(0);
        }

        // ---------------------------------------------------------------
        // Testes de report — agregacao com carimbos
        // ---------------------------------------------------------------

        [Fact]
        public void Report_WithTitleBlockResults_AggregatesWithModelResults()
        {
            // Arrange — simular relatorio com regras 3D + carimbo
            var report = new ModelCheckReport { TotalElementsAnalyzed = 50 };

            // Regra 3D
            var modelResult = new ModelCheckRuleResult
            {
                RuleName = "Material Ausente",
                ElapsedMs = 100
            };
            modelResult.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Material Ausente",
                Severity = ModelCheckSeverity.Error,
                ElementId = 1,
                IsSheetIssue = false
            });

            // Regra de carimbo
            var titleBlockResult = new ModelCheckRuleResult
            {
                RuleName = "Carimbo: Projetista",
                Description = "Verifica o preenchimento do atributo 'Projetista'."
            };
            titleBlockResult.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Carimbo: Projetista",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 500,
                IsSheetIssue = true
            });
            titleBlockResult.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Carimbo: Projetista",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 600,
                IsSheetIssue = true
            });

            report.Results.Add(modelResult);
            report.Results.Add(titleBlockResult);

            // Act & Assert
            report.TotalIssues.Should().Be(3, "1 modelo + 2 carimbo");
            report.CountBySeverity(ModelCheckSeverity.Error).Should().Be(1);
            report.CountBySeverity(ModelCheckSeverity.Warning).Should().Be(2);

            // Verificar que issues de carimbo sao separaveis via IsSheetIssue
            var sheetIssues = report.GetAllIssues()
                .Where(i => i.IsSheetIssue)
                .ToList();
            sheetIssues.Should().HaveCount(2);
            sheetIssues.Should().AllSatisfy(i => i.RuleName.Should().StartWith("Carimbo:"));

            var modelIssues = report.GetAllIssues()
                .Where(i => !i.IsSheetIssue)
                .ToList();
            modelIssues.Should().HaveCount(1);
        }
    }
}
