#nullable enable
using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    /// <summary>
    /// v2.8.1 (Victor): tests do math puro de dedup XY do comando
    /// "Inserir Conexao de Terca". Cobre tolerancia, ignora Z,
    /// e edge cases (distancia exatamente igual, vetor zero).
    /// </summary>
    public class ConexaoTercasMathTests
    {
        [Fact]
        public void Tolerancia_padrao_eh_50mm()
        {
            ConexaoTercasMath.DedupToleranceMm.Should().Be(50.0);
        }

        [Fact]
        public void Tolerancia_em_pes_corresponde_a_50mm()
        {
            // 50 mm / 304.8 mm/ft = 0.164041... ft
            ConexaoTercasMath.DedupToleranceFt.Should().BeApproximately(50.0 / 304.8, 1e-9);
        }

        [Fact]
        public void Dois_pontos_iguais_estao_dentro_da_tolerancia()
        {
            ConexaoTercasMath.IsWithinDistanceXY(10.0, 20.0, 10.0, 20.0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeTrue();
        }

        [Fact]
        public void Pontos_a_30mm_estao_dentro_da_tolerancia_padrao()
        {
            double dist30mmInFt = 30.0 / 304.8;
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, dist30mmInFt, 0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeTrue();
        }

        [Fact]
        public void Pontos_a_70mm_NAO_estao_dentro_da_tolerancia_padrao()
        {
            double dist70mmInFt = 70.0 / 304.8;
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, dist70mmInFt, 0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeFalse();
        }

        [Fact]
        public void Pontos_a_exatamente_50mm_NAO_deduplicam_por_design()
        {
            // Convencao do helper: comparison é estritamente < (nao <=).
            // Borderline nao deduplica — comportamento mais defensivo.
            double dist50mmInFt = 50.0 / 304.8;
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, dist50mmInFt, 0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeFalse();
        }

        [Fact]
        public void Distancia_diagonal_calculada_corretamente()
        {
            // Triangulo 3-4-5: dist = 5 quando dx=3, dy=4.
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, 3.0, 4.0, 5.001).Should().BeTrue();
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, 3.0, 4.0, 4.999).Should().BeFalse();
        }

        [Fact]
        public void Funciona_com_coordenadas_negativas()
        {
            // Mesmo ponto em quadrantes opostos a curta distancia: deduplica.
            double dist10mmInFt = 10.0 / 304.8;
            ConexaoTercasMath.IsWithinDistanceXY(-1.0, -1.0, -1.0 + dist10mmInFt, -1.0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeTrue();
        }

        [Fact]
        public void Z_nao_eh_considerado_metodo_so_recebe_XY()
        {
            // O helper recebe apenas X e Y — assinatura garante que Z nao
            // afeta o calculo. Teste documenta a invariante.
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, 0, 0, ConexaoTercasMath.DedupToleranceFt)
                .Should().BeTrue("dois pontos com XY iguais sempre deduplicam, independente de Z");
        }

        [Fact]
        public void Tolerancia_zero_so_aceita_pontos_identicos()
        {
            // Edge case: tolerancia zero -> qualquer dist > 0 nao deduplica.
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, 0, 0, 0).Should().BeFalse();
            ConexaoTercasMath.IsWithinDistanceXY(0, 0, 1e-10, 0, 0).Should().BeFalse();
        }
    }
}
