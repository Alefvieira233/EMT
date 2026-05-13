using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Services.CncExport;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.CncExport
{
    public class DstvProfileMapperTests
    {
        [Theory]
        [InlineData("W-Wide Flange", "W12X26", DstvProfileType.I)]
        [InlineData("W-Wide Flange", "W14X22", DstvProfileType.I)]
        [InlineData("W-Wide Flange", "W360X51", DstvProfileType.I)]
        [InlineData("HE", "HEA200", DstvProfileType.I)]
        [InlineData("HE", "HEB300", DstvProfileType.I)]
        [InlineData("HE", "HEM400", DstvProfileType.I)]
        [InlineData("IPE", "IPE160", DstvProfileType.I)]
        [InlineData("IPN", "IPN200", DstvProfileType.I)]
        [InlineData("HP", "HP14X73", DstvProfileType.I)]
        [InlineData("CS-Coluna Soldada", "CS300X62", DstvProfileType.I)]
        [InlineData("VS-Viga Soldada", "VS400X51", DstvProfileType.I)]
        public void Map_PerfisI_RetornaI(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("Channel", "C12X20.7", DstvProfileType.U)]
        [InlineData("MC-Channel", "MC10X22", DstvProfileType.U)]
        [InlineData("UPN", "UPN240", DstvProfileType.U)]
        [InlineData("UPE", "UPE160", DstvProfileType.U)]
        [InlineData("U", "U120", DstvProfileType.U)]
        public void Map_PerfisU_RetornaU(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("L-Angle", "L4X4X1/2", DstvProfileType.L)]
        [InlineData("Cantoneira", "L100X100X10", DstvProfileType.L)]
        public void Map_Cantoneiras_RetornaL(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("HSS-Hollow Structural", "HSS6X4X1/4", DstvProfileType.M)]   // retangular
        [InlineData("HSS-Hollow Structural", "HSS8X4X3/8", DstvProfileType.M)]
        [InlineData("HSS-Hollow Structural", "HSS6X0.25", DstvProfileType.RO)]   // redondo
        [InlineData("HSS-Hollow Structural", "HSS5.5X0.258", DstvProfileType.RO)]
        public void Map_Hss_DistingueRetangularDeRedondo(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("Pipe", "PIPE6STD", DstvProfileType.RO)]
        [InlineData("Tubo Redondo", "PIP100X4", DstvProfileType.RO)]
        public void Map_TubosRedondos_RetornaRO(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("WT-Tee", "WT9X25", DstvProfileType.T)]
        [InlineData("Perfil T", "T100X8", DstvProfileType.T)]
        public void Map_Tees_RetornaT(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData("Plate", "PL12.5", DstvProfileType.B)]
        [InlineData("Chapa", "PL10X300", DstvProfileType.B)]
        public void Map_Chapas_RetornaB(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Theory]
        [InlineData(null, null, DstvProfileType.SO)]
        [InlineData("", "", DstvProfileType.SO)]
        [InlineData("CustomFamily", "CustomType", DstvProfileType.SO)]
        public void Map_Desconhecido_RetornaSO(string family, string type, DstvProfileType esperado)
        {
            DstvProfileMapper.Map(family, type).Should().Be(esperado);
        }

        [Fact]
        public void Map_PorFamiliaQuandoTipoNaoReconhece()
        {
            // Tipo nao reconhecido por padrao mas familia e clara
            DstvProfileMapper.Map("Wide Flange Beam", "Custom-001")
                .Should().Be(DstvProfileType.I);
        }

        [Fact]
        public void ToDstvCode_DevolveStringCorreta()
        {
            DstvProfileType.I.ToDstvCode().Should().Be("I");
            DstvProfileType.U.ToDstvCode().Should().Be("U");
            DstvProfileType.L.ToDstvCode().Should().Be("L");
            DstvProfileType.B.ToDstvCode().Should().Be("B");
            DstvProfileType.RO.ToDstvCode().Should().Be("RO");
            DstvProfileType.M.ToDstvCode().Should().Be("M");
            DstvProfileType.SO.ToDstvCode().Should().Be("SO");
        }
    }
}
