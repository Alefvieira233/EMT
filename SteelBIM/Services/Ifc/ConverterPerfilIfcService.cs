using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services.Ifc
{
    public class ConverterPerfilIfcService
    {
        public (int convertidos, int ignorados) Executar(Document doc, ConverterPerfilIfcConfig config)
        {
            int convertidos = 0;
            int ignorados = 0;

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

                foreach (ConversaoElementoIfc conversao in config.Conversoes)
                {
                    Element origem = doc.GetElement(conversao.ElementoOrigem);
                    if (origem == null)
                    { ignorados++; continue; }

                    Line linha = ObterLinhaDoElemento(origem);
                    if (linha == null)
                    { ignorados++; continue; }

                    Level nivel = ObterNivelDoElemento(origem, doc, niveis, config.NivelPadrao);
                    if (nivel == null)
                    { ignorados++; continue; }

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
                            nova = doc.Create.NewFamilyInstance(
                                linha.GetEndPoint(0), simbolo, nivel, StructuralType.Column);
                        else
                            nova = doc.Create.NewFamilyInstance(
                                linha, simbolo, nivel, StructuralType.Beam);

                        string ifcMaterial = origem.LookupParameter("IfcMaterial")?.AsString();
                        if (!string.IsNullOrWhiteSpace(ifcMaterial))
                            TentarAplicarMaterial(nova, ifcMaterial, doc);

                        convertidos++;

                        if (config.DeletarOriginal)
                            doc.Delete(origem.Id);
                    }
                    catch (Exception)
                    {
                        ignorados++;
                    }
                }

                t.Commit();
            }

            return (convertidos, ignorados);
        }

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
