#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck.ModelCheckRules
{
    /// <summary>
    /// Detecta marcas duplicadas — mesma marca atribuida a elementos de tipo DIFERENTE.
    /// Obs: a mesma marca em elementos identicos e permitido (e esperado).
    /// </summary>
    public class DuplicateMarkRule : IModelCheckRule
    {
        public string Name => "Marca Duplicada";
        public string Description => "Localiza marcas de fabricacao atribuidas a elementos de tipos diferentes (verdadeiras duplicatas).";

        public IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId>? scopeIds)
        {
            var issues = new List<ModelCheckIssue>();
            if (doc == null)
                return issues;

            FilteredElementCollector collector = (scopeIds != null && scopeIds.Count > 0)
                ? new FilteredElementCollector(doc, scopeIds)
                : new FilteredElementCollector(doc);
            collector = collector
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming);

            // Agrupar por marca
            var marcas = new Dictionary<string, List<FamilyInstance>>(StringComparer.OrdinalIgnoreCase);

            int skippedOnError = 0;
            foreach (FamilyInstance elem in collector)
            {
                if (elem == null || elem.Symbol == null)
                    continue;

                try
                {
                    Parameter? markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    string? mark = markParam?.AsString();

                    if (string.IsNullOrWhiteSpace(mark))
                        continue;

                    if (!marcas.ContainsKey(mark))
                        marcas[mark] = new List<FamilyInstance>();

                    marcas[mark].Add(elem);
                }
                catch (Exception)
                {
                    skippedOnError++;
                }
            }

            if (skippedOnError > 0)
                Logger.Warn("[{Rule}] {Count} elemento(s) pulado(s) por erro na leitura de parametros.", Name, skippedOnError);

            // Verificar cada marca para tipos diferentes
            foreach (var marcaEntry in marcas)
            {
                string marca = marcaEntry.Key;
                var elementos = marcaEntry.Value;

                if (elementos.Count <= 1)
                    continue;

                // Agrupar por tipo (Symbol)
                var tiposUnicos = elementos
                    .GroupBy(e => e.Symbol?.Id.Value ?? -1)
                    .ToList();

                // Se ha mais de 1 tipo, e uma duplicata real
                if (tiposUnicos.Count > 1)
                {
                    foreach (var tipoGroup in tiposUnicos)
                    {
                        foreach (var elem in tipoGroup)
                        {
                            issues.Add(new ModelCheckIssue
                            {
                                RuleName = Name,
                                Severity = ModelCheckSeverity.Error,
                                ElementId = elem.Id.Value,
                                Description = $"Marca '{marca}' atribuida a multiplos TIPOS diferentes. " +
                                            $"Este elemento e do tipo '{elem.Symbol?.Name}', " +
                                            $"mas a mesma marca existe em outro(s) tipo(s).",
                                Suggestion = "Revise e corrija as marcas para garantir que cada marca seja unica por tipo."
                            });
                        }
                    }
                }
            }

            return issues;
        }
    }
}
