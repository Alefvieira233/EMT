#nullable enable
using System;
using System.Linq;
using Autodesk.Revit.DB;
using SteelBIM.Infrastructure;
using SteelBIM.Models.CncExport;

namespace SteelBIM.Services.CncExport
{
    /// <summary>
    /// Constroi o cabecalho (bloco ST) de um arquivo NC1 a partir de um
    /// FamilyInstance estrutural do Revit.
    /// </summary>
    public static class DstvHeaderBuilder
    {
        /// <summary>
        /// Preenche os campos do cabecalho do <see cref="DstvFile"/> a partir do elemento.
        /// Nao preenche furos — para isso use <see cref="DstvHoleExtractor"/>.
        /// </summary>
        public static void Build(
            Document doc,
            FamilyInstance element,
            ExportarDstvConfig config,
            DstvFile output)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            config ??= new ExportarDstvConfig();

            ElementType? type = doc.GetElement(element.GetTypeId()) as ElementType;
            string familyName = element.Symbol?.Family?.Name ?? "";
            string typeName = type?.Name ?? element.Name ?? "";

            // ---------- Campos administrativos ----------

            output.OrderNumber = string.IsNullOrWhiteSpace(config.CodigoProjeto)
                ? GetProjectCode(doc)
                : config.CodigoProjeto;

            output.DrawingNumber = GetDrawingNumber(doc);
            output.Phase = string.IsNullOrWhiteSpace(config.Fase) ? "1" : config.Fase;
            output.PieceMark = GetPieceMark(element, config);
            output.SteelQuality = GetSteelQuality(doc, element, type);
            output.Quantity = 1; // ajustado pelo orquestrador quando for "UmPorMarca"
            output.SurfaceTreatment = string.IsNullOrWhiteSpace(config.TratamentoSuperficiePadrao)
                ? GetSurfaceTreatment(element)
                : config.TratamentoSuperficiePadrao;

            // ---------- Perfil ----------

            output.ProfileName = typeName;
            output.ProfileType = DstvProfileMapper.Map(familyName, typeName);

            // v2.8.10 (Etapa D): CHAPA tem mapeamento de dimensoes diferente de viga.
            // A ordem dos 6 campos numericos do ST e' (comp, largura, esp, esp, esp, 0).
            // Validado byte-a-byte contra CH02/CH03/CH04 do fabricante.
            if (output.ProfileType == DstvProfileType.B)
            {
                PreencherDimensoesChapa(doc, element, output);
            }
            else
            {
                PreencherDimensoesPerfil(element, type, output);
            }

            // ---------- Comprimento (vigas/perfis — chapa ja' setou em PreencherDimensoesChapa) ----------
            if (output.ProfileType != DstvProfileType.B)
                output.CutLengthMm = GetCutLengthMm(element);

            // ---------- Peso linear ----------
            output.WeightPerMeter = ComputeWeightPerMeter(doc, element, output);

            // v2.8.10 (Etapa D): area de pintura (m2/m) — fica em 0 ate' termos
            // uma fonte confiavel (parametro shared "Painting Surface" ou calculo
            // baseado em perimetro). Para vigas comuns, o fabricante tipicamente
            // calcula a partir da secao; para chapa nao se aplica (fica 0 mesmo).
            output.PaintingSurfacePerMeter = TryReadSharedLengthMm(type, "Painting Surface", "Area de Pintura", "m2/m");
            if (output.PaintingSurfacePerMeter <= 0)
            {
                output.PaintingSurfacePerMeter = 0;
                Logger.Info("[DstvHeaderBuilder] PaintingSurfacePerMeter = 0 para {Id} ({Profile}); sem parametro shared 'Painting Surface'.",
                    element.Id?.Value, output.ProfileName);
            }
        }

        // ============================================================
        //  CHAPA (DstvProfileType.B) — mapeamento + contorno AK
        // ============================================================
        // v2.8.10 (Etapa D): mapeamento de dimensoes + extracao de contorno
        // a partir da geometria do Revit. Validado byte-a-byte vs CH02/CH03/CH04.

        private static void PreencherDimensoesChapa(Document doc, FamilyInstance element, DstvFile output)
        {
            // 1. Caminho preferencial: extrai o contorno externo da face principal e
            // deriva as dimensoes do PLANO da face (comprimento/largura) + espessura
            // (volume/area). Funciona para chapa em QUALQUER orientacao — inclusive
            // inclinada/em parede — porque as dimensoes saem do frame local da face,
            // nao do bounding box world-aligned. Para chapa horizontal o resultado e'
            // identico ao bbox (sem regressao).
            if (TentarExtrairContornoEDimensoesChapa(element, output))
            {
                output.IncluirContornoAk = true;
                return;
            }

            // 2. Fallback: bounding box world-aligned. So e' confiavel para chapa
            // horizontal (normal em Z); chapa inclinada daria dimensoes infladas.
            output.IncluirContornoAk = false;
            Logger.Warn("[DstvHeaderBuilder] Chapa {Id} ({Profile}) — contorno nao extraido; AK desligado e dimensoes via bounding box (confiavel so se horizontal).",
                element.Id?.Value, output.ProfileName);

            BoundingBoxXYZ? bbox = element.get_BoundingBox(null);
            if (bbox == null)
            {
                Logger.Warn("[DstvHeaderBuilder] Chapa {Id} sem bounding box — dimensoes ficam zeradas.", element.Id?.Value);
                return;
            }

            double dxMm = UnitUtils.ConvertFromInternalUnits(bbox.Max.X - bbox.Min.X, UnitTypeId.Millimeters);
            double dyMm = UnitUtils.ConvertFromInternalUnits(bbox.Max.Y - bbox.Min.Y, UnitTypeId.Millimeters);
            double dzMm = UnitUtils.ConvertFromInternalUnits(bbox.Max.Z - bbox.Min.Z, UnitTypeId.Millimeters);

            AplicarDimensoes(output, DstvChapaDimensionsMapper.FromBoundingBox(dxMm, dyMm, dzMm));
        }

        private static void AplicarDimensoes(DstvFile output, DstvChapaDimensionsMapper.ChapaDimensions dims)
        {
            output.CutLengthMm = dims.CutLengthMm;
            output.ProfileHeightMm = dims.ProfileHeightMm;
            output.FlangeWidthMm = dims.FlangeWidthMm;
            output.WebThicknessMm = dims.WebThicknessMm;
            output.FlangeThicknessMm = dims.FlangeThicknessMm;
            output.FilletRadiusMm = dims.FilletRadiusMm;
        }

        private static void PreencherDimensoesPerfil(FamilyInstance element, ElementType? type, DstvFile output)
        {
            output.ProfileHeightMm = ReadLengthMm(type, BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
            output.FlangeWidthMm = ReadLengthMm(type, BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
            // Revit nao expoe BuiltInParameter universal para espessura mesa/alma em todas as versoes
            // (nomes diferem entre I-shape, HSS, channel, etc). Tentar shared parameters.
            output.FlangeThicknessMm = TryReadSharedLengthMm(type, "Flange Thickness", "Espessura Mesa", "tf");
            output.WebThicknessMm = TryReadSharedLengthMm(type, "Web Thickness", "Espessura Alma", "tw");

            // Raio de filete — Revit nao tem BuiltInParameter universal, tentar shared parameter
            output.FilletRadiusMm = TryReadSharedLengthMm(type, "Fillet Radius", "Raio Filete", "k");
        }

        /// <summary>
        /// Extrai o contorno externo da face principal (maior face planar da chapa),
        /// preenche <see cref="DstvFile.ContornoAk"/> e deriva as dimensoes do PLANO da
        /// face (comprimento/largura do contorno + espessura via volume/area). Retorna
        /// true se conseguiu contorno valido (>= 3 pontos) E espessura confiavel.
        ///
        /// Por usar o frame local da face (e nao o bounding box world-aligned), funciona
        /// para chapa em qualquer orientacao — inclusive inclinada/em parede.
        ///
        /// ⚠️ Revit-bound: requer smoke test em projeto real.
        /// </summary>
        private static bool TentarExtrairContornoEDimensoesChapa(FamilyInstance element, DstvFile output)
        {
            Options opts = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement? geo;
            try
            { geo = element.get_Geometry(opts); }
            catch (Exception ex)
            {
                Logger.Debug("[DstvHeaderBuilder] get_Geometry falhou para chapa {Id}: {Msg}", element.Id?.Value, ex.Message);
                return false;
            }

            if (geo == null)
                return false;

            // Encontrar maior face planar (a face "principal" da chapa) + volume do solido
            PlanarFace? maiorFace = EncontrarMaiorFacePlanar(geo, out double volumeSolidoFt3);
            if (maiorFace == null)
                return false;

            EdgeArrayArray loops = maiorFace.EdgeLoops;
            if (loops == null || loops.Size == 0)
                return false;

            // Loop externo = primeiro (maior perimetro tipicamente). EdgeLoops do Revit
            // tem o externo primeiro e os holes depois.
            EdgeArray loopExterno = loops.get_Item(0);
            if (loopExterno == null || loopExterno.Size == 0)
                return false;

            // Extrair pontos do loop e projetar no plano da face. As coordenadas DSTV
            // sao 2D (X horizontal, Y vertical) no plano da chapa.
            var pontosBruto = new System.Collections.Generic.List<(double X, double Y, double Raio)>();
            try
            {
                XYZ origem = maiorFace.Origin;
                XYZ vx = maiorFace.XVector;
                XYZ vy = maiorFace.YVector;

                foreach (Edge edge in loopExterno)
                {
                    Curve? c = edge.AsCurve();
                    if (c == null)
                        continue;
                    XYZ p = c.GetEndPoint(0);
                    XYZ local = p - origem;
                    double u = local.DotProduct(vx);
                    double v = local.DotProduct(vy);
                    double uMm = UnitUtils.ConvertFromInternalUnits(u, UnitTypeId.Millimeters);
                    double vMm = UnitUtils.ConvertFromInternalUnits(v, UnitTypeId.Millimeters);
                    // Raio = 0 (canto reto). Arcos exigiriam inspecionar Curve.IsBound + Curve as Arc
                    // — fica como follow-up Revit-bound.
                    pontosBruto.Add((uMm, vMm, 0.0));
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("[DstvHeaderBuilder] falha ao extrair pontos do loop da chapa {Id}: {Msg}", element.Id?.Value, ex.Message);
                return false;
            }

            if (pontosBruto.Count < 3)
                return false;

            // Normalizar para a convencao DSTV do fabricante (canto minimo em 0,0,
            // maior extensao no X = comprimento, winding CCW) e fechar o contorno.
            // O eixo/origem da PlanarFace do Revit e' arbitrario; a normalizacao
            // alinha o AK ao bloco ST e o torna deterministico (validado por unit test).
            System.Collections.Generic.List<(double X, double Y, double Raio)> normalizado =
                DstvContornoAkBuilder.Normalizar(pontosBruto);

            output.ContornoAk.Clear();
            output.ContornoAk.AddRange(DstvContornoAkBuilder.FecharContorno(normalizado));
            if (output.ContornoAk.Count < 4)
                return false;

            // Dimensoes do PLANO da face: comprimento/largura = extensao do contorno
            // normalizado; espessura = volume do solido / area da face (relacao do prisma).
            // Independe da orientacao da chapa no mundo — resolve o caso de chapa inclinada.
            double faceAreaFt2 = maiorFace.Area;
            double espessuraMm = (faceAreaFt2 > 1e-9 && volumeSolidoFt3 > 1e-9)
                ? UnitUtils.ConvertFromInternalUnits(volumeSolidoFt3 / faceAreaFt2, UnitTypeId.Millimeters)
                : 0.0;
            if (espessuraMm <= 0)
                return false; // sem espessura confiavel — cai no fallback bounding box

            try
            {
                AplicarDimensoes(output, DstvChapaDimensionsMapper.FromContorno(normalizado, espessuraMm));
            }
            catch (Exception ex)
            {
                Logger.Debug("[DstvHeaderBuilder] dimensoes do contorno invalidas para chapa {Id}: {Msg}", element.Id?.Value, ex.Message);
                return false;
            }

            return true;
        }

        // Maior face planar do elemento + volume do solido que a contem (em unidades
        // internas: area ft2, volume ft3). O volume permite derivar a espessura da chapa
        // (volume/area) independentemente da orientacao no mundo.
        private static PlanarFace? EncontrarMaiorFacePlanar(GeometryElement geo, out double volumeSolidoFt3)
        {
            PlanarFace? melhor = null;
            double maiorArea = 0.0;
            volumeSolidoFt3 = 0.0;

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 1e-9)
                {
                    foreach (Face f in solid.Faces)
                    {
                        if (f is PlanarFace pf && pf.Area > maiorArea)
                        {
                            maiorArea = pf.Area;
                            melhor = pf;
                            volumeSolidoFt3 = solid.Volume;
                        }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    GeometryElement? inner = gi.GetInstanceGeometry();
                    if (inner != null)
                    {
                        PlanarFace? candidato = EncontrarMaiorFacePlanar(inner, out double volInnerFt3);
                        if (candidato != null && candidato.Area > maiorArea)
                        {
                            maiorArea = candidato.Area;
                            melhor = candidato;
                            volumeSolidoFt3 = volInnerFt3;
                        }
                    }
                }
            }

            return melhor;
        }

        // ============================================================
        //  Mark / piece number
        // ============================================================

        public static string GetPieceMark(FamilyInstance element, ExportarDstvConfig? config)
        {
            // A6: leitura dos valores Revit aqui; a PRECEDENCIA fica no helper puro testavel.
            string? valorConfig = null;
            if (config != null && !string.IsNullOrWhiteSpace(config.NomeParametroMarca))
                valorConfig = element.LookupParameter(config.NomeParametroMarca)?.AsString();

            string? mark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
            return DstvPieceMark.Escolher(valorConfig, mark, element.Id?.Value ?? 0);
        }

        // ============================================================
        //  Material / steel quality
        // ============================================================

        public static string GetSteelQuality(Document doc, FamilyInstance element, ElementType? type)
        {
            ElementId matId = ElementId.InvalidElementId;

            Parameter? pMat = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (pMat != null && pMat.HasValue)
                matId = pMat.AsElementId();

            if ((matId == null || matId == ElementId.InvalidElementId) && type != null)
            {
                Parameter? pTypeMat = type.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (pTypeMat != null && pTypeMat.HasValue)
                    matId = pTypeMat.AsElementId();
            }

            if ((matId == null || matId == ElementId.InvalidElementId) && element != null)
            {
                var mats = element.GetMaterialIds(false);
                if (mats != null)
                    matId = mats.FirstOrDefault(x => x != ElementId.InvalidElementId)
                                          ?? ElementId.InvalidElementId;
            }

            if (matId == null || matId == ElementId.InvalidElementId)
                return "";

            Material? mat = doc.GetElement(matId) as Material;
            return mat?.Name ?? "";
        }

        // ============================================================
        //  Surface treatment
        // ============================================================

        private static string GetSurfaceTreatment(FamilyInstance element)
        {
            // Procurar por nomes comuns de parametro
            string[] candidatos = { "Surface Treatment", "Tratamento Superficie", "Pintura", "Acabamento", "Coating" };
            foreach (string nome in candidatos)
            {
                Parameter? p = element.LookupParameter(nome);
                string? v = p?.AsString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
            return "";
        }

        // ============================================================
        //  Project / drawing
        // ============================================================

        public static string GetProjectCode(Document? doc)
        {
            try
            {
                ProjectInfo? info = doc?.ProjectInformation;
                if (info != null)
                {
                    // Tentar Project Number primeiro
                    Parameter? pNum = info.get_Parameter(BuiltInParameter.PROJECT_NUMBER);
                    string? num = pNum?.AsString();
                    if (!string.IsNullOrWhiteSpace(num))
                        return num;

                    // Fallback: Project Name
                    Parameter? pName = info.get_Parameter(BuiltInParameter.PROJECT_NAME);
                    string? name = pName?.AsString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }

            // Fallback: nome do arquivo
            try
            {
                if (!string.IsNullOrWhiteSpace(doc?.PathName))
                    return System.IO.Path.GetFileNameWithoutExtension(doc.PathName);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }

            return "";
        }

        public static string GetDrawingNumber(Document? doc)
        {
            try
            {
                ProjectInfo? info = doc?.ProjectInformation;
                if (info != null)
                {
                    Parameter? p = info.LookupParameter("Drawing Number") ?? info.LookupParameter("Numero do Desenho");
                    string? v = p?.AsString();
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            return "";
        }

        // ============================================================
        //  Dimensoes
        // ============================================================

        private static double ReadLengthMm(Element? elem, BuiltInParameter bip)
        {
            if (elem == null)
                return 0;
            try
            {
                Parameter? p = elem.get_Parameter(bip);
                if (p == null || !p.HasValue)
                    return 0;
                if (p.StorageType != StorageType.Double)
                    return 0;
                return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler dimensao do perfil"); return 0; }
        }

        private static double TryReadSharedLengthMm(Element? elem, params string[] candidateNames)
        {
            if (elem == null || candidateNames == null)
                return 0;
            foreach (string name in candidateNames)
            {
                try
                {
                    Parameter? p = elem.LookupParameter(name);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            }
            return 0;
        }

        public static double GetCutLengthMm(FamilyInstance element)
        {
            if (element == null)
                return 0;

            // CUT_LENGTH (preferencial — leva em conta corte/coping)
            Parameter? pCut = element.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            if (pCut != null && pCut.HasValue && pCut.StorageType == StorageType.Double)
            {
                double cut = pCut.AsDouble();
                if (cut > 0)
                    return UnitUtils.ConvertFromInternalUnits(cut, UnitTypeId.Millimeters);
            }

            // INSTANCE_LENGTH (comprimento bruto)
            Parameter? pLen = element.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
            if (pLen != null && pLen.HasValue && pLen.StorageType == StorageType.Double)
            {
                double len = pLen.AsDouble();
                if (len > 0)
                    return UnitUtils.ConvertFromInternalUnits(len, UnitTypeId.Millimeters);
            }

            // Fallback: location curve
            try
            {
                if (element.Location is LocationCurve lc && lc.Curve != null)
                    return UnitUtils.ConvertFromInternalUnits(lc.Curve.Length, UnitTypeId.Millimeters);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }

            return 0;
        }

        // ============================================================
        //  Peso linear (kg/m)
        // ============================================================

        private static double ComputeWeightPerMeter(Document doc, FamilyInstance element, DstvFile partial)
        {
            // 1. Tentar parametro direto
            string[] pesoNames = { "Weight per Meter", "Peso Linear", "Peso/m", "kg/m" };
            foreach (string n in pesoNames)
            {
                try
                {
                    Parameter? p = element.LookupParameter(n) ??
                                  (doc.GetElement(element.GetTypeId()) as ElementType)?.LookupParameter(n);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    {
                        double v = p.AsDouble();
                        if (v > 0)
                            return v;
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            }

            // 2. Calcular a partir de volume + densidade do material
            try
            {
                Parameter? pVol = element.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if (pVol == null || !pVol.HasValue)
                    return 0;

                double volumeFt3 = pVol.AsDouble();
                double volumeM3 = UnitUtils.ConvertFromInternalUnits(volumeFt3, UnitTypeId.CubicMeters);

                double cutLengthMm = partial.CutLengthMm > 0
                    ? partial.CutLengthMm
                    : GetCutLengthMm(element);

                if (cutLengthMm <= 0 || volumeM3 <= 0)
                    return 0;

                double cutLengthM = cutLengthMm / 1000.0;
                double areaM2 = volumeM3 / cutLengthM;

                // Densidade do aco — buscar do material, default 7850 kg/m3
                double densityKgM3 = GetDensityKgPerM3(doc, element) ?? 7850.0;

                return Math.Round(areaM2 * densityKgM3, 2);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }

            return 0;
        }

        private static double? GetDensityKgPerM3(Document doc, FamilyInstance element)
        {
            try
            {
                ElementId matId = ElementId.InvalidElementId;
                Parameter? p = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (p != null && p.HasValue)
                    matId = p.AsElementId();

                if (matId == null || matId == ElementId.InvalidElementId)
                    return null;
                Material? mat = doc.GetElement(matId) as Material;
                if (mat == null)
                    return null;

                ElementId psaId = mat.StructuralAssetId;
                if (psaId == null || psaId == ElementId.InvalidElementId)
                    return null;

                PropertySetElement? pse = doc.GetElement(psaId) as PropertySetElement;
                StructuralAsset? asset = pse?.GetStructuralAsset();
                if (asset == null)
                    return null;

                // Density retornado em kg/ft3 (Revit internal). Converter para kg/m3.
                double rawDensity = asset.Density;
                double densityKgM3 = UnitUtils.ConvertFromInternalUnits(rawDensity, UnitTypeId.KilogramsPerCubicMeter);
                return densityKgM3 > 0 ? densityKgM3 : (double?)null;
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao obter dado do material"); return null; }
        }
    }
}
