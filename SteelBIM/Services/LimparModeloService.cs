#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.11 (Onda 4 — P5): limpeza SELETIVA dos elementos auxiliares criados pelo
    /// SteelBIM antes da entrega — filtros de vista e grupos temporarios com prefixo "EMT".
    /// NAO apaga elementos modelados (pilares/vigas/armaduras) nem familias/tipos: purge de
    /// familia nao tem API segura no Revit 2025 — para isso o usuario usa o "Purgar Nao
    /// Utilizados" nativo (aba Gerenciar). Gerencia a propria transacao.
    /// </summary>
    public static class LimparModeloService
    {
        private const string PrefixoEmt = "EMT";

        public sealed class Resultado
        {
            public int FiltrosRemovidos { get; set; }
            public int GruposDesfeitos { get; set; }
            public List<string> Falhas { get; } = new List<string>();
        }

        public static Resultado Limpar(Document doc)
        {
            Resultado r = new Resultado();
            if (doc == null)
                return r;

            using (Transaction t = new Transaction(doc, "SteelBIM - Limpar Modelo"))
            {
                t.Start();

                // 1) Desfazer grupos temporarios do plugin (prefixo EMT), preservando os membros.
                List<Group> grupos = new FilteredElementCollector(doc)
                    .OfClass(typeof(Group))
                    .Cast<Group>()
                    .Where(g => (g.Name ?? string.Empty).StartsWith(PrefixoEmt, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (Group g in grupos)
                {
                    try
                    {
                        g.UngroupMembers();
                        r.GruposDesfeitos++;
                    }
                    catch (Exception ex)
                    {
                        r.Falhas.Add($"Grupo {g.Id.Value}: {ex.Message}");
                    }
                }

                // 2) Remover filtros de vista criados pelo plugin (prefixo EMT).
                List<ParameterFilterElement> filtros = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .Where(f => (f.Name ?? string.Empty).StartsWith(PrefixoEmt, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (ParameterFilterElement f in filtros)
                {
                    try
                    {
                        doc.Delete(f.Id);
                        r.FiltrosRemovidos++;
                    }
                    catch (Exception ex)
                    {
                        r.Falhas.Add($"Filtro {f.Id.Value}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            return r;
        }
    }
}
