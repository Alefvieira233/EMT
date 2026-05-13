#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) para detectar o tipo topologico da trelica:
    /// plana (banzos paralelos), duas aguas (pico central) ou shed (um lado mais alto).
    /// </summary>
    /// <remarks>
    /// Detecta-se analisando os Z dos nos do banzo superior:
    /// - Plana: todos os Z superiores aproximadamente iguais.
    /// - Duas aguas: perfil V invertido — Z sobe ate um pico e depois desce.
    /// - Shed: Z monotonamente crescente (ou decrescente).
    /// Ver secao 6.2 do docs/PLANO-LAPIDACAO.md.
    /// </remarks>
    public static class TrelicaTopologia
    {
        /// <summary>Tolerancia relativa: 1% do vao para considerar Z igual.</summary>
        public const double TolRelativaVao = 0.01;

        /// <summary>Tipos topologicos de trelica suportados.</summary>
        public enum Topologia
        {
            Desconhecida = 0,
            Plana = 1,
            DuasAguas = 2,
            Shed = 3
        }

        /// <summary>
        /// Detecta a topologia a partir da lista de nos do banzo superior ordenados por X.
        /// </summary>
        /// <param name="nosSuperior">Lista de (x, z) dos nos do banzo superior ordenados por x.</param>
        public static Topologia Detectar(IReadOnlyList<(double X, double Z)> nosSuperior)
        {
            if (nosSuperior is null) throw new ArgumentNullException(nameof(nosSuperior));
            if (nosSuperior.Count < 2) return Topologia.Desconhecida;

            double vao = nosSuperior[^1].X - nosSuperior[0].X;
            if (vao <= 0) return Topologia.Desconhecida;
            double tolZ = vao * TolRelativaVao;

            double zMin = nosSuperior.Min(p => p.Z);
            double zMax = nosSuperior.Max(p => p.Z);
            if (zMax - zMin <= tolZ) return Topologia.Plana;

            // Acha o indice do pico
            int idxPico = 0;
            for (int i = 1; i < nosSuperior.Count; i++)
                if (nosSuperior[i].Z > nosSuperior[idxPico].Z) idxPico = i;

            // Se o pico e a extremidade -> shed (monotonico)
            if (idxPico == 0 || idxPico == nosSuperior.Count - 1)
                return Topologia.Shed;

            // Caso contrario, sobe ate o pico e desce -> duas aguas
            bool subiuAteOPico = true;
            for (int i = 1; i <= idxPico; i++)
                if (nosSuperior[i].Z + tolZ < nosSuperior[i - 1].Z) { subiuAteOPico = false; break; }

            bool desceuDepoisDoPico = true;
            for (int i = idxPico + 1; i < nosSuperior.Count; i++)
                if (nosSuperior[i].Z > nosSuperior[i - 1].Z + tolZ) { desceuDepoisDoPico = false; break; }

            if (subiuAteOPico && desceuDepoisDoPico) return Topologia.DuasAguas;
            return Topologia.Desconhecida;
        }
    }
}
