using System;
using System.Collections.Generic;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Calculadora pura (sem Autodesk.Revit.DB.Level) usada por
    /// <see cref="LevelMatcher"/>. Vive em arquivo separado para ser
    /// linkavel pelo projeto de testes sem trazer dependencia Revit.
    /// </summary>
    public static class LevelMatcherPure
    {
        /// <summary>
        /// Retorna o indice do nivel com elevacao mais proxima de
        /// <paramref name="zCentroFt"/>. Retorna -1 se a lista for nula ou
        /// vazia. Empate: retorna o de menor indice (determinismo).
        /// </summary>
        public static int EncontrarIndiceMaisProximoDeZ(
            IReadOnlyList<double> elevacoesFt,
            double zCentroFt)
        {
            if (elevacoesFt == null || elevacoesFt.Count == 0)
                return -1;

            int melhorIdx = 0;
            double melhorDist = Math.Abs(elevacoesFt[0] - zCentroFt);
            for (int i = 1; i < elevacoesFt.Count; i++)
            {
                double dist = Math.Abs(elevacoesFt[i] - zCentroFt);
                if (dist < melhorDist)
                {
                    melhorDist = dist;
                    melhorIdx = i;
                }
            }
            return melhorIdx;
        }
    }
}
