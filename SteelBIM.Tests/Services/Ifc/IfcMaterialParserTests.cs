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
        public void ExtrairNomeSecao_AceitaPerfisDeAco(string? entrada, string esperado)
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

        // ============================================================
        // v2.7.8 (auditoria 2026-05-25 #003 perf): memoization cache
        // de ExtrairTipoEDimensoes. Cobertos:
        // - cache popula em miss
        // - cache hit nao recomputa (sem side-effect)
        // - reset limpa
        // - strings distintas geram entries distintos
        // - whitespace/null nao polui cache (skipped no early-return)
        // ============================================================

        [Fact]
        public void Cache_ResetForTests_LimpaContador()
        {
            IfcMaterialParser.ResetCacheForTests();
            Assert.Equal(0, IfcMaterialParser.CacheCount);

            IfcMaterialParser.CalcularScore("W 360x44.6", "W360X44");
            Assert.True(IfcMaterialParser.CacheCount > 0, "Cache deve ter entries apos uma chamada");

            IfcMaterialParser.ResetCacheForTests();
            Assert.Equal(0, IfcMaterialParser.CacheCount);
        }

        [Fact]
        public void Cache_RepeatedCalls_NaoCresceContador()
        {
            IfcMaterialParser.ResetCacheForTests();

            // Primeira call popula 2 entries (secao + perfil)
            IfcMaterialParser.CalcularScore("W 360x44.6", "W360X44");
            int countAfter1 = IfcMaterialParser.CacheCount;

            // 100 calls iguais nao adicionam entries novos (cache hit)
            for (int i = 0; i < 100; i++)
                IfcMaterialParser.CalcularScore("W 360x44.6", "W360X44");

            Assert.Equal(countAfter1, IfcMaterialParser.CacheCount);
        }

        [Fact]
        public void Cache_StringsDistintas_GeramEntriesDistintas()
        {
            IfcMaterialParser.ResetCacheForTests();

            IfcMaterialParser.CalcularScore("W 360x44.6", "W360X44");
            int afterPrimeiro = IfcMaterialParser.CacheCount;

            // String diferente (mesmo tipo W mas dim diferente) deve adicionar entry
            IfcMaterialParser.CalcularScore("W 200x15", "W200X22");
            Assert.True(IfcMaterialParser.CacheCount > afterPrimeiro,
                "Strings novas devem expandir cache");
        }

        [Fact]
        public void Cache_NullOuWhitespace_NaoPolui()
        {
            IfcMaterialParser.ResetCacheForTests();

            // CalcularScore com null/empty retorna 0 ANTES de tocar cache
            int s1 = IfcMaterialParser.CalcularScore(null, "W360X44");
            int s2 = IfcMaterialParser.CalcularScore("", "W360X44");
            int s3 = IfcMaterialParser.CalcularScore("   ", "W360X44");

            Assert.Equal(0, s1);
            Assert.Equal(0, s2);
            Assert.Equal(0, s3);
            Assert.Equal(0, IfcMaterialParser.CacheCount); // nenhuma entrada poluindo
        }

        [Fact]
        public void Cache_NaoQuebraCalculoSemantico_ComparaComBaseline()
        {
            // Smoke regression: cache NAO deve mudar o resultado de CalcularScore.
            // Computa baseline limpo, depois roda 10x e compara.
            IfcMaterialParser.ResetCacheForTests();
            int baseline = IfcMaterialParser.CalcularScore("W 360x44.6", "W360x44.6");

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(baseline, IfcMaterialParser.CalcularScore("W 360x44.6", "W360x44.6"));
            }
        }
    }
}
