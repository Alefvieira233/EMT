#nullable enable
using System;
using System.Collections.Generic;
using SteelBIM.Services.DiagramaMontagem;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Extrai a orientacao da secao transversal de um perfil estrutural a partir
    /// das suas faces laterais (nao-caps). Retorna o vetor de referencia no plano
    /// perpendicular ao eixo — normal da face lateral de maior area, projetada e
    /// normalizada.
    ///
    /// Utilizado por <see cref="ConverterPerfilIfcService"/> apos extrair o eixo
    /// via <see cref="SectionAxisExtractor"/> para alinhar a rotacao da
    /// FamilyInstance criada no Revit com a orientacao original do elemento IFC.
    ///
    /// Criterio de "face lateral": normal com |dot(n, eixo)| &lt;= 0.85
    /// (faces caps, perpendiculares ao eixo, sao excluidas).
    /// </summary>
    public static class SectionOrientationExtractor
    {
        private const double DotCapMax = 0.85;
        private const double MinArea = 1e-9;
        private const double MinVecLen = 1e-9;

        /// <summary>
        /// Dada a lista de faces do solido e o eixo normalizado do perfil,
        /// retorna o vetor de referencia da secao transversal (normal da face
        /// lateral de maior area projetada no plano perp ao eixo). Null quando
        /// nao ha faces laterais validas (ex: secao circular sem PlanarFaces).
        /// </summary>
        public static Vec3? ExtrairReferenciaSecao(
            IReadOnlyList<FaceData>? faces,
            Vec3 eixoNormalizado)
        {
            if (faces == null || faces.Count == 0)
                return null;

            FaceData? melhor = null;
            double maiorArea = MinArea;

            foreach (FaceData f in faces)
            {
                if (f.Area <= MinArea)
                    continue;

                // Faces caps: normal quase paralela ao eixo — excluir
                double dot = Math.Abs(
                    f.Normal.X * eixoNormalizado.X +
                    f.Normal.Y * eixoNormalizado.Y +
                    f.Normal.Z * eixoNormalizado.Z);

                if (dot > DotCapMax)
                    continue;

                if (f.Area > maiorArea)
                {
                    maiorArea = f.Area;
                    melhor = f;
                }
            }

            if (!melhor.HasValue)
                return null;

            // Projetar a normal da face escolhida no plano perp ao eixo
            Vec3 n = melhor.Value.Normal;
            double dotN =
                n.X * eixoNormalizado.X +
                n.Y * eixoNormalizado.Y +
                n.Z * eixoNormalizado.Z;

            Vec3 projetado = new Vec3(
                n.X - dotN * eixoNormalizado.X,
                n.Y - dotN * eixoNormalizado.Y,
                n.Z - dotN * eixoNormalizado.Z);

            double len = projetado.Length;
            if (len < MinVecLen)
                return null;

            return projetado * (1.0 / len);
        }
    }
}
