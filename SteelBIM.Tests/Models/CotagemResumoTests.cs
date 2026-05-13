using System.Reflection;
using FerramentaEMT.Models;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Models
{
    /// <summary>
    /// Cobertura do POCO retornado pelos entry-points de <c>CotasService</c>
    /// (P1.4 do plano de lapidacao 2026-04-29). Service em si nao e testavel
    /// fora do Revit; cobrimos a estrutura do resultado.
    /// </summary>
    public class CotagemResumoTests
    {
        [Fact]
        public void CanceladoPeloUsuario_RetornaInstanciaComCanceladoTrue()
        {
            var r = CotagemResumo.CanceladoPeloUsuario();

            r.Cancelado.Should().BeTrue();
            r.CotasCriadas.Should().Be(0);
            r.ElementosCotados.Should().Be(0);
            r.MensagemSucessoFormatada.Should().BeNull();
            r.Avisos.Should().BeEmpty();
        }

        [Fact]
        public void Default_NaoCancelado_EZerado()
        {
            var r = new CotagemResumo();

            r.Cancelado.Should().BeFalse();
            r.CotasCriadas.Should().Be(0);
            r.ElementosCotados.Should().Be(0);
            r.MensagemSucessoFormatada.Should().BeNull();
        }

        [Fact]
        public void Avisos_NuncaNull_DefaultArrayVazio()
        {
            var r = new CotagemResumo();

            r.Avisos.Should().NotBeNull();
            r.Avisos.Should().BeEmpty();
        }

        [Fact]
        public void Init_PermiteInicializacao_PreservaValores()
        {
            var r = new CotagemResumo
            {
                CotasCriadas = 5,
                ElementosCotados = 3,
                Avisos = new[] { "aviso 1", "aviso 2" },
                MensagemSucessoFormatada = "tudo certo",
            };

            r.CotasCriadas.Should().Be(5);
            r.ElementosCotados.Should().Be(3);
            r.Avisos.Should().HaveCount(2).And.Contain("aviso 1").And.Contain("aviso 2");
            r.MensagemSucessoFormatada.Should().Be("tudo certo");
            r.Cancelado.Should().BeFalse();
        }

        [Fact]
        public void Propriedades_SaoInitOnly_NaoMutaveis()
        {
            var props = typeof(CotagemResumo).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var p in props)
            {
                var setter = p.SetMethod;
                setter.Should().NotBeNull(because: "{0} deve ter setter (init-only conta como SetMethod)", p.Name);

                // Init-only setters tem o modreq IsExternalInit; setters convencionais nao.
                var modifiers = setter!.ReturnParameter.GetRequiredCustomModifiers();
                modifiers.Should().Contain(t => t.Name == "IsExternalInit",
                    because: "{0} deve ser init-only (CotagemResumo e imutavel apos construcao)", p.Name);
            }
        }
    }
}
