#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    /// <summary>
    /// Marcação Inteligente de Peças para fabricação metálica.
    /// Agrupa elementos idênticos por assinatura de fabricação
    /// (tipo + perfil + material + comprimento de corte + parâmetros dimensionais)
    /// e atribui marcas únicas por grupo.
    /// </summary>
    public class MarcarPecasService
    {
        private const string Titulo = "Marcação de Peças";

        // Palavras que indicam parametros dimensionais (participam da assinatura)
        private static readonly string[] PalavrasDimensao =
        {
            "altura", "height", "largura", "width", "espessura", "thickness",
            "comprimento", "length", "corte", "cut", "diam", "diameter",
            "raio", "radius", "perfil", "profile", "seção", "secao", "section",
            "bitola", "size", "alma", "web", "mesa", "flange", "depth"
        };

        // Palavras a ignorar (nao participam da assinatura)
        private static readonly string[] PalavrasIgnoradas =
        {
            "marca", "mark", "coment", "comment", "material", "fase", "phase",
            "nível", "nivel", "level", "ifc", "guid", "peso", "weight",
            "volume", "área", "area", "id"
        };

        // Cores para destaque visual dos grupos
        private static readonly Color[] CoresPadrao =
        {
            new Color(0, 100, 200),     // Azul
            new Color(0, 180, 80),      // Verde
            new Color(255, 140, 0),     // Laranja
            new Color(200, 50, 50),     // Vermelho
            new Color(150, 50, 200),    // Roxo
            new Color(0, 180, 180),     // Ciano
            new Color(200, 150, 0),     // Ouro
            new Color(100, 60, 140),    // Violeta
            new Color(180, 80, 80),     // Terracota
            new Color(60, 140, 100),    // Esmeralda
            new Color(80, 80, 180),     // Indigo
            new Color(180, 100, 0),     // Caramelo
        };

        // ================================================================
        //  Modelo interno
        // ================================================================

        private sealed class ElementoComAssinatura
        {
            public FamilyInstance Elemento { get; set; } = null!;
            public string CategoriaLogica { get; set; } = "";
            public string Assinatura { get; set; } = "";
            public string Familia { get; set; } = "";
            public string TipoPerfil { get; set; } = "";
            public string Material { get; set; } = "";
            public double ComprimentoCorteM { get; set; }
        }

        public sealed class ResultadoMarcacao
        {
            public int TotalElementos { get; set; }
            public int TotalGrupos { get; set; }
            public int MarcasAtribuidas { get; set; }
            public int ElementosPulados { get; set; }
            public List<string> Falhas { get; set; } = new();
            public List<GrupoMarcado> Grupos { get; set; } = new();
        }

        public sealed class GrupoMarcado
        {
            public string Marca { get; set; } = "";
            public string CategoriaLogica { get; set; } = "";
            public string TipoPerfil { get; set; } = "";
            public string Material { get; set; } = "";
            public double ComprimentoCorteM { get; set; }
            public int Quantidade { get; set; }
            public List<ElementId> ElementIds { get; set; } = new();
        }

        // ================================================================
        //  Ponto de entrada
        // ================================================================

        public ResultadoMarcacao Executar(UIDocument uidoc, MarcarPecasConfig config)
        {
            Document doc = uidoc.Document;
            var resultado = new ResultadoMarcacao();

            // 1. Coletar elementos
            List<FamilyInstance> elementos = ObterElementos(uidoc, doc, config);
            if (elementos.Count == 0)
            {
                AppDialogService.ShowWarning(Titulo,
                    "Nenhum elemento estrutural válido encontrado.",
                    "Seleção vazia");
                return resultado;
            }

            // 2. Classificar e montar assinaturas
            List<ElementoComAssinatura> classificados = ClassificarElementos(doc, elementos, config);
            if (classificados.Count == 0)
            {
                AppDialogService.ShowWarning(Titulo,
                    "Nenhum elemento corresponde às categorias selecionadas.",
                    "Filtro vazio");
                return resultado;
            }

            resultado.TotalElementos = classificados.Count;

            // 3. Agrupar por assinatura
            Dictionary<string, List<ElementoComAssinatura>> grupos = classificados
                .GroupBy(e => e.Assinatura)
                .ToDictionary(g => g.Key, g => g.ToList());

            resultado.TotalGrupos = grupos.Count;

            // 4. Ordenar grupos por categoria, depois por tipo de perfil
            var gruposOrdenados = grupos
                .OrderBy(g => g.Value.First().CategoriaLogica)
                .ThenBy(g => g.Value.First().TipoPerfil)
                .ThenBy(g => g.Value.First().ComprimentoCorteM)
                .ToList();

            // 5. Atribuir marcas e gravar nos elementos
            using (Transaction t = new Transaction(doc, "Marcação Inteligente de Peças"))
            {
                t.Start();

                // Numeração separada por categoria
                Dictionary<string, int> contadores = new()
                {
                    { "Viga", config.NumeroInicial },
                    { "Pilar", config.NumeroInicial },
                    { "Contraventamento", config.NumeroInicial }
                };

                foreach (var par in gruposOrdenados)
                {
                    List<ElementoComAssinatura> membros = par.Value;
                    string categoria = membros.First().CategoriaLogica;
                    string prefixo = config.ObterPrefixo(categoria);

                    if (!contadores.ContainsKey(categoria))
                        contadores[categoria] = config.NumeroInicial;

                    string marca = $"{prefixo}-{config.FormatarNumero(contadores[categoria])}";

                    var grupoMarcado = new GrupoMarcado
                    {
                        Marca = marca,
                        CategoriaLogica = categoria,
                        TipoPerfil = membros.First().TipoPerfil,
                        Material = membros.First().Material,
                        ComprimentoCorteM = membros.First().ComprimentoCorteM,
                        Quantidade = membros.Count
                    };

                    foreach (var membro in membros)
                    {
                        bool gravou = GravarMarca(doc, membro.Elemento, marca, config);
                        if (gravou)
                        {
                            resultado.MarcasAtribuidas++;
                            grupoMarcado.ElementIds.Add(membro.Elemento.Id);
                        }
                        else
                        {
                            resultado.ElementosPulados++;
                        }
                    }

                    resultado.Grupos.Add(grupoMarcado);
                    contadores[categoria]++;
                }

                // 6. Destaque visual (cores por grupo)
                if (config.DestaqueVisual)
                {
                    AplicarDestaqueVisual(doc, resultado.Grupos);
                }

                t.Commit();
            }

            // 7. Resumo
            ExibirResumo(resultado, config);
            return resultado;
        }

        // ================================================================
        //  Selecao de elementos
        // ================================================================

        private List<FamilyInstance> ObterElementos(
            UIDocument uidoc, Document doc, MarcarPecasConfig config)
        {
            switch (config.Escopo)
            {
                case EscopoMarcacao.ModeloInteiro:
                    return ColetarDoModelo(doc);

                case EscopoMarcacao.VistaAtiva:
                    return ColetarDaVista(doc);

                case EscopoMarcacao.SelecaoManual:
                default:
                    return ColetarSelecaoManual(uidoc, doc);
            }
        }

        private List<FamilyInstance> ColetarDoModelo(Document doc)
        {
            var result = new List<FamilyInstance>();
            var cats = new[] {
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns
            };

            foreach (var cat in cats)
            {
                result.AddRange(
                    new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>());
            }
            return result;
        }

        private List<FamilyInstance> ColetarDaVista(Document doc)
        {
            var result = new List<FamilyInstance>();
            View vista = doc.ActiveView;
            var cats = new[] {
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns
            };

            foreach (var cat in cats)
            {
                result.AddRange(
                    new FilteredElementCollector(doc, vista.Id)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>());
            }
            return result;
        }

        private List<FamilyInstance> ColetarSelecaoManual(UIDocument uidoc, Document doc)
        {
            // Tentar elementos ja selecionados
            var selecionados = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Where(fi =>
                {
                    var cat = fi.Category?.BuiltInCategory;
                    return cat == BuiltInCategory.OST_StructuralFraming
                        || cat == BuiltInCategory.OST_StructuralColumns;
                })
                .ToList();

            if (selecionados.Count > 0)
                return selecionados;

            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroEstruturalMarcacao(),
                    "Selecione as peças a marcar e pressione Enter");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<FamilyInstance>()
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<FamilyInstance>();
            }
        }

        // ================================================================
        //  Classificacao e assinatura
        // ================================================================

        private List<ElementoComAssinatura> ClassificarElementos(
            Document doc, List<FamilyInstance> elementos, MarcarPecasConfig config)
        {
            var resultado = new List<ElementoComAssinatura>();

            foreach (var elem in elementos)
            {
                string? categoriaLogica = ClassificarCategoria(elem);
                if (categoriaLogica == null) continue;

                // Filtro por categorias habilitadas
                if (categoriaLogica == "Viga" && !config.MarcarVigas) continue;
                if (categoriaLogica == "Pilar" && !config.MarcarPilares) continue;
                if (categoriaLogica == "Contraventamento" && !config.MarcarContraventamentos) continue;

                ElementType? tipo = doc.GetElement(elem.GetTypeId()) as ElementType;
                Material? material = ObterMaterialPrincipal(doc, elem, tipo);

                double comprimentoM = ObterComprimentoCorteMetros(elem, config.ToleranciaComprimentoMm);
                string assinatura = MontarAssinaturaFabricacao(
                    elem, tipo, material, categoriaLogica, comprimentoM);

                resultado.Add(new ElementoComAssinatura
                {
                    Elemento = elem,
                    CategoriaLogica = categoriaLogica,
                    Assinatura = assinatura,
                    Familia = elem.Symbol?.Family?.Name ?? "<Sem família>",
                    TipoPerfil = tipo?.Name ?? elem.Name ?? "<Sem tipo>",
                    Material = material?.Name ?? "<Sem material>",
                    ComprimentoCorteM = comprimentoM
                });
            }

            return resultado;
        }

        private static string? ClassificarCategoria(FamilyInstance elem)
        {
            var cat = elem.Category?.BuiltInCategory;
            if (cat == BuiltInCategory.OST_StructuralColumns)
                return "Pilar";
            if (cat == BuiltInCategory.OST_StructuralFraming)
            {
                if (elem.StructuralType == StructuralType.Brace)
                    return "Contraventamento";
                return "Viga";
            }
            return null;
        }

        // ================================================================
        //  Assinatura de fabricacao
        //  Replica fielmente a logica do MontarAssinaturaFabricacao
        //  do ListaMateriaisExportService, garantindo compatibilidade.
        // ================================================================

        private static string MontarAssinaturaFabricacao(
            FamilyInstance elem, ElementType? tipo, Material? material,
            string categoriaLogica, double comprimentoM)
        {
            StringBuilder sb = new StringBuilder();

            // Categoria
            sb.Append("CAT=").Append(categoriaLogica).Append('|');

            // Tipo (ID garante que mesmo perfil = mesmo tipo)
            sb.Append("TYPE=").Append(tipo?.Id?.Value ?? elem.GetTypeId().Value).Append('|');

            // Material
            sb.Append("MAT=").Append(material?.Id?.Value ?? -1).Append('|');

            // Comprimento de corte (arredondado)
            if (comprimentoM > 0.0)
                sb.Append("CUT=").Append(FormatarNumero(comprimentoM)).Append('|');

            // Parametros dimensionais do tipo
            AppendParametrosDimensao(sb, "T", tipo as Element);

            // Parametros dimensionais da instancia
            AppendParametrosDimensao(sb, "I", elem as Element);

            return sb.ToString();
        }

        private static void AppendParametrosDimensao(StringBuilder sb, string prefixo, Element? elemento)
        {
            if (elemento == null) return;

            foreach (Parameter param in elemento.Parameters.Cast<Parameter>())
            {
                if (param?.Definition == null) continue;
                if (!ParametroParticipaDaChave(param.Definition.Name)) continue;

                string valor = ObterValorParametroParaChave(param);
                if (string.IsNullOrWhiteSpace(valor)) continue;

                sb.Append(prefixo).Append(':')
                  .Append(Normalizar(param.Definition.Name)).Append('=')
                  .Append(valor).Append('|');
            }
        }

        private static bool ParametroParticipaDaChave(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return false;
            string normalizado = Normalizar(nome);
            if (PalavrasIgnoradas.Any(normalizado.Contains)) return false;
            return PalavrasDimensao.Any(normalizado.Contains);
        }

        private static string ObterValorParametroParaChave(Parameter param)
        {
            return param.StorageType switch
            {
                StorageType.Double => FormatarNumero(param.AsDouble()),
                StorageType.Integer => param.AsInteger().ToString(CultureInfo.InvariantCulture),
                StorageType.String => Normalizar(param.AsString()),
                _ => string.Empty
            };
        }

        private static string Normalizar(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? string.Empty
                : valor.Trim().ToLowerInvariant();
        }

        private static string FormatarNumero(double valor)
        {
            return Math.Round(valor, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        // ================================================================
        //  Material e comprimento
        // ================================================================

        private static Material? ObterMaterialPrincipal(Document doc, FamilyInstance elem, ElementType? tipo)
        {
            ElementId? matId = ObterMaterialEstruturalId(elem)
                            ?? ObterMaterialEstruturalId(tipo as Element)
                            ?? ObterPrimeiroMaterialGeometrico(elem);

            if (matId == null || matId == ElementId.InvalidElementId)
                return null;

            return doc.GetElement(matId) as Material;
        }

        private static ElementId? ObterMaterialEstruturalId(Element? elemento)
        {
            if (elemento == null) return null;
            Parameter? p = elemento.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (p != null && p.HasValue)
            {
                ElementId id = p.AsElementId();
                if (id != null && id != ElementId.InvalidElementId)
                    return id;
            }
            return null;
        }

        private static ElementId? ObterPrimeiroMaterialGeometrico(Element? elemento)
        {
            if (elemento == null) return null;
            try
            {
                ICollection<ElementId> ids = elemento.GetMaterialIds(false);
                return ids?.FirstOrDefault(x => x != ElementId.InvalidElementId);
            }
            catch { return null; }
        }

        /// <summary>
        /// Obtem comprimento de corte em metros, arredondado conforme tolerancia.
        /// </summary>
        private static double ObterComprimentoCorteMetros(FamilyInstance elem, double toleranciaMm)
        {
            double comprimentoInterno = 0;

            // Tentar parametro CUT_LENGTH
            Parameter? pCut = elem.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            if (pCut != null && pCut.HasValue && pCut.StorageType == StorageType.Double)
                comprimentoInterno = pCut.AsDouble();

            // Fallback: parametro INSTANCE_LENGTH
            if (comprimentoInterno <= 0)
            {
                Parameter? pLen = elem.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
                if (pLen != null && pLen.HasValue && pLen.StorageType == StorageType.Double)
                    comprimentoInterno = pLen.AsDouble();
            }

            // Fallback: curva
            if (comprimentoInterno <= 0)
            {
                Curve? curva = RevitUtils.GetElementCurve(elem);
                if (curva != null)
                    comprimentoInterno = curva.Length;
            }

            if (comprimentoInterno <= 0) return 0;

            double metros = UnitUtils.ConvertFromInternalUnits(comprimentoInterno, UnitTypeId.Meters);

            // Arredondar para a tolerancia especificada
            double toleranciaM = toleranciaMm / 1000.0;
            if (toleranciaM > 0)
                metros = Math.Round(metros / toleranciaM) * toleranciaM;

            return metros;
        }

        // ================================================================
        //  Gravacao de marcas
        // ================================================================

        private static bool GravarMarca(
            Document doc, FamilyInstance elem, string marca, MarcarPecasConfig config)
        {
            Parameter? param = null;

            switch (config.Destino)
            {
                case DestinoMarca.ParametroMark:
                    param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    break;

                case DestinoMarca.ParametroComments:
                    param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    break;

                case DestinoMarca.ParametroCustomizado:
                    param = elem.LookupParameter(config.NomeParametroCustomizado);
                    break;
            }

            if (param == null || param.IsReadOnly)
                return false;

            // Verificar se ja tem marca e se devemos sobrescrever
            if (!config.SobrescreverExistentes)
            {
                string? valorAtual = param.AsString();
                if (!string.IsNullOrWhiteSpace(valorAtual))
                    return false;
            }

            param.Set(marca);
            return true;
        }

        // ================================================================
        //  Destaque visual
        // ================================================================

        private static void AplicarDestaqueVisual(Document doc, List<GrupoMarcado> grupos)
        {
            View? vistaAtiva = doc.ActiveView;
            if (vistaAtiva == null) return;

            // Obter um FillPatternElement para preenchimento solido
            ElementId? solidFillId = ObterPreenchimentoSolido(doc);

            for (int i = 0; i < grupos.Count; i++)
            {
                Color cor = CoresPadrao[i % CoresPadrao.Length];
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();

                ogs.SetProjectionLineColor(cor);

                if (solidFillId != null)
                {
                    ogs.SetSurfaceForegroundPatternId(solidFillId);
                    ogs.SetSurfaceForegroundPatternColor(cor);
                }

                foreach (ElementId id in grupos[i].ElementIds)
                {
                    try
                    {
                        vistaAtiva.SetElementOverrides(id, ogs);
                    }
                    catch (Exception ex)
                    {
                        // Elemento pode nao ser visivel nesta vista
                        Logger.Debug("[MarcarPecas] Override falhou para elemento {Id}: {Msg}", id, ex.Message);
                    }
                }
            }
        }

        private static ElementId? ObterPreenchimentoSolido(Document doc)
        {
            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill)
                    ?.Id;
            }
            catch
            {
                return null;
            }
        }

        // ================================================================
        //  Resumo
        // ================================================================

        private static void ExibirResumo(ResultadoMarcacao resultado, MarcarPecasConfig config)
        {
            if (resultado.MarcasAtribuidas == 0 && resultado.ElementosPulados == 0)
            {
                AppDialogService.ShowWarning(Titulo,
                    "Nenhuma marca foi atribuída.\n\n" +
                    "Verifique se os elementos possuem o parâmetro de destino selecionado " +
                    "e se ele não é somente-leitura.",
                    "Nenhuma marca atribuída");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Marcação concluída com sucesso!");
            sb.AppendLine();
            sb.AppendLine($"Elementos processados: {resultado.TotalElementos}");
            sb.AppendLine($"Grupos únicos: {resultado.TotalGrupos}");
            sb.AppendLine($"Marcas atribuídas: {resultado.MarcasAtribuidas}");

            if (resultado.ElementosPulados > 0)
                sb.AppendLine($"Elementos pulados (já marcados): {resultado.ElementosPulados}");

            sb.AppendLine();
            sb.AppendLine("Resumo por categoria:");

            // Agrupar por categoria
            var porCategoria = resultado.Grupos
                .GroupBy(g => g.CategoriaLogica)
                .OrderBy(g => g.Key);

            foreach (var cat in porCategoria)
            {
                int gruposCat = cat.Count();
                int elemsCat = cat.Sum(g => g.Quantidade);
                sb.AppendLine($"  {cat.Key}: {gruposCat} marca(s), {elemsCat} peça(s)");

                // Listar os 10 primeiros grupos
                int mostrados = 0;
                foreach (var g in cat.Take(10))
                {
                    sb.AppendLine($"    {g.Marca}: {g.TipoPerfil} | " +
                                  $"L={g.ComprimentoCorteM:F3}m | " +
                                  $"{g.Material} | Qtd={g.Quantidade}");
                    mostrados++;
                }
                if (cat.Count() > 10)
                    sb.AppendLine($"    ... +{cat.Count() - 10} grupos");
            }

            if (resultado.Falhas.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Observações:");
                foreach (string falha in resultado.Falhas.Take(10))
                    sb.AppendLine($"  • {falha}");
            }

            if (config.DestaqueVisual)
            {
                sb.AppendLine();
                sb.AppendLine("Destaque visual aplicado na vista ativa. " +
                              "Use o comando 'Limpar' para remover as cores.");
            }

            AppDialogService.ShowInfo(Titulo, sb.ToString(), "Marcação concluída");
        }

        // ================================================================
        //  Filtro de selecao
        // ================================================================

        private class FiltroEstruturalMarcacao : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                var cat = elem.Category?.BuiltInCategory;
                return cat == BuiltInCategory.OST_StructuralFraming
                    || cat == BuiltInCategory.OST_StructuralColumns;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
