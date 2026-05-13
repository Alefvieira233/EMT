using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Tests.Models.ModelCheck
{
    public class ModelCheckConfigTests
    {
        [Fact]
        public void Constructor_Default_MostRulesEnabled()
        {
            var config = new ModelCheckConfig();

            config.RunMissingMaterial.Should().BeTrue();
            config.RunMissingMark.Should().BeTrue();
            config.RunDuplicateMark.Should().BeTrue();
            config.RunOverlappingElements.Should().BeTrue();
            config.RunMissingProfile.Should().BeTrue();
            config.RunZeroLength.Should().BeTrue();
            config.RunMissingLevel.Should().BeTrue();
            config.RunStructuralWithoutType.Should().BeTrue();
            config.RunOrphanGroup.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_MissingCommentIsDisabled()
        {
            var config = new ModelCheckConfig();

            config.RunMissingComment.Should().BeFalse();
        }

        [Fact]
        public void Constructor_Default_ScopeViewOnlyIsTrue()
        {
            var config = new ModelCheckConfig();

            config.ScopeViewOnly.Should().BeTrue();
        }

        [Fact]
        public void Constructor_Default_ExportExcelIsFalse()
        {
            var config = new ModelCheckConfig();

            config.ExportExcel.Should().BeFalse();
            config.ExcelPath.Should().BeEmpty();
        }

        [Fact]
        public void GetEnabledRulesCount_Default_Returns9()
        {
            var config = new ModelCheckConfig();

            config.GetEnabledRulesCount().Should().Be(9);
        }

        [Fact]
        public void GetEnabledRulesCount_AllEnabled_Returns10()
        {
            var config = new ModelCheckConfig { RunMissingComment = true };

            config.GetEnabledRulesCount().Should().Be(10);
        }

        [Fact]
        public void Constructor_Default_TitleBlockIsDisabled()
        {
            var config = new ModelCheckConfig();

            config.RunTitleBlockParameters.Should().BeFalse();
            config.TitleBlockScopeActiveSheetOnly.Should().BeFalse();
            config.TitleBlockFamilyName.Should().BeEmpty();
            config.TitleBlockTypeName.Should().BeEmpty();
            config.TitleBlockParameters.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void GetEnabledRulesCount_WithTitleBlockParameters_IncludesParameterCount()
        {
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = true,
                TitleBlockParameters = new List<string> { "Projetista", "Revisao" }
            };

            // 9 regras padrao + 2 parametros de carimbo = 11
            config.GetEnabledRulesCount().Should().Be(11);
        }

        [Fact]
        public void GetEnabledRulesCount_TitleBlockDisabled_DoesNotCount()
        {
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = false,
                TitleBlockParameters = new List<string> { "Projetista", "Revisao" }
            };

            // 9 regras padrao, TitleBlock desligado nao conta
            config.GetEnabledRulesCount().Should().Be(9);
        }

        [Fact]
        public void GetEnabledRulesCount_TitleBlockEnabled_EmptyList_Returns9()
        {
            var config = new ModelCheckConfig
            {
                RunTitleBlockParameters = true,
                TitleBlockParameters = new List<string>()
            };

            // Habilitado mas lista vazia = +0
            config.GetEnabledRulesCount().Should().Be(9);
        }

        [Fact]
        public void GetEnabledRulesCount_AllDisabled_Returns0()
        {
            var config = new ModelCheckConfig
            {
                RunMissingMaterial = false,
                RunMissingMark = false,
                RunDuplicateMark = false,
                RunOverlappingElements = false,
                RunMissingProfile = false,
                RunZeroLength = false,
                RunMissingLevel = false,
                RunStructuralWithoutType = false,
                RunMissingComment = false,
                RunOrphanGroup = false
            };

            config.GetEnabledRulesCount().Should().Be(0);
        }
    }
}
