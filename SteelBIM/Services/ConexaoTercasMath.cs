#nullable enable
using System;

namespace SteelBIM.Services
{
    /// <summary>
    /// Math puro do <see cref="ConexaoTercasService"/> — sem dependencia de
    /// <c>Autodesk.Revit.DB</c>. Permite testes xUnit sem precisar de Revit attached.
    ///
    /// <para>v2.8.1 (Victor) — extraido pra unidade testavel.</para>
    /// </summary>
    public static class ConexaoTercasMath
    {
        /// <summary>
        /// Tolerancia de deduplicacao em milimetros. Dois nós dentro dessa
        /// distancia (no plano XY) sao considerados o mesmo nó — apenas
        /// uma conexao é inserida pra ambos.
        /// </summary>
        public const double DedupToleranceMm = 50.0;

        /// <summary>1 pé = 304.8 mm (constante exata por definicao do Revit).</summary>
        public const double MmPerFoot = 304.8;

        /// <summary>Tolerancia em pés (unidade interna do Revit).</summary>
        public static readonly double DedupToleranceFt = DedupToleranceMm / MmPerFoot;

        /// <summary>
        /// True se a distancia XY entre dois pontos é estritamente menor que
        /// <paramref name="toleranceFt"/>. Z é ignorado.
        ///
        /// <para>Convencao: <c>&lt;</c> e nao <c>&lt;=</c> — exatamente na tolerancia
        /// nao deduplica (mais defensivo, evita perder conexoes em casos
        /// borderline de geometria perfeitamente alinhada).</para>
        /// </summary>
        public static bool IsWithinDistanceXY(
            double x1, double y1,
            double x2, double y2,
            double toleranceFt)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy) < toleranceFt;
        }
    }
}
