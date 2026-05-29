using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Core;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Services.DiagramaMontagem;
using SteelBIM.Utils;

namespace SteelBIM.Services.Ifc
{
    public class ConverterPerfilIfcService
    {
        /// <summary>
        /// v2.7.7 (auditoria 2026-05-25 #008): conversao agora reporta progresso
        /// e respeita CancellationToken. Antes era loop sem feedback em galpoes
        /// com 6000+ elementos (Revit congelava 30-120s).
        ///
        /// Parametros novos sao OPCIONAIS (defaults null/default) — call sites
        /// existentes continuam funcionando sem mudanca. UI wiring do
        /// ProgressWindow + botao Cancel fica para Sprint 1/2 do roadmap
        /// v2.8.0 (via IfcConversionHandler.Progress / .CancellationToken).
        ///
        /// Comportamento de cancelamento: <see cref="OperationCanceledException"/>
        /// dentro do <c>using (Transaction)</c> dispara rollback automatico
        /// (Transaction.Dispose() rolla back se nao-commitada). Service captura
        /// a excecao e retorna a tupla atual — chamador ve quantos elementos
        /// estavam processados antes do cancel (mas nenhum foi commitado).
        /// </summary>
        /// <param name="doc">Documento Revit ativo.</param>
        /// <param name="config">Configuracao de conversao.</param>
        /// <param name="progress">
        /// Opcional. Recebe <see cref="ProgressReport"/> a cada
        /// <see cref="ProgressReportEveryNElements"/> elementos processados
        /// (e no inicio e fim). null = sem reporting (default).
        /// </param>
        /// <param name="ct">
        /// Opcional. Verificado antes de cada elemento. Se cancelado, throws
        /// <see cref="OperationCanceledException"/> internamente, captura,
        /// rollback da transaction, e retorna counts ate o ponto do cancel.
        /// </param>
        public (int convertidos, int ignorados) Executar(
            Document doc,
            ConverterPerfilIfcConfig config,
            IProgress<ProgressReport> progress = null,
            CancellationToken ct = default)
        {
            int convertidos = 0;
            int ignorados = 0;
            int total = config.Conversoes.Count;

            // v2.7.7: reportar inicio antes do work pesado (UI mostra "0/N" imediato)
            progress?.Report(new ProgressReport(0, total, "Preparando conversao..."));

            // v2.7.0 BUG 2: carregar todos os Levels uma vez para o LevelMatcher
            // escolher por proximidade Z em vez de cair no fallback fixo.
            var niveis = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            using (Transaction t = new Transaction(doc, "Converter Perfis IFC"))
            {
                t.Start();

                try
                {
                    int processados = 0;
                    foreach (ConversaoElementoIfc conversao in config.Conversoes)
                    {
                        // v2.7.7: cancelamento honra ADR-004. Throw interno aqui
                        // sobe pro catch externo, transaction da rollback no
                        // Dispose (using block). Convertidos/ignorados ate aqui
                        // sao retornados pro chamador como feedback.
                        ct.ThrowIfCancellationRequested();

                        Element origem = doc.GetElement(conversao.ElementoOrigem);
                        if (origem == null)
                        {
                            // v2.8.6: log detalhado pra diagnostico (antes silencioso).
                            Logger.Warn("[ConverterPerfilIfc] IGNORADO #{Idx}/{Tot}: ElementoOrigem {Id} nao existe mais no documento",
                                processados + 1, total, conversao.ElementoOrigem?.Value);
                            ignorados++;
                            processados++;
                            continue;
                        }

                        Line linha = ObterLinhaDoElemento(origem);
                        if (linha == null)
                        {
                            Logger.Warn("[ConverterPerfilIfc] IGNORADO #{Idx}/{Tot}: elemento {Id} ({Cat}) — SectionAxisExtractor + bbox fallback nao conseguiram extrair linha de eixo (geometria sem caps planares e sem variancia detectavel)",
                                processados + 1, total, origem.Id.Value, origem.Category?.Name ?? "?");
                            ignorados++;
                            processados++;
                            continue;
                        }

                        Level nivel = ObterNivelDoElemento(origem, doc, niveis, config.NivelPadrao);
                        if (nivel == null)
                        {
                            Logger.Warn("[ConverterPerfilIfc] IGNORADO #{Idx}/{Tot}: elemento {Id} ({Cat}) — sem Level associado e sem NivelPadrao",
                                processados + 1, total, origem.Id.Value, origem.Category?.Name ?? "?");
                            ignorados++;
                            processados++;
                            continue;
                        }

                        FamilySymbol simbolo = conversao.PerfilDestino;
                        if (!simbolo.IsActive)
                        {
                            simbolo.Activate();
                            doc.Regenerate();
                        }

                        try
                        {
                            bool ehColuna = simbolo.Category?.Id?.Value ==
                                            (long)BuiltInCategory.OST_StructuralColumns;

                            FamilyInstance nova;
                            if (ehColuna)
                            {
                                XYZ start = linha.GetEndPoint(0);
                                XYZ end = linha.GetEndPoint(1);
                                XYZ dir = (end - start).Normalize();
                                bool vertical = Math.Abs(dir.Z) > Math.Cos(5.0 * Math.PI / 180.0);

                                if (vertical)
                                {
                                    nova = doc.Create.NewFamilyInstance(
                                        start, simbolo, nivel, StructuralType.Column);

                                    // Ajustar topo para o endpoint Z do IFC
                                    Parameter topOffset = nova.get_Parameter(
                                        BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                                    if (topOffset != null && !topOffset.IsReadOnly)
                                        topOffset.Set(end.Z - nivel.Elevation);
                                }
                                else
                                {
                                    // Coluna inclinada ou diagonal: preservar linha 3D completa
                                    nova = doc.Create.NewFamilyInstance(
                                        linha, simbolo, nivel, StructuralType.Brace);
                                }
                            }
                            else
                            {
                                nova = doc.Create.NewFamilyInstance(
                                    linha, simbolo, nivel, StructuralType.Beam);
                            }

                            TentarAplicarRotacaoSecao(nova, origem, linha, doc);

                            string ifcMaterial = origem.LookupParameter("IfcMaterial")?.AsString();
                            if (!string.IsNullOrWhiteSpace(ifcMaterial))
                                TentarAplicarMaterial(nova, ifcMaterial, doc);

                            convertidos++;

                            if (config.DeletarOriginal)
                                doc.Delete(origem.Id);
                        }
                        catch (Exception ex)
                        {
                            // v2.8.6: log detalhado do erro silencioso (antes "ignorados++" sem info).
                            // Causas tipicas: NewFamilyInstance com geometria invalida (Z-degenerada),
                            // simbolo nao ativavel, host inexistente, parametro readonly inesperado.
                            Logger.Warn(ex,
                                "[ConverterPerfilIfc] IGNORADO #{Idx}/{Tot}: elemento {Id} ({Cat}) — excecao na criacao do FamilyInstance (perfil destino: {Perfil})",
                                processados + 1, total,
                                origem.Id.Value,
                                origem.Category?.Name ?? "?",
                                simbolo?.Name ?? "?");
                            ignorados++;
                        }

                        processados++;

                        // v2.7.7: throttle de progresso — reporta a cada N (ou no
                        // fim) pra evitar inundar Dispatcher em galpoes grandes
                        // (6983 elementos * 1 report cada = lag UI). Default 25
                        // = ~280 reports em galpao do Alef, suave.
                        if (progress != null
                            && (processados % ProgressReportEveryNElements == 0
                                || processados == total))
                        {
                            progress.Report(new ProgressReport(
                                processados,
                                total,
                                $"Convertendo {processados}/{total} (convertidos {convertidos}, ignorados {ignorados})..."));
                        }
                    }

                    t.Commit();

                    // v2.8.6: resumo final pos-commit pra auditoria. Permite
                    // o usuario abrir o log e ver "convertidos N de M, ignorados I"
                    // sem precisar contar manualmente entries individuais.
                    Logger.Info(
                        "[ConverterPerfilIfc] Conversao concluida: {Conv} convertidos, {Ign} ignorados de {Total} totais. Verifique entries IGNORADO acima para diagnostico individual.",
                        convertidos, ignorados, total);
                }
                catch (OperationCanceledException)
                {
                    // v2.7.7: cancelamento limpo. Transaction.Dispose() (using
                    // block exit) faz rollback automatico pois t.Commit() nunca
                    // foi chamado. Logger registra pra investigacao caso usuario
                    // reporte "convertia mas parou no meio".
                    Logger.Info(
                        "[ConverterPerfilIfc] Cancelado pelo usuario apos {Conv}/{Total} elementos (rollback automatico)",
                        convertidos + ignorados, total);
                    progress?.Report(new ProgressReport(
                        convertidos + ignorados, total,
                        "Cancelado — alteracoes revertidas."));
                }
            }

            return (convertidos, ignorados);
        }

        /// <summary>
        /// v2.7.7: frequencia de reports de progresso. Cada N elementos processados,
        /// um <see cref="ProgressReport"/> e enviado. Valor escolhido empirico:
        /// 25 = ~280 reports em galpao de 6983 elementos (Alef), suave pra UI WPF
        /// sem inundacao do Dispatcher.
        /// </summary>
        private const int ProgressReportEveryNElements = 25;

        public List<Element> ColetarElementosIfc(Document doc)
        {
            var resultado = new List<Element>();

            foreach (Element e in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .WhereElementIsNotElementType())
            {
                if (EhElementoIfc(e))
                    resultado.Add(e);
            }

            AdicionarFamilyInstanceIfc(doc, BuiltInCategory.OST_GenericModel, resultado);
            AdicionarFamilyInstanceIfc(doc, BuiltInCategory.OST_StructuralColumns, resultado);
            AdicionarFamilyInstanceIfc(doc, BuiltInCategory.OST_StructuralFraming, resultado);

            return resultado;
        }

        private void AdicionarFamilyInstanceIfc(
            Document doc,
            BuiltInCategory categoria,
            List<Element> lista)
        {
            foreach (Element e in new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(categoria)
                .WhereElementIsNotElementType())
            {
                if (EhElementoIfc(e))
                    lista.Add(e);
            }
        }

        /// <summary>
        /// Identifica elemento IFC pela presenca do parametro IfcGUID (todos os elementos
        /// importados de IFC tem esse parametro).
        /// </summary>
        public static bool EhElementoIfc(Element e)
        {
            Parameter guid = e.LookupParameter("IfcGUID");
            return guid != null
                && guid.StorageType == StorageType.String
                && !string.IsNullOrWhiteSpace(guid.AsString());
        }

        /// <summary>
        /// v2.7.1: identifica se um elemento e um perfil estrutural linear
        /// (vigas/pilares/terças) — usado pelo filtro UX do
        /// <c>ConverterPerfilIfcWindow</c> para esconder acessorios IFC
        /// nao-conversiveis (armaduras, chapas, ganchos, BoltArrays).
        ///
        /// Aceita por DOIS criterios (OR):
        /// <list type="number">
        /// <item>Categoria estrutural nativa do Revit (Framing/Columns)</item>
        /// <item>DirectShape generico com geometria linear (bbox razao &gt;= 3:1)</item>
        /// </list>
        ///
        /// Delega o calculo geometrico ao helper puro <see cref="IfcStructuralFilterPure"/>
        /// — preserva testabilidade do criterio dimensional.
        /// </summary>
        public static bool EhPerfilEstruturalLinear(Element e)
        {
            if (e == null)
                return false;

            long catId = e.Category?.Id?.Value ?? 0;
            if (catId == (long)BuiltInCategory.OST_StructuralFraming)
                return true;
            if (catId == (long)BuiltInCategory.OST_StructuralColumns)
                return true;

            BoundingBoxXYZ bbox = e.get_BoundingBox(null);
            if (bbox == null)
                return false;

            double dx = bbox.Max.X - bbox.Min.X;
            double dy = bbox.Max.Y - bbox.Min.Y;
            double dz = bbox.Max.Z - bbox.Min.Z;
            return IfcStructuralFilterPure.EhLinearPorBbox(dx, dy, dz);
        }

        /// <summary>
        /// Coleta todos os parametros cujo nome comeca com "Ifc" e tem valor string nao vazio.
        /// </summary>
        public static Dictionary<string, string> ColetarParametrosIfc(Element e)
        {
            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Parameter p in e.Parameters)
            {
                string nome = p.Definition?.Name;
                if (nome == null
                    || !nome.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase)
                    || p.StorageType != StorageType.String)
                    continue;

                string valor = p.AsString();
                if (!string.IsNullOrWhiteSpace(valor))
                    resultado[nome] = valor;
            }
            return resultado;
        }

        public static bool TemIfcMaterial(Element e)
        {
            Parameter p = e.LookupParameter("IfcMaterial");
            return p != null
                && p.StorageType == StorageType.String
                && !string.IsNullOrWhiteSpace(p.AsString());
        }

        /// <summary>
        /// Preserva a rotacao da secao transversal do elemento IFC na FamilyInstance
        /// criada no Revit. Extrai o vetor de referencia da maior face lateral do IFC
        /// via <see cref="SectionOrientationExtractor"/> e rotaciona o elemento para
        /// alinhar com esse vetor, partindo da orientacao padrao do Revit.
        ///
        /// Orientacao padrao Revit: para eixo nao-vertical, a referencia e a projecao
        /// de Z-global perpendicular ao eixo (flanges ficam "para cima" por padrao).
        /// Para eixo quase-vertical (coluna), a referencia padrao e X-global no plano XY.
        ///
        /// Operacao silenciosa: qualquer falha e logada em Debug e ignorada, sem
        /// impactar o elemento ja criado.
        /// </summary>
        private void TentarAplicarRotacaoSecao(
            FamilyInstance nova,
            Element origem,
            Line linha,
            Document doc)
        {
            List<FaceData> faces = SectionAxisExtractor.ColetarFaces(origem);
            if (faces.Count < 3)
                return;

            XYZ eixoDir = (linha.GetEndPoint(1) - linha.GetEndPoint(0)).Normalize();
            Vec3 eixoVec = new Vec3(eixoDir.X, eixoDir.Y, eixoDir.Z);

            Vec3? ifcRef = SectionOrientationExtractor.ExtrairReferenciaSecao(faces, eixoVec);
            if (!ifcRef.HasValue)
                return;

            // Referencia padrao Revit: componente de Z-global (ou X-global para eixo vertical)
            // projetada no plano perpendicular ao eixo do elemento.
            XYZ globalRef = Math.Abs(eixoDir.Z) > 0.99 ? XYZ.BasisX : XYZ.BasisZ;
            double dotGlobal = globalRef.DotProduct(eixoDir);
            XYZ revitRef = globalRef - eixoDir.Multiply(dotGlobal);
            if (revitRef.GetLength() < 1e-9)
                return;
            revitRef = revitRef.Normalize();

            XYZ ifcRefXyz = new XYZ(ifcRef.Value.X, ifcRef.Value.Y, ifcRef.Value.Z).Normalize();

            double dot = Math.Max(-1.0, Math.Min(1.0, revitRef.DotProduct(ifcRefXyz)));
            double angulo = Math.Acos(dot);
            if (angulo < 0.5 * Math.PI / 180.0)
                return; // diferenca < 0.5 graus — dentro da tolerancia

            // Sinal: produto vetorial determina sentido da rotacao
            XYZ cross = revitRef.CrossProduct(ifcRefXyz);
            if (cross.DotProduct(eixoDir) < 0)
                angulo = -angulo;

            try
            {
                Line eixoRot = Line.CreateBound(
                    linha.GetEndPoint(0),
                    linha.GetEndPoint(0) + eixoDir);
                ElementTransformUtils.RotateElement(doc, nova.Id, eixoRot, angulo);

                Logger.Debug("[ConverterPerfilIfc] {Id}: rotacao secao aplicada {Deg:F1} graus",
                    nova.Id, angulo * 180.0 / Math.PI);
            }
            catch (Exception ex)
            {
                Logger.Debug("[ConverterPerfilIfc] {Id}: rotacao secao ignorada — {Msg}",
                    nova.Id, ex.Message);
            }
        }

        private void TentarAplicarMaterial(FamilyInstance instancia, string ifcMaterial, Document doc)
        {
            string nomeMaterial = IfcMaterialParser.ExtrairNomeMaterial(ifcMaterial);
            if (string.IsNullOrWhiteSpace(nomeMaterial))
                return;

            Material material = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m =>
                    m.Name.IndexOf(nomeMaterial, StringComparison.OrdinalIgnoreCase) >= 0
                    || nomeMaterial.IndexOf(m.Name, StringComparison.OrdinalIgnoreCase) >= 0);

            if (material == null)
                return;

            Parameter p = instancia.LookupParameter("Structural Material")
                ?? instancia.LookupParameter("Material Estrutural")
                ?? instancia.LookupParameter("Material");

            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.ElementId)
                p.Set(material.Id);
        }

        private Line ObterLinhaDoElemento(Element elemento)
        {
            // Caminho rapido para elementos com LocationCurve linear (raro em DirectShape IFC)
            if (elemento.Location is LocationCurve lc && lc.Curve is Line ln)
                return ln;

            // v2.7.0 BUG 1: usar SectionAxisExtractor que extrai eixo via caps planares
            // (centroides de PlanarFaces extremas anti-paralelas) com fallback PCA
            // sobre vertices. Preserva inclinacao 3D real — diagonal de tesoura
            // 45 graus mantem 45 graus apos conversao.
            Line eixo = SectionAxisExtractor.ExtrairEixo(elemento);
            if (eixo != null)
                return eixo;

            // Fallback ultimo recurso: AABB world-aligned (comportamento legado do
            // Victor). So acionado se SectionAxisExtractor falhar — preserva
            // compatibilidade com pecas sem geometria visivel.
            BoundingBoxXYZ bbox = elemento.get_BoundingBox(null);
            if (bbox == null)
                return null;

            XYZ diag = bbox.Max - bbox.Min;
            double dx = Math.Abs(diag.X);
            double dy = Math.Abs(diag.Y);
            double dz = Math.Abs(diag.Z);
            XYZ centro = bbox.Min + diag.Multiply(0.5);

            XYZ start, end;
            if (dx >= dy && dx >= dz)
            {
                start = new XYZ(bbox.Min.X, centro.Y, centro.Z);
                end = new XYZ(bbox.Max.X, centro.Y, centro.Z);
            }
            else if (dy >= dx && dy >= dz)
            {
                start = new XYZ(centro.X, bbox.Min.Y, centro.Z);
                end = new XYZ(centro.X, bbox.Max.Y, centro.Z);
            }
            else
            {
                start = new XYZ(centro.X, centro.Y, bbox.Min.Z);
                end = new XYZ(centro.X, centro.Y, bbox.Max.Z);
            }

            if (start.DistanceTo(end) < RevitUtils.EPS)
                return null;

            return Line.CreateBound(start, end);
        }

        private Level ObterNivelDoElemento(
            Element elemento,
            Document doc,
            IReadOnlyList<Level> niveisOrdenados,
            Level fallback)
        {
            // Caminho 1: LevelId direto (preservado do MVP do Victor)
            if (elemento.LevelId != null && elemento.LevelId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(elemento.LevelId) is Level nivel)
                    return nivel;
            }

            // Caminho 2: parametros "Level"/"Nivel" via LookupParameter (preservado)
            Parameter paramNivel = elemento.LookupParameter("Level")
                ?? elemento.LookupParameter("Nivel");

            if (paramNivel?.StorageType == StorageType.ElementId)
            {
                if (doc.GetElement(paramNivel.AsElementId()) is Level nivel)
                    return nivel;
            }

            // v2.7.0 BUG 2: nivel mais proximo do Z medio do bbox (substitui
            // fallback fixo do MVP do Victor que desaclopava pecas em pisos
            // diferentes em modelos multi-pavimento).
            BoundingBoxXYZ bbox = elemento.get_BoundingBox(null);
            if (bbox != null && niveisOrdenados != null && niveisOrdenados.Count > 0)
            {
                double zCentro = (bbox.Min.Z + bbox.Max.Z) / 2.0;
                return LevelMatcher.MaisProximoDeZ(niveisOrdenados, zCentro, fallback);
            }

            return fallback;
        }
    }
}
