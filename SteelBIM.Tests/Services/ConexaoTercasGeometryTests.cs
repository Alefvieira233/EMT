#nullable enable
using System.Collections.Generic;
using FluentAssertions;
using SteelBIM.Services;
using Xunit;

namespace SteelBIM.Tests.Services
{
    /// <summary>
    /// v2.8.2: tests dos helpers puros do ConexaoTercasGeometry.
    /// Cobre filtros de extremidade livre + proximidade de vigas de
    /// referencia + distancia minima. Usa ValueTuples (X,Y,Z) ao inves
    /// de XYZ — independente de Revit attached.
    /// </summary>
    public class ConexaoTercasGeometryTests
    {
        // Tipos curtos pra legibilidade
        private static (double X, double Y, double Z) Pt(double x, double y, double z = 0) => (x, y, z);
        private static ((double, double, double), (double, double, double)) Curve(
            (double, double, double) a, (double, double, double) b) => (a, b);

        // ============== IsEndpointFree ==============

        [Fact]
        public void IsEndpointFree_lista_vazia_retorna_true()
        {
            ConexaoTercasGeometry.IsEndpointFree(
                Pt(0, 0),
                new List<((double, double, double), (double, double, double))>(),
                toleranciaFt: 0.16)
                .Should().BeTrue();
        }

        [Fact]
        public void IsEndpointFree_ponto_isolado_de_outras_curvas_retorna_true()
        {
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(100, 0), Pt(200, 0)),
                Curve(Pt(0, 100), Pt(0, 200)),
            };
            ConexaoTercasGeometry.IsEndpointFree(Pt(50, 50), outras, 0.16)
                .Should().BeTrue();
        }

        [Fact]
        public void IsEndpointFree_ponto_coincide_com_start_de_outra_curva_retorna_false()
        {
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(10, 0), Pt(20, 0)),
            };
            ConexaoTercasGeometry.IsEndpointFree(Pt(10, 0), outras, 0.16)
                .Should().BeFalse();
        }

        [Fact]
        public void IsEndpointFree_ponto_coincide_com_end_de_outra_curva_retorna_false()
        {
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(10, 0), Pt(20, 5)),
            };
            ConexaoTercasGeometry.IsEndpointFree(Pt(20, 5), outras, 0.16)
                .Should().BeFalse();
        }

        [Fact]
        public void IsEndpointFree_ponto_no_MEIO_de_outra_curva_retorna_true()
        {
            // Endpoint compara apenas com start/end, nao com pontos interiores
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            ConexaoTercasGeometry.IsEndpointFree(Pt(50, 0), outras, 0.16)
                .Should().BeTrue();
        }

        [Fact]
        public void IsEndpointFree_ponto_proximo_de_endpoint_dentro_da_tolerancia_retorna_false()
        {
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(10, 0), Pt(20, 0)),
            };
            // 0.1 ft = 30.48 mm — dentro da tolerancia de 0.16 ft (50 mm)
            ConexaoTercasGeometry.IsEndpointFree(Pt(10.1, 0), outras, 0.16)
                .Should().BeFalse();
        }

        [Fact]
        public void IsEndpointFree_ponto_alem_da_tolerancia_retorna_true()
        {
            var outras = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(10, 0), Pt(20, 0)),
            };
            // 0.5 ft = 152.4 mm — alem da tolerancia de 0.16 ft (50 mm)
            ConexaoTercasGeometry.IsEndpointFree(Pt(10.5, 0), outras, 0.16)
                .Should().BeTrue();
        }

        // ============== IsCloseToReference ==============

        [Fact]
        public void IsCloseToReference_lista_vazia_retorna_false()
        {
            ConexaoTercasGeometry.IsCloseToReference(
                Pt(0, 0),
                new List<((double, double, double), (double, double, double))>(),
                maxDistFt: 10.0)
                .Should().BeFalse();
        }

        [Fact]
        public void IsCloseToReference_ponto_sobre_a_curva_retorna_true()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            ConexaoTercasGeometry.IsCloseToReference(Pt(50, 0), refs, 0.1)
                .Should().BeTrue();
        }

        [Fact]
        public void IsCloseToReference_ponto_dentro_da_dist_max_retorna_true()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            // ponto a 3 ft da curva (no eixo Y) — dentro de maxDist 5 ft
            ConexaoTercasGeometry.IsCloseToReference(Pt(50, 3), refs, 5.0)
                .Should().BeTrue();
        }

        [Fact]
        public void IsCloseToReference_ponto_fora_da_dist_max_retorna_false()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            // ponto a 10 ft da curva — fora de maxDist 5 ft
            ConexaoTercasGeometry.IsCloseToReference(Pt(50, 10), refs, 5.0)
                .Should().BeFalse();
        }

        [Fact]
        public void IsCloseToReference_extrapolacao_alem_dos_endpoints_calcula_para_endpoint_mais_proximo()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            // ponto X=150 está 50 ft alem do end(100,0,0); dist = 50
            ConexaoTercasGeometry.IsCloseToReference(Pt(150, 0), refs, 51.0)
                .Should().BeTrue();
            ConexaoTercasGeometry.IsCloseToReference(Pt(150, 0), refs, 49.0)
                .Should().BeFalse();
        }

        [Fact]
        public void IsCloseToReference_multiplas_curvas_pega_a_mais_proxima()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),    // longe
                Curve(Pt(0, 50), Pt(100, 50)),  // perto do ponto
            };
            // ponto (50, 49, 0) está a 1 ft da segunda curva — dentro de maxDist 2 ft
            ConexaoTercasGeometry.IsCloseToReference(Pt(50, 49), refs, 2.0)
                .Should().BeTrue();
        }

        // ============== MinDistanceToReferences ==============

        [Fact]
        public void MinDistanceToReferences_lista_vazia_retorna_MaxValue()
        {
            ConexaoTercasGeometry.MinDistanceToReferences(
                Pt(0, 0),
                new List<((double, double, double), (double, double, double))>())
                .Should().Be(double.MaxValue);
        }

        [Fact]
        public void MinDistanceToReferences_ponto_sobre_curva_retorna_zero()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 0), Pt(100, 0)),
            };
            ConexaoTercasGeometry.MinDistanceToReferences(Pt(50, 0), refs)
                .Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void MinDistanceToReferences_multiplas_curvas_retorna_a_menor()
        {
            var refs = new List<((double, double, double), (double, double, double))>
            {
                Curve(Pt(0, 100), Pt(100, 100)),   // dist 100 do ponto (50,0,0)
                Curve(Pt(0, 0), Pt(100, 0)),       // dist 0 do ponto (50,0,0)
            };
            ConexaoTercasGeometry.MinDistanceToReferences(Pt(50, 0), refs)
                .Should().BeApproximately(0, 1e-9);
        }

        // ============== DistanceToSegment ==============

        [Fact]
        public void DistanceToSegment_ponto_sobre_segmento_retorna_zero()
        {
            ConexaoTercasGeometry.DistanceToSegment(
                Pt(5, 0), Pt(0, 0), Pt(10, 0))
                .Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void DistanceToSegment_ponto_perpendicular_retorna_dist_perpendicular()
        {
            ConexaoTercasGeometry.DistanceToSegment(
                Pt(5, 3), Pt(0, 0), Pt(10, 0))
                .Should().BeApproximately(3, 1e-9);
        }

        [Fact]
        public void DistanceToSegment_clamping_quando_projecao_alem_do_end()
        {
            // Ponto a (15, 0, 0) projeta em t=1.5 (alem do segmento [0..10])
            // Clamping em t=1 -> projecao = end (10, 0, 0); dist = 5
            ConexaoTercasGeometry.DistanceToSegment(
                Pt(15, 0), Pt(0, 0), Pt(10, 0))
                .Should().BeApproximately(5, 1e-9);
        }

        [Fact]
        public void DistanceToSegment_clamping_quando_projecao_antes_do_start()
        {
            // Ponto a (-5, 0, 0) projeta em t=-0.5; clamp em t=0 -> start
            // dist = 5
            ConexaoTercasGeometry.DistanceToSegment(
                Pt(-5, 0), Pt(0, 0), Pt(10, 0))
                .Should().BeApproximately(5, 1e-9);
        }

        [Fact]
        public void DistanceToSegment_segmento_degenerado_calcula_para_o_ponto()
        {
            // a == b: segmento eh ponto unico; retorna p.DistanceTo(a)
            ConexaoTercasGeometry.DistanceToSegment(
                Pt(3, 4), Pt(0, 0), Pt(0, 0))
                .Should().BeApproximately(5, 1e-9); // triangulo 3-4-5
        }

        // ============== IntersectXY (v2.8.3) ==============

        [Fact]
        public void IntersectXY_perpendiculares_no_meio_dos_segmentos_retorna_ponto_correto()
        {
            // Terça horizontal (0,0)→(100,0) cruzando viga vertical (50,-50)→(50,50)
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(50, -50), Pt(50, 50));

            result.Should().NotBeNull();
            result!.Value.X.Should().BeApproximately(50, 1e-9);
            result.Value.Y.Should().BeApproximately(0, 1e-9);
            result.Value.Z.Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void IntersectXY_paralelas_retorna_null()
        {
            // Duas terças paralelas — nao se cruzam
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(0, 10), Pt(100, 10));

            result.Should().BeNull();
        }

        [Fact]
        public void IntersectXY_sobrepostas_retorna_null()
        {
            // Linhas identicas (paralelas com det=0)
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(0, 0), Pt(100, 0));

            result.Should().BeNull();
        }

        [Fact]
        public void IntersectXY_extrapolacao_alem_da_terca_retorna_null()
        {
            // Viga estaria em X=150, mas a terça vai so ate X=100 → fora do segmento
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(150, -50), Pt(150, 50));

            result.Should().BeNull();
        }

        [Fact]
        public void IntersectXY_extrapolacao_alem_da_viga_retorna_null()
        {
            // Cruzamento XY em (50, 0) mas a viga so vai de (50, 10) ate (50, 50) → t fora de [0,1]
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(50, 10), Pt(50, 50));

            result.Should().BeNull();
        }

        [Fact]
        public void IntersectXY_preserva_Z_da_terca_em_terca_inclinada()
        {
            // Terça inclinada: Z=0 em p0, Z=10 em p1. Cruzamento em s=0.5 → Z=5
            var result = ConexaoTercasGeometry.IntersectXY(
                (0, 0, 0), (100, 0, 10),
                (50, -50, 0), (50, 50, 0));

            result.Should().NotBeNull();
            result!.Value.Z.Should().BeApproximately(5, 1e-9);
        }

        [Fact]
        public void IntersectXY_viga_muito_abaixo_da_terca_retorna_null()
        {
            // Terça em Z=20, viga em Z=0 — gap de 20 ft > default 10 ft → null
            var result = ConexaoTercasGeometry.IntersectXY(
                (0, 0, 20), (100, 0, 20),
                (50, -50, 0), (50, 50, 0));

            result.Should().BeNull();
        }

        [Fact]
        public void IntersectXY_viga_no_mesmo_nivel_passa_no_guard_vertical()
        {
            // Terça em Z=5, viga em Z=4 — gap de 1 ft < default 10 ft → OK
            var result = ConexaoTercasGeometry.IntersectXY(
                (0, 0, 5), (100, 0, 5),
                (50, -50, 4), (50, 50, 4));

            result.Should().NotBeNull();
            result!.Value.Z.Should().BeApproximately(5, 1e-9, "Z preserva o da terça, nao da viga");
        }

        [Fact]
        public void IntersectXY_3_vigas_paralelas_terca_perpendicular_3_intersecoes()
        {
            // Cenario real: galpao com 3 vigas paralelas em X=10, 50, 90
            // 1 terça transversal cruzando todas em Y=0
            var terca = (start: Pt(0, 0), end: Pt(100, 0));

            var viga1 = ConexaoTercasGeometry.IntersectXY(
                terca.start, terca.end, Pt(10, -10), Pt(10, 10));
            var viga2 = ConexaoTercasGeometry.IntersectXY(
                terca.start, terca.end, Pt(50, -10), Pt(50, 10));
            var viga3 = ConexaoTercasGeometry.IntersectXY(
                terca.start, terca.end, Pt(90, -10), Pt(90, 10));

            viga1.Should().NotBeNull();
            viga2.Should().NotBeNull();
            viga3.Should().NotBeNull();
            viga1!.Value.X.Should().BeApproximately(10, 1e-9);
            viga2!.Value.X.Should().BeApproximately(50, 1e-9);
            viga3!.Value.X.Should().BeApproximately(90, 1e-9);
        }

        [Fact]
        public void IntersectXY_intersecao_obliqua_calcula_corretamente()
        {
            // Terça (0,0)→(10,10) e viga (0,10)→(10,0) — cruzam em (5,5)
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(10, 10),
                Pt(0, 10), Pt(10, 0));

            result.Should().NotBeNull();
            result!.Value.X.Should().BeApproximately(5, 1e-9);
            result.Value.Y.Should().BeApproximately(5, 1e-9);
        }

        [Fact]
        public void IntersectXY_no_endpoint_exatamente_aceita()
        {
            // Viga começa exatamente no endpoint da terça (s=0, t=0)
            var result = ConexaoTercasGeometry.IntersectXY(
                Pt(0, 0), Pt(100, 0),
                Pt(0, 0), Pt(0, 50));

            result.Should().NotBeNull();
            result!.Value.X.Should().BeApproximately(0, 1e-9);
        }

        // v2.8.11: cruzamento na PONTA (terças/vigas das extremidades) — sem tolerancia o
        // clamp estrito rejeitava (causa do "ultimas terças sem ligacao"); com tolerancia aceita.
        [Fact]
        public void IntersectXY_CruzamentoNaPonta_RejeitaSemTol_AceitaComTol()
        {
            // terça (0,0)->(100,0); viga vertical em X=101 (1 alem da ponta da terça) -> s=1.01
            var terP0 = Pt(0, 0);
            var terP1 = Pt(100, 0);
            var vigP0 = Pt(101, -50);
            var vigP1 = Pt(101, 50);

            // clamp estrito (tol default 0): s=1.01 > 1 -> null
            ConexaoTercasGeometry.IntersectXY(terP0, terP1, vigP0, vigP1).Should().BeNull();

            // com tolerancia 2 (tolS = 2/100 = 0.02 >= 0.01): aceita; ponto fixado na ponta (s->1)
            var r = ConexaoTercasGeometry.IntersectXY(
                terP0, terP1, vigP0, vigP1, maxVerticalGapFt: 10.0, toleranciaSegmentoFt: 2.0);
            r.Should().NotBeNull();
            r!.Value.X.Should().BeApproximately(100, 1e-9);
            r.Value.Y.Should().BeApproximately(0, 1e-9);
        }
    }
}
