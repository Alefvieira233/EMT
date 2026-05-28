#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.2: helpers geometricos puros (sem Revit) usados pelo
    /// <see cref="ConexaoTercasService"/> para filtrar extremidades de
    /// terças. Recebem coords como ValueTuples <c>(X, Y, Z)</c> ao inves
    /// de <c>Autodesk.Revit.DB.XYZ</c> pra permitir cobertura xUnit sem
    /// Revit attached.
    ///
    /// <para>O caller (Service) e responsavel por converter XYZ -&gt; tupla
    /// antes de chamar — typicamente uma linha unica:
    /// <c>var t = (xyz.X, xyz.Y, xyz.Z);</c></para>
    ///
    /// <para>Adapta as funcoes EsExtremoLibre, EstaCercaDeReferencia,
    /// DistanciaMinimaAReferencias do algoritmo de inserir conexoes em
    /// correas laterais.</para>
    /// </summary>
    public static class ConexaoTercasGeometry
    {
        /// <summary>
        /// Distancia maxima padrao (em pés) entre uma extremidade de terça
        /// e a curva da viga de apoio mais proxima. Acima disso, o
        /// endpoint é considerado "longe demais" e descartado.
        /// 2000 mm ≈ 6.5616 ft.
        /// </summary>
        public const double DefaultMaxDistanceToBeamMm = 2000.0;

        /// <summary>1 mm em pés (304.8 mm/ft exato).</summary>
        public const double FtPerMm = 1.0 / 304.8;

        /// <summary>
        /// Verdadeiro se <paramref name="ponto"/> NAO coincide (dentro de
        /// <paramref name="toleranciaFt"/>) com nenhum endpoint das
        /// <paramref name="curvasOutrasTercas"/>. Usado pra detectar
        /// extremidade "livre" — quando a terça termina em um nó onde
        /// nenhuma outra terça também termina.
        ///
        /// <para>Convencao: comparison estritamente menor (&lt;) — exatamente
        /// na tolerancia NAO conecta, comportamento defensivo igual ao
        /// dedup XY.</para>
        /// </summary>
        public static bool IsEndpointFree(
            (double X, double Y, double Z) ponto,
            IEnumerable<((double X, double Y, double Z) start, (double X, double Y, double Z) end)> curvasOutrasTercas,
            double toleranciaFt)
        {
            foreach (var (a, b) in curvasOutrasTercas)
            {
                if (Distance(ponto, a) < toleranciaFt)
                    return false;
                if (Distance(ponto, b) < toleranciaFt)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Verdadeiro se <paramref name="ponto"/> está dentro de
        /// <paramref name="maxDistFt"/> da curva mais proxima entre as
        /// <paramref name="curvasReferencia"/>. Usa projecao no segmento
        /// (Closest Point on Line Segment) — clampada entre start e end.
        ///
        /// <para>Convencao: comparison estritamente menor (&lt;).</para>
        /// </summary>
        public static bool IsCloseToReference(
            (double X, double Y, double Z) ponto,
            IEnumerable<((double X, double Y, double Z) start, (double X, double Y, double Z) end)> curvasReferencia,
            double maxDistFt)
        {
            foreach (var curva in curvasReferencia)
            {
                if (DistanceToSegment(ponto, curva.start, curva.end) < maxDistFt)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Retorna a menor distancia entre <paramref name="ponto"/> e as
        /// <paramref name="curvasReferencia"/>. Lista vazia -> double.MaxValue.
        /// </summary>
        public static double MinDistanceToReferences(
            (double X, double Y, double Z) ponto,
            IEnumerable<((double X, double Y, double Z) start, (double X, double Y, double Z) end)> curvasReferencia)
        {
            double min = double.MaxValue;
            foreach (var curva in curvasReferencia)
            {
                double d = DistanceToSegment(ponto, curva.start, curva.end);
                if (d < min)
                    min = d;
            }
            return min;
        }

        /// <summary>
        /// Distancia ponto-segmento (com clamping). Sem dependencia do
        /// Revit Curve.Project — pra ficar testavel sem Revit attached.
        /// </summary>
        internal static double DistanceToSegment(
            (double X, double Y, double Z) p,
            (double X, double Y, double Z) a,
            (double X, double Y, double Z) b)
        {
            double abx = b.X - a.X;
            double aby = b.Y - a.Y;
            double abz = b.Z - a.Z;
            double lenSq = abx * abx + aby * aby + abz * abz;
            if (lenSq < 1e-12)
                return Distance(p, a); // segmento degenerado vira ponto

            double apx = p.X - a.X;
            double apy = p.Y - a.Y;
            double apz = p.Z - a.Z;
            double t = (apx * abx + apy * aby + apz * abz) / lenSq;
            t = Math.Max(0.0, Math.Min(1.0, t));

            double projX = a.X + abx * t;
            double projY = a.Y + aby * t;
            double projZ = a.Z + abz * t;
            return Distance(p, (projX, projY, projZ));
        }

        private static double Distance(
            (double X, double Y, double Z) a,
            (double X, double Y, double Z) b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
