#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SteelBIM.Utils
{
    /// <summary>
    /// v2.8.2: extension methods de extracao de Solids com suporte a
    /// <see cref="GeometryInstance"/> e <see cref="Transform"/>.
    ///
    /// <para>Por que existe: <c>Element.get_Geometry(Options)</c> pode
    /// retornar GeometryInstance (caso de FamilyInstance — perfis estruturais,
    /// chapas de conexao, etc). Pra obter os solids reais precisa resolver
    /// a instance via GetSymbolGeometry (geometria local da familia)
    /// ou GetInstanceGeometry (geometria ja transformada no documento).</para>
    ///
    /// <para>Usado pelo <see cref="SteelBIM.Services.ConexaoTercasService"/>
    /// para extrair a face hospedeira correta da terça
    /// (maior face planar = alma do perfil U/C/I) antes de
    /// <c>NewFamilyInstance(face, ...)</c>.</para>
    ///
    /// <para>Adapta utilitarios de geometria do snapshot externo
    /// "INGENIERIA CODIGO" — disponibiliza isso de forma reutilizavel
    /// dentro do nosso namespace Utils.</para>
    /// </summary>
    public static class EngineerGeometry
    {
        /// <summary>
        /// Retorna todos os <see cref="Solid"/> com volume positivo do
        /// elemento. Resolve <see cref="GeometryInstance"/> internamente.
        ///
        /// <param name="ele">O elemento a inspecionar.</param>
        /// <param name="isRealLocation">
        /// Se <c>true</c>, usa <c>GetInstanceGeometry()</c> (geometria
        /// transformada na posicao real do elemento no projeto). Se
        /// <c>false</c>, usa <c>GetSymbolGeometry()</c> (geometria local
        /// da familia, na origem). Pra picking de face hospedeira em
        /// terça, use <c>false</c>.
        /// </param>
        /// <param name="transform">
        /// Recebe o <see cref="Transform"/> da GeometryInstance encontrada
        /// (Identity se elemento nao tem instance). Util pra mapear pontos
        /// de volta entre coords locais e do projeto.
        /// </param>
        /// </summary>
        public static List<Solid> GetAllSolids(this Element ele, bool isRealLocation, out Transform transform)
        {
            transform = Transform.Identity;
            var list = new List<Solid>();
            var opt = new Options() { IncludeNonVisibleObjects = false, ComputeReferences = true };
            var geometryElement = ele.get_Geometry(opt);

            if (geometryElement == null)
                return list;

            foreach (GeometryObject geoObj in geometryElement)
            {
                if (geoObj is GeometryInstance gi)
                {
                    var geoEl = gi.GetSymbolGeometry();
                    if (isRealLocation)
                        geoEl = gi.GetInstanceGeometry();
                    transform = gi.Transform;
                    foreach (var o in geoEl)
                        if (o is Solid solid)
                            list.Add(solid);
                }
                else if (geoObj is Solid solid)
                {
                    list.Add(solid);
                }
            }

            return list.Where(s => s.Volume > 0).ToList();
        }

        /// <summary>
        /// Variante de <see cref="GetAllSolids"/> com
        /// <c>DetailLevel = ViewDetailLevel.Fine</c>. Util quando a
        /// geometria simplificada (Coarse/Medium) nao tem a face
        /// desejada — comum em perfis estruturais que mostram so eixo
        /// em vista grosseira mas geometria completa em fine.
        /// </summary>
        public static List<Solid> GetAllSolidsFine(this Element ele, bool isRealLocation, out Transform transform)
        {
            transform = Transform.Identity;
            var list = new List<Solid>();
            var opt = new Options()
            {
                IncludeNonVisibleObjects = false,
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            var geo = ele.get_Geometry(opt);

            if (geo == null)
                return list;

            foreach (GeometryObject obj in geo)
            {
                if (obj is GeometryInstance gi)
                {
                    var geoEl = isRealLocation ? gi.GetInstanceGeometry() : gi.GetSymbolGeometry();
                    transform = gi.Transform;
                    foreach (var o in geoEl)
                        if (o is Solid s)
                            list.Add(s);
                }
                else if (obj is Solid s)
                {
                    list.Add(s);
                }
            }

            return list.Where(s => s.Volume > 0).ToList();
        }

        /// <summary>
        /// v2.8.3: calcula o centroide ponderado por volume dos solids
        /// fornecidos. Retorna <see cref="XYZ.Zero"/> se a lista for vazia
        /// ou se nenhum solid tiver volume positivo.
        ///
        /// <para>Usado pelo <see cref="SteelBIM.Services.ConexaoTercasService"/>
        /// pra centrar geometricamente a instancia da conexao APOS a insercao
        /// face-based. Resolve o caso em que a familia foi modelada com origem
        /// fora do centro geometrico (ex: ponto base em um canto da chapa) —
        /// sem essa correcao, a chapa fica deslocada do ponto pedido.</para>
        ///
        /// <para>Insight reportado em validacao externa: familias com ponto
        /// base em canto produzem conexoes "saindo para baixo" mesmo quando
        /// o algoritmo passa o XYZ correto, porque NewFamilyInstance
        /// posiciona a origem da familia (= o canto) no ponto pedido.</para>
        ///
        /// <para>Centroide ponderado por volume:</para>
        /// <c>centroide = sum(solid.Centroid * solid.Volume) / sum(solid.Volume)</c>
        /// </summary>
        public static XYZ ComputeWeightedCentroid(IEnumerable<Solid> solids)
        {
            if (solids == null)
                return XYZ.Zero;

            double totalVol = 0;
            double sumX = 0, sumY = 0, sumZ = 0;

            foreach (Solid s in solids)
            {
                if (s == null || s.Volume <= 0)
                    continue;
                XYZ c;
                try
                { c = s.ComputeCentroid(); }
                catch { continue; } // defensivo: algumas geometrias degeneradas explodem

                sumX += c.X * s.Volume;
                sumY += c.Y * s.Volume;
                sumZ += c.Z * s.Volume;
                totalVol += s.Volume;
            }

            if (totalVol < 1e-12)
                return XYZ.Zero;
            return new XYZ(sumX / totalVol, sumY / totalVol, sumZ / totalVol);
        }
    }
}
