using FluentAssertions;
using Moq;
using SteelBIM.Core;
using Xunit;

namespace SteelBIM.Tests.Core
{
    /// <summary>
    /// v2.7.9: testes-contract da interface <see cref="IUIDecisionService"/>
    /// (template ADR-003). Não testa o adapter de producao (que precisa Revit),
    /// só prova que:
    /// <list type="number">
    /// <item>Interface eh mockavel via Moq</item>
    /// <item>Signatures dos 4 metodos sao chamaveis com defaults</item>
    /// <item>Confirm() retorna bool corretamente</item>
    /// </list>
    ///
    /// Services que adotam ADR-003 (1o foi AutoVistaService em v2.7.9) usam
    /// esse pattern em testes: mockar <see cref="IUIDecisionService"/>, injetar,
    /// verificar chamadas com Moq.Verify.
    /// </summary>
    public class IUIDecisionServiceTests
    {
        [Fact]
        public void Mock_Info_Captura_Chamada_Com_Params()
        {
            var mock = new Mock<IUIDecisionService>();

            mock.Object.Info("Titulo", "Mensagem", "Cabecalho");

            mock.Verify(m => m.Info("Titulo", "Mensagem", "Cabecalho"), Times.Once);
        }

        [Fact]
        public void Mock_Warn_Captura_Chamada_Com_Headline_Null()
        {
            var mock = new Mock<IUIDecisionService>();

            mock.Object.Warn("T", "M");

            // headline default = null
            mock.Verify(m => m.Warn("T", "M", null), Times.Once);
        }

        [Fact]
        public void Mock_Error_Captura_Chamada()
        {
            var mock = new Mock<IUIDecisionService>();

            mock.Object.Error("Erro", "Detalhes do erro");

            mock.Verify(m => m.Error("Erro", "Detalhes do erro", null), Times.Once);
        }

        [Fact]
        public void Mock_Confirm_Retorna_True_Quando_Setup()
        {
            var mock = new Mock<IUIDecisionService>();
            mock.Setup(m => m.Confirm(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(true);

            bool result = mock.Object.Confirm("T", "M");

            result.Should().BeTrue();
            mock.Verify(m => m.Confirm("T", "M", null, "Continuar", "Cancelar"), Times.Once);
        }

        [Fact]
        public void Mock_Confirm_Retorna_False_Por_Default()
        {
            var mock = new Mock<IUIDecisionService>();

            // Sem Setup, Moq retorna default(bool) = false
            bool result = mock.Object.Confirm("T", "M");

            result.Should().BeFalse();
        }

        [Fact]
        public void Mock_Permite_Verify_Times_Never_Em_Metodo_Nao_Chamado()
        {
            var mock = new Mock<IUIDecisionService>();

            mock.Object.Warn("T", "M");

            // Garante que Error/Info/Confirm NAO foram chamados
            mock.Verify(m => m.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
            mock.Verify(m => m.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
            mock.Verify(m => m.Confirm(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);
        }
    }
}
