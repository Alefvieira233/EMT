#nullable enable

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.11 (B6): prefixos dos GRUPOS auxiliares criados pelo plugin (agrupamento visual),
    /// centralizados num único lugar. Antes duplicados como string em AgrupamentoVisualService
    /// e LimparModeloService — se o esquema mudar, muda só aqui.
    /// </summary>
    public static class EmtPrefixos
    {
        /// <summary>Prefixo comum a todos os grupos do plugin (cobre COL_ e VIG_).</summary>
        public const string Comum = "EMT_";

        public const string GruposPilares = "EMT_COL_";
        public const string GruposVigas = "EMT_VIG_";
    }
}
