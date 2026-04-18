using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;

namespace FerramentaEMT.Services
{
    internal sealed class NumeracaoElementoInfo
    {
        public NumeracaoElementoInfo(
            ElementId id,
            ElementId categoriaId,
            string categoriaNome,
            string familiaNome,
            string tipoNome)
        {
            Id = id;
            CategoriaId = categoriaId;
            CategoriaNome = categoriaNome;
            FamiliaNome = familiaNome;
            TipoNome = tipoNome;
        }

        public ElementId Id { get; }
        public ElementId CategoriaId { get; }
        public string CategoriaNome { get; }
        public string FamiliaNome { get; }
        public string TipoNome { get; }
    }

    internal sealed class NumeracaoParametroInfo
    {
        public NumeracaoParametroInfo(string chave, string nome, StorageType storageType, bool isPreferencial)
        {
            Chave = chave;
            Nome = nome;
            StorageType = storageType;
            IsPreferencial = isPreferencial;
        }

        public string Chave { get; }
        public string Nome { get; }
        public StorageType StorageType { get; }
        public bool IsPreferencial { get; }
    }

    internal static class NumeracaoItensCatalog
    {
        private const string SemFamilia = "<Sem família>";
        private const string SemTipo = "<Sem tipo>";

        public static List<NumeracaoElementoInfo> ColetarCandidatos(UIDocument uidoc, NumeracaoEscopo escopo)
        {
            if (uidoc is null)
                return new List<NumeracaoElementoInfo>();

            Document doc = uidoc.Document;
            IEnumerable<Element> elementos = escopo switch
            {
                NumeracaoEscopo.ModeloInteiro => new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Cast<Element>(),
                NumeracaoEscopo.VistaAtiva => doc.ActiveView == null
                    ? Enumerable.Empty<Element>()
                    : new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Cast<Element>(),
                NumeracaoEscopo.SelecaoAtual => uidoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .Where(x => x != null)
                    .Cast<Element>(),
                _ => Enumerable.Empty<Element>()
            };

            return elementos
                .Where(EhElementoElegivel)
                .Select(x => CriarInfo(doc, x))
                .Where(x => x != null)
                .GroupBy(x => x.Id.Value)
                .Select(x => x.First())
                .OrderBy(x => x.CategoriaNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static List<NumeracaoElementoInfo> Filtrar(
            IEnumerable<NumeracaoElementoInfo> candidatos,
            NumeracaoItensConfig config)
        {
            if (candidatos is null || config is null)
                return new List<NumeracaoElementoInfo>();

            return candidatos
                .Where(x => x.CategoriaId.Value == config.CategoriaIdValor)
                .Where(x => string.IsNullOrWhiteSpace(config.FamiliaNome) ||
                            string.Equals(x.FamiliaNome, config.FamiliaNome, StringComparison.CurrentCultureIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(config.TipoNome) ||
                            string.Equals(x.TipoNome, config.TipoNome, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Id.Value)
                .ToList();
        }

        public static List<NumeracaoParametroInfo> ColetarParametrosGravaveisEmComum(
            Document doc,
            IEnumerable<NumeracaoElementoInfo> elementosInfo)
        {
            if (doc is null || elementosInfo is null)
                return new List<NumeracaoParametroInfo>();

            Dictionary<string, NumeracaoParametroInfo> intersecao = null;

            foreach (NumeracaoElementoInfo info in elementosInfo)
            {
                Element elemento = doc.GetElement(info.Id);
                if (elemento == null)
                    continue;

                Dictionary<string, NumeracaoParametroInfo> parametrosDoElemento = EnumerarParametrosNumeraveis(elemento)
                    .GroupBy(x => x.Chave)
                    .Select(x => x.First())
                    .ToDictionary(x => x.Chave, x => x, StringComparer.OrdinalIgnoreCase);

                if (intersecao == null)
                {
                    intersecao = parametrosDoElemento;
                }
                else
                {
                    intersecao = intersecao
                        .Where(x => parametrosDoElemento.TryGetValue(x.Key, out NumeracaoParametroInfo atual) &&
                                    atual.StorageType == x.Value.StorageType)
                        .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                }

                if (intersecao.Count == 0)
                    break;
            }

            return intersecao == null
                ? new List<NumeracaoParametroInfo>()
                : intersecao.Values
                    .OrderByDescending(x => x.IsPreferencial)
                    .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.StorageType.ToString(), StringComparer.Ordinal)
                    .ToList();
        }

        public static Parameter EncontrarParametro(Element elemento, string chave, StorageType storageType)
        {
            if (elemento == null || string.IsNullOrWhiteSpace(chave))
                return null;

            foreach (Parameter parametro in elemento.Parameters.Cast<Parameter>())
            {
                if (parametro == null ||
                    parametro.StorageType != storageType ||
                    string.IsNullOrWhiteSpace(ObterChaveParametro(parametro)))
                {
                    continue;
                }

                if (string.Equals(ObterChaveParametro(parametro), chave, StringComparison.OrdinalIgnoreCase))
                    return parametro;
            }

            return null;
        }

        private static bool EhElementoElegivel(Element elemento)
        {
            if (elemento == null || elemento.Category == null)
                return false;

            if (elemento is ElementType || elemento is View)
                return false;

            if (string.IsNullOrWhiteSpace(elemento.Category.Name))
                return false;

            return true;
        }

        private static NumeracaoElementoInfo CriarInfo(Document doc, Element elemento)
        {
            if (elemento?.Category == null)
                return null;

            ElementType tipo = doc.GetElement(elemento.GetTypeId()) as ElementType;
            string familiaNome = ObterNomeFamilia(elemento, tipo);
            string tipoNome = ObterNomeTipo(elemento, tipo);

            return new NumeracaoElementoInfo(
                elemento.Id,
                elemento.Category.Id,
                elemento.Category.Name ?? string.Empty,
                familiaNome,
                tipoNome);
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

            return SemFamilia;
        }

        private static string ObterNomeTipo(Element elemento, ElementType tipo)
        {
            if (tipo != null && !string.IsNullOrWhiteSpace(tipo.Name))
                return tipo.Name;

            if (!string.IsNullOrWhiteSpace(elemento?.Name))
                return elemento.Name;

            return SemTipo;
        }

        private static IEnumerable<NumeracaoParametroInfo> EnumerarParametrosNumeraveis(Element elemento)
        {
            foreach (Parameter parametro in elemento.Parameters.Cast<Parameter>())
            {
                if (parametro == null || parametro.Definition == null || parametro.IsReadOnly)
                    continue;

                if (parametro.StorageType != StorageType.String &&
                    parametro.StorageType != StorageType.Integer)
                {
                    continue;
                }

                string nome = parametro.Definition.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nome))
                    continue;

                string chave = ObterChaveParametro(parametro);
                if (string.IsNullOrWhiteSpace(chave))
                    continue;

                bool isPreferencial =
                    parametro.Id.Value == (long)BuiltInParameter.ALL_MODEL_MARK ||
                    nome.Equals("Marca", StringComparison.CurrentCultureIgnoreCase) ||
                    nome.Equals("Mark", StringComparison.OrdinalIgnoreCase);

                yield return new NumeracaoParametroInfo(chave, nome, parametro.StorageType, isPreferencial);
            }
        }

        private static string ObterChaveParametro(Parameter parametro)
        {
            return parametro?.Id == null
                ? string.Empty
                : parametro.Id.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
