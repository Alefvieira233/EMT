using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    public class ListaMateriaisExportService
    {
        private const string Titulo = "Exportar Lista de Materiais";
        private const string NomeTemplateLdm = "ModeloLDM.xlsx";
        private const string NomeAbaCapa = "CAPA";
        private const string NomeAbaPlanilhaBase = "Planilha Base";
        private const string CorCabecalhoLdm = "#A5A5A5";
        private const string CorSecaoPrimariaLdm = "#FABF8F";
        private const string CorSecaoSecundariaLdm = "#FFFF99";
        private const double DensidadePadraoConcretoKgM3 = 25.0;
        private const double DensidadePadraoAcoKgM3 = 7850.0;

        private static readonly BuiltInCategory[] CategoriasPerfisConexao =
        {
            BuiltInCategory.OST_StructConnectionProfiles
        };

        private static readonly BuiltInCategory[] CategoriasConexoesDetalhadas =
        {
            BuiltInCategory.OST_StructConnectionPlates,
            BuiltInCategory.OST_StructConnectionAnchors,
            BuiltInCategory.OST_StructConnectionBolts,
            BuiltInCategory.OST_StructConnectionHoles,
            BuiltInCategory.OST_StructConnectionModifiers,
            BuiltInCategory.OST_StructConnectionOthers,
            BuiltInCategory.OST_StructConnectionShearStuds,
            BuiltInCategory.OST_StructConnectionWelds
        };

        private static readonly BuiltInCategory[] CategoriasFundacoes =
        {
            BuiltInCategory.OST_StructuralFoundation
        };

        private static readonly string[] PalavrasDimensao =
        {
            "altura", "height", "largura", "width", "espessura", "thickness",
            "comprimento", "length", "corte", "cut", "diam", "diameter",
            "raio", "radius", "perfil", "profile", "seção", "secao", "section",
            "bitola", "size", "alma", "web", "mesa", "flange", "depth"
        };

        private static readonly string[] PalavrasIgnoradas =
        {
            "marca", "mark", "coment", "comment", "material", "fase", "phase",
            "nível", "nivel", "level", "ifc", "guid", "peso", "weight",
            "volume", "área", "area", "id"
        };

        private static readonly string[] TokensPesoLinear =
        {
            "kg/m", "kg por m", "kg por metro", "kg metro",
            "peso linear", "massa linear",
            "peso por metro", "massa por metro",
            "mass per unit length", "mass per length", "mass/length",
            "weight per unit length", "weight per length", "weight/length",
            "unit mass", "unit weight"
        };

        private static readonly Regex NumeroTextoRegex =
            new Regex(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        public Result Exportar(UIDocument uidoc, ExportarListaMateriaisConfig config, ref string message)
        {
            try
            {
                if (uidoc is null)
                {
                    AppDialogService.ShowError(Titulo, "UIDocument nulo.", "Documento indisponível");
                    return Result.Failed;
                }

                if (config is null)
                {
                    AppDialogService.ShowWarning(Titulo, "Configuração inválida.", "Dados incompletos");
                    return Result.Failed;
                }

                if (!config.TemCategoriaSelecionada())
                {
                    AppDialogService.ShowWarning(Titulo, "Selecione ao menos uma categoria.", "Seleção incompleta");
                    return Result.Cancelled;
                }

                if (!config.TemAbaSelecionada())
                {
                    AppDialogService.ShowWarning(Titulo, "Selecione ao menos uma aba de saída.", "Seleção incompleta");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                List<ListaMateriaisLinha> linhas = ColetarLinhas(doc, uidoc, config);
                if (linhas.Count == 0)
                {
                    AppDialogService.ShowWarning(Titulo, "Nenhum elemento elegível foi encontrado com os filtros escolhidos.", "Nenhum item encontrado");
                    return Result.Cancelled;
                }

                List<ListaMateriaisGrupo> grupos = AgruparLinhas(linhas);
                SalvarWorkbook(config.CaminhoArquivo, grupos, config, doc.Title);

                int elementosEstruturais = grupos.Count(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.ElementosEstruturais);
                int perfisConexao = grupos.Count(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.PerfisConexao);
                int conexoes = grupos.Count(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.Conexoes);

                AppDialogService.ShowInfo(
                    Titulo,
                    "Exportação concluída com sucesso." +
                    $"\n\nArquivo: {config.CaminhoArquivo}" +
                    $"\nLinhas consolidadas: {grupos.Count}" +
                    $"\nElementos estruturais: {elementosEstruturais}" +
                    $"\nPerfis de conexão: {perfisConexao}" +
                    $"\nConexões: {conexoes}",
                    "Arquivo gerado");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                AppDialogService.ShowError(Titulo, ex.Message, "Falha na exportação");
                return Result.Failed;
            }
        }

        private static List<ListaMateriaisLinha> ColetarLinhas(Document doc, UIDocument uidoc, ExportarListaMateriaisConfig config)
        {
            List<Element> elementos = ColetarElementos(doc, uidoc, config);
            List<ListaMateriaisLinha> linhas = new List<ListaMateriaisLinha>();
            Dictionary<ElementId, ElementType> tiposCache = new Dictionary<ElementId, ElementType>();
            // Sprint 2: cache de Materials (fix N+1 — em projetos com milhares de elementos
            // o doc.GetElement por material era chamado uma vez por elemento; tipicamente
            // existem <50 materiais distintos, entao o cache resolve quase 100% das chamadas).
            Dictionary<ElementId, Material> materiaisCache = new Dictionary<ElementId, Material>();

            foreach (Element elemento in elementos)
            {
                ListaMateriaisCategoriaLogica? categoriaLogica = ClassificarElemento(elemento);
                if (!categoriaLogica.HasValue || !CategoriaPermitida(categoriaLogica.Value, config))
                    continue;

                ElementType tipo = ObterTipoElemento(doc, elemento, tiposCache);
                Material material = ObterMaterialPrincipal(doc, elemento, tipo, materiaisCache);
                string nomeMaterial = material?.Name ?? "<Sem material estrutural>";

                double comprimentoM = ObterComprimentoCorteMetros(elemento);
                double areaM2 = ObterAreaMetrosQuadrados(elemento, material);
                double volumeM3 = ObterVolumeMetrosCubicos(elemento, material);
                int quantidadeBase = ObterQuantidadeBase(elemento, categoriaLogica.Value);
                (double pesoKg, string origemPeso) = ObterPesoInfo(
                    doc,
                    elemento,
                    tipo,
                    material,
                    categoriaLogica.Value,
                    comprimentoM,
                    volumeM3);

                if (quantidadeBase > 1 && DeveRatearMedidasPorQuantidade(elemento))
                {
                    areaM2 /= quantidadeBase;
                    volumeM3 /= quantidadeBase;
                    pesoKg /= quantidadeBase;
                }

                string familia = ObterNomeFamilia(elemento, tipo);
                string tipoPerfil = ObterNomeTipoPerfil(elemento, tipo);
                string categoriaExibicao = ObterNomeCategoria(elemento, categoriaLogica.Value);
                string marca = ObterMarca(elemento);
                string assinatura = MontarAssinaturaFabricacao(elemento, tipo, material, categoriaLogica.Value, comprimentoM);
                string detalheAgrupamento = MontarDetalheAgrupamento(elemento, tipo, categoriaLogica.Value, comprimentoM);
                ListaMateriaisSecaoPlanilha secao = ObterSecaoPlanilha(categoriaLogica.Value);
                MaterialBaseTipo materialBaseTipo = ClassificarMaterialBase(doc, material, categoriaLogica.Value);

                linhas.Add(new ListaMateriaisLinha(
                    elemento.Id,
                    secao,
                    materialBaseTipo,
                    categoriaExibicao,
                    familia,
                    tipoPerfil,
                    nomeMaterial,
                    marca,
                    comprimentoM,
                    areaM2,
                    volumeM3,
                    pesoKg,
                    origemPeso,
                    assinatura,
                    quantidadeBase,
                    detalheAgrupamento));
            }

            return linhas;
        }

        private static List<Element> ColetarElementos(Document doc, UIDocument uidoc, ExportarListaMateriaisConfig config)
        {
            if (config.Escopo == ListaMateriaisEscopo.SelecaoAtual)
            {
                return uidoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .Where(x => x != null)
                    .ToList();
            }

            // Guard: ActiveView pode ser null em contextos API-only. Fallback: modelo inteiro.
            FilteredElementCollector collector =
                (config.Escopo == ListaMateriaisEscopo.VistaAtiva && doc.ActiveView != null)
                    ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                    : new FilteredElementCollector(doc);

            List<ElementFilter> filtros = ObterFiltrosCategoria(config);
            if (filtros.Count > 0)
                collector.WherePasses(new LogicalOrFilter(filtros));

            return collector
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(EhElementoFisico)
                .ToList();
        }

        private static List<ElementFilter> ObterFiltrosCategoria(ExportarListaMateriaisConfig config)
        {
            List<BuiltInCategory> categorias = new List<BuiltInCategory>();

            if (config.IncluirVigas || config.IncluirContraventamentos)
                categorias.Add(BuiltInCategory.OST_StructuralFraming);

            if (config.IncluirPilares)
                categorias.Add(BuiltInCategory.OST_StructuralColumns);

            if (config.IncluirFundacoes)
                categorias.AddRange(CategoriasFundacoes);

            if (config.IncluirChapasConexoes)
            {
                categorias.AddRange(CategoriasPerfisConexao);
                categorias.AddRange(CategoriasConexoesDetalhadas);
            }

            return categorias
                .Distinct()
                .Select(x => new ElementCategoryFilter(x) as ElementFilter)
                .ToList();
        }

        private static bool EhElementoFisico(Element elemento)
        {
            return elemento != null &&
                   elemento.Category != null &&
                   !(elemento is View) &&
                   !(elemento is ElementType);
        }

        private static ListaMateriaisCategoriaLogica? ClassificarElemento(Element elemento)
        {
            if (elemento?.Category == null)
                return null;

            BuiltInCategory categoria = (BuiltInCategory)elemento.Category.Id.Value;

            if (categoria == BuiltInCategory.OST_StructuralColumns)
                return ListaMateriaisCategoriaLogica.Pilares;

            if (CategoriasFundacoes.Contains(categoria))
                return ListaMateriaisCategoriaLogica.Fundacoes;

            if (categoria == BuiltInCategory.OST_StructuralFraming)
            {
                if (elemento is FamilyInstance instancia && instancia.StructuralType == StructuralType.Brace)
                    return ListaMateriaisCategoriaLogica.Contraventamentos;

                return ListaMateriaisCategoriaLogica.Vigas;
            }

            if (CategoriasPerfisConexao.Contains(categoria))
                return ListaMateriaisCategoriaLogica.PerfisConexao;

            if (CategoriasConexoesDetalhadas.Contains(categoria))
                return ListaMateriaisCategoriaLogica.ChapasConexoes;

            return null;
        }

        private static bool CategoriaPermitida(ListaMateriaisCategoriaLogica categoria, ExportarListaMateriaisConfig config)
        {
            return categoria switch
            {
                ListaMateriaisCategoriaLogica.Vigas => config.IncluirVigas,
                ListaMateriaisCategoriaLogica.Pilares => config.IncluirPilares,
                ListaMateriaisCategoriaLogica.Fundacoes => config.IncluirFundacoes,
                ListaMateriaisCategoriaLogica.Contraventamentos => config.IncluirContraventamentos,
                ListaMateriaisCategoriaLogica.PerfisConexao => config.IncluirChapasConexoes,
                ListaMateriaisCategoriaLogica.ChapasConexoes => config.IncluirChapasConexoes,
                _ => false
            };
        }

        private static Material ObterMaterialPrincipal(Document doc, Element elemento, ElementType tipo)
        {
            // Overload sem cache (mantido por compatibilidade — uso interno e via cache)
            return ObterMaterialPrincipal(doc, elemento, tipo, null);
        }

        /// <summary>
        /// Sprint 2: variante com cache para evitar N+1 doc.GetElement em listas grandes.
        /// </summary>
        private static Material ObterMaterialPrincipal(
            Document doc,
            Element elemento,
            ElementType tipo,
            Dictionary<ElementId, Material> cache)
        {
            ElementId materialId = ObterMaterialEstruturalId(elemento) ??
                                   ObterMaterialEstruturalId(tipo) ??
                                   ObterPrimeiroMaterialGeometrico(elemento);

            if (materialId == null || materialId == ElementId.InvalidElementId)
                return null;

            if (cache != null && cache.TryGetValue(materialId, out Material cached))
                return cached;

            Material mat = doc.GetElement(materialId) as Material;
            if (cache != null)
                cache[materialId] = mat;
            return mat;
        }

        private static ElementId ObterMaterialEstruturalId(Element elemento)
        {
            if (elemento == null)
                return null;

            Parameter parametro = elemento.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (parametro != null && parametro.HasValue)
            {
                ElementId id = parametro.AsElementId();
                if (id != null && id != ElementId.InvalidElementId)
                    return id;
            }

            return null;
        }

        private static ElementId ObterPrimeiroMaterialGeometrico(Element elemento)
        {
            if (elemento == null)
                return null;

            try
            {
                ICollection<ElementId> ids = elemento.GetMaterialIds(false);
                return ids?.FirstOrDefault(x => x != ElementId.InvalidElementId);
            }
            catch
            {
                return null;
            }
        }

        private static string ObterMarca(Element elemento)
        {
            Parameter parametro = elemento?.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            string marca = parametro?.AsString();
            return string.IsNullOrWhiteSpace(marca) ? string.Empty : marca.Trim();
        }

        private static string ObterNomeFamilia(Element elemento, ElementType tipo)
        {
            if (elemento is FamilyInstance instancia &&
                instancia.Symbol != null &&
                !string.IsNullOrWhiteSpace(instancia.Symbol.FamilyName))
            {
                return instancia.Symbol.FamilyName;
            }

            if (tipo != null && !string.IsNullOrWhiteSpace(tipo.FamilyName))
                return tipo.FamilyName;

            return "<Sem família>";
        }

        private static string ObterNomeTipoPerfil(Element elemento, ElementType tipo)
        {
            if (tipo != null && !string.IsNullOrWhiteSpace(tipo.Name))
                return tipo.Name;

            return !string.IsNullOrWhiteSpace(elemento?.Name) ? elemento.Name : "<Sem tipo>";
        }

        private static string ObterNomeCategoria(Element elemento, ListaMateriaisCategoriaLogica categoria)
        {
            BuiltInCategory? categoriaNativa = elemento?.Category != null
                ? (BuiltInCategory)elemento.Category.Id.Value
                : null;

            if (categoriaNativa.HasValue)
            {
                return categoriaNativa.Value switch
                {
                    BuiltInCategory.OST_StructConnectionProfiles => "Perfil de Conexão",
                    BuiltInCategory.OST_StructConnectionPlates => "Chapa",
                    BuiltInCategory.OST_StructConnectionBolts => "Parafuso",
                    BuiltInCategory.OST_StructConnectionAnchors => "Chumbador",
                    BuiltInCategory.OST_StructConnectionShearStuds => "Stud",
                    BuiltInCategory.OST_StructConnectionWelds => "Solda",
                    BuiltInCategory.OST_StructConnectionHoles => "Furo",
                    BuiltInCategory.OST_StructConnectionModifiers => "Modificador",
                    BuiltInCategory.OST_StructConnectionOthers => "Conexão",
                    _ => categoria switch
                    {
                        ListaMateriaisCategoriaLogica.Vigas => "Viga",
                        ListaMateriaisCategoriaLogica.Pilares => "Pilar",
                        ListaMateriaisCategoriaLogica.Fundacoes => "Fundação",
                        ListaMateriaisCategoriaLogica.Contraventamentos => "Contraventamento",
                        ListaMateriaisCategoriaLogica.PerfisConexao => "Perfil de Conexão",
                        ListaMateriaisCategoriaLogica.ChapasConexoes => "Conexão",
                        _ => "Outro"
                    }
                };
            }

            return categoria switch
            {
                ListaMateriaisCategoriaLogica.Vigas => "Viga",
                ListaMateriaisCategoriaLogica.Pilares => "Pilar",
                ListaMateriaisCategoriaLogica.Fundacoes => "Fundação",
                ListaMateriaisCategoriaLogica.Contraventamentos => "Contraventamento",
                ListaMateriaisCategoriaLogica.PerfisConexao => "Perfil de Conexão",
                ListaMateriaisCategoriaLogica.ChapasConexoes => "Conexão",
                _ => "Outro"
            };
        }

        private static ElementType ObterTipoElemento(
            Document doc,
            Element elemento,
            Dictionary<ElementId, ElementType> tiposCache)
        {
            if (doc == null || elemento == null)
                return null;

            ElementId tipoId = elemento.GetTypeId();
            if (tipoId == null || tipoId == ElementId.InvalidElementId)
                return null;

            if (tiposCache.TryGetValue(tipoId, out ElementType tipo))
                return tipo;

            tipo = doc.GetElement(tipoId) as ElementType;
            tiposCache[tipoId] = tipo;
            return tipo;
        }

        private static ListaMateriaisSecaoPlanilha ObterSecaoPlanilha(ListaMateriaisCategoriaLogica categoria)
        {
            return categoria switch
            {
                ListaMateriaisCategoriaLogica.PerfisConexao => ListaMateriaisSecaoPlanilha.PerfisConexao,
                ListaMateriaisCategoriaLogica.ChapasConexoes => ListaMateriaisSecaoPlanilha.Conexoes,
                _ => ListaMateriaisSecaoPlanilha.ElementosEstruturais
            };
        }

        private static bool UsaPesoLinear(ListaMateriaisCategoriaLogica categoria)
        {
            return categoria == ListaMateriaisCategoriaLogica.Vigas ||
                   categoria == ListaMateriaisCategoriaLogica.Pilares ||
                   categoria == ListaMateriaisCategoriaLogica.Contraventamentos ||
                   categoria == ListaMateriaisCategoriaLogica.PerfisConexao;
        }

        private static int ObterQuantidadeBase(Element elemento, ListaMateriaisCategoriaLogica categoria)
        {
            if (elemento == null || categoria != ListaMateriaisCategoriaLogica.ChapasConexoes)
                return 1;

            return TryObterQuantidadeEspecialConexao(elemento, out int quantidade) && quantidade > 0
                ? quantidade
                : 1;
        }

        private static bool DeveRatearMedidasPorQuantidade(Element elemento)
        {
            if (elemento?.Category == null)
                return false;

            BuiltInCategory categoria = (BuiltInCategory)elemento.Category.Id.Value;
            return categoria == BuiltInCategory.OST_StructConnectionBolts ||
                   categoria == BuiltInCategory.OST_StructConnectionAnchors ||
                   categoria == BuiltInCategory.OST_StructConnectionShearStuds;
        }

        private static bool TryObterQuantidadeEspecialConexao(Element elemento, out int quantidade)
        {
            quantidade = 0;
            if (elemento?.Category == null)
                return false;

            BuiltInCategory categoria = (BuiltInCategory)elemento.Category.Id.Value;

            if (categoria == BuiltInCategory.OST_StructConnectionShearStuds)
            {
                int studs = ObterParametroInteiro(elemento, BuiltInParameter.STRUCTURAL_NUMBER_OF_STUDS) ?? 0;
                if (studs > 0)
                {
                    quantidade = studs;
                    return true;
                }
            }

            int numeroTotal = ObterParametroInteiro(elemento, BuiltInParameter.STEEL_ELEM_PATTERN_NUMBER) ?? 0;
            if (numeroTotal > 0)
            {
                quantidade = numeroTotal;
                return true;
            }

            int numeroX = ObterParametroInteiro(elemento, BuiltInParameter.STEEL_ELEM_PATTERN_NUMBER_X) ?? 0;
            int numeroY = ObterParametroInteiro(elemento, BuiltInParameter.STEEL_ELEM_PATTERN_NUMBER_Y) ?? 0;

            if (numeroX > 0 && numeroY > 0)
            {
                quantidade = numeroX * numeroY;
                return true;
            }

            if (numeroX > 0)
            {
                quantidade = numeroX;
                return true;
            }

            if (numeroY > 0)
            {
                quantidade = numeroY;
                return true;
            }

            foreach (Parameter parametro in elemento.Parameters.Cast<Parameter>())
            {
                if (parametro == null || !parametro.HasValue || parametro.Definition == null)
                    continue;

                string nome = NormalizarToken(parametro.Definition.Name);
                if (nome != "number" &&
                    nome != "count" &&
                    nome != "quantidade" &&
                    nome != "numero" &&
                    nome != "number of studs")
                {
                    continue;
                }

                if (parametro.StorageType == StorageType.Integer)
                {
                    int valor = parametro.AsInteger();
                    if (valor > 0)
                    {
                        quantidade = valor;
                        return true;
                    }
                }
            }

            return false;
        }

        private static MaterialBaseTipo ClassificarMaterialBase(
            Document doc,
            Material material,
            ListaMateriaisCategoriaLogica categoria)
        {
            if (categoria == ListaMateriaisCategoriaLogica.PerfisConexao ||
                categoria == ListaMateriaisCategoriaLogica.ChapasConexoes)
            {
                return MaterialBaseTipo.Metalico;
            }

            if (doc == null || material == null || material.StructuralAssetId == ElementId.InvalidElementId)
                return MaterialBaseTipo.Outro;

            try
            {
                PropertySetElement assetElement = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                Parameter classeParametro = assetElement?.get_Parameter(BuiltInParameter.PHY_MATERIAL_PARAM_CLASS);
                if (classeParametro == null || !classeParametro.HasValue)
                    return MaterialBaseTipo.Outro;

                StructuralAssetClass classe = (StructuralAssetClass)classeParametro.AsInteger();
                return classe switch
                {
                    StructuralAssetClass.Concrete => MaterialBaseTipo.Concreto,
                    StructuralAssetClass.Metal => MaterialBaseTipo.Metalico,
                    _ => MaterialBaseTipo.Outro
                };
            }
            catch
            {
                return MaterialBaseTipo.Outro;
            }
        }

        private static string MontarDetalheAgrupamento(
            Element elemento,
            ElementType tipo,
            ListaMateriaisCategoriaLogica categoria,
            double comprimentoM)
        {
            List<string> partes = new List<string>();

            if (categoria != ListaMateriaisCategoriaLogica.ChapasConexoes && comprimentoM > 0.0)
                partes.Add($"Corte={comprimentoM:0.###} m");

            AppendParametrosDimensaoDisplay(partes, "Tipo", tipo);
            AppendParametrosDimensaoDisplay(partes, "Instância", elemento);

            if (categoria == ListaMateriaisCategoriaLogica.ChapasConexoes)
                AppendBoundingBoxDisplay(partes, elemento);

            return partes.Count == 0 ? "-" : string.Join("; ", partes);
        }

        private static void AppendParametrosDimensaoDisplay(List<string> partes, string prefixo, Element elemento)
        {
            if (elemento == null)
                return;

            foreach (Parameter parametro in elemento.Parameters.Cast<Parameter>())
            {
                if (parametro == null || parametro.Definition == null || !ParametroParticipaDaChave(parametro.Definition.Name))
                    continue;

                string valor = ObterValorParametroParaDisplay(parametro);
                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                partes.Add($"{prefixo}: {parametro.Definition.Name}={valor}");
            }
        }

        private static string ObterValorParametroParaDisplay(Parameter parametro)
        {
            if (parametro == null || !parametro.HasValue)
                return string.Empty;

            string valorTexto = parametro.AsValueString();
            if (!string.IsNullOrWhiteSpace(valorTexto))
                return valorTexto.Trim();

            return parametro.StorageType switch
            {
                StorageType.Double => FormatarNumeroChave(parametro.AsDouble()),
                StorageType.Integer => parametro.AsInteger().ToString(CultureInfo.InvariantCulture),
                StorageType.String => parametro.AsString()?.Trim() ?? string.Empty,
                _ => string.Empty
            };
        }

        private static void AppendBoundingBoxDisplay(List<string> partes, Element elemento)
        {
            BoundingBoxXYZ box = elemento?.get_BoundingBox(null);
            if (box == null)
                return;

            double dx = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.X - box.Min.X), UnitTypeId.Meters);
            double dy = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.Y - box.Min.Y), UnitTypeId.Meters);
            double dz = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.Z - box.Min.Z), UnitTypeId.Meters);

            double[] medidas = { dx, dy, dz };
            Array.Sort(medidas);
            Array.Reverse(medidas);

            partes.Add($"Envelope={medidas[0]:0.###} x {medidas[1]:0.###} x {medidas[2]:0.###} m");
        }

        private static double ObterComprimentoCorteMetros(Element elemento)
        {
            if (elemento == null)
                return 0.0;

            double comprimentoInterno =
                ObterParametroDouble(elemento, BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) ??
                ObterParametroDouble(elemento, BuiltInParameter.INSTANCE_LENGTH_PARAM) ??
                0.0;

            if (comprimentoInterno <= 0.0)
            {
                Curve curva = RevitUtils.GetElementCurve(elemento);
                if (curva != null)
                    comprimentoInterno = curva.Length;
            }

            if (comprimentoInterno <= 0.0)
                return 0.0;

            return UnitUtils.ConvertFromInternalUnits(comprimentoInterno, UnitTypeId.Meters);
        }

        private static double ObterAreaMetrosQuadrados(Element elemento, Material material)
        {
            if (elemento == null || material == null)
                return 0.0;

            try
            {
                double areaInterna = elemento.GetMaterialArea(material.Id, false);
                if (areaInterna > 0.0)
                    return UnitUtils.ConvertFromInternalUnits(areaInterna, UnitTypeId.SquareMeters);
            }
            catch
            {
                // ignore and fallback below
            }

            return 0.0;
        }

        private static double ObterVolumeMetrosCubicos(Element elemento, Material material)
        {
            if (elemento == null)
                return 0.0;

            try
            {
                if (material != null)
                {
                    double volumeInternoMaterial = elemento.GetMaterialVolume(material.Id);
                    if (volumeInternoMaterial > 0.0)
                        return UnitUtils.ConvertFromInternalUnits(volumeInternoMaterial, UnitTypeId.CubicMeters);
                }
            }
            catch
            {
                // ignore and fallback below
            }

            double volumeInternoHost = ObterParametroDouble(elemento, BuiltInParameter.HOST_VOLUME_COMPUTED) ?? 0.0;
            return volumeInternoHost > 0.0
                ? UnitUtils.ConvertFromInternalUnits(volumeInternoHost, UnitTypeId.CubicMeters)
                : 0.0;
        }

        private static (double PesoKg, string OrigemPeso) ObterPesoInfo(
            Document doc,
            Element elemento,
            ElementType tipo,
            Material material,
            ListaMateriaisCategoriaLogica categoria,
            double comprimentoM,
            double volumeM3)
        {
            if (UsaPesoLinear(categoria) &&
                comprimentoM > 0.0 &&
                TryObterPesoLinearKgM(elemento, tipo, out double pesoLinearKgM))
            {
                return (pesoLinearKgM * comprimentoM, "kg/m");
            }

            double pesoPorDensidade = ObterPesoPorDensidadeKg(doc, material, volumeM3);
            if (pesoPorDensidade > 0.0)
                return (pesoPorDensidade, "densidade/volume");

            double pesoPadrao = ObterPesoPadraoKg(doc, material, categoria, volumeM3);
            if (pesoPadrao > 0.0)
                return (pesoPadrao, ObterOrigemPesoPadrao(doc, material, categoria));

            return (0.0, "sem base");
        }

        private static bool TryObterPesoLinearKgM(Element elemento, ElementType tipo, out double pesoLinearKgM)
        {
            pesoLinearKgM = 0.0;

            return TryObterPesoLinearKgM(elemento, out pesoLinearKgM) ||
                   TryObterPesoLinearKgM(tipo, out pesoLinearKgM);
        }

        private static bool TryObterPesoLinearKgM(Element origem, out double pesoLinearKgM)
        {
            pesoLinearKgM = 0.0;
            if (origem == null)
                return false;

            foreach (Parameter parametro in origem.Parameters.Cast<Parameter>())
            {
                if (!ParametroParecePesoLinear(parametro))
                    continue;

                if (TryConverterParametroParaKgM(parametro, out pesoLinearKgM) &&
                    pesoLinearKgM > 0.0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ParametroParecePesoLinear(Parameter parametro)
        {
            if (parametro == null || !parametro.HasValue || parametro.Definition == null)
                return false;

            string nome = NormalizarToken(parametro.Definition.Name);
            if (!string.IsNullOrWhiteSpace(nome) && TokensPesoLinear.Any(nome.Contains))
                return true;

            ForgeTypeId dataType = null;
            try
            {
                dataType = parametro.Definition.GetDataType();
            }
            catch
            {
                dataType = null;
            }

            if (dataType != null && dataType.Equals(SpecTypeId.MassPerUnitLength))
                return true;

            string valorTexto = parametro.StorageType == StorageType.String
                ? parametro.AsString()
                : parametro.AsValueString();

            string valorNormalizado = NormalizarToken(valorTexto);
            return !string.IsNullOrWhiteSpace(valorNormalizado) && TokensPesoLinear.Any(valorNormalizado.Contains);
        }

        private static bool TryConverterParametroParaKgM(Parameter parametro, out double pesoLinearKgM)
        {
            pesoLinearKgM = 0.0;
            if (parametro == null || !parametro.HasValue)
                return false;

            try
            {
                switch (parametro.StorageType)
                {
                    case StorageType.Double:
                    {
                        ForgeTypeId dataType = null;
                        try
                        {
                            dataType = parametro.Definition?.GetDataType();
                        }
                        catch
                        {
                            dataType = null;
                        }

                        if (dataType != null && dataType.Equals(SpecTypeId.MassPerUnitLength))
                        {
                            pesoLinearKgM = UnitUtils.ConvertFromInternalUnits(
                                parametro.AsDouble(),
                                UnitTypeId.KilogramsPerMeter);
                            return pesoLinearKgM > 0.0;
                        }

                        if (TryParsePesoLinearStringKgM(parametro.AsValueString(), out pesoLinearKgM))
                            return true;

                        if (TryParsePesoLinearStringKgM(parametro.AsString(), out pesoLinearKgM))
                            return true;

                        if (ParametroNomeIndicaKgPorMetro(parametro.Definition?.Name))
                        {
                            pesoLinearKgM = parametro.AsDouble();
                            return pesoLinearKgM > 0.0;
                        }

                        return false;
                    }
                    case StorageType.Integer:
                    {
                        if (!ParametroNomeIndicaKgPorMetro(parametro.Definition?.Name))
                            return false;

                        pesoLinearKgM = parametro.AsInteger();
                        return pesoLinearKgM > 0.0;
                    }
                    case StorageType.String:
                        return TryParsePesoLinearStringKgM(parametro.AsString(), out pesoLinearKgM);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool ParametroNomeIndicaKgPorMetro(string nomeParametro)
        {
            string nome = NormalizarToken(nomeParametro);
            return !string.IsNullOrWhiteSpace(nome) && TokensPesoLinear.Any(nome.Contains);
        }

        private static bool TryParsePesoLinearStringKgM(string texto, out double pesoLinearKgM)
        {
            pesoLinearKgM = 0.0;
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            Match match = NumeroTextoRegex.Match(texto);
            if (!match.Success)
                return false;

            string numero = match.Value;
            if (numero.Contains(",") && numero.Contains("."))
            {
                numero = numero.LastIndexOf(',') > numero.LastIndexOf('.')
                    ? numero.Replace(".", string.Empty).Replace(',', '.')
                    : numero.Replace(",", string.Empty);
            }
            else
            {
                numero = numero.Replace(',', '.');
            }

            return double.TryParse(numero, NumberStyles.Float, CultureInfo.InvariantCulture, out pesoLinearKgM) &&
                   pesoLinearKgM > 0.0;
        }

        private static double ObterPesoPorDensidadeKg(Document doc, Material material, double volumeM3)
        {
            if (doc == null || material == null || volumeM3 <= 0.0)
                return 0.0;

            try
            {
                if (material.StructuralAssetId == ElementId.InvalidElementId)
                    return 0.0;

                PropertySetElement assetElement = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                StructuralAsset asset = assetElement?.GetStructuralAsset();
                if (asset == null || asset.Density <= 0.0)
                    return 0.0;

                double densidadeKgM3 = UnitUtils.ConvertFromInternalUnits(asset.Density, UnitTypeId.KilogramsPerCubicMeter);
                return densidadeKgM3 > 0.0 ? densidadeKgM3 * volumeM3 : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private static double ObterPesoPadraoKg(
            Document doc,
            Material material,
            ListaMateriaisCategoriaLogica categoria,
            double volumeM3)
        {
            if (volumeM3 <= 0.0)
                return 0.0;

            MaterialBaseTipo materialBaseTipo = InferirMaterialBaseTipo(doc, material, categoria);
            return materialBaseTipo switch
            {
                MaterialBaseTipo.Concreto => DensidadePadraoConcretoKgM3 * volumeM3,
                MaterialBaseTipo.Metalico => DensidadePadraoAcoKgM3 * volumeM3,
                _ => 0.0
            };
        }

        private static string ObterOrigemPesoPadrao(
            Document doc,
            Material material,
            ListaMateriaisCategoriaLogica categoria)
        {
            MaterialBaseTipo materialBaseTipo = InferirMaterialBaseTipo(doc, material, categoria);
            return materialBaseTipo switch
            {
                MaterialBaseTipo.Concreto => $"massa específica padrão ({DensidadePadraoConcretoKgM3:0} kg/m³)",
                MaterialBaseTipo.Metalico => $"massa específica padrão ({DensidadePadraoAcoKgM3:0} kg/m³)",
                _ => "sem base"
            };
        }

        private static MaterialBaseTipo InferirMaterialBaseTipo(
            Document doc,
            Material material,
            ListaMateriaisCategoriaLogica categoria)
        {
            MaterialBaseTipo classificado = ClassificarMaterialBase(doc, material, categoria);
            if (classificado != MaterialBaseTipo.Outro)
                return classificado;

            string nomeMaterial = NormalizarToken(material?.Name);
            if (nomeMaterial.Contains("concreto") || nomeMaterial.Contains("concrete"))
                return MaterialBaseTipo.Concreto;

            if (nomeMaterial.Contains("aco") || nomeMaterial.Contains("aço") || nomeMaterial.Contains("steel"))
                return MaterialBaseTipo.Metalico;

            if (categoria == ListaMateriaisCategoriaLogica.Fundacoes)
                return MaterialBaseTipo.Concreto;

            return MaterialBaseTipo.Outro;
        }

        private static string MontarAssinaturaFabricacao(
            Element elemento,
            ElementType tipo,
            Material material,
            ListaMateriaisCategoriaLogica categoria,
            double comprimentoM)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("CAT=").Append(categoria).Append('|');
            sb.Append("TYPE=").Append(tipo?.Id?.Value ?? elemento?.GetTypeId().Value ?? -1).Append('|');
            sb.Append("MAT=").Append(material?.Id?.Value ?? -1).Append('|');

            if (categoria != ListaMateriaisCategoriaLogica.ChapasConexoes && comprimentoM > 0.0)
                sb.Append("CUT=").Append(FormatarNumeroChave(comprimentoM)).Append('|');

            AppendParametrosDimensao(sb, "T", tipo);
            AppendParametrosDimensao(sb, "I", elemento);

            if (categoria == ListaMateriaisCategoriaLogica.ChapasConexoes)
                AppendBoundingBox(sb, elemento);

            return sb.ToString();
        }

        private static void AppendParametrosDimensao(StringBuilder sb, string prefixo, Element elemento)
        {
            if (elemento == null)
                return;

            foreach (Parameter parametro in elemento.Parameters.Cast<Parameter>())
            {
                if (parametro == null || parametro.Definition == null || !ParametroParticipaDaChave(parametro.Definition.Name))
                    continue;

                string valor = ObterValorParametroParaChave(parametro);
                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                sb.Append(prefixo)
                  .Append(':')
                  .Append(NormalizarToken(parametro.Definition.Name))
                  .Append('=')
                  .Append(valor)
                  .Append('|');
            }
        }

        private static bool ParametroParticipaDaChave(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return false;

            string normalizado = NormalizarToken(nome);
            if (PalavrasIgnoradas.Any(normalizado.Contains))
                return false;

            return PalavrasDimensao.Any(normalizado.Contains);
        }

        private static string ObterValorParametroParaChave(Parameter parametro)
        {
            return parametro.StorageType switch
            {
                StorageType.Double => FormatarNumeroChave(parametro.AsDouble()),
                StorageType.Integer => parametro.AsInteger().ToString(CultureInfo.InvariantCulture),
                StorageType.String => NormalizarToken(parametro.AsString()),
                _ => string.Empty
            };
        }

        private static void AppendBoundingBox(StringBuilder sb, Element elemento)
        {
            BoundingBoxXYZ box = elemento?.get_BoundingBox(null);
            if (box == null)
                return;

            double dx = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.X - box.Min.X), UnitTypeId.Meters);
            double dy = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.Y - box.Min.Y), UnitTypeId.Meters);
            double dz = UnitUtils.ConvertFromInternalUnits(Math.Abs(box.Max.Z - box.Min.Z), UnitTypeId.Meters);

            double[] medidas = { dx, dy, dz };
            Array.Sort(medidas);
            Array.Reverse(medidas);

            sb.Append("BBOX=")
              .Append(FormatarNumeroChave(medidas[0])).Append('x')
              .Append(FormatarNumeroChave(medidas[1])).Append('x')
              .Append(FormatarNumeroChave(medidas[2])).Append('|');
        }

        private static string NormalizarToken(string valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? string.Empty
                : valor.Trim().ToLowerInvariant();
        }

        private static string FormatarNumeroChave(double valor)
        {
            return Math.Round(valor, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static double? ObterParametroDouble(Element elemento, BuiltInParameter parametro)
        {
            Parameter p = elemento?.get_Parameter(parametro);
            if (p == null || !p.HasValue || p.StorageType != StorageType.Double)
                return null;

            return p.AsDouble();
        }

        private static int? ObterParametroInteiro(Element elemento, BuiltInParameter parametro)
        {
            Parameter p = elemento?.get_Parameter(parametro);
            if (p == null || !p.HasValue || p.StorageType != StorageType.Integer)
                return null;

            return p.AsInteger();
        }

        private static List<ListaMateriaisGrupo> AgruparLinhas(IEnumerable<ListaMateriaisLinha> linhas)
        {
            return linhas
                .GroupBy(
                    x => new
                    {
                        x.ChaveAgrupamento,
                        x.OrigemPeso,
                        PesoKg = Math.Round(x.PesoKg, 6)
                    })
                .Select(grupo =>
                {
                    ListaMateriaisLinha primeiro = grupo.First();
                    return new ListaMateriaisGrupo(
                        primeiro.SecaoPlanilha,
                        primeiro.MaterialBaseTipo,
                        primeiro.Categoria,
                        primeiro.Familia,
                        primeiro.TipoPerfil,
                        primeiro.Material,
                        ConsolidarMarcas(grupo.Select(x => x.Marca)),
                        primeiro.ComprimentoCorteM,
                        primeiro.AreaM2,
                        primeiro.VolumeM3,
                        primeiro.PesoKg,
                        primeiro.OrigemPeso,
                        grupo.Sum(x => x.QuantidadeBase),
                        primeiro.DetalheAgrupamento);
                })
                .OrderBy(x => x.SecaoPlanilha)
                .ThenBy(x => x.Categoria, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Material, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.TipoPerfil, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string ConsolidarMarcas(IEnumerable<string> marcas)
        {
            List<string> distintas = marcas
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (distintas.Count == 0)
                return string.Empty;

            if (distintas.Count <= 5)
                return string.Join(", ", distintas);

            return string.Join(", ", distintas.Take(5)) + $" +{distintas.Count - 5}";
        }

        private static void SalvarWorkbook(
            string caminhoArquivo,
            List<ListaMateriaisGrupo> grupos,
            ExportarListaMateriaisConfig config,
            string documentTitle)
        {
            string diretorio = Path.GetDirectoryName(caminhoArquivo);
            if (!string.IsNullOrWhiteSpace(diretorio) && !Directory.Exists(diretorio))
                Directory.CreateDirectory(diretorio);

            string codigoDocumento = GerarCodigoDocumento(caminhoArquivo, documentTitle);
            using XLWorkbook workbook = CriarWorkbookLdm();

            CriarAbaCapa(workbook, documentTitle, codigoDocumento);

            if (config.ExportarResumo)
                CriarPlanilhaBase(workbook, grupos, documentTitle);
            else
                RemoverWorksheetSeExistir(workbook, NomeAbaPlanilhaBase);

            if (config.ExportarPerfisLineares)
            {
                List<ListaMateriaisGrupo> elementosEstruturais = grupos
                    .Where(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.ElementosEstruturais)
                    .ToList();
                CriarAbaDetalhe(workbook, "Elementos Estruturais", elementosEstruturais);
            }

            if (config.ExportarChapas)
            {
                List<ListaMateriaisGrupo> perfisConexao = grupos
                    .Where(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.PerfisConexao)
                    .ToList();
                CriarAbaDetalhe(workbook, "Perfis de Conexão", perfisConexao);

                List<ListaMateriaisGrupo> conexoes = grupos
                    .Where(x => x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.Conexoes)
                    .ToList();
                CriarAbaDetalhe(workbook, "Conexões", conexoes);
            }

            if (config.ExportarResumo)
                CriarAbaResumo(workbook, grupos);

            workbook.SaveAs(caminhoArquivo);
        }

        private static XLWorkbook CriarWorkbookLdm()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatos =
            {
                Path.Combine(baseDir, "Templates", NomeTemplateLdm),
                Path.Combine(baseDir, NomeTemplateLdm)
            };

            foreach (string candidato in candidatos)
            {
                if (File.Exists(candidato))
                    return new XLWorkbook(candidato);
            }

            return new XLWorkbook();
        }

        private static string GerarCodigoDocumento(string caminhoArquivo, string documentTitle)
        {
            string baseName = Path.GetFileNameWithoutExtension(caminhoArquivo);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = documentTitle;

            string sanitizado = Regex.Replace(baseName ?? "LISTA-MATERIAIS", @"\s+", "-")
                .Trim('-')
                .ToUpperInvariant();

            return string.IsNullOrWhiteSpace(sanitizado) ? "LISTA-MATERIAIS" : sanitizado;
        }

        private static IXLWorksheet ObterOuCriarWorksheet(XLWorkbook workbook, string nome)
        {
            return workbook.Worksheets.FirstOrDefault(x =>
                       string.Equals(x.Name, nome, StringComparison.OrdinalIgnoreCase)) ??
                   workbook.Worksheets.Add(nome);
        }

        private static void RemoverWorksheetSeExistir(XLWorkbook workbook, string nome)
        {
            IXLWorksheet worksheet = workbook.Worksheets.FirstOrDefault(x =>
                string.Equals(x.Name, nome, StringComparison.OrdinalIgnoreCase));

            worksheet?.Delete();
        }

        private static void CriarAbaCapa(XLWorkbook workbook, string documentTitle, string codigoDocumento)
        {
            IXLWorksheet ws = ObterOuCriarWorksheet(workbook, NomeAbaCapa);
            bool possuiModelo = ws.LastCellUsed() != null;

            if (!possuiModelo)
                ConfigurarLayoutCapaFallback(ws);

            ws.Range("A12:F17").Clear(XLClearOptions.Contents);
            ws.Range("A33:F45").Clear(XLClearOptions.Contents);

            string titulo = (documentTitle ?? "PROJETO").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(titulo))
                titulo = "PROJETO";

            ws.Cell("A12").Value = titulo;
            ws.Cell("A13").Value = "CIVIL - ESTRUTURAS DE CONCRETO E METÁLICO";
            ws.Cell("A14").Value = "LISTA DE MATERIAIS";
            ws.Cell("A15").Value = titulo;

            ws.Cell("A33").Value = "R00";
            ws.Cell("B33").Value = "EMISSÃO AUTOMÁTICA DA LISTA DE MATERIAIS";
            ws.Cell("D33").Value = DateTime.Today;
            ws.Cell("E33").Value = "FERRAMENTA EMT";
            ws.Cell("F33").Value = "-";

            ws.Cell("A35").Value = "REVISÃO";
            ws.Cell("B35").Value = "MODIFICAÇÃO";
            ws.Cell("D35").Value = "DATA";
            ws.Cell("E35").Value = "AUTOR";
            ws.Cell("F35").Value = "APROVAÇÃO";

            ws.Cell("A36").Value = "CLIENTE";
            ws.Cell("C36").Value = "EMPREENDIMENTO";
            ws.Cell("C37").Value = titulo;
            ws.Cell("C39").Value = "EXPORTAÇÃO AUTOMÁTICA";

            ws.Cell("A41").Value = "PROJETISTA";
            ws.Cell("C41").Value = "FASE DO PROJETO";
            ws.Cell("E41").Value = "RESPONSÁVEL PROJETISTA";
            ws.Cell("C42").Value = "PROJETO EXECUTIVO";
            ws.Cell("E42").Value = "FERRAMENTA EMT";
            ws.Cell("E43").Value = "EMISSÃO AUTOMÁTICA";
            ws.Cell("C44").Value = "CÓDIGO DO PROJETO";
            ws.Cell("C45").Value = codigoDocumento;
            ws.Cell("E44").Value = "REVISÃO";
            ws.Cell("E45").Value = "00";

            ws.Cell("D33").Style.DateFormat.Format = "dd/MM/yyyy";
        }

        private static void CriarAbaDetalhe(XLWorkbook workbook, string nome, List<ListaMateriaisGrupo> grupos)
        {
            IXLWorksheet ws = workbook.Worksheets.Add(nome);

            string[] headers =
            {
                "Marca(s)",
                "Categoria",
                "Familia",
                "Tipo/Perfil",
                "Material",
                "Comprimento de corte (m)",
                "Area (m2)",
                "Volume (m3)",
                "Quantidade",
                "Peso (kg)",
                "Origem do peso",
                "Detalhe do agrupamento"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            EstilizarCabecalho(ws, 1, headers.Length);

            if (grupos.Count == 0)
            {
                ws.Cell(2, 1).Value = "Nenhum item encontrado para esta aba.";
                ws.Columns().AdjustToContents();
                return;
            }

            int linha = 2;
            foreach (ListaMateriaisGrupo grupo in grupos)
            {
                ws.Cell(linha, 1).Value = grupo.Marcas;
                ws.Cell(linha, 2).Value = grupo.Categoria;
                ws.Cell(linha, 3).Value = grupo.Familia;
                ws.Cell(linha, 4).Value = grupo.TipoPerfil;
                ws.Cell(linha, 5).Value = grupo.Material;
                ws.Cell(linha, 6).Value = grupo.ComprimentoCorteM;
                ws.Cell(linha, 7).Value = grupo.AreaM2;
                ws.Cell(linha, 8).Value = grupo.VolumeM3;
                ws.Cell(linha, 9).Value = grupo.Quantidade;
                ws.Cell(linha, 10).Value = grupo.PesoKg;
                ws.Cell(linha, 11).Value = grupo.OrigemPeso;
                ws.Cell(linha, 12).Value = grupo.DetalheAgrupamento;
                linha++;
            }

            int ultimaLinha = linha - 1;
            int linhaTotais = linha + 1;

            ws.Range(1, 1, ultimaLinha, headers.Length).SetAutoFilter();
            ws.SheetView.FreezeRows(1);

            ws.Cell(linhaTotais, 1).Value = "Totais do grupo";
            ws.Cell(linhaTotais, 6).FormulaA1 = $"SUMPRODUCT(F2:F{ultimaLinha},I2:I{ultimaLinha})";
            ws.Cell(linhaTotais, 7).FormulaA1 = $"SUMPRODUCT(G2:G{ultimaLinha},I2:I{ultimaLinha})";
            ws.Cell(linhaTotais, 8).FormulaA1 = $"SUMPRODUCT(H2:H{ultimaLinha},I2:I{ultimaLinha})";
            ws.Cell(linhaTotais, 9).FormulaA1 = $"SUM(I2:I{ultimaLinha})";
            ws.Cell(linhaTotais, 10).FormulaA1 = $"SUMPRODUCT(J2:J{ultimaLinha},I2:I{ultimaLinha})";
            ws.Cell(linhaTotais, 11).Value = "-";
            ws.Cell(linhaTotais, 12).Value = "-";

            ws.Range(linhaTotais, 1, linhaTotais, headers.Length).Style.Font.Bold = true;
            ws.Range(1, 1, linhaTotais, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            FormatarColunasNumericas(ws, 2, ultimaLinha, linhaTotais);
            ws.Columns().AdjustToContents();
        }

        private static void CriarAbaResumo(XLWorkbook workbook, List<ListaMateriaisGrupo> grupos)
        {
            IXLWorksheet ws = workbook.Worksheets.Add("Resumo");
            int linha = 1;

            ws.Cell(linha, 1).Value = "Totais por categoria";
            ws.Cell(linha, 1).Style.Font.Bold = true;
            ws.Cell(linha, 1).Style.Font.FontSize = 14;
            linha += 2;

            string[] headersCategoria =
            {
                "Categoria",
                "Quantidade",
                "Comprimento total (m)",
                "Area total (m2)",
                "Volume total (m3)",
                "Peso total (kg)"
            };

            for (int i = 0; i < headersCategoria.Length; i++)
                ws.Cell(linha, i + 1).Value = headersCategoria[i];

            EstilizarCabecalho(ws, linha, headersCategoria.Length);

            int linhaCategoria = linha + 1;
            foreach (var grupoCategoria in grupos
                         .GroupBy(x => x.Categoria)
                         .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                ws.Cell(linhaCategoria, 1).Value = grupoCategoria.Key;
                ws.Cell(linhaCategoria, 2).Value = grupoCategoria.Sum(x => x.Quantidade);
                ws.Cell(linhaCategoria, 3).Value = grupoCategoria.Sum(x => x.ComprimentoTotalM);
                ws.Cell(linhaCategoria, 4).Value = grupoCategoria.Sum(x => x.AreaTotalM2);
                ws.Cell(linhaCategoria, 5).Value = grupoCategoria.Sum(x => x.VolumeTotalM3);
                ws.Cell(linhaCategoria, 6).Value = grupoCategoria.Sum(x => x.PesoTotalKg);
                linhaCategoria++;
            }

            linha = linhaCategoria + 2;
            ws.Cell(linha, 1).Value = "Totais por material e tipo/perfil";
            ws.Cell(linha, 1).Style.Font.Bold = true;
            ws.Cell(linha, 1).Style.Font.FontSize = 14;
            linha += 2;

            string[] headersMaterial =
            {
                "Aba",
                "Material",
                "Tipo/Perfil",
                "Categorias",
                "Quantidade",
                "Comprimento total (m)",
                "Area total (m2)",
                "Volume total (m3)",
                "Peso total (kg)"
            };

            for (int i = 0; i < headersMaterial.Length; i++)
                ws.Cell(linha, i + 1).Value = headersMaterial[i];

            EstilizarCabecalho(ws, linha, headersMaterial.Length);

            int linhaMaterial = linha + 1;
            foreach (var grupoMaterial in grupos
                         .GroupBy(x => new { x.SecaoPlanilha, x.Material, x.TipoPerfil })
                         .OrderBy(x => x.Key.SecaoPlanilha)
                         .ThenBy(x => x.Key.Material, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.Key.TipoPerfil, StringComparer.CurrentCultureIgnoreCase))
            {
                ws.Cell(linhaMaterial, 1).Value = ObterNomeSecaoPlanilha(grupoMaterial.Key.SecaoPlanilha);
                ws.Cell(linhaMaterial, 2).Value = grupoMaterial.Key.Material;
                ws.Cell(linhaMaterial, 3).Value = grupoMaterial.Key.TipoPerfil;
                ws.Cell(linhaMaterial, 4).Value = string.Join(", ", grupoMaterial
                    .Select(x => x.Categoria)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
                ws.Cell(linhaMaterial, 5).Value = grupoMaterial.Sum(x => x.Quantidade);
                ws.Cell(linhaMaterial, 6).Value = grupoMaterial.Sum(x => x.ComprimentoTotalM);
                ws.Cell(linhaMaterial, 7).Value = grupoMaterial.Sum(x => x.AreaTotalM2);
                ws.Cell(linhaMaterial, 8).Value = grupoMaterial.Sum(x => x.VolumeTotalM3);
                ws.Cell(linhaMaterial, 9).Value = grupoMaterial.Sum(x => x.PesoTotalKg);
                linhaMaterial++;
            }

            int ultimaLinha = Math.Max(linhaMaterial - 1, 1);
            ws.Range(1, 1, ultimaLinha, headersMaterial.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Columns().AdjustToContents();
        }

        private static void ConfigurarLayoutCapaFallback(IXLWorksheet ws)
        {
            ws.Range("A12:F12").Merge();
            ws.Range("A13:F13").Merge();
            ws.Range("A14:F14").Merge();
            ws.Range("A15:F15").Merge();
            ws.Range("C37:F37").Merge();
            ws.Range("C39:F39").Merge();
            ws.Range("C42:D42").Merge();
            ws.Range("C44:D44").Merge();
            ws.Range("C45:D45").Merge();
            ws.Range("E42:F42").Merge();
            ws.Range("E43:F43").Merge();
            ws.Range("E44:F44").Merge();
            ws.Range("E45:F45").Merge();

            ws.Range("A12:F15").Style.Font.FontName = "Arial";
            ws.Range("A12:F15").Style.Font.FontSize = 16;
            ws.Range("A12:F15").Style.Font.Bold = true;
            ws.Range("A12:F15").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("A12:F15").Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            ws.Range("A12:F15").Style.Border.RightBorder = XLBorderStyleValues.Thin;

            ws.Range("A33:F45").Style.Font.FontName = "Arial";
            ws.Range("A33:F45").Style.Font.FontSize = 10;
            ws.Range("A33:F45").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range("A33:F45").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range("A33:F45").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Column(1).Width = 12;
            ws.Column(2).Width = 24;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 16;
            ws.Column(6).Width = 16;
        }

        private static void PrepararPlanilhaBaseLdm(IXLWorksheet ws)
        {
            int ultimaLinha = Math.Max(ws.LastRowUsed()?.RowNumber() ?? 8, 8);
            ws.Range(1, 1, ultimaLinha, 4).Clear(XLClearOptions.Contents);

            ws.Range("A1:A2").Unmerge();
            ws.Range("A1:A2").Merge();
            ws.Range("B1:B2").Unmerge();
            ws.Range("B1:B2").Merge();
            ws.Range("C1:D1").Unmerge();
            ws.Range("C1:D1").Merge();
            ws.Range("A3:A4").Unmerge();
            ws.Range("A3:A4").Merge();
            ws.Range("B3:B4").Unmerge();
            ws.Range("B3:B4").Merge();
            ws.Range("C3:C4").Unmerge();
            ws.Range("C3:C4").Merge();
            ws.Range("D3:D4").Unmerge();
            ws.Range("D3:D4").Merge();

            ws.Column(1).Width = 10.43;
            ws.Column(2).Width = 99.14;
            ws.Column(3).Width = 13.29;
            ws.Column(4).Width = 16.14;

            ws.Row(3).Height = 15.75;
            ws.Row(4).Height = 15.75;

            EstilizarTopoPlanilhaBaseLdm(ws);
        }

        private static void EstilizarTopoPlanilhaBaseLdm(IXLWorksheet ws)
        {
            IXLRange obraLabel = ws.Range("A1:A2");
            obraLabel.Style.Font.FontName = "Arial";
            obraLabel.Style.Font.FontSize = 10;
            obraLabel.Style.Font.Bold = true;
            obraLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            obraLabel.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            obraLabel.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            obraLabel.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            obraLabel.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            obraLabel.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            IXLRange obraValor = ws.Range("B1:B2");
            obraValor.Style.Font.FontName = "Arial";
            obraValor.Style.Font.FontSize = 10;
            obraValor.Style.Font.Bold = true;
            obraValor.Style.Font.FontColor = XLColor.FromHtml("#000066");
            obraValor.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            obraValor.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            obraValor.Style.Alignment.WrapText = true;
            obraValor.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            obraValor.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            obraValor.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            IXLRange revisao = ws.Range("C1:D1");
            revisao.Style.Font.FontName = "Arial";
            revisao.Style.Font.FontSize = 10;
            revisao.Style.Font.Bold = true;
            revisao.Style.Font.FontColor = XLColor.Red;
            revisao.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            revisao.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            revisao.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            revisao.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            revisao.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            revisao.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            IXLCell dataLabel = ws.Cell("C2");
            dataLabel.Style.Font.FontName = "Arial";
            dataLabel.Style.Font.FontSize = 10;
            dataLabel.Style.Font.Bold = true;
            dataLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            dataLabel.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            dataLabel.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            dataLabel.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            IXLCell dataValor = ws.Cell("D2");
            dataValor.Style.Font.FontName = "Arial";
            dataValor.Style.Font.FontSize = 10;
            dataValor.Style.Font.Bold = true;
            dataValor.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dataValor.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            dataValor.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            dataValor.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            AplicarCabecalhoLdm(ws.Range("A3:A4"));
            AplicarCabecalhoLdm(ws.Range("B3:B4"), true);
            AplicarCabecalhoLdm(ws.Range("C3:C4"));
            AplicarCabecalhoLdm(ws.Range("D3:D4"));
            ws.Cell("D3").Style.NumberFormat.Format = "#,##0";
        }

        private static void AplicarCabecalhoLdm(IXLRange range, bool wrapText = false)
        {
            range.Style.Font.FontName = "Arial";
            range.Style.Font.FontSize = 10;
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(CorCabecalhoLdm);
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Alignment.WrapText = wrapText;
            range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            range.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            range.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        }

        private static void EscreverLinhaSecaoLdm(IXLWorksheet ws, int linha, string item, string descricao, string corHex)
        {
            ws.Cell(linha, 1).Value = item;
            ws.Cell(linha, 2).Value = descricao;
            ws.Cell(linha, 3).Value = string.Empty;
            ws.Cell(linha, 4).Value = string.Empty;

            AplicarEstiloLinhaLdm(ws, linha, corHex, true);
        }

        private static void EscreverLinhaItemLdm(IXLWorksheet ws, int linha, string item, ListaBaseLinha dados)
        {
            ws.Cell(linha, 1).Value = item;
            ws.Cell(linha, 2).Value = dados.Descricao;
            ws.Cell(linha, 3).Value = dados.Unidade;
            ws.Cell(linha, 4).Value = dados.Quantidade;

            AplicarEstiloLinhaLdm(ws, linha, null, false);
        }

        private static void AplicarEstiloLinhaLdm(IXLWorksheet ws, int linha, string corHex, bool negrito)
        {
            IXLRange range = ws.Range(linha, 1, linha, 4);
            range.Style.Font.FontName = "Arial";
            range.Style.Font.FontSize = 10;
            range.Style.Font.Bold = negrito;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Alignment.WrapText = true;
            range.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            range.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorder = XLBorderStyleValues.Hair;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            range.Style.Fill.BackgroundColor = string.IsNullOrWhiteSpace(corHex)
                ? XLColor.White
                : XLColor.FromHtml(corHex);

            ws.Row(linha).Height = 15.75;
            ws.Cell(linha, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(linha, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Cell(linha, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(linha, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(linha, 1).Style.NumberFormat.Format = "@";
            ws.Cell(linha, 2).Style.NumberFormat.Format = "@";
            ws.Cell(linha, 3).Style.NumberFormat.Format = "@";
            ws.Cell(linha, 4).Style.NumberFormat.Format = "#,##0.00";

            for (int coluna = 1; coluna <= 4; coluna++)
            {
                ws.Cell(linha, coluna).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                ws.Cell(linha, coluna).Style.Border.RightBorder = XLBorderStyleValues.Thin;
            }
        }

        private static void CriarPlanilhaBase(XLWorkbook workbook, List<ListaMateriaisGrupo> grupos, string documentTitle)
        {
            IXLWorksheet ws = ObterOuCriarWorksheet(workbook, NomeAbaPlanilhaBase);
            string tituloDocumento = (documentTitle ?? "MODELO").Trim().ToUpperInvariant();

            PrepararPlanilhaBaseLdm(ws);

            ws.Cell("A1").Value = "OBRA:";
            ws.Cell("B1").FormulaA1 = "CAPA!A12&\" - \"&CAPA!A15";
            ws.Cell("C1").FormulaA1 = "CAPA!E44&\" - \"&CAPA!E45";
            ws.Cell("C2").Value = "DATA:";
            ws.Cell("D2").Value = DateTime.Today;

            ws.Cell("A3").Value = "Item";
            ws.Cell("B3").Value = "Relação Orientativa de Materiais";
            ws.Cell("C3").Value = "Unidade";
            ws.Cell("D3").Value = "Quantidade";

            int linha = 6;

            List<ListaBaseLinha> concretos = CriarLinhasBaseConcreto(grupos);
            if (concretos.Count > 0)
            {
                EscreverLinhaSecaoLdm(ws, linha++, "1", "ESTRUTURA DE CONCRETO", CorSecaoPrimariaLdm);
                EscreverLinhaSecaoLdm(
                    ws,
                    linha++,
                    "1.1",
                    $"ESTRUTURA DE CONCRETO - {tituloDocumento}",
                    CorSecaoSecundariaLdm);

                int indice = 1;
                foreach (ListaBaseLinha item in concretos)
                    EscreverLinhaItemLdm(ws, linha++, $"1.1.{indice++}", item);

                linha++;
            }

            List<ListaBaseLinha> metalicos = CriarLinhasBaseMetalicas(grupos);
            if (metalicos.Count > 0)
            {
                EscreverLinhaSecaoLdm(ws, linha++, "2", "ESTRUTURA METÁLICA", CorSecaoPrimariaLdm);
                EscreverLinhaSecaoLdm(
                    ws,
                    linha++,
                    "2.1",
                    $"ESTRUTURA METÁLICA - {tituloDocumento}",
                    CorSecaoSecundariaLdm);

                int indice = 1;
                foreach (ListaBaseLinha item in metalicos)
                    EscreverLinhaItemLdm(ws, linha++, $"2.1.{indice++}", item);

                linha++;
            }

            List<ListaBaseSubsecao> fundacoes = CriarSubsecoesBaseFundacoes(grupos);
            if (fundacoes.Count > 0)
            {
                EscreverLinhaSecaoLdm(ws, linha++, "3", "FUNDAÇÃO", CorSecaoPrimariaLdm);

                int indiceSubsecao = 1;
                foreach (ListaBaseSubsecao subsecao in fundacoes)
                {
                    string codigoSubsecao = $"3.{indiceSubsecao++}";
                    EscreverLinhaSecaoLdm(ws, linha++, codigoSubsecao, subsecao.Titulo, CorSecaoSecundariaLdm);

                    int indiceItem = 1;
                    foreach (ListaBaseLinha item in subsecao.Itens)
                        EscreverLinhaItemLdm(ws, linha++, $"{codigoSubsecao}.{indiceItem++}", item);

                    linha++;
                }
            }

            ws.Cell("D2").Style.DateFormat.Format = "dd/MM/yyyy";
        }

        private static List<ListaBaseLinha> CriarLinhasBaseConcreto(List<ListaMateriaisGrupo> grupos)
        {
            return grupos
                .Where(x =>
                    x.MaterialBaseTipo == MaterialBaseTipo.Concreto &&
                    x.VolumeTotalM3 > 0.0 &&
                    !EhCategoriaFundacao(x.Categoria))
                .GroupBy(x => new { x.Categoria, x.Material })
                .OrderBy(x => x.Key.Categoria, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Key.Material, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new ListaBaseLinha(
                    $"{x.Key.Categoria} estrutural de concreto - {x.Key.Material}",
                    "m³",
                    x.Sum(y => y.VolumeTotalM3)))
                .ToList();
        }

        private static List<ListaBaseLinha> CriarLinhasBaseMetalicas(List<ListaMateriaisGrupo> grupos)
        {
            List<ListaBaseLinha> linhas = grupos
                .Where(x =>
                    x.MaterialBaseTipo == MaterialBaseTipo.Metalico &&
                    (x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.ElementosEstruturais ||
                     x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.PerfisConexao) &&
                    x.PesoTotalKg > 0.0)
                .GroupBy(x => new { x.Material, x.TipoPerfil })
                .OrderBy(x => x.Key.TipoPerfil, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Key.Material, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new ListaBaseLinha(
                    MontarDescricaoMetalicaBase(x.Key.TipoPerfil, x.Key.Material),
                    "kg",
                    x.Sum(y => y.PesoTotalKg)))
                .ToList();

            linhas.AddRange(
                grupos
                    .Where(x =>
                        x.MaterialBaseTipo == MaterialBaseTipo.Metalico &&
                        x.SecaoPlanilha == ListaMateriaisSecaoPlanilha.Conexoes &&
                        x.PesoTotalKg > 0.0)
                    .GroupBy(x => x.Material)
                    .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ListaBaseLinha(
                        $"CHAPAS E ACESSÓRIOS ({x.Key})",
                        "kg",
                        x.Sum(y => y.PesoTotalKg))));

            return linhas;
        }

        private static List<ListaBaseSubsecao> CriarSubsecoesBaseFundacoes(List<ListaMateriaisGrupo> grupos)
        {
            return grupos
                .Where(x => EhCategoriaFundacao(x.Categoria) && (x.PesoTotalKg > 0.0 || x.VolumeTotalM3 > 0.0))
                .GroupBy(x => x.MaterialBaseTipo)
                .OrderBy(x => x.Key)
                .Select(x => new ListaBaseSubsecao(
                    ObterTituloSubsecaoFundacao(x.Key),
                    x.GroupBy(y => new { y.TipoPerfil, y.Familia, y.Material, y.MaterialBaseTipo })
                     .OrderBy(y => y.Key.TipoPerfil, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(y => y.Key.Familia, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(y => y.Key.Material, StringComparer.CurrentCultureIgnoreCase)
                     .Select(y => new ListaBaseLinha(
                         MontarDescricaoFundacaoBase(y.Key.TipoPerfil, y.Key.Familia, y.Key.Material),
                         ObterUnidadeFundacaoBase(y),
                         ObterQuantidadeFundacaoBase(y)))
                     .ToList()))
                .Where(x => x.Itens.Count > 0)
                .ToList();
        }

        private static string ObterTituloSubsecaoFundacao(MaterialBaseTipo materialBaseTipo)
        {
            return materialBaseTipo switch
            {
                MaterialBaseTipo.Concreto => "FUNDAÇÕES - CONCRETO",
                MaterialBaseTipo.Metalico => "FUNDAÇÕES - AÇO",
                _ => "FUNDAÇÕES"
            };
        }

        private static string MontarDescricaoFundacaoBase(string tipoPerfil, string familia, string material)
        {
            string descricao = !string.IsNullOrWhiteSpace(tipoPerfil)
                ? tipoPerfil.Trim()
                : !string.IsNullOrWhiteSpace(familia)
                    ? familia.Trim()
                    : "Fundação";

            string materialTexto = material?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(materialTexto))
                return descricao;

            return descricao.IndexOf(materialTexto, StringComparison.CurrentCultureIgnoreCase) >= 0
                ? descricao
                : $"{descricao} - {materialTexto}";
        }

        private static string ObterUnidadeFundacaoBase(
            IGrouping<dynamic, ListaMateriaisGrupo> grupo)
        {
            return grupo.Sum(x => x.PesoTotalKg) > 0.0 ? "kg" : "m³";
        }

        private static double ObterQuantidadeFundacaoBase(
            IGrouping<dynamic, ListaMateriaisGrupo> grupo)
        {
            double pesoTotal = grupo.Sum(x => x.PesoTotalKg);
            if (pesoTotal > 0.0)
                return pesoTotal;

            return grupo.Sum(x => x.VolumeTotalM3);
        }

        private static bool EhCategoriaFundacao(string categoria)
        {
            return string.Equals(categoria, "Fundação", StringComparison.CurrentCultureIgnoreCase);
        }

        private static string MontarDescricaoMetalicaBase(string tipoPerfil, string material)
        {
            string tipo = tipoPerfil?.Trim() ?? string.Empty;
            string mat = material?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tipo))
                return mat;

            if (string.IsNullOrWhiteSpace(mat))
                return tipo;

            return tipo.IndexOf(mat, StringComparison.CurrentCultureIgnoreCase) >= 0
                ? tipo
                : $"{tipo} {mat}";
        }

        private static string ObterNomeSecaoPlanilha(ListaMateriaisSecaoPlanilha secaoPlanilha)
        {
            return secaoPlanilha switch
            {
                ListaMateriaisSecaoPlanilha.ElementosEstruturais => "Elementos Estruturais",
                ListaMateriaisSecaoPlanilha.PerfisConexao => "Perfis de Conexão",
                ListaMateriaisSecaoPlanilha.Conexoes => "Conexões",
                _ => "Outros"
            };
        }

        private static void EstilizarCabecalho(IXLWorksheet ws, int linha, int totalColunas)
        {
            IXLRange range = ws.Range(linha, 1, linha, totalColunas);
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        private static void FormatarColunasNumericas(IXLWorksheet ws, int primeiraLinhaDados, int ultimaLinhaDados, int linhaTotais)
        {
            if (ultimaLinhaDados < primeiraLinhaDados)
                return;

            ws.Range(primeiraLinhaDados, 6, linhaTotais, 8).Style.NumberFormat.Format = "0.000";
            ws.Range(primeiraLinhaDados, 9, linhaTotais, 9).Style.NumberFormat.Format = "0";
            ws.Range(primeiraLinhaDados, 10, linhaTotais, 10).Style.NumberFormat.Format = "0.00";
        }

        private enum ListaMateriaisCategoriaLogica
        {
            Vigas = 0,
            Pilares = 1,
            Fundacoes = 2,
            Contraventamentos = 3,
            PerfisConexao = 4,
            ChapasConexoes = 5
        }

        private enum ListaMateriaisSecaoPlanilha
        {
            ElementosEstruturais = 0,
            PerfisConexao = 1,
            Conexoes = 2
        }

        private enum MaterialBaseTipo
        {
            Outro = 0,
            Concreto = 1,
            Metalico = 2
        }

        private sealed class ListaMateriaisLinha
        {
            public ListaMateriaisLinha(
                ElementId elementId,
                ListaMateriaisSecaoPlanilha secaoPlanilha,
                MaterialBaseTipo materialBaseTipo,
                string categoria,
                string familia,
                string tipoPerfil,
                string material,
                string marca,
                double comprimentoCorteM,
                double areaM2,
                double volumeM3,
                double pesoKg,
                string origemPeso,
                string chaveAgrupamento,
                int quantidadeBase,
                string detalheAgrupamento)
            {
                ElementId = elementId;
                SecaoPlanilha = secaoPlanilha;
                MaterialBaseTipo = materialBaseTipo;
                Categoria = categoria;
                Familia = familia;
                TipoPerfil = tipoPerfil;
                Material = material;
                Marca = marca;
                ComprimentoCorteM = comprimentoCorteM;
                AreaM2 = areaM2;
                VolumeM3 = volumeM3;
                PesoKg = pesoKg;
                OrigemPeso = origemPeso;
                ChaveAgrupamento = chaveAgrupamento;
                QuantidadeBase = quantidadeBase;
                DetalheAgrupamento = detalheAgrupamento;
            }

            public ElementId ElementId { get; }
            public ListaMateriaisSecaoPlanilha SecaoPlanilha { get; }
            public MaterialBaseTipo MaterialBaseTipo { get; }
            public string Categoria { get; }
            public string Familia { get; }
            public string TipoPerfil { get; }
            public string Material { get; }
            public string Marca { get; }
            public double ComprimentoCorteM { get; }
            public double AreaM2 { get; }
            public double VolumeM3 { get; }
            public double PesoKg { get; }
            public string OrigemPeso { get; }
            public string ChaveAgrupamento { get; }
            public int QuantidadeBase { get; }
            public string DetalheAgrupamento { get; }
        }

        private sealed class ListaMateriaisGrupo
        {
            public ListaMateriaisGrupo(
                ListaMateriaisSecaoPlanilha secaoPlanilha,
                MaterialBaseTipo materialBaseTipo,
                string categoria,
                string familia,
                string tipoPerfil,
                string material,
                string marcas,
                double comprimentoCorteM,
                double areaM2,
                double volumeM3,
                double pesoKg,
                string origemPeso,
                int quantidade,
                string detalheAgrupamento)
            {
                SecaoPlanilha = secaoPlanilha;
                MaterialBaseTipo = materialBaseTipo;
                Categoria = categoria;
                Familia = familia;
                TipoPerfil = tipoPerfil;
                Material = material;
                Marcas = marcas;
                ComprimentoCorteM = comprimentoCorteM;
                AreaM2 = areaM2;
                VolumeM3 = volumeM3;
                PesoKg = pesoKg;
                OrigemPeso = origemPeso;
                Quantidade = quantidade;
                DetalheAgrupamento = detalheAgrupamento;
            }

            public ListaMateriaisSecaoPlanilha SecaoPlanilha { get; }
            public MaterialBaseTipo MaterialBaseTipo { get; }
            public string Categoria { get; }
            public string Familia { get; }
            public string TipoPerfil { get; }
            public string Material { get; }
            public string Marcas { get; }
            public double ComprimentoCorteM { get; }
            public double AreaM2 { get; }
            public double VolumeM3 { get; }
            public double PesoKg { get; }
            public string OrigemPeso { get; }
            public int Quantidade { get; }
            public string DetalheAgrupamento { get; }

            public double ComprimentoTotalM => ComprimentoCorteM * Quantidade;
            public double AreaTotalM2 => AreaM2 * Quantidade;
            public double VolumeTotalM3 => VolumeM3 * Quantidade;
            public double PesoTotalKg => PesoKg * Quantidade;
        }

        private sealed class ListaBaseLinha
        {
            public ListaBaseLinha(string descricao, string unidade, double quantidade)
            {
                Descricao = descricao;
                Unidade = unidade;
                Quantidade = quantidade;
            }

            public string Descricao { get; }
            public string Unidade { get; }
            public double Quantidade { get; }
        }

        private sealed class ListaBaseSubsecao
        {
            public ListaBaseSubsecao(string titulo, List<ListaBaseLinha> itens)
            {
                Titulo = titulo ?? string.Empty;
                Itens = itens ?? new List<ListaBaseLinha>();
            }

            public string Titulo { get; }
            public List<ListaBaseLinha> Itens { get; }
        }
    }
}
