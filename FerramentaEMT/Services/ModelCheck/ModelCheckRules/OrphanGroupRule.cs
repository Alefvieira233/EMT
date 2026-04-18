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
    /// Verifica grupos orfaos (grupos com 0 elementos).
    /// </summary>
    public class OrphanGroupRule : IModelCheckRule
    {
        public string Name => "Grupo Orfao";
        public string Description => "Localiza grupos com zero elementos.";

        public IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId>? scopeIds)
        {
            var issues = new List<ModelCheckIssue>();
            if (doc == null)
                return issues;

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Group));

            int skippedOnError = 0;
            foreach (Group grupo in collector.Cast<Group>())
            {
                if (grupo == null)
                    continue;

                try
                {
                    ICollection<ElementId> membros = grupo.GetMemberIds();

                    if (membros == null || membros.Count == 0)
                    {
                        issues.Add(new ModelCheckIssue
                        {
                            RuleName = Name,
                            Severity = ModelCheckSeverity.Warning,
                            ElementId = grupo.Id.Value,
                            Description = $"Grupo '{grupo.Name}' nao tem elementos (orfao).",
                            Suggestion = "Delete este grupo ou adicione elementos a ele."
                        });
                    }
                }
                catch (Exception)
                {
                    skippedOnError++;
                }
            }

            if (skippedOnError > 0)
                Logger.Warn("[{Rule}] {Count} grupo(s) pulado(s) por erro na leitura de membros.", Name, skippedOnError);

            return issues;
        }
    }
}
