using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using FerramentaEMT.Models;

namespace FerramentaEMT.Tests.Models
{
    /// <summary>
    /// Cobertura do shape do resultado retornado pelo <c>CortarElementosService</c>.
    /// Como a logica real depende do Revit API (geometria, SubTransaction, JoinGeometryUtils),
    /// testamos apenas o payload — que e onde moram os bugs de contagem, contratos e fallbacks-null.
    /// </summary>
    public class CortarElementosResultadoTests
    {
        [Fact]
        public void Constructor_FullArgs_PreservaValores()
        {
            var ids = new List<long> { 42L, 43L };
            var diag = new List<string> { "linha 1", "linha 2" };

            var r = new CortarElementosResultado(
                totalSelecionados: 10,
                hostsAnalisados: 3,
                cuttersAnalisados: 4,
                paresIntersectando: 5,
                alteracoesAplicadas: 2,
                jaConformes: 1,
                falhas: 2,
                elementosRelacionados: ids,
                diagnostico: diag);

            r.TotalSelecionados.Should().Be(10);
            r.HostsAnalisados.Should().Be(3);
            r.CuttersAnalisados.Should().Be(4);
            r.ParesIntersectando.Should().Be(5);
            r.AlteracoesAplicadas.Should().Be(2);
            r.JaConformes.Should().Be(1);
            r.Falhas.Should().Be(2);
            r.ElementosRelacionados.Should().BeSameAs(ids);
            r.Diagnostico.Should().BeSameAs(diag);
        }

        [Fact]
        public void Constructor_ListasNull_UsaColecoesVaziasNaoNulas()
        {
            var r = new CortarElementosResultado(
                totalSelecionados: 0,
                hostsAnalisados: 0,
                cuttersAnalisados: 0,
                paresIntersectando: 0,
                alteracoesAplicadas: 0,
                jaConformes: 0,
                falhas: 0,
                elementosRelacionados: null,
                diagnostico: null);

            r.ElementosRelacionados.Should().NotBeNull();
            r.ElementosRelacionados.Should().BeEmpty();
            r.Diagnostico.Should().NotBeNull();
            r.Diagnostico.Should().BeEmpty();
        }

        [Fact]
        public void HouveAlteracao_AlteracoesPositivas_RetornaTrue()
        {
            var r = NovoResultado(alteracoesAplicadas: 3, jaConformes: 0);

            r.HouveAlteracao.Should().BeTrue();
            r.HouveSucesso.Should().BeTrue();
        }

        [Fact]
        public void HouveAlteracao_AlteracoesZero_RetornaFalse()
        {
            var r = NovoResultado(alteracoesAplicadas: 0, jaConformes: 0);

            r.HouveAlteracao.Should().BeFalse();
        }

        [Fact]
        public void HouveSucesso_SomenteJaConformes_RetornaTrue()
        {
            // Regra chave: se ha pares ja corretos, a rodada nao foi um "erro" —
            // e o que o comando usa para decidir entre Info ("nada novo para cortar")
            // e Warning ("nenhum corte aplicado").
            var r = NovoResultado(alteracoesAplicadas: 0, jaConformes: 2);

            r.HouveAlteracao.Should().BeFalse();
            r.HouveSucesso.Should().BeTrue();
        }

        [Fact]
        public void HouveSucesso_NadaFeito_RetornaFalse()
        {
            var r = NovoResultado(alteracoesAplicadas: 0, jaConformes: 0);

            r.HouveAlteracao.Should().BeFalse();
            r.HouveSucesso.Should().BeFalse();
        }

        private static CortarElementosResultado NovoResultado(int alteracoesAplicadas, int jaConformes)
        {
            return new CortarElementosResultado(
                totalSelecionados: 5,
                hostsAnalisados: 2,
                cuttersAnalisados: 2,
                paresIntersectando: alteracoesAplicadas + jaConformes,
                alteracoesAplicadas: alteracoesAplicadas,
                jaConformes: jaConformes,
                falhas: 0,
                elementosRelacionados: new List<long>(),
                diagnostico: new List<string>());
        }
    }
}
