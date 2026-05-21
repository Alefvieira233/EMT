using System;
using System.Collections.Generic;
using System.Linq;
using SteelBIM.Services.DiagramaMontagem;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Descricao pura de uma face planar (sem dependencia de Autodesk.Revit.DB.Face)
    /// usada pelo <see cref="CapsAxisCalculator"/> para identificar caps de uma peca
    /// estrutural a partir do seu Solid.
    /// </summary>
    public readonly struct FaceData
    {
        public readonly Vec3 Center;
        public readonly Vec3 Normal;
        public readonly double Area;

        public FaceData(Vec3 center, Vec3 normal, double area)
        {
            Center = center;
            Normal = normal;
            Area = area;
        }
    }

    /// <summary>
    /// Resultado puro do calculo do eixo central de uma peca estrutural.
    /// Start/End sao os centroides das faces extremas (caps) OU as projecoes
    /// min/max dos vertices ao longo do eixo principal (PCA fallback).
    /// </summary>
    public readonly struct AxisLineData
    {
        public readonly Vec3 Start;
        public readonly Vec3 End;

        public AxisLineData(Vec3 start, Vec3 end)
        {
            Start = start;
            End = end;
        }

        public double Length => (End - Start).Length;
    }

    /// <summary>
    /// Calculador puro do eixo central de uma peca estrutural via faces extremas
    /// (caps) — perpendiculares ao eixo, com normais anti-paralelas, areas
    /// similares e maior distancia entre centroides.
    ///
    /// Quando a peca tem caps PlanarFaces bem definidos, este approach preserva
    /// fielmente inclinacao 3D real (vs AABB world-aligned do approach legado
    /// que destruia diagonais de tesoura).
    /// </summary>
    public static class CapsAxisCalculator
    {
        // Tolerancias para identificar par de caps validos:
        private const double DotAntiParaleloMin = 0.85;  // |dot| > 0.85 => normais ~anti-paralelas
        private const double AreaSimilarityMax = 0.30;    // |dA - dB|/max < 30% => areas similares
        private const double MinDistFt = 1e-3;            // distancia minima entre centroides (~0.3mm)

        /// <summary>
        /// Tenta identificar 2 faces extremas (caps) e retorna o eixo conectando
        /// seus centroides. Null quando nao ha par valido (caller usa PCA).
        /// </summary>
        public static AxisLineData? Identificar(IReadOnlyList<FaceData> faces)
        {
            if (faces == null || faces.Count < 2)
                return null;

            // Construir todos os pares e rankear por (anti-paralela AND area similar AND maior distancia)
            AxisLineData? melhor = null;
            double melhorDist = MinDistFt;

            for (int i = 0; i < faces.Count; i++)
            {
                FaceData a = faces[i];
                if (a.Area <= 0)
                    continue;

                for (int j = i + 1; j < faces.Count; j++)
                {
                    FaceData b = faces[j];
                    if (b.Area <= 0)
                        continue;

                    // Normais quase anti-paralelas (dot proximo de -1)
                    double dot = a.Normal.X * b.Normal.X + a.Normal.Y * b.Normal.Y + a.Normal.Z * b.Normal.Z;
                    if (-dot < DotAntiParaleloMin)
                        continue;

                    // Areas similares
                    double areaDiff = Math.Abs(a.Area - b.Area) / Math.Max(a.Area, b.Area);
                    if (areaDiff > AreaSimilarityMax)
                        continue;

                    // Distancia entre centroides
                    double dist = (a.Center - b.Center).Length;
                    if (dist > melhorDist)
                    {
                        melhorDist = dist;
                        melhor = new AxisLineData(a.Center, b.Center);
                    }
                }
            }

            return melhor;
        }
    }

    /// <summary>
    /// Calculador puro do eixo principal de uma nuvem de vertices via Principal
    /// Component Analysis (PCA). Fallback usado quando <see cref="CapsAxisCalculator"/>
    /// nao identifica caps validos — pecas com geometria irregular (Solid de
    /// arco, geometria seccionada, etc).
    ///
    /// Algoritmo: matriz de covariancia 3x3 -> maior eigenvector via power
    /// iteration -> projecao dos vertices nesse vetor -> min/max define start/end.
    /// </summary>
    public static class PcaAxisCalculator
    {
        private const int PowerIterations = 50;
        private const double Tolerance = 1e-9;

        /// <summary>
        /// Calcula eixo principal e retorna a linha que vai do vertice mais
        /// "atras" ao mais "a frente" ao longo desse eixo. Null se vertices
        /// degenerados (< 2 pontos OU colapsados num so ponto).
        /// </summary>
        public static AxisLineData? CalcularEixoPrincipal(IReadOnlyList<Vec3> vertices)
        {
            if (vertices == null || vertices.Count < 2)
                return null;

            // 1. Centroide
            double cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                cx += vertices[i].X;
                cy += vertices[i].Y;
                cz += vertices[i].Z;
            }
            cx /= vertices.Count;
            cy /= vertices.Count;
            cz /= vertices.Count;

            // 2. Matriz de covariancia (3x3 simetrica)
            double cxx = 0, cyy = 0, czz = 0, cxy = 0, cxz = 0, cyz = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                double dx = vertices[i].X - cx;
                double dy = vertices[i].Y - cy;
                double dz = vertices[i].Z - cz;
                cxx += dx * dx;
                cyy += dy * dy;
                czz += dz * dz;
                cxy += dx * dy;
                cxz += dx * dz;
                cyz += dy * dz;
            }

            // Caso degenerado: todas as variances proximas de zero
            double totalVar = cxx + cyy + czz;
            if (totalVar < Tolerance)
                return null;

            // 3. Power iteration: encontrar eigenvector dominante
            // Seed: eixo do maior diagonal da matriz
            Vec3 v;
            if (cxx >= cyy && cxx >= czz)
                v = new Vec3(1, 0, 0);
            else if (cyy >= czz)
                v = new Vec3(0, 1, 0);
            else
                v = new Vec3(0, 0, 1);

            for (int iter = 0; iter < PowerIterations; iter++)
            {
                // M * v
                double nx = cxx * v.X + cxy * v.Y + cxz * v.Z;
                double ny = cxy * v.X + cyy * v.Y + cyz * v.Z;
                double nz = cxz * v.X + cyz * v.Y + czz * v.Z;
                double norm = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (norm < Tolerance)
                    return null;
                v = new Vec3(nx / norm, ny / norm, nz / norm);
            }

            // 4. Projetar todos os vertices em v -> min/max determina endpoints
            double minProj = double.PositiveInfinity;
            double maxProj = double.NegativeInfinity;
            int minIdx = 0, maxIdx = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                double dx = vertices[i].X - cx;
                double dy = vertices[i].Y - cy;
                double dz = vertices[i].Z - cz;
                double proj = dx * v.X + dy * v.Y + dz * v.Z;
                if (proj < minProj)
                { minProj = proj; minIdx = i; }
                if (proj > maxProj)
                { maxProj = proj; maxIdx = i; }
            }

            // 5. Start = centroide + minProj * v (projecao do vertice mais "atras" sobre o eixo)
            //    End   = centroide + maxProj * v
            // (projecao real do vertice extremo sobre o eixo passando pelo centroide)
            Vec3 centroide = new Vec3(cx, cy, cz);
            Vec3 start = centroide + v * minProj;
            Vec3 end = centroide + v * maxProj;

            // Validar nao-degenerado
            if ((end - start).Length < Tolerance)
                return null;

            // Garantir referencia explicita aos indices (silencia warning)
            _ = minIdx;
            _ = maxIdx;

            return new AxisLineData(start, end);
        }
    }
}
