#nullable enable
// Origem: projeto Victor (incorporado e adaptado na Onda 3, Miniciclo 3).
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Services.ModelCheck
{
    /// <summary>
    /// Servico de visualizacao para resultados do ModelCheck.
    /// Gerencia a criacao/reutilizacao de uma vista 3D dedicada ("EMT - Verificacao do Modelo"),
    /// isolamento temporario de elementos com problemas e navegacao para folhas/vistas donas.
    /// </summary>
    internal sealed class ModelCheckVisualizationService
    {
        private const string ViewNamePrefix = "EMT - Verificação do Modelo";

        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private View3D? _resultsView;

        /// <summary>Descricao da ultima acao de navegacao realizada.</summary>
        public string LastNavigationDescription { get; private set; } = string.Empty;

        /// <summary>
        /// Inicializa o servico de visualizacao para o documento ativo.
        /// </summary>
        /// <param name="uidoc">Documento ativo do Revit (UIDocument).</param>
        public ModelCheckVisualizationService(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
        }

        /// <summary>
        /// Abre (ou cria) a vista 3D de resultados e isola temporariamente os elementos indicados.
        /// Se todos os elementos forem view-specific de uma unica vista, abre essa vista diretamente.
        /// </summary>
        /// <param name="rawIds">IDs dos elementos a exibir.</param>
        /// <returns><c>true</c> se a navegacao foi realizada; <c>false</c> se nenhum ID valido.</returns>
        public bool OpenResultsView(IEnumerable<long> rawIds)
        {
            List<ElementId> elementIds = NormalizeElementIds(rawIds);
            if (elementIds.Count == 0)
                return false;

            if (TryOpenOwnerViewForViewSpecificElements(elementIds))
                return true;

            if (TodosOsElementosSaoViewSpecific(elementIds))
            {
                _uidoc.Selection.SetElementIds(elementIds);
                LastNavigationDescription =
                    "Os itens selecionados pertencem a múltiplas vistas ou folhas. A seleção foi atualizada, mas não existe uma única vista para enquadrar todos ao mesmo tempo.";
                return true;
            }

            View3D resultsView = GetOrCreateResultsView();
            _resultsView = resultsView;

            _uidoc.ActiveView = resultsView;
            TryClearTemporaryIsolation(resultsView);

            try
            {
                resultsView.IsolateElementsTemporary(elementIds);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao isolar elementos temporariamente na vista de resultados");
            }

            _uidoc.Selection.SetElementIds(elementIds);
            _uidoc.ShowElements(elementIds);
            LastNavigationDescription = "Vista 3D aberta com os elementos do relatório isolados temporariamente.";
            return true;
        }

        /// <summary>
        /// Seleciona e enquadra os elementos na vista de resultados ja aberta.
        /// Se os elementos forem view-specific de uma unica vista, abre essa vista.
        /// </summary>
        /// <param name="rawIds">IDs dos elementos a focar.</param>
        /// <returns><c>true</c> se a navegacao foi realizada; <c>false</c> se nenhum ID valido.</returns>
        public bool FocusElements(IEnumerable<long> rawIds)
        {
            List<ElementId> elementIds = NormalizeElementIds(rawIds);
            if (elementIds.Count == 0)
                return false;

            if (TryOpenOwnerViewForViewSpecificElements(elementIds))
                return true;

            if (TodosOsElementosSaoViewSpecific(elementIds))
            {
                _uidoc.Selection.SetElementIds(elementIds);
                LastNavigationDescription =
                    "Os itens pertencem a vistas diferentes. A seleção foi atualizada, mas o enquadramento automático foi mantido apenas na vista atual.";
                return true;
            }

            if (_resultsView != null && _uidoc.ActiveView.Id != _resultsView.Id)
                _uidoc.ActiveView = _resultsView;

            _uidoc.Selection.SetElementIds(elementIds);
            _uidoc.ShowElements(elementIds);
            LastNavigationDescription = "Elementos selecionados e enquadrados na vista ativa.";
            return true;
        }

        /// <summary>
        /// Navega para a folha que contem os elementos indicados (carimbos, viewports, etc.).
        /// </summary>
        /// <param name="rawIds">IDs dos elementos de folha a focar.</param>
        /// <returns><c>true</c> se a navegacao foi realizada; <c>false</c> se nenhum ID valido.</returns>
        public bool FocusSheetElements(IEnumerable<long> rawIds)
        {
            List<ElementId> elementIds = NormalizeElementIds(rawIds);
            if (elementIds.Count == 0)
                return false;

            if (TryOpenSheetElement(elementIds))
                return true;

            if (TryOpenOwnerViewForViewSpecificElements(elementIds))
                return true;

            _uidoc.Selection.SetElementIds(elementIds);
            LastNavigationDescription =
                "A seleção foi atualizada, mas não foi encontrada uma folha única para abrir automaticamente.";
            return true;
        }

        private List<ElementId> NormalizeElementIds(IEnumerable<long> rawIds)
        {
            if (rawIds == null)
                return new List<ElementId>();

            return rawIds
                .Where(id => id > 0)
                .Distinct()
                .Select(id => new ElementId(id))
                .Where(id => _doc.GetElement(id) != null)
                .ToList();
        }

        private View3D GetOrCreateResultsView()
        {
            View3D? existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(view =>
                    !view.IsTemplate &&
                    view.Name.StartsWith(ViewNamePrefix, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            ViewFamilyType? threeDimensionalType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(viewType => viewType.ViewFamily == ViewFamily.ThreeDimensional);

            if (threeDimensionalType == null)
                throw new InvalidOperationException("Não foi encontrado um tipo de vista 3D disponível no projeto.");

            using (Transaction transaction = new Transaction(_doc, "Criar vista 3D da verificação"))
            {
                transaction.Start();

                View3D created = View3D.CreateIsometric(_doc, threeDimensionalType.Id);
                created.Name = BuildAvailableViewName();

                transaction.Commit();
                return created;
            }
        }

        private string BuildAvailableViewName()
        {
            HashSet<string> existingNames = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(view => !view.IsTemplate)
                .Select(view => view.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(ViewNamePrefix))
                return ViewNamePrefix;

            for (int index = 2; index <= 99; index++)
            {
                string candidate = $"{ViewNamePrefix} {index:00}";
                if (!existingNames.Contains(candidate))
                    return candidate;
            }

            return $"{ViewNamePrefix} {DateTime.Now:HHmmss}";
        }

        private static void TryClearTemporaryIsolation(View view)
        {
            if (view == null)
                return;

            try
            {
                if (view.IsTemporaryHideIsolateActive())
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao limpar isolamento temporario da vista de resultados");
            }
        }

        private bool TryOpenOwnerViewForViewSpecificElements(List<ElementId> elementIds)
        {
            if (!TryGetOwnerViewCompartilhada(elementIds, out View? ownerView) || ownerView is null)
                return false;

            _uidoc.ActiveView = ownerView;
            _uidoc.Selection.SetElementIds(elementIds);

            try
            {
                _uidoc.ShowElements(elementIds);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao enquadrar elementos na vista dona");
            }

            LastNavigationDescription = $"Vista '{ownerView.Name}' aberta para revisar o item selecionado.";
            return true;
        }

        private bool TryOpenSheetElement(List<ElementId> elementIds)
        {
            List<ViewSheet> sheets = elementIds
                .Select(id => _doc.GetElement(id))
                .OfType<ViewSheet>()
                .Distinct()
                .ToList();

            if (sheets.Count == 0)
                return false;

            ViewSheet sheetToOpen = sheets[0];
            _uidoc.ActiveView = sheetToOpen;

            List<ElementId> selectableIds = elementIds
                .Where(id => _doc.GetElement(id) is not ViewSheet)
                .ToList();

            if (selectableIds.Count > 0)
                _uidoc.Selection.SetElementIds(selectableIds);

            LastNavigationDescription = sheets.Count == 1
                ? $"Folha '{sheetToOpen.SheetNumber} - {sheetToOpen.Name}' aberta para revisão."
                : $"Folha '{sheetToOpen.SheetNumber} - {sheetToOpen.Name}' aberta. Há problemas em {sheets.Count} folhas no item selecionado.";

            return true;
        }

        private bool TryGetOwnerViewCompartilhada(List<ElementId> elementIds, out View? ownerView)
        {
            ownerView = null;
            if (elementIds.Count == 0)
                return false;

            List<Element> elements = elementIds
                .Select(id => _doc.GetElement(id))
                .Where(element => element != null)
                .ToList();

            if (elements.Count != elementIds.Count)
                return false;

            if (elements.Any(element => element.OwnerViewId == ElementId.InvalidElementId))
                return false;

            List<ElementId> ownerViewIds = elements
                .Select(element => element.OwnerViewId)
                .Distinct()
                .ToList();

            if (ownerViewIds.Count != 1)
                return false;

            ownerView = _doc.GetElement(ownerViewIds[0]) as View;
            return ownerView is not null;
        }

        private bool TodosOsElementosSaoViewSpecific(List<ElementId> elementIds)
        {
            if (elementIds.Count == 0)
                return false;

            return elementIds
                .Select(id => _doc.GetElement(id))
                .Where(element => element != null)
                .All(element => element.OwnerViewId != ElementId.InvalidElementId);
        }
    }
}
