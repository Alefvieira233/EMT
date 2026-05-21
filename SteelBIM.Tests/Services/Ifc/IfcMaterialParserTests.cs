using SteelBIM.Services.Ifc;
using Xunit;

namespace SteelBIM.Tests.Services.Ifc
{
    /// <summary>
    /// Testes puros do <see cref="IfcMaterialParser"/> (BUG 3 v2.7.0):
    /// extracao de secao + material do campo IfcMaterial, com deteccao de
    /// pseudo-secoes de concreto (R_M1, SQ_M1, etc) que antes eram aceitas
    /// como falsos perfis estruturais.
    /// </summary>
    public class IfcMaterialParserTests
    {
        // ============================================================
        // ExtrairNomeSecao — aceitar perfis de aco, rejeitar concreto
        // ============================================================

        [Theory]
        // Cenarios CYPE/AISC tipicos (aceitar)
        [InlineData("A572, Grade 50 (HR Shapes) | AISC 360-16 (W 360x44.6)", "W 360x44.6")]
        // Cenario sem parenteses: parser remove so prefixos com hifen (ex: "AISC 360-16")
        // ou puro-digito. "ASME" sozinho nao se encaixa, fica preservado.
        [InlineData("A36 | ASME C 200/80/2/2/25/C", "ASME C 200/80/2/2/25/C")]
        [InlineData("A572 | AISC (W 200x15)", "W 200x15")]
        [InlineData("S275 | EN 10025-2 (IPE 200)", "IPE 200")]
        [InlineData("S355 | EN 10025-2 (HEA 240)", "HEA 240")]
        [InlineData("A500 GrB | AISC (HSS 200x100x8)", "HSS 200x100x8")]
        // Cenario com prefixo hifenado (removido)
        [InlineData("A572 | AISC-360 W 200x15", "W 200x15")]
        // Vazios/null
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData(null, "")]
        public void ExtrairNomeSecao_AceitaPerfisDeAco(string entrada, string esperado)
        {
            Assert.Equal(esperado, IfcMaterialParser.ExtrairNomeSecao(entrada));
        }

        // ============================================================
        // BUG 3 v2.7.0: pseudo-secoes de concreto devem retornar string vazia
        // ============================================================

        [Theory]
        // R_M, S_M, RQ_M, SQ_M com numero (classificacao de armadura)
        [InlineData("Concrete f'c=4000 psi | ACI 318-25 (R_M1 R_M1 200/400)", "")]
        [InlineData("C25/30 | EN 1992-1-1:2004 (SQ_M1 250/400)", "")]
        [InlineData("C30/37 | EN 1992 (R_M2 300/500)", "")]
        [InlineData("Concreto | NBR 6118 (S_M3 400/600)", "")]
        // Padrao 12phi10 (12 barras de 10mm)
        [InlineData("Armadura | NBR 7480 (12phi10)", "")]
        [InlineData("Steel rebar | EN 10080 (8phi16)", "")]
        // Padrao XX/YY sozinho (so dimensao, sem prefixo de tipo estrutural)
        [InlineData("Concreto | EN 1992 (200/400)", "")]
        public void ExtrairNomeSecao_RejeitaPseudoSecoesDeConcreto(string entrada, string esperado)
        {
            Assert.Equal(esperado, IfcMaterialParser.ExtrairNomeSecao(entrada));
        }

        // ============================================================
        // ExtrairNomeMaterial — usado para agrupamento (BUG 3 v2.7.0)
        // ============================================================

        [Theory]
        [InlineData("A572, Grade 50 (HR Shapes) | AISC 360-16 (W 360x44.6)", "A572, Grade 50")]
        [InlineData("A36 | ASME C 200/80/2/2/25/C", "A36")]
        [InlineData("S275 | EN 10025-2 (IPE 200)", "S275")]
        [InlineData("", "")]
        [InlineData("Só material sem pipe", "Só material sem pipe")]
        public void ExtrairNomeMaterial_ExtraiCorretamente(string entrada, string esperado)
        {
            Assert.Equal(esperado, IfcMaterialParser.ExtrairNomeMaterial(entrada));
        }

        // ============================================================
        // Regression — comportamento original v1.4.0 do Victor preservado
        // ============================================================

        [Fact]
        public void NormalizarNome_RemoveEspacosUpperCase()
        {
            Assert.Equal("W360X44.6", IfcMaterialParser.NormalizarNome("W 360x44.6"));
            Assert.Equal("IPE200", IfcMaterialParser.NormalizarNome("ipe 200"));
            Assert.Equal("", IfcMaterialParser.NormalizarNome(""));
        }

        [Fact]
        public void CalcularScore_TipoIgualMaximoPontos()
        {
            // W 200x15 vs W 200x15 -> tipo igual (100) + dim1 (50) + dim2 (30) = 180
            int score = IfcMaterialParser.CalcularScore("W 200x15", "W 200x15");
            Assert.Equal(180, score);
        }

        [Fact]
        public void CalcularScore_TipoCompatibilizado()
        {
            // C compativel com Ue (70) — match dimensional menor
            int scoreCu = IfcMaterialParser.CalcularScore("C 200/80", "UE 200/80");
            Assert.True(scoreCu >= 70, $"C->Ue score: {scoreCu}");
        }

        [Fact]
        public void CalcularScore_TiposIncompativeisZero()
        {
            // W (I-beam) com L (cantoneira) — sem mapeamento
            int score = IfcMaterialParser.CalcularScore("W 200x15", "L 100x100x8");
            Assert.Equal(0, score);
        }
    }
}
