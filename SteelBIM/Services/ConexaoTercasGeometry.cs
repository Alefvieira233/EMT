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

        /// <summary>
        /// v2.8.3: calcula a intersecao 2D (no plano XY) entre 2 segmentos —
        /// terça e viga de apoio. Preserva o Z da terça no ponto resultante.
        ///
        /// <para>Cobertura semantica:</para>
        /// <list type="bullet">
        ///   <item>Resolve sistema linear 2x2: <c>p1 + s*d1 = p2 + t*d2</c> em XY.</item>
        ///   <item>Retorna <c>null</c> se as retas sao paralelas (det ≈ 0).</item>
        ///   <item>Retorna <c>null</c> se <c>s ∉ [0,1]</c> ou <c>t ∉ [0,1]</c>
        ///         (extrapolacao alem dos segmentos).</item>
        ///   <item>Retorna <c>null</c> se a distancia 3D no ponto de intersecao
        ///         (Z da terça vs Z do segmento da viga interpolado em t) for
        ///         maior que <paramref name="maxVerticalGapFt"/>. Isso evita
        ///         que uma viga muito abaixo do plano da terça seja
        ///         considerada cruzamento valido (terça flutuando no ar).</item>
        ///   <item>Z resultante = Z da terça em <c>s</c> (preserva altura da terça).</item>
        /// </list>
        /// </summary>
        public static (double X, double Y, double Z)? IntersectXY(
            (double X, double Y, double Z) tercaP0,
            (double X, double Y, double Z) tercaP1,
            (double X, double Y, double Z) vigaP0,
            (double X, double Y, double Z) vigaP1,
            double maxVerticalGapFt = 10.0,
            double toleranciaSegmentoFt = 0.0)
        {
            double d1x = tercaP1.X - tercaP0.X;
            double d1y = tercaP1.Y - tercaP0.Y;
            double d2x = vigaP1.X - vigaP0.X;
            double d2y = vigaP1.Y - vigaP0.Y;

            // Determinante do sistema 2x2:
            //   [ d1x  -d2x ] [s]   [vigaP0.X - tercaP0.X]
            //   [ d1y  -d2y ] [t] = [vigaP0.Y - tercaP0.Y]
            double det = d1x * (-d2y) - (-d2x) * d1y; // = -d1x*d2y + d2x*d1y
            if (Math.Abs(det) < 1e-12)
                return null; // paralelas (ou um dos segmentos degenerado)

            double rhsX = vigaP0.X - tercaP0.X;
            double rhsY = vigaP0.Y - tercaP0.Y;

            // Regra de Cramer
            double s = (rhsX * (-d2y) - (-d2x) * rhsY) / det;
            double t = (d1x * rhsY - d1y * rhsX) / det;

            // Clamping em segmentos (nao retas infinitas). toleranciaSegmentoFt (em pes)
            // vira tolerancia de PARAMETRO em cada segmento — assim cruzamentos exatamente na
            // PONTA (s/t ~ 0 ou 1, comum nas terças/vigas das extremidades) nao sao rejeitados
            // por erro de ponto-flutuante nem por um pequeno overhang. Default 0 = clamp estrito.
            double lenTerca = Math.Sqrt(d1x * d1x + d1y * d1y);
            double lenViga = Math.Sqrt(d2x * d2x + d2y * d2y);
            double tolS = lenTerca > 1e-9 ? toleranciaSegmentoFt / lenTerca : 0.0;
            double tolT = lenViga > 1e-9 ? toleranciaSegmentoFt / lenViga : 0.0;
            if (s < -tolS || s > 1.0 + tolS || t < -tolT || t > 1.0 + tolT)
                return null;

            // s/t podem ter passado um pouco do segmento (dentro da tolerancia); fixa em [0,1]
            // para o ponto cair sobre as pecas reais.
            s = Math.Max(0.0, Math.Min(1.0, s));
            t = Math.Max(0.0, Math.Min(1.0, t));

            // Ponto na terça em parametro s — preserva Z da terça (interpolando se inclinada)
            double x = tercaP0.X + s * d1x;
            double y = tercaP0.Y + s * d1y;
            double z = tercaP0.Z + s * (tercaP1.Z - tercaP0.Z);

            // Guard vertical: Z da viga no ponto t, comparado com Z da terça.
            // Se a viga estiver muito abaixo (terça nao apoia ali), descarta.
            double vigaZ = vigaP0.Z + t * (vigaP1.Z - vigaP0.Z);
            if (Math.Abs(z - vigaZ) > maxVerticalGapFt)
                return null;

            return (x, y, z);
        }
    }
}
