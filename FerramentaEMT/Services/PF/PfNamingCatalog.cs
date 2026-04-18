using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Models.PF;

namespace FerramentaEMT.Services.PF
{
    internal static class PfNamingCatalog
    {
        private const string SemFamilia = "<Sem família>";
        private const string SemTipo = "<Sem tipo>";

        public static List<NumeracaoElementoInfo> ColetarCandidatos(
            UIDocument uidoc,
            NumeracaoEscopo escopo,
            PfNamingTarget alvo)
        {
            if (uidoc == null)
                return new List<NumeracaoElementoInfo>();

            Document doc = uidoc.Document;

            return EnumerarElementos(uidoc, escopo, alvo)
                .Where(x => EhElegivel(alvo, x))
                .Select(x => CriarInfo(doc, x))
                .Where(x => x != null)
                .GroupBy(x => x.Id.Value)
                .Select(x => x.First())
                .OrderBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Id.Value)
                .ToList();
        }

        public static List<NumeracaoElementoInfo> Filtrar(
            IEnumerable<NumeracaoElementoInfo> candidatos,
            PfNamingConfig config)
        {
            if (candidatos == null || config == null)
                return new List<NumeracaoElementoInfo>();

            return candidatos
                .Where(x => string.IsNullOrWhiteSpace(config.FamiliaNome) ||
                            string.Equals(x.FamiliaNome, config.FamiliaNome, StringComparison.CurrentCultureIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(config.TipoNome) ||
                            string.Equals(x.TipoNome, config.TipoNome, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Id.Value)
                .ToList();
        }

        public static string GetTargetDisplayName(PfNamingTarget alvo)
        {
            return alvo switch
            {
                PfNamingTarget.Pilares => "Pilares",
                PfNamingTarget.Vigas => "Vigas",
                PfNamingTarget.LajesPf => "Lajes / Pisos",
                _ => "Elementos PM"
            };
        }

        private static IEnumerable<Element> EnumerarElementos(UIDocument uidoc, NumeracaoEscopo escopo, PfNamingTarget alvo)
        {
            Document doc = uidoc.Document;

            bool somenteInstancias = alvo == PfNamingTarget.Pilares || alvo == PfNamingTarget.Vigas;

            return escopo switch
            {
                NumeracaoEscopo.ModeloInteiro => somenteInstancias
                    ? new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Cast<Element>()
                    : new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Cast<Element>(),
                NumeracaoEscopo.VistaAtiva => doc.ActiveView == null
                    ? Enumerable.Empty<Element>()
                    : somenteInstancias
                        ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                            .OfClass(typeof(FamilyInstance))
                            .WhereElementIsNotElementType()
                            .ToElements()
                            .Cast<Element>()
                        : new FilteredElementCollector(doc, doc.ActiveView.Id)
                            .WhereElementIsNotElementType()
                            .ToElements()
                            .Cast<Element>(),
                NumeracaoEscopo.SelecaoAtual => uidoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .Where(x => x != null),
                _ => Enumerable.Empty<Element>()
            };
        }

        private static bool EhElegivel(PfNamingTarget alvo, Element element)
        {
            return alvo switch
            {
                PfNamingTarget.Pilares => PfElementService.IsStructuralColumn(element),
                PfNamingTarget.Vigas => PfElementService.IsStructuralBeam(element),
                PfNamingTarget.LajesPf => PfElementService.IsPfLaje(element),
                _ => false
            };
        }

        private static NumeracaoElementoInfo CriarInfo(Document doc, Element instance)
        {
            if (instance?.Category == null)
                return null;

            ElementType tipo = doc.GetElement(instance.GetTypeId()) as ElementType;
            string familiaNome = ObterNomeFamilia(instance as FamilyInstance, tipo);
            string tipoNome = ObterNomeTipo(instance, tipo);

            return new NumeracaoElementoInfo(
                instance.Id,
                instance.Category.Id,
                instance.Category.Name ?? string.Empty,
                familiaNome,
                tipoNome);
        }

        private static string ObterNomeFamilia(FamilyInstance instancia, ElementType tipo)
        {
            if (instancia?.Symbol != null && !string.IsNullOrWhiteSpace(instancia.Symbol.FamilyName))
                return instancia.Symbol.FamilyName;

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
    }
}
