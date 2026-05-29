#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services
{
    /// <summary>
    /// Servico do comando "Inserir Conexao de Terca" (v2.8.1 inicial,
    /// reescrito em v2.8.2 com algoritmo face-based validado).
    ///
    /// <para>Resolve os 4 problemas reportados pelo Victor em teste real
    /// (audio 28/05):</para>
    /// <list type="number">
    ///   <item><b>Duplicacao por seleção Element+Face</b>: filtros explicitos
    ///         <see cref="ConexaoTercasGeometry.IsEndpointFree"/> e
    ///         <see cref="ConexaoTercasGeometry.IsCloseToReference"/> rodam
    ///         ANTES da geracao de pontos, evitando dependencia exclusiva
    ///         de dedup posterior. Dedup XY 50mm continua como guard
    ///         final (defesa em profundidade).</item>
    ///   <item><b>Falta de referencia terça↔viga</b>: o command agora exige
    ///         pick #2 (vigas de apoio). O service projeta a extremidade
    ///         da terça na curva da viga mais proxima pra obter o ponto
    ///         de insercao real, nao a extremidade da terça em si.</item>
    ///   <item><b>Alinhamento no eixo vs alma</b>: inserção face-based usando
    ///         a maior face planar do solid da terça
    ///         (<c>NewFamilyInstance(face, point, dir, symbol)</c>).
    ///         Em U/C/I, a maior face é a alma — a chapa encosta
    ///         naturalmente sem precisar de rotacao manual.</item>
    ///   <item><b>Rotações manuais imprevisíveis</b>: eliminadas. A inserção
    ///         face-based ja orienta a familia corretamente; aplica-se
    ///         apenas o offset de rotacao opcional do usuario.</item>
    /// </list>
    ///
    /// <para>Algoritmo (1 caminho, sem fallback):</para>
    /// <list type="number">
    ///   <item>Para cada terça: extrai curva, p0/p1, GetTransform.</item>
    ///   <item>Filtra extremidades livres + proximas de viga.</item>
    ///   <item>Projeta endpoint escolhido na curva da viga mais proxima.</item>
    ///   <item>Obtem solids via <c>GetAllSolids(false)</c> + maior face planar.</item>
    ///   <item>Modo Completo opcional: raycast pra GetBottomFace + altura.</item>
    ///   <item><c>NewFamilyInstance(sideFace, finalPoint, ejeX, symbol)</c>.</item>
    ///   <item>Guard: corrige posicao via MoveElement se Location divergir
    ///         (caso WorkPlaneBased ignorar XYZ).</item>
    /// </list>
    /// </summary>
    public class ConexaoTercasService
    {
        // ---------- Constantes ----------

        /// <summary>
        /// Delta minimo entre Location.Point e finalPoint pra disparar o
        /// guard MoveElement. 1e-4 ft ≈ 0.03 mm — abaixo disso a divergencia
        /// é ruído de ponto flutuante.
        /// </summary>
        private const double MoveGuardThresholdFt = 1e-4;

        // ---------- Entry point ----------

        public Result Executar(UIDocument uidoc, Document doc, ConexaoTercasConfig config, IList<Reference> refs)
        {
            if (config?.SymbolSelecionado == null)
                return Result.Failed;

            if (refs == null || refs.Count == 0)
            {
                AppDialogService.ShowWarning("Conexão de Terça", "Nenhuma terça foi selecionada.", "Selecao vazia");
                return Result.Failed;
            }

            // Vigas de apoio sao obrigatorias em v2.8.2.
            // Falta dela = comportamento antigo (v2.8.1) sem ancoragem em viga.
            if (config.VigasRefs == null || config.VigasRefs.Count == 0)
            {
                AppDialogService.ShowWarning(
                    "Conexão de Terça",
                    "Nenhuma viga de apoio foi selecionada.\n\nO comando precisa de pelo menos uma viga para projetar as conexões.",
                    "Vigas obrigatorias");
                return Result.Failed;
            }

            double rotOffsetRad = RevitUtils.DegToRad(config.OffsetRotacaoGraus);
            double offsetVFt = config.OffsetVerticalAdicionalMm * RevitUtils.FT_PER_MM;

            // 1. Resolve curvas das terças e das vigas (em tuplas pra usar nos helpers puros).
            var tercas = ResolveTercaInfos(doc, refs);
            var vigaCurves = ResolveVigaCurves(doc, config.VigasRefs);

            // 2. Coleta pontos a inserir.
            //
            // v2.8.3 — algoritmo MUDOU: em vez de pegar a extremidade mais
            // proxima de UMA viga (que perdia vigas intermediarias), agora
            // pra cada terça itera TODAS as vigas e calcula a intersecao XY
            // (helper ConexaoTercasGeometry.IntersectXY). Isso garante que
            // viga do meio receba conexao tambem.
            //
            // Z resultante = Z da terça (preservado pelo IntersectXY),
            // resolve simultaneamente o "conexao saindo abaixo da terça"
            // — antes usavamos Z do eixo da viga (errado).
            var pontos = new List<PontoCon>();
            foreach (var terca in tercas)
            {
                if (config.ColocarExtremidades)
                {
                    // Cruza a terça com TODAS as vigas selecionadas
                    foreach (var viga in vigaCurves)
                    {
                        var cross = ConexaoTercasGeometry.IntersectXY(
                            ToTuple(terca.P0), ToTuple(terca.P1),
                            ToTuple(viga.Curve.GetEndPoint(0)),
                            ToTuple(viga.Curve.GetEndPoint(1)));
                        if (cross == null)
                            continue;

                        XYZ finalPoint = new XYZ(cross.Value.X, cross.Value.Y, cross.Value.Z);
                        pontos.Add(new PontoCon(finalPoint, terca, viga));
                    }
                }
                if (config.ColocarMeio)
                {
                    // "Meio" = ponto medio da terça associado a viga mais proxima
                    XYZ meio = (terca.P0 + terca.P1) / 2.0;
                    VigaInfo? melhorViga = FindClosestViga(meio, vigaCurves);
                    if (melhorViga == null)
                        continue;

                    // Preserva Z da terça em "meio" (mesma logica do IntersectXY)
                    XYZ projXY = ProjectOnCurve(meio, melhorViga.Value.Curve);
                    XYZ finalPoint = new XYZ(projXY.X, projXY.Y, meio.Z);
                    pontos.Add(new PontoCon(finalPoint, terca, melhorViga.Value));
                }
            }

            // 3. Transaction unica — insere face-based + aplica params + guard.
            var colocados = new List<XYZ>();
            int count = 0;

            using (Transaction t = new Transaction(doc, "Inserir Conexões de Terça"))
            {
                t.Start();

                if (!config.SymbolSelecionado.IsActive)
                    config.SymbolSelecionado.Activate();

                foreach (KeyValuePair<string, double> kvp in config.ParametrosInternos)
                {
                    Parameter p = config.SymbolSelecionado.LookupParameter(kvp.Key);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                        p.Set(kvp.Value);
                }

                doc.Regenerate();

                foreach (PontoCon pt in pontos)
                {
                    // Guard final: dedup XY 50mm. Mantido em paralelo a IsEndpointFree
                    // como defesa em profundidade — caso a malha tenha terças que
                    // compartilham mesmo node mas nao foram pegas pelo IsEndpointFree
                    // (ex: usuario selecionou só uma das duas).
                    bool jaColocado = colocados.Any(c => ConexaoTercasMath.IsWithinDistanceXY(
                        c.X, c.Y, pt.Base.X, pt.Base.Y, ConexaoTercasMath.DedupToleranceFt));
                    if (jaColocado)
                        continue;

                    if (InserirConexao(doc, pt, offsetVFt, rotOffsetRad, config))
                    {
                        colocados.Add(pt.Base);
                        count++;
                    }
                }

                t.Commit();
            }

            AppDialogService.ShowInfo(
                "Conexão de Terça",
                $"{count} conexão(ões) inserida(s) com sucesso.",
                "Concluído");

            return Result.Succeeded;
        }

        // ---------- Helpers de resolucao ----------

        private static List<TercaInfo> ResolveTercaInfos(Document doc, IList<Reference> refs)
        {
            var list = new List<TercaInfo>();
            foreach (Reference r in refs)
            {
                Element el = doc.GetElement(r);
                Curve? c = RevitUtils.GetElementCurve(el);
                if (c == null)
                    continue;

                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);
                XYZ dir = RevitUtils.SafeNormalize(p1 - p0);
                if (RevitUtils.IsZeroVector(dir))
                    continue;

                list.Add(new TercaInfo(el, p0, p1, dir));
            }
            return list;
        }

        private static List<VigaInfo> ResolveVigaCurves(Document doc, IList<Reference> vigasRefs)
        {
            var list = new List<VigaInfo>();
            foreach (Reference r in vigasRefs)
            {
                Element el = doc.GetElement(r);
                Curve? c = RevitUtils.GetElementCurve(el);
                if (c == null)
                    continue;
                list.Add(new VigaInfo(el, c));
            }
            return list;
        }

        // ---------- Helpers de projecao ----------

        private static VigaInfo? FindClosestViga(XYZ ponto, IList<VigaInfo> vigas)
        {
            VigaInfo? best = null;
            double minDist = double.MaxValue;
            foreach (var v in vigas)
            {
                IntersectionResult? proj = null;
                try
                { proj = v.Curve.Project(ponto); }
                catch { }
                double d = proj?.Distance ?? double.MaxValue;
                if (d < minDist)
                {
                    minDist = d;
                    best = v;
                }
            }
            return best;
        }

        private static XYZ ProjectOnCurve(XYZ ponto, Curve curva)
        {
            try
            {
                IntersectionResult? proj = curva.Project(ponto);
                return proj?.XYZPoint ?? ponto;
            }
            catch
            {
                return ponto;
            }
        }

        private static (double X, double Y, double Z) ToTuple(XYZ p) => (p.X, p.Y, p.Z);

        /// <summary>
        /// v2.8.5 — normal da face normalizada com fallback defensivo.
        /// Retorna <c>XYZ.Zero</c> se a face nao tem normal valida.
        /// </summary>
        private static XYZ SafeNormalizeFaceNormal(PlanarFace face)
        {
            if (face == null)
                return XYZ.Zero;
            XYZ n = face.FaceNormal;
            if (n == null || n.IsZeroLength())
                return XYZ.Zero;
            try
            { return n.Normalize(); }
            catch { return XYZ.Zero; }
        }

        /// <summary>
        /// v2.8.5 — distancia 3D do centro geometrico da face ao ponto dado.
        /// Usado pra escolher a face do lado correto da alma em terças U/C.
        /// </summary>
        private static double DistanceFaceCenterToPoint(PlanarFace face, XYZ point)
        {
            if (face == null || point == null)
                return double.MaxValue;
            try
            {
                BoundingBoxUV bb = face.GetBoundingBox();
                UV centerUV = (bb.Min + bb.Max) / 2;
                XYZ faceCenter = face.Evaluate(centerUV);
                return faceCenter.DistanceTo(point);
            }
            catch { return double.MaxValue; }
        }

        // ---------- Insercao face-based ----------

        /// <summary>
        /// Insere uma instancia de conexao usando a maior face planar do
        /// solid da terça como host. Retorna true se a instancia foi criada.
        /// </summary>
        private static bool InserirConexao(
            Document doc,
            PontoCon pt,
            double offsetVertFt,
            double rotOffsetRad,
            ConexaoTercasConfig config)
        {
            try
            {
                // Solids da terça (geometria local da familia — isRealLocation=false).
                var solids = pt.Terca.Element.GetAllSolids(false, out var _);

                // Eixos locais da terça pra orientar a familia (calculados PRIMEIRO
                // pra usar na heuristica de face)
                Transform tf = (pt.Terca.Element as FamilyInstance)?.GetTransform() ?? Transform.Identity;
                XYZ ejeX = tf.BasisX;
                XYZ ejeZ = tf.BasisZ.IsZeroLength() ? XYZ.BasisZ : tf.BasisZ.Normalize();
                if (ejeZ.Z < 0)
                    ejeZ = -ejeZ;
                if ((pt.Terca.Element as FamilyInstance)?.Mirrored == true)
                    ejeX = -ejeX;

                // v2.8.5 — nova heurística da face hospedeira:
                //
                // Bug das versoes anteriores: usar DotProduct(BasisZ_global) preferia
                // faces APONTANDO PRA CIMA. Em U/C com mesas pra baixo, a face SUPERIOR
                // da mesa (horizontal, normal +Z) vencia a face LATERAL da alma
                // (vertical, normal ~0 em Z). Resultado: chapa saia DEITADA sobre
                // o topo da terça, em vez de em pe encostada na alma.
                //
                // Nova heuristica em 2 passos:
                //   1. FILTRAR candidatas que sao faces da ALMA:
                //      - Normal perpendicular ao eixo da terça (descarta extremidades)
                //      - Normal NAO-vertical (descarta mesas horizontais)
                //   2. ESCOLHER entre as candidatas pela PROXIMIDADE ao pt.Base (viga):
                //      - Face cujo centro esta mais perto da viga = face do lado correto
                //      - InverterFace inverte (escolhe a oposta)
                XYZ tercaDir = pt.Terca.Direction.GetLength() > RevitUtils.EPS
                    ? pt.Terca.Direction.Normalize()
                    : XYZ.BasisX;

                var todasFaces = solids
                    .SelectMany(s => s.Faces.Cast<Face>())
                    .OfType<PlanarFace>()
                    .OrderByDescending(f => f.Area)
                    .Take(6)
                    .ToList();

                // Filtra faces da ALMA: perpendicular ao eixo da terça E nao-vertical
                var almaFaces = todasFaces
                    .Where(f =>
                    {
                        XYZ n = SafeNormalizeFaceNormal(f);
                        if (n.IsZeroLength())
                            return false;
                        double dotEixo = Math.Abs(n.DotProduct(tercaDir));
                        double dotZ = Math.Abs(n.DotProduct(XYZ.BasisZ));
                        // dotEixo < 0.3 => normal nao-paralela ao eixo (descarta extremidades)
                        // dotZ < 0.7    => normal nao-vertical (descarta mesas)
                        //   threshold 0.7 (vs 0.3) tolera terça inclinada (telhado ate ~45°)
                        return dotEixo < 0.3 && dotZ < 0.7;
                    })
                    .ToList();

                // Se nenhuma face passar no filtro (perfil atipico), cai pra TOP 2 maiores
                var candidatas = almaFaces.Count > 0
                    ? almaFaces
                    : todasFaces.Take(2).ToList();

                // Escolhe pela proximidade ao pt.Base (ponto na viga).
                // Em U/C, ambas as faces da alma tem distancia similar em Z (centradas
                // na altura da alma), mas Y diferente (lados opostos). pt.Base.Y eh
                // o Y da viga; face do MESMO lado tem distancia XY menor.
                PlanarFace? sideFace = config.InverterFace
                    ? candidatas.OrderByDescending(f => DistanceFaceCenterToPoint(f, pt.Base)).FirstOrDefault()
                    : candidatas.OrderBy(f => DistanceFaceCenterToPoint(f, pt.Base)).FirstOrDefault();

                if (sideFace == null)
                {
                    Logger.Warn("[ConexaoTerca] Terça {Id} nao tem face planar — pulando", pt.Terca.Element.Id);
                    return false;
                }

                // v2.8.3: pt.Base.Z ja eh Z da terça (preservado pelo IntersectXY).
                // offsetVertFt continua sendo apenas o ajuste opcional do usuario.
                XYZ insertPt = new XYZ(pt.Base.X, pt.Base.Y, pt.Base.Z - offsetVertFt);

                FamilyInstance fi = doc.Create.NewFamilyInstance(
                    sideFace, insertPt, ejeX, config.SymbolSelecionado);

                if (fi == null)
                {
                    Logger.Warn("[ConexaoTerca] NewFamilyInstance retornou null em terça {Id}", pt.Terca.Element.Id);
                    return false;
                }

                doc.Regenerate();

                // v2.8.3 — CENTRAMENTO REAL via centroide ponderado por volume.
                //
                // Substitui o guard anterior (que comparava apenas Location.Point)
                // por uma correcao geometrica de verdade. Insight: familias modeladas
                // com origem fora do centro (ex: ponto base em um canto da chapa,
                // como acontece em familias externas) saiam deslocadas porque
                // NewFamilyInstance(face, point, ...) posiciona a ORIGEM da familia
                // no point, nao o centro geometrico. Resultado: chapa "vaza" pra
                // um lado, parecendo flutuar abaixo ou ao lado da terça.
                //
                // Solucao: depois da insercao, calcular o centroide real da
                // geometria inserida e mover a instancia pra centrar no insertPt.
                // Funciona pra famílias com origem em QUALQUER lugar — centro,
                // canto, ponto arbitrário — sem exigir convenção rígida.
                try
                {
                    var instSolids = fi.GetAllSolids(isRealLocation: true, out _);
                    XYZ centroide = EngineerGeometry.ComputeWeightedCentroid(instSolids);

                    if (!centroide.IsZeroLength())
                    {
                        XYZ offsetCentramento = insertPt - centroide;
                        if (offsetCentramento.GetLength() > MoveGuardThresholdFt)
                        {
                            ElementTransformUtils.MoveElement(doc, fi.Id, offsetCentramento);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[ConexaoTerca] centramento via centroide falhou");
                }

                // Rotacao opcional do usuario (offset adicional)
                if (Math.Abs(rotOffsetRad) > RevitUtils.EPS)
                {
                    doc.Regenerate();
                    XYZ dirNorm = pt.Terca.Direction.GetLength() > RevitUtils.EPS
                        ? pt.Terca.Direction.Normalize()
                        : XYZ.BasisX;
                    Line eixo = Line.CreateBound(insertPt, insertPt + dirNorm);
                    try
                    { ElementTransformUtils.RotateElement(doc, fi.Id, eixo, rotOffsetRad); }
                    catch (Exception ex) { Logger.Warn(ex, "[ConexaoTerca] RotateElement offset falhou"); }
                }

                // Modo Completo: ajusta parametros de altura + espessura viga
                if (config.ModoCompleto)
                {
                    AplicarModoCompleto(doc, fi, pt, config);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[ConexaoTerca] insercao falhou em terça {Id}",
                    pt.Terca.Element.Id);
                return false;
            }
        }

        // ---------- Modo Completo ----------

        /// <summary>
        /// Calcula a altura ate o topo da viga de apoio via raycast vertical
        /// e aplica nos parametros da familia (com fallback PT/ES nos nomes).
        /// Silencioso se a familia nao tem os parametros — modo Completo
        /// vira no-op nesse caso (nao falha).
        /// </summary>
        private static void AplicarModoCompleto(Document doc, FamilyInstance fi, PontoCon pt, ConexaoTercasConfig config)
        {
            double alturaFt = CalcularAlturaAteTopoViga(doc, pt, config.VigaTipoI);
            if (alturaFt <= 0)
                return;

            // Lookup duplo: aceita PT-BR e ES-LATAM (familias do Victor + Silvia)
            Parameter? pAlt = fi.LookupParameter("Altura_PlacaInf_a_Terca")
                          ?? fi.LookupParameter("Altura_PlacaInf_a_Correa");
            if (pAlt != null && !pAlt.IsReadOnly && pAlt.StorageType == StorageType.Double)
            {
                try
                { pAlt.Set(alturaFt); }
                catch (Exception ex) { Logger.Warn(ex, "[ConexaoTerca] Set Altura_PlacaInf falhou"); }
            }

            Parameter? pEsp = fi.LookupParameter("Espesor_Viga_Principal")
                          ?? fi.LookupParameter("Espessura_Viga_Principal");
            if (pEsp != null && !pEsp.IsReadOnly && pEsp.StorageType == StorageType.Double)
            {
                Element vigaType = doc.GetElement(pt.Viga.Element.GetTypeId());
                double valor = config.VigaTipoI
                    ? (vigaType?.LookupParameter("tw")?.AsDouble() ?? 0)
                    : (vigaType?.LookupParameter("h")?.AsDouble() ?? 0);
                if (valor > 0)
                {
                    try
                    { pEsp.Set(valor); }
                    catch (Exception ex) { Logger.Warn(ex, "[ConexaoTerca] Set Espesor_Viga_Principal falhou"); }
                }
            }
        }

        /// <summary>
        /// Raycast vertical do ponto de insercao até a face inferior da viga
        /// (orientacao para baixo, dot product com -Z > 0.7). Retorna a
        /// distancia até a primeira intersecao, ajustada por <c>tf</c> se
        /// viga eh tipo I.
        /// </summary>
        private static double CalcularAlturaAteTopoViga(Document doc, PontoCon pt, bool vigaTipoI)
        {
            // GetBottomFace na viga (a face com normal antiparalela ao Z)
            var solids = pt.Viga.Element.GetAllSolidsFine(true, out var _);
            PlanarFace? bottom = solids
                .SelectMany(s => s.Faces.Cast<Face>())
                .OfType<PlanarFace>()
                .Where(f => f.FaceNormal.IsZeroLength() ? false : f.FaceNormal.Normalize().DotProduct(-XYZ.BasisZ) > 0.7)
                .OrderByDescending(f => f.Area)
                .FirstOrDefault();

            if (bottom == null)
                return 0;

            // Raycast curto pra baixo a partir do ponto da terça
            Line ray = Line.CreateBound(pt.Base, pt.Base - XYZ.BasisZ * (200.0 / 304.8));
            SetComparisonResult result = bottom.Intersect(ray, out IntersectionResultArray? results);
            if (result != SetComparisonResult.Overlap || results == null || results.Size == 0)
                return 0;

            double dist = pt.Base.DistanceTo(results.get_Item(0).XYZPoint);

            if (vigaTipoI)
            {
                Element vigaType = doc.GetElement(pt.Viga.Element.GetTypeId());
                double tf = vigaType?.LookupParameter("tf")?.AsDouble() ?? 0;
                dist = Math.Max(0, dist - tf);
            }

            return dist;
        }

        // ---------- Structs internas ----------

        private readonly struct TercaInfo
        {
            public Element Element { get; }
            public XYZ P0 { get; }
            public XYZ P1 { get; }
            public XYZ Direction { get; }

            public XYZ Start => P0;
            public XYZ End => P1;

            public TercaInfo(Element el, XYZ p0, XYZ p1, XYZ dir)
            {
                Element = el;
                P0 = p0;
                P1 = p1;
                Direction = dir;
            }
        }

        private readonly struct VigaInfo
        {
            public Element Element { get; }
            public Curve Curve { get; }

            public VigaInfo(Element el, Curve c)
            {
                Element = el;
                Curve = c;
            }
        }

        private readonly struct PontoCon
        {
            public XYZ Base { get; }
            public TercaInfo Terca { get; }
            public VigaInfo Viga { get; }

            public PontoCon(XYZ b, TercaInfo t, VigaInfo v)
            {
                Base = b;
                Terca = t;
                Viga = v;
            }
        }
    }
}
