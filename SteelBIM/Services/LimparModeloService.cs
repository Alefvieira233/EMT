#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.11 (Onda 4 — P5): limpeza SELETIVA dos elementos auxiliares criados pelo
    /// SteelBIM antes da entrega — desfaz os grupos temporarios com prefixo "EMT_"
    /// (EMT_COL_/EMT_VIG_, criados pelo AgrupamentoVisualService), preservando os membros.
    /// NAO apaga elementos modelados (pilares/vigas/armaduras) nem familias/tipos: purge de
    /// familia nao tem API segura no Revit 2025 — para isso o usuario usa o "Purgar Nao
    /// Utilizados" nativo (aba Gerenciar). Gerencia a propria transacao.
    /// </summary>
    public static class LimparModeloService
    {
        // Prefixo real dos grupos do plugin (vide AgrupamentoVisualService: EMT_COL_/EMT_VIG_).
        // Com o underscore para nao pegar grupos do usuario que comecem com "EMT".
        private const string PrefixoEmt = "EMT_";

        public sealed class Resultado
        {
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

                // Desfazer grupos temporarios do plugin (prefixo EMT_), preservando os membros.
                // Mesma estrategia do AgrupamentoVisualService.DesfazerGruposCriados (provado):
                // filtra por GroupType.Name (nao Group.Name) com guarda de null.
                List<Group> grupos = new FilteredElementCollector(doc)
                    .OfClass(typeof(Group))
                    .Cast<Group>()
                    .Where(g => g.GroupType != null
                                && g.GroupType.Name != null
                                && g.GroupType.Name.StartsWith(PrefixoEmt, StringComparison.OrdinalIgnoreCase))
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

                t.Commit();
            }

            return r;
        }
    }
}
