using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    /// <summary>
    /// UpdateSession eh estatica — usamos uma collection xUnit para
    /// serializar os testes (evita race em estado global compartilhado
    /// entre testes paralelos).
    /// </summary>
    [Collection("UpdateSession")]
    public class UpdateSessionTests
    {
        public UpdateSessionTests()
        {
            UpdateSession.Reset();
        }

        [Fact]
        public void Inicio_zero_falhas_e_nao_disabled()
        {
            UpdateSession.ConsecutiveIoFailures.Should().Be(0);
            UpdateSession.IsDisabledForSession.Should().BeFalse();
        }

        [Fact]
        public void Uma_falha_nao_disable()
        {
            UpdateSession.RecordIoFailure("test");
            UpdateSession.ConsecutiveIoFailures.Should().Be(1);
            UpdateSession.IsDisabledForSession.Should().BeFalse();
        }

        [Fact]
        public void Duas_falhas_disable()
        {
            UpdateSession.RecordIoFailure("test1");
            UpdateSession.RecordIoFailure("test2");
            UpdateSession.ConsecutiveIoFailures.Should().Be(2);
            UpdateSession.IsDisabledForSession.Should().BeTrue();
        }

        [Fact]
        public void Tres_falhas_continua_disabled_e_count_continua_subindo()
        {
            UpdateSession.RecordIoFailure("a");
            UpdateSession.RecordIoFailure("b");
            UpdateSession.RecordIoFailure("c");
            UpdateSession.ConsecutiveIoFailures.Should().Be(3);
            UpdateSession.IsDisabledForSession.Should().BeTrue();
        }

        [Fact]
        public void Sucesso_zera_contador_mas_nao_re_habilita_se_ja_disabled()
        {
            UpdateSession.RecordIoFailure("a");
            UpdateSession.RecordIoFailure("b");
            UpdateSession.IsDisabledForSession.Should().BeTrue();

            UpdateSession.RecordIoSuccess();

            UpdateSession.ConsecutiveIoFailures.Should().Be(0);
            // disabled persiste — so reboot reabilita (intencional)
            UpdateSession.IsDisabledForSession.Should().BeTrue();
        }

        [Fact]
        public void Sucesso_zera_contador_quando_nao_disabled()
        {
            UpdateSession.RecordIoFailure("a");
            UpdateSession.ConsecutiveIoFailures.Should().Be(1);

            UpdateSession.RecordIoSuccess();

            UpdateSession.ConsecutiveIoFailures.Should().Be(0);
            UpdateSession.IsDisabledForSession.Should().BeFalse();
        }

        [Fact]
        public void Reset_limpa_tudo()
        {
            UpdateSession.RecordIoFailure("a");
            UpdateSession.RecordIoFailure("b");
            UpdateSession.IsDisabledForSession.Should().BeTrue();

            UpdateSession.Reset();

            UpdateSession.ConsecutiveIoFailures.Should().Be(0);
            UpdateSession.IsDisabledForSession.Should().BeFalse();
        }

        [Fact]
        public void RecordIoFailure_aceita_context_null_sem_crashar()
        {
            // Defensive: o caller pode passar null se nao tiver contexto
            UpdateSession.RecordIoFailure(null);
            UpdateSession.ConsecutiveIoFailures.Should().Be(1);
        }
    }
}
