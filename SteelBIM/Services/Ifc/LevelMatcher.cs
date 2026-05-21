using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Wrapper Revit-facing do <see cref="LevelMatcherPure"/>. Adapta a logica
    /// pura testavel para o tipo <c>Level</c> da API do Revit.
    ///
    /// Substitui o fallback fixo do <c>config.NivelPadrao</c> usado pelo MVP
    /// do Victor — pecas em pisos diferentes agora ficam associadas aos
    /// niveis corretos por proximidade Z do bbox.
    /// </summary>
    public static class LevelMatcher
    {
        /// <summary>
        /// Retorna o <c>Level</c> mais proximo do Z medio dado, ou
        /// <paramref name="fallback"/> se a lista for vazia.
        /// </summary>
        public static Level MaisProximoDeZ(
            IReadOnlyList<Level> niveis,
            double zCentroFt,
            Level fallback)
        {
            if (niveis == null || niveis.Count == 0)
                return fallback;

            double[] elevs = new double[niveis.Count];
            for (int i = 0; i < niveis.Count; i++)
                elevs[i] = niveis[i].Elevation;

            int idx = LevelMatcherPure.EncontrarIndiceMaisProximoDeZ(elevs, zCentroFt);
            return idx >= 0 ? niveis[idx] : fallback;
        }
    }
}
