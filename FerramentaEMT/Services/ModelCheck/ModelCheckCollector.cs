#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace FerramentaEMT.Services.ModelCheck
{
    /// <summary>
    /// Utilitario estatico para coleta centralizada de elementos estruturais
    /// e validacao de material estrutural.
    /// Origem: projeto Victor (incorporado e adaptado na Onda 3, Miniciclo 1).
    /// </summary>
    internal static class ModelCheckCollector
    {
        private static readonly BuiltInCategory[] DefaultStructuralCategories =
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFoundation
        };

        /// <summary>
        /// Coleta instancias de familia (FamilyInstance) das categorias estruturais informadas.
        /// Se <paramref name="categories"/> estiver vazio, usa as categorias padrao
        /// (Framing, Columns, Foundation).
        /// Quando <paramref name="scopeIds"/> nao for nulo, filtra apenas os elementos
        /// cujo Id esteja na lista de escopo.
        /// Garante deduplicacao por ElementId.
        /// </summary>
        /// <param name="doc">Documento Revit ativo.</param>
        /// <param name="scopeIds">
        /// Lista opcional de IDs para restringir o escopo da coleta.
        /// Se nulo ou vazio, coleta todos os elementos das categorias.
        /// </param>
        /// <param name="categories">
        /// Categorias estruturais a coletar. Se nenhuma for informada, usa as categorias padrao.
        /// </param>
        /// <returns>Sequencia de FamilyInstance sem duplicatas.</returns>
        internal static IEnumerable<FamilyInstance> CollectStructuralInstances(
            Document doc,
            IList<ElementId>? scopeIds,
            params BuiltInCategory[] categories)
        {
            if (doc == null)
                yield break;

            BuiltInCategory[] targetCategories =
                categories != null && categories.Length > 0
                    ? categories
                    : DefaultStructuralCategories;

            // Converte para HashSet para filtragem O(1) quando ha escopo definido.
            HashSet<ElementId>? scopeSet = scopeIds != null && scopeIds.Count > 0
                ? new HashSet<ElementId>(scopeIds)
                : null;

            HashSet<long> emittedIds = new HashSet<long>();

            foreach (BuiltInCategory category in targetCategories)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(category);

                foreach (FamilyInstance element in collector.Cast<FamilyInstance>())
                {
                    if (element == null)
                        continue;

                    // Filtra pelo escopo se definido.
                    if (scopeSet != null && !scopeSet.Contains(element.Id))
                        continue;

                    if (emittedIds.Add(element.Id.Value))
                        yield return element;
                }
            }
        }

        /// <summary>
        /// Verifica se o elemento possui material estrutural valido atribuido
        /// (parametro STRUCTURAL_MATERIAL_PARAM).
        /// Trata tanto armazenamento por ElementId quanto por String.
        /// </summary>
        /// <param name="element">Elemento Revit a verificar.</param>
        /// <returns>True se o material estrutural estiver preenchido e valido.</returns>
        internal static bool HasValidStructuralMaterial(Element element)
        {
            if (element == null)
                return false;

            Parameter materialParameter = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (materialParameter == null || !materialParameter.HasValue)
                return false;

            if (materialParameter.StorageType == StorageType.ElementId)
            {
                ElementId materialId = materialParameter.AsElementId();
                return materialId != null && materialId != ElementId.InvalidElementId;
            }

            if (materialParameter.StorageType == StorageType.String)
                return !string.IsNullOrWhiteSpace(materialParameter.AsString());

            return true;
        }
    }
}
