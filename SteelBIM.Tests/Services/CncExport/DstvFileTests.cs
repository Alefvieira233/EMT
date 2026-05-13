using FerramentaEMT.Models.CncExport;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.CncExport
{
    public class DstvFileTests
    {
        [Theory]
        [InlineData(90, 90, false)]
        [InlineData(90.4, 90.3, false)]    // dentro da tolerancia (0.5 grau)
        [InlineData(45, 90, true)]
        [InlineData(90, 60, true)]
        [InlineData(89.4, 90, true)]       // fora da tolerancia
        public void HasMiteredEnds_DetectaCortesNaoPerpendiculares(double start, double end, bool esperado)
        {
            var f = new DstvFile { CutAngleStartDeg = start, CutAngleEndDeg = end };
            f.HasMiteredEnds().Should().Be(esperado);
        }

        [Fact]
        public void DstvFile_NovaInstancia_TemValoresPadrao()
        {
            var f = new DstvFile();
            f.Phase.Should().Be("1");
            f.Quantity.Should().Be(1);
            f.ProfileType.Should().Be(DstvProfileType.SO);
            f.CutAngleStartDeg.Should().Be(90);
            f.CutAngleEndDeg.Should().Be(90);
            f.Holes.Should().BeEmpty();
            f.HasMiteredEnds().Should().BeFalse();
        }

        [Fact]
        public void DstvHole_NovoInstancia_TemFaceWebFrontPorPadrao()
        {
            var h = new DstvHole();
            h.Face.Should().Be(DstvFace.WebFront);
            h.DepthMm.Should().Be(0);
        }

        [Fact]
        public void DstvFace_ToDstvCode_RetornaCodigoCorreto()
        {
            DstvFace.WebFront.ToDstvCode().Should().Be("v");
            DstvFace.WebBack.ToDstvCode().Should().Be("h");
            DstvFace.TopFlange.ToDstvCode().Should().Be("o");
            DstvFace.BottomFlange.ToDstvCode().Should().Be("u");
            DstvFace.Side.ToDstvCode().Should().Be("s");
        }
    }
}
