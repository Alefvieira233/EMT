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
    }
}
