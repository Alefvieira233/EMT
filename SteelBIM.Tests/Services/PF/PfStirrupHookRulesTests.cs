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
    }
}
