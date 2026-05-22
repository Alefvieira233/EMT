using System.Collections.Generic;
using Autodesk.Revit.DB;
using SteelBIM.Infrastructure;
using SteelBIM.Services.DiagramaMontagem;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Extrai o eixo central de um elemento estrutural a partir de seu Solid,
    /// preservando inclinacao 3D real. Substitui o approach legado AABB
    /// world-aligned do <c>ConverterPerfilIfcService.ObterLinhaDoElemento</c>
    /// que destruia diagonais (ex: diagonal de tesoura a 45 graus virava
    /// linha horizontal).
    ///
    /// Approach principal: <see cref="CapsAxisCalculator"/> via PlanarFaces
    /// extremas (perpendiculares ao eixo, anti-paralelas, areas similares).
    /// Fallback defensivo: <see cref="PcaAxisCalculator"/> sobre os vertices
    /// do Solid quando nao ha PlanarFaces suficientes ou caps ambiguos.
    /// </summary>
    public static class SectionAxisExtractor
    {
        /// <summary>
        /// Extrai o eixo do elemento. Retorna null se nao for possivel
        /// (sem Solid, sem geometria valida).
        /// </summary>
        public static Line ExtrairEixo(Element elemento)
        {
            if (elemento == null)
                return null;

            Options opts = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geo;
            try
            { geo = elemento.get_Geometry(opts); }
            catch
            { return null; }

            if (geo == null)
                return null;

            // Coletar PlanarFaces e vertices de todos os Solids do elemento
            var faces = new List<FaceData>();
            var vertices = new List<Vec3>();
            ColetarGeometria(geo, faces, vertices);

            if (faces.Count == 0 && vertices.Count < 2)
                return null;

            // Approach principal: caps
            AxisLineData? caps = CapsAxisCalculator.Identificar(faces);
            if (caps.HasValue)
            {
                Logger.Debug("[SectionAxisExtractor] {Id}: caps approach OK ({Faces} faces analisadas)",
                    elemento.Id, faces.Count);
                return ConstruirLine(caps.Value);
            }

            // Fallback: PCA sobre vertices
            AxisLineData? pca = PcaAxisCalculator.CalcularEixoPrincipal(vertices);
            if (pca.HasValue)
            {
                Logger.Debug("[SectionAxisExtractor] {Id}: PCA fallback ({Vertices} vertices analisados)",
                    elemento.Id, vertices.Count);
                return ConstruirLine(pca.Value);
            }

            Logger.Debug("[SectionAxisExtractor] {Id}: sem eixo detectavel ({Faces} faces, {Vertices} vertices)",
                elemento.Id, faces.Count, vertices.Count);
            return null;
        }

        private static void ColetarGeometria(
            GeometryElement geo,
            List<FaceData> faces,
            List<Vec3> vertices)
        {
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 1e-9)
                {
                    foreach (Face f in solid.Faces)
                    {
                        if (f is PlanarFace pf)
                        {
                            try
                            {
                                BoundingBoxUV uv = pf.GetBoundingBox();
                                XYZ centro = pf.Evaluate((uv.Min + uv.Max) * 0.5);
                                XYZ normal = pf.FaceNormal;
                                double area = pf.Area;
                                faces.Add(new FaceData(
                                    new Vec3(centro.X, centro.Y, centro.Z),
                                    new Vec3(normal.X, normal.Y, normal.Z),
                                    area));
                            }
                            catch { /* face geometria irregular — ignora silenciosamente */ }
                        }
                    }

                    foreach (Edge edge in solid.Edges)
                    {
                        try
                        {
                            Curve c = edge.AsCurve();
                            if (c == null)
                                continue;
                            XYZ p0 = c.GetEndPoint(0);
                            XYZ p1 = c.GetEndPoint(1);
                            vertices.Add(new Vec3(p0.X, p0.Y, p0.Z));
                            vertices.Add(new Vec3(p1.X, p1.Y, p1.Z));
                        }
                        catch { /* edge degenerado */ }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    // DirectShape de IFC tipicamente expoe Solid direto, mas alguns
                    // wrappers usam GeometryInstance — descer recursivamente.
                    GeometryElement inner = gi.GetInstanceGeometry();
                    if (inner != null)
                        ColetarGeometria(inner, faces, vertices);
                }
            }
        }

        private static Line ConstruirLine(AxisLineData data)
        {
            XYZ start = new XYZ(data.Start.X, data.Start.Y, data.Start.Z);
            XYZ end = new XYZ(data.End.X, data.End.Y, data.End.Z);
            if (start.DistanceTo(end) < 1e-6)
                return null;
            return Line.CreateBound(start, end);
        }

        /// <summary>
        /// Coleta as faces planares do elemento em uma unica passagem de geometria.
        /// Usado por <see cref="SectionOrientationExtractor"/> para extrair a
        /// orientacao da secao transversal sem repetir a travessia do Solid.
        /// </summary>
        public static List<FaceData> ColetarFaces(Element elemento)
        {
            var faces = new List<FaceData>();
            var vertices = new List<Vec3>();

            if (elemento == null)
                return faces;

            Options opts = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geo;
            try
            { geo = elemento.get_Geometry(opts); }
            catch { return faces; }

            if (geo != null)
                ColetarGeometria(geo, faces, vertices);

            return faces;
        }
    }
}
