#nullable enable

using Autodesk.Revit.DB;
using FerramentaEMT.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper estatico que centraliza todas as chamadas de API Revit necessarias
    /// para a funcionalidade "Cotar Trelica" (dimensionamento, tags, texto).
    ///
    /// Todos os metodos retornam null em caso de falha (com log).
    /// Caller (CotarTrelicaService) trata null como "nao conseguiu criar dimensao/tag/etc".
    /// </summary>
    public static class TrelicaRevitHelper
    {
        private const double FACE_PARALLEL_THRESHOLD = 0.9; // |DotProduct| > 0.9 = paralelo
        private const double DEFAULT_TOLERANCIA_PES = 0.0164; // ~5mm em pes

        #region Extracao de References

        /// <summary>
        /// Extrai uma Reference de um endpoint (ponta) de uma barra estrutural (FamilyInstance).
        ///
        /// Tenta encontrar a face no extremo (cuja normal é paralela à direcao da barra).
        /// Se nao conseguir, retorna referencia da barra inteira como fallback.
        ///
        /// endpoint: 0 = inicio, 1 = fim da LocationCurve.
        /// vista: usada para ComputeReferences na geometria.
        /// </summary>
        public static Reference? ObterReferenciaExtremo(
            FamilyInstance fi, int endpoint, View vista)
        {
            try
            {
                if (fi == null || vista == null)
                {
                    Logger.Warn("TrelicaRevitHelper.ObterReferenciaExtremo: fi ou vista é null");
                    return null;
                }

                // 1. Obter direcao da barra e ponto alvo no extremo
                if (!(fi.Location is LocationCurve locCurve))
                {
                    Logger.Warn("TrelicaRevitHelper.ObterReferenciaExtremo: Location nao é LocationCurve");
                    return new Reference(fi); // Fallback
                }

                XYZ ponto0 = locCurve.Curve.GetEndPoint(0);
                XYZ ponto1 = locCurve.Curve.GetEndPoint(1);
                XYZ targetPoint = endpoint == 0 ? ponto0 : ponto1;
                XYZ barDir = (ponto1 - ponto0).Normalize();

                // 2. Obter geometria com referências
                var opt = new Options
                {
                    ComputeReferences = true,
                    View = vista,
                    DetailLevel = ViewDetailLevel.Fine
                };
                GeometryElement geo = fi.get_Geometry(opt);

                // 3. Procurar na geometria (pode estar em GeometryInstance)
                foreach (GeometryObject obj in geo)
                {
                    if (obj is GeometryInstance geoInst)
                    {
                        foreach (GeometryObject instObj in geoInst.GetInstanceGeometry())
                        {
                            Reference? ref_found = ProcurarFaceParalela(instObj, barDir, targetPoint);
                            if (ref_found != null)
                                return ref_found;
                        }
                    }
                    else
                    {
                        Reference? ref_found = ProcurarFaceParalela(obj, barDir, targetPoint);
                        if (ref_found != null)
                            return ref_found;
                    }
                }

                // 4. Fallback: retornar referencia da barra inteira
                Logger.Info("TrelicaRevitHelper.ObterReferenciaExtremo: usando fallback (barra inteira)");
                return new Reference(fi);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.ObterReferenciaExtremo] falha");
                return null;
            }
        }

        private static Reference? ProcurarFaceParalela(GeometryObject obj, XYZ barDir, XYZ targetPoint)
        {
            if (obj is not Solid solid)
                return null;

            PlanarFace? candidata = null;
            double menorDistancia = double.MaxValue;

            foreach (Face face in solid.Faces)
            {
                if (face is not PlanarFace planarFace)
                    continue;

                // Checar se normal é paralelo à direcao da barra
                XYZ normal = planarFace.FaceNormal;
                double parallelismo = Math.Abs(normal.DotProduct(barDir));

                if (parallelismo > FACE_PARALLEL_THRESHOLD)
                {
                    // Este é um candidato; achar o mais proximo do targetPoint
                    double distancia = face.Project(targetPoint)?.Distance ?? double.MaxValue;
                    if (distancia < menorDistancia)
                    {
                        menorDistancia = distancia;
                        candidata = planarFace;
                    }
                }
            }

            if (candidata != null && candidata.Reference != null)
                return candidata.Reference;

            return null;
        }

        /// <summary>
        /// Encontra a barra cuja ponta (endpoint) esta mais proxima de uma posicao X em coordenadas de vista.
        ///
        /// Retorna (barra, endpoint) ou null se nenhuma encontrada dentro da tolerancia.
        /// </summary>
        public static (FamilyInstance Barra, int Endpoint)? EncontrarBarraNoNo(
            IReadOnlyList<FamilyInstance> barras, double targetX, View vista,
            double toleranciaPes = 0.0164)
        {
            try
            {
                if (barras == null || barras.Count == 0)
                {
                    Logger.Warn("TrelicaRevitHelper.EncontrarBarraNoNo: barras lista vazia ou null");
                    return null;
                }

                if (vista == null)
                {
                    Logger.Warn("TrelicaRevitHelper.EncontrarBarraNoNo: vista é null");
                    return null;
                }

                (FamilyInstance, int)? melhorCandidata = null;
                double menorDistancia = double.MaxValue;

                foreach (FamilyInstance barra in barras)
                {
                    if (!(barra.Location is LocationCurve locCurve) || locCurve.Curve == null)
                        continue;

                    // Testar endpoint 0 e 1
                    for (int ep = 0; ep < 2; ep++)
                    {
                        XYZ ponto3D = locCurve.Curve.GetEndPoint(ep);
                        (double x2D, double _) = ProjetarPonto(ponto3D, vista);
                        double distancia = Math.Abs(x2D - targetX);

                        if (distancia < menorDistancia)
                        {
                            menorDistancia = distancia;
                            melhorCandidata = (barra, ep);
                        }
                    }
                }

                if (melhorCandidata.HasValue && menorDistancia <= toleranciaPes)
                {
                    Logger.Info($"TrelicaRevitHelper.EncontrarBarraNoNo: encontrada barra, distancia={menorDistancia}");
                    return melhorCandidata.Value;
                }

                Logger.Warn($"TrelicaRevitHelper.EncontrarBarraNoNo: nenhuma barra dentro tolerancia (melhor={menorDistancia})");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.EncontrarBarraNoNo] falha");
                return null;
            }
        }

        #endregion

        #region Criacao de Dimensoes

        /// <summary>
        /// Cria uma "running dimension" (corrente de cotas) a partir de um ReferenceArray.
        ///
        /// dimLinePoint: ponto por onde a linha de cota passa.
        /// dimLineDir: direcao da linha de cota (tipicamente vista.RightDirection para horizontal).
        ///
        /// Requer no minimo 2 referencias validas.
        /// </summary>
        public static Dimension? CriarRunningDimension(
            Document doc, View vista, ReferenceArray refs, XYZ dimLinePoint, XYZ dimLineDir)
        {
            try
            {
                if (doc == null || vista == null || refs == null || dimLinePoint == null || dimLineDir == null)
                {
                    Logger.Warn("TrelicaRevitHelper.CriarRunningDimension: parametros null");
                    return null;
                }

                if (refs.Size < 2)
                {
                    Logger.Warn($"TrelicaRevitHelper.CriarRunningDimension: ReferenceArray tem apenas {refs.Size} referencias (minimo 2)");
                    return null;
                }

                // Criar linha de dimensao unbounded
                Line dimLine = Line.CreateUnbound(dimLinePoint, dimLineDir);

                // Criar dimensao
                Dimension dim = doc.Create.NewDimension(vista, dimLine, refs);

                if (dim != null)
                {
                    Logger.Info($"TrelicaRevitHelper.CriarRunningDimension: dimensao criada com {refs.Size} referencias");
                }
                else
                {
                    Logger.Warn("TrelicaRevitHelper.CriarRunningDimension: doc.Create.NewDimension retornou null");
                }

                return dim;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.CriarRunningDimension] falha");
                return null;
            }
        }

        #endregion

        #region Criacao de Tags

        /// <summary>
        /// Cria uma IndependentTag (tag isolada) em um elemento estrutural.
        ///
        /// Usa TagMode.TM_ADDBY_CATEGORY e TagOrientation.Horizontal.
        /// comLeader: se true, adiciona leader (seta).
        /// </summary>
        public static IndependentTag? CriarTag(
            Document doc, View vista, FamilyInstance fi, XYZ posicao, bool comLeader = false)
        {
            try
            {
                if (doc == null || vista == null || fi == null || posicao == null)
                {
                    Logger.Warn("TrelicaRevitHelper.CriarTag: parametros null");
                    return null;
                }

                IndependentTag tag = IndependentTag.Create(
                    doc,
                    vista.Id,
                    new Reference(fi),
                    comLeader,
                    TagMode.TM_ADDBY_CATEGORY,
                    TagOrientation.Horizontal,
                    posicao);

                if (tag != null)
                {
                    Logger.Info("TrelicaRevitHelper.CriarTag: tag criada com sucesso");
                }
                else
                {
                    Logger.Warn("TrelicaRevitHelper.CriarTag: IndependentTag.Create retornou null");
                }

                return tag;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.CriarTag] falha");
                return null;
            }
        }

        /// <summary>
        /// Calcula a posicao de uma tag para uma barra: ponto medio deslocado perpendicular.
        ///
        /// offsetPes: deslocamento perpendicular em pés (positivo = para cima/direita na vista).
        /// </summary>
        public static XYZ CalcularPosicaoTag(FamilyInstance fi, View vista, double offsetPes)
        {
            try
            {
                if (fi == null || vista == null)
                {
                    Logger.Warn("TrelicaRevitHelper.CalcularPosicaoTag: fi ou vista é null");
                    return vista?.Origin ?? XYZ.Zero;
                }

                if (!(fi.Location is LocationCurve locCurve))
                {
                    Logger.Warn("TrelicaRevitHelper.CalcularPosicaoTag: Location nao é LocationCurve");
                    return vista.Origin;
                }

                XYZ ponto0 = locCurve.Curve.GetEndPoint(0);
                XYZ ponto1 = locCurve.Curve.GetEndPoint(1);
                XYZ midpoint = (ponto0 + ponto1) * 0.5;

                // Deslocar perpendicular (usando UpDirection da vista)
                XYZ offset = vista.UpDirection * offsetPes;
                XYZ posicaoFinal = midpoint + offset;

                Logger.Info($"TrelicaRevitHelper.CalcularPosicaoTag: posicao calculada com offset={offsetPes}");
                return posicaoFinal;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.CalcularPosicaoTag] falha");
                return vista?.Origin ?? XYZ.Zero;
            }
        }

        #endregion

        #region Criacao de TextNotes

        /// <summary>
        /// Cria um TextNote em uma posicao com o texto fornecido.
        ///
        /// textTypeId: se null, usa o tipo de texto padrao do documento.
        /// </summary>
        public static TextNote? CriarTextoNota(
            Document doc, View vista, XYZ posicao, string texto, ElementId? textTypeId = null)
        {
            try
            {
                if (doc == null || vista == null || posicao == null || string.IsNullOrEmpty(texto))
                {
                    Logger.Warn("TrelicaRevitHelper.CriarTextoNota: parametros invalidos ou null");
                    return null;
                }

                // Usar tipo padrao se nao especificado
                ElementId typeId = textTypeId ?? doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);

                if (typeId == null || typeId == ElementId.InvalidElementId)
                {
                    Logger.Warn("TrelicaRevitHelper.CriarTextoNota: nenhum TextNoteType padrao encontrado no documento");
                    return null;
                }

                TextNote note = TextNote.Create(doc, vista.Id, posicao, texto, typeId);

                if (note != null)
                {
                    Logger.Info($"TrelicaRevitHelper.CriarTextoNota: texto criado (tamanho={texto.Length})");
                }
                else
                {
                    Logger.Warn("TrelicaRevitHelper.CriarTextoNota: TextNote.Create retornou null");
                }

                return note;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.CriarTextoNota] falha");
                return null;
            }
        }

        #endregion

        #region Helpers de Coordenadas

        /// <summary>
        /// Projeta um ponto 3D para coordenadas 2D de vista (RightDirection, UpDirection).
        /// </summary>
        public static (double X, double Z) ProjetarPonto(XYZ ponto3D, View vista)
        {
            try
            {
                if (ponto3D == null || vista == null)
                {
                    Logger.Warn("TrelicaRevitHelper.ProjetarPonto: parametros null");
                    return (0.0, 0.0);
                }

                XYZ u = vista.RightDirection;
                XYZ v = vista.UpDirection;

                if (u.IsZeroLength() || v.IsZeroLength())
                {
                    Logger.Warn("TrelicaRevitHelper.ProjetarPonto: RightDirection ou UpDirection da vista tem comprimento zero");
                    return (0.0, 0.0);
                }

                XYZ delta = ponto3D - vista.Origin;

                double x = u.DotProduct(delta);
                double z = v.DotProduct(delta);

                return (x, z);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.ProjetarPonto] falha");
                return (0.0, 0.0);
            }
        }

        /// <summary>
        /// Desconverte coordenadas 2D de vista (RightDirection, UpDirection) para 3D.
        /// Retorna um ponto no plano da vista.
        /// </summary>
        public static XYZ DesprojetarPonto(double x2D, double z2D, View vista)
        {
            try
            {
                if (vista == null)
                {
                    Logger.Warn("TrelicaRevitHelper.DesprojetarPonto: vista é null");
                    return XYZ.Zero;
                }

                XYZ u = vista.RightDirection;
                XYZ v = vista.UpDirection;

                if (u.IsZeroLength() || v.IsZeroLength())
                {
                    Logger.Warn("TrelicaRevitHelper.DesprojetarPonto: RightDirection ou UpDirection da vista tem comprimento zero");
                    return XYZ.Zero;
                }

                XYZ resultado = vista.Origin + u * x2D + v * z2D;

                return resultado;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[TrelicaRevitHelper.DesprojetarPonto] falha");
                return XYZ.Zero;
            }
        }

        #endregion
    }
}
