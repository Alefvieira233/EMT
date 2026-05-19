using SteelBIM.Services.PF;
using Xunit;

namespace SteelBIM.Tests.Services.PF
{
    public class PfStirrupHookRulesTests
    {
        [Theory]
        [InlineData(90, 12.0)]
        [InlineData(135, 10.0)]   // default de estribo — era 6.0 (bug)
        [InlineData(180, 5.0)]
        [InlineData(45, 10.0)]    // fallback conservador
        [InlineData(0, 10.0)]
        public void NbrStirrupHookMultiplier_PorAngulo(int ang, double esperado)
        {
            Assert.Equal(esperado, PfStirrupHookRules.NbrStirrupHookMultiplier(ang));
        }

        [Fact]
        public void Multiplier135_NaoEh6_RegressaoV240()
        {
            Assert.NotEqual(6.0, PfStirrupHookRules.NbrStirrupHookMultiplier(135));
            Assert.Equal(10.0, PfStirrupHookRules.NbrStirrupHookMultiplier(135));
        }

        // ============================================================
        // v2.6.1 (hotfix P0 NBR-1 + NBR-2): IsCompliantWithNbr
        // Helper que valida multiplier de hook EXISTENTE antes de reusar,
        // evitando silenciosamente herdar multiplier insuficiente.
        // ============================================================

        [Theory]
        [InlineData(90, 12.0, true)]      // exato no minimo
        [InlineData(90, 14.0, true)]      // acima do minimo (OK)
        [InlineData(90, 11.99, true)]     // dentro da tolerancia default 0.01
        [InlineData(90, 11.95, false)]    // claramente abaixo (0.05 de margem)
        [InlineData(135, 10.0, true)]     // exato (post v2.4.1)
        [InlineData(135, 12.0, true)]     // acima (mais conservador)
        [InlineData(135, 6.0, false)]     // bug classico v2.4.0 — RECUSADO
        [InlineData(135, 9.95, false)]    // claramente abaixo
        [InlineData(180, 5.0, true)]      // exato
        [InlineData(180, 4.95, false)]    // claramente abaixo
        [InlineData(45, 10.0, true)]      // fallback NBR-safe
        [InlineData(45, 8.0, false)]      // abaixo do fallback
        [InlineData(0, 10.0, true)]
        public void IsCompliantWithNbr_VerificaMultiplier(int ang, double mult, bool esperado)
        {
            Assert.Equal(esperado, PfStirrupHookRules.IsCompliantWithNbr(ang, mult));
        }

        [Fact]
        public void IsCompliantWithNbr_135ComMultiplier6_RejeitaRegressaoV240()
        {
            // Cenario: projeto Revit ja tinha RebarHookType chamado
            // "EMT Gancho 135 graus" com MultiplierForTailLength = 6.0,
            // criado por v2.4.0 antes do hotfix v2.4.1. PfRebarService e
            // Bloco/RebarCreationService AGORA devem recusar reusar esse
            // hook (em v2.6.0 reusavam silenciosamente).
            Assert.False(PfStirrupHookRules.IsCompliantWithNbr(135, 6.0));
        }

        [Fact]
        public void IsCompliantWithNbr_NaoRegredeV261_NBR1_BlocoRebarCreationService()
        {
            // Guard P0 NBR-1: Bloco/RebarCreationService.GetHookTypeByAngle
            // agora delega a este helper. Se alguem mudar a tolerancia ou
            // o sinal da comparacao, este teste pega.
            Assert.True(PfStirrupHookRules.IsCompliantWithNbr(135, 10.0));
            Assert.False(PfStirrupHookRules.IsCompliantWithNbr(135, 9.5));
        }

        [Fact]
        public void IsCompliantWithNbr_NaoRegredeV261_NBR2_PfRebarServiceEarlyReturn()
        {
            // Guard P0 NBR-2: PfRebarService.GetHookTypeByAngle linha 917
            // agora valida multiplier ANTES do early-return. Cobre o caso
            // em que o template do cliente ja tem hook 135 configurado.
            Assert.False(PfStirrupHookRules.IsCompliantWithNbr(135, 6.0));
            Assert.False(PfStirrupHookRules.IsCompliantWithNbr(135, 8.0));
            Assert.True(PfStirrupHookRules.IsCompliantWithNbr(135, 10.0));
            Assert.True(PfStirrupHookRules.IsCompliantWithNbr(135, 11.0));
        }

        [Fact]
        public void IsCompliantWithNbr_ToleranciaCustomizavel()
        {
            // Tolerancia default 0.01 — chamador pode afrouxar/apertar.
            Assert.True(PfStirrupHookRules.IsCompliantWithNbr(135, 9.999, tolerance: 0.01));
            Assert.False(PfStirrupHookRules.IsCompliantWithNbr(135, 9.999, tolerance: 0.0001));
        }
    }
}
