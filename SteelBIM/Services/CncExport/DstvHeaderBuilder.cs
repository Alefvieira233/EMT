using System;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.CncExport;

namespace FerramentaEMT.Services.CncExport
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
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (output == null) throw new ArgumentNullException(nameof(output));
            config ??= new ExportarDstvConfig();

            ElementType type = doc.GetElement(element.GetTypeId()) as ElementType;
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

            // ---------- Dimensoes do perfil (do tipo) ----------

            output.ProfileHeightMm     = ReadLengthMm(type, BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
            output.FlangeWidthMm       = ReadLengthMm(type, BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
            // Revit nao expoe BuiltInParameter universal para espessura mesa/alma em todas as versoes
            // (nomes diferem entre I-shape, HSS, channel, etc). Tentar shared parameters.
            output.FlangeThicknessMm   = TryReadSharedLengthMm(type, "Flange Thickness", "Espessura Mesa", "tf");
            output.WebThicknessMm      = TryReadSharedLengthMm(type, "Web Thickness", "Espessura Alma", "tw");

            // Raio de filete — Revit nao tem BuiltInParameter universal, tentar shared parameter
            output.FilletRadiusMm = TryReadSharedLengthMm(type, "Fillet Radius", "Raio Filete", "k");

            // ---------- Comprimento ----------

            output.CutLengthMm = GetCutLengthMm(element);

            // ---------- Peso linear ----------

            output.WeightPerMeter = ComputeWeightPerMeter(doc, element, output);
        }

        // ============================================================
        //  Mark / piece number
        // ============================================================

        public static string GetPieceMark(FamilyInstance element, ExportarDstvConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.NomeParametroMarca))
            {
                Parameter p = element.LookupParameter(config.NomeParametroMarca);
                string v = p?.AsString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }

            // ALL_MODEL_MARK
            Parameter pMark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            string mark = pMark?.AsString();
            if (!string.IsNullOrWhiteSpace(mark)) return mark;

            // Fallback: usar ID do elemento
            return $"ID-{element.Id?.Value ?? 0}";
        }

        // ============================================================
        //  Material / steel quality
        // ============================================================

        public static string GetSteelQuality(Document doc, FamilyInstance element, ElementType type)
        {
            ElementId matId = ElementId.InvalidElementId;

            Parameter pMat = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (pMat != null && pMat.HasValue) matId = pMat.AsElementId();

            if ((matId == null || matId == ElementId.InvalidElementId) && type != null)
            {
                Parameter pTypeMat = type.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (pTypeMat != null && pTypeMat.HasValue) matId = pTypeMat.AsElementId();
            }

            if ((matId == null || matId == ElementId.InvalidElementId) && element != null)
            {
                var mats = element.GetMaterialIds(false);
                if (mats != null) matId = mats.FirstOrDefault(x => x != ElementId.InvalidElementId)
                                          ?? ElementId.InvalidElementId;
            }

            if (matId == null || matId == ElementId.InvalidElementId) return "";

            Material mat = doc.GetElement(matId) as Material;
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
                Parameter p = element.LookupParameter(nome);
                string v = p?.AsString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return "";
        }

        // ============================================================
        //  Project / drawing
        // ============================================================

        public static string GetProjectCode(Document doc)
        {
            try
            {
                ProjectInfo info = doc?.ProjectInformation;
                if (info != null)
                {
                    // Tentar Project Number primeiro
                    Parameter pNum = info.get_Parameter(BuiltInParameter.PROJECT_NUMBER);
                    string num = pNum?.AsString();
                    if (!string.IsNullOrWhiteSpace(num)) return num;

                    // Fallback: Project Name
                    Parameter pName = info.get_Parameter(BuiltInParameter.PROJECT_NAME);
                    string name = pName?.AsString();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
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

        public static string GetDrawingNumber(Document doc)
        {
            try
            {
                ProjectInfo info = doc?.ProjectInformation;
                if (info != null)
                {
                    Parameter p = info.LookupParameter("Drawing Number") ?? info.LookupParameter("Numero do Desenho");
                    string v = p?.AsString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            return "";
        }

        // ============================================================
        //  Dimensoes
        // ============================================================

        private static double ReadLengthMm(Element elem, BuiltInParameter bip)
        {
            if (elem == null) return 0;
            try
            {
                Parameter p = elem.get_Parameter(bip);
                if (p == null || !p.HasValue) return 0;
                if (p.StorageType != StorageType.Double) return 0;
                return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao ler dimensao do perfil"); return 0; }
        }

        private static double TryReadSharedLengthMm(Element elem, params string[] candidateNames)
        {
            if (elem == null || candidateNames == null) return 0;
            foreach (string name in candidateNames)
            {
                try
                {
                    Parameter p = elem.LookupParameter(name);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            }
            return 0;
        }

        public static double GetCutLengthMm(FamilyInstance element)
        {
            if (element == null) return 0;

            // CUT_LENGTH (preferencial — leva em conta corte/coping)
            Parameter pCut = element.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            if (pCut != null && pCut.HasValue && pCut.StorageType == StorageType.Double)
            {
                double cut = pCut.AsDouble();
                if (cut > 0) return UnitUtils.ConvertFromInternalUnits(cut, UnitTypeId.Millimeters);
            }

            // INSTANCE_LENGTH (comprimento bruto)
            Parameter pLen = element.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
            if (pLen != null && pLen.HasValue && pLen.StorageType == StorageType.Double)
            {
                double len = pLen.AsDouble();
                if (len > 0) return UnitUtils.ConvertFromInternalUnits(len, UnitTypeId.Millimeters);
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
                    Parameter p = element.LookupParameter(n) ??
                                  (doc.GetElement(element.GetTypeId()) as ElementType)?.LookupParameter(n);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    {
                        double v = p.AsDouble();
                        if (v > 0) return v;
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro do header DSTV"); }
            }

            // 2. Calcular a partir de volume + densidade do material
            try
            {
                Parameter pVol = element.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if (pVol == null || !pVol.HasValue) return 0;

                double volumeFt3 = pVol.AsDouble();
                double volumeM3 = UnitUtils.ConvertFromInternalUnits(volumeFt3, UnitTypeId.CubicMeters);

                double cutLengthMm = partial.CutLengthMm > 0
                    ? partial.CutLengthMm
                    : GetCutLengthMm(element);

                if (cutLengthMm <= 0 || volumeM3 <= 0) return 0;

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
                Parameter p = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (p != null && p.HasValue) matId = p.AsElementId();

                if (matId == null || matId == ElementId.InvalidElementId) return null;
                Material mat = doc.GetElement(matId) as Material;
                if (mat == null) return null;

                ElementId psaId = mat.StructuralAssetId;
                if (psaId == null || psaId == ElementId.InvalidElementId) return null;

                PropertySetElement pse = doc.GetElement(psaId) as PropertySetElement;
                StructuralAsset asset = pse?.GetStructuralAsset();
                if (asset == null) return null;

                // Density retornado em kg/ft3 (Revit internal). Converter para kg/m3.
                double rawDensity = asset.Density;
                double densityKgM3 = UnitUtils.ConvertFromInternalUnits(rawDensity, UnitTypeId.KilogramsPerCubicMeter);
                return densityKgM3 > 0 ? densityKgM3 : (double?)null;
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao obter dado do material"); return null; }
        }
    }
}
