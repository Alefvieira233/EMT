using SteelBIM.Utils;
using Xunit;

namespace SteelBIM.Tests.Utils
{
    /// <summary>
    /// Garante que os textos de ressalva tecnica/legal (exibidos antes de gerar
    /// armadura/conexao automaticamente) permanecem presentes e com as frases-chave
    /// de responsabilidade — evita que um refactor remova o aviso silenciosamente.
    /// </summary>
    public class DisclaimerTextsTests
    {
        [Fact]
        public void Armadura_NaoVazio_ContemRessalvaNormativa()
        {
            Assert.False(string.IsNullOrWhiteSpace(DisclaimerTexts.Armadura));
            Assert.Contains("projeto estrutural", DisclaimerTexts.Armadura);
            Assert.Contains("NBR 6118", DisclaimerTexts.Armadura);
        }

        [Fact]
        public void Conexoes_NaoVazio_AvisaSemVerificacaoDeCapacidade()
        {
            Assert.False(string.IsNullOrWhiteSpace(DisclaimerTexts.Conexoes));
            Assert.Contains("capacidade", DisclaimerTexts.Conexoes);
            Assert.Contains("projeto estrutural", DisclaimerTexts.Conexoes);
        }
    }
}
