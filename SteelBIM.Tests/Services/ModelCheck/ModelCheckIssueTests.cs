using Xunit;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Tests.Services.ModelCheck
{
    public class ModelCheckIssueTests
    {
        [Fact]
        public void Issue_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var issue = new ModelCheckIssue();

            // Assert
            Assert.Equal(string.Empty, issue.RuleName);
            Assert.Equal(ModelCheckSeverity.Warning, issue.Severity);
            Assert.Null(issue.ElementId);
            Assert.Equal(string.Empty, issue.Description);
            Assert.Equal(string.Empty, issue.Suggestion);
            Assert.False(issue.IsSheetIssue);
        }

        [Fact]
        public void Issue_CanSetProperties()
        {
            // Arrange
            var issue = new ModelCheckIssue();

            // Act
            issue.RuleName = "Test Rule";
            issue.Severity = ModelCheckSeverity.Error;
            issue.ElementId = 12345;
            issue.Description = "Test Description";
            issue.Suggestion = "Test Suggestion";
            issue.IsSheetIssue = true;

            // Assert
            Assert.Equal("Test Rule", issue.RuleName);
            Assert.Equal(ModelCheckSeverity.Error, issue.Severity);
            Assert.Equal(12345, issue.ElementId);
            Assert.Equal("Test Description", issue.Description);
            Assert.Equal("Test Suggestion", issue.Suggestion);
            Assert.True(issue.IsSheetIssue);
        }

        [Fact]
        public void Issue_SeverityValues_OrderCorrectly()
        {
            // Verify enum values for severity ordering
            Assert.Equal(0, (int)ModelCheckSeverity.Info);
            Assert.Equal(1, (int)ModelCheckSeverity.Warning);
            Assert.Equal(2, (int)ModelCheckSeverity.Error);
        }

        [Fact]
        public void Issue_ElementIdCanBeNull()
        {
            // Arrange
            var issue = new ModelCheckIssue { ElementId = null };

            // Assert
            Assert.Null(issue.ElementId);
            Assert.False(issue.ElementId.HasValue);
        }
    }
}
