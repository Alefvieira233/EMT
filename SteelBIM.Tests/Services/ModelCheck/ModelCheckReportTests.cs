using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Tests.Services.ModelCheck
{
    public class ModelCheckReportTests
    {
        [Fact]
        public void Report_EmptyReport_HasZeroIssues()
        {
            // Arrange & Act
            var report = new ModelCheckReport();

            // Assert
            Assert.Equal(0, report.TotalIssues);
            Assert.Empty(report.Results);
        }

        [Fact]
        public void Report_AddingRuleResult_UpdatesTotalIssues()
        {
            // Arrange
            var report = new ModelCheckReport();
            var resultado = new ModelCheckRuleResult
            {
                RuleName = "Test Rule",
                Description = "Test",
                ElapsedMs = 100
            };

            resultado.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Test Rule",
                Severity = ModelCheckSeverity.Error,
                ElementId = 1,
                Description = "Problem 1"
            });

            resultado.Issues.Add(new ModelCheckIssue
            {
                RuleName = "Test Rule",
                Severity = ModelCheckSeverity.Warning,
                ElementId = 2,
                Description = "Problem 2"
            });

            // Act
            report.Results.Add(resultado);

            // Assert
            Assert.Equal(2, report.TotalIssues);
            Assert.Single(report.Results);
        }

        [Fact]
        public void Report_CountBySeverity_ReturnsCorrectCounts()
        {
            // Arrange
            var report = new ModelCheckReport();

            var resultado1 = new ModelCheckRuleResult
            {
                RuleName = "Rule 1",
                Description = "Test",
                ElapsedMs = 100
            };

            resultado1.Issues.Add(new ModelCheckIssue
            {
                Severity = ModelCheckSeverity.Error,
                ElementId = 1
            });

            resultado1.Issues.Add(new ModelCheckIssue
            {
                Severity = ModelCheckSeverity.Error,
                ElementId = 2
            });

            resultado1.Issues.Add(new ModelCheckIssue
            {
                Severity = ModelCheckSeverity.Warning,
                ElementId = 3
            });

            var resultado2 = new ModelCheckRuleResult
            {
                RuleName = "Rule 2",
                Description = "Test",
                ElapsedMs = 50
            };

            resultado2.Issues.Add(new ModelCheckIssue
            {
                Severity = ModelCheckSeverity.Info,
                ElementId = 4
            });

            report.Results.Add(resultado1);
            report.Results.Add(resultado2);

            // Act
            int countErrors = report.CountBySeverity(ModelCheckSeverity.Error);
            int countWarnings = report.CountBySeverity(ModelCheckSeverity.Warning);
            int countInfos = report.CountBySeverity(ModelCheckSeverity.Info);

            // Assert
            Assert.Equal(2, countErrors);
            Assert.Equal(1, countWarnings);
            Assert.Equal(1, countInfos);
        }

        [Fact]
        public void Report_GetAllIssues_ReturnsAllIssues()
        {
            // Arrange
            var report = new ModelCheckReport();

            var resultado = new ModelCheckRuleResult
            {
                RuleName = "Rule",
                Description = "Test",
                ElapsedMs = 100
            };

            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Error });
            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Warning });
            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Info });

            report.Results.Add(resultado);

            // Act
            var allIssues = report.GetAllIssues().ToList();

            // Assert
            Assert.Equal(3, allIssues.Count);
        }

        [Fact]
        public void Report_GetAllIssues_FilteredBySeverity()
        {
            // Arrange
            var report = new ModelCheckReport();

            var resultado = new ModelCheckRuleResult
            {
                RuleName = "Rule",
                Description = "Test",
                ElapsedMs = 100
            };

            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Error });
            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Error });
            resultado.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Warning });

            report.Results.Add(resultado);

            // Act
            var onlyErrors = report.GetAllIssues(ModelCheckSeverity.Error).ToList();

            // Assert
            Assert.Equal(2, onlyErrors.Count);
            onlyErrors.Should().AllSatisfy(i => Assert.Equal(ModelCheckSeverity.Error, i.Severity));
        }

        [Fact]
        public void Report_MultipleResults_AggregatesCorrectly()
        {
            // Arrange
            var report = new ModelCheckReport { TotalElementsAnalyzed = 100 };

            var resultado1 = new ModelCheckRuleResult
            {
                RuleName = "Rule 1",
                ElapsedMs = 50
            };
            resultado1.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Error });
            resultado1.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Error });

            var resultado2 = new ModelCheckRuleResult
            {
                RuleName = "Rule 2",
                ElapsedMs = 75
            };
            resultado2.Issues.Add(new ModelCheckIssue { Severity = ModelCheckSeverity.Warning });

            report.Results.Add(resultado1);
            report.Results.Add(resultado2);

            report.Duration = 125;

            // Assert
            Assert.Equal(2, report.Results.Count);
            Assert.Equal(3, report.TotalIssues);
            Assert.Equal(100, report.TotalElementsAnalyzed);
            Assert.Equal(125, report.Duration);
        }

        [Fact]
        public void Report_ExecutionTime_IsSet()
        {
            // Arrange: captura janela de tempo ao redor da construcao.
            // Bug antigo: timeBefore era definido DEPOIS do ctor, entao
            // ExecutionTime < timeBefore e o assert quebrava.
            var timeBefore = DateTime.Now;
            var report = new ModelCheckReport();
            var timeAfter = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.True(report.ExecutionTime >= timeBefore,
                $"ExecutionTime {report.ExecutionTime:O} deveria ser >= timeBefore {timeBefore:O}");
            Assert.True(report.ExecutionTime <= timeAfter,
                $"ExecutionTime {report.ExecutionTime:O} deveria ser <= timeAfter {timeAfter:O}");
        }
    }
}
