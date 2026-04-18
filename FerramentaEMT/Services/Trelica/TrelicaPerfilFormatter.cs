#nullable enable
using System;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) para formatar nomes de perfis metalicos
    /// no padrao do escritorio EMT (ver docs/reference-projects/cobertura-samsung/NOTAS.md
    /// e docs/reference-projects/galpao-padrao-emt/NOTAS.md).
    /// </summary>
    /// <remarks>
    /// Regras EMT observadas:
    /// - Cantoneira dupla: "2x L 76 x 76 x 6.3" (prefixo "2x" quando agrupada).
    /// - Perfil W/HP/I soldado: mantem nome do tipo direto (ex: "W 200 x 26.6").
    /// - Perfil U laminado / UE dobrado: "U 150 x 65 x 4.76" ou "UE 150x60x20x2.00".
    /// - Barra redonda: "BR 10" (diametro em mm).
    /// - Vazios/null viram "-" (placeholder padrao EMT em tabelas).
    /// </remarks>
    public static class TrelicaPerfilFormatter
    {
        /// <summary>Placeholder usado quando o perfil nao esta definido.</summary>
        public const string Placeholder = "-";

        /// <summary>
        /// Formata um nome de tipo Revit (ex: "L 76x76x6.3") com o multiplicador
        /// de quantidade de pecas paralelas (cantoneira dupla = 2).
        /// </summary>
        /// <param name="nomeTipo">Nome do tipo de perfil (FamilyName + TypeName ou so TypeName).</param>
        /// <param name="multiplicador">Numero de pecas paralelas (1=simples, 2=dupla).</param>
        public static string Formatar(string? nomeTipo, int multiplicador = 1)
        {
            if (string.IsNullOrWhiteSpace(nomeTipo)) return Placeholder;
            string limpo = Normalizar(nomeTipo!);
            if (multiplicador <= 1) return limpo;
            return $"{multiplicador}x {limpo}";
        }

        /// <summary>
        /// Normaliza espacamento e maiusculas de um nome de perfil para exibicao EMT.
        /// </summary>
        public static string Normalizar(string nomeTipo)
        {
            if (string.IsNullOrWhiteSpace(nomeTipo)) return Placeholder;
            return nomeTipo.Trim().Replace("  ", " ");
        }

        /// <summary>
        /// Detecta se um nome de perfil corresponde a uma cantoneira simples
        /// (inicio com "L " ou "L-"). Usado para sugerir multiplicador 2 quando
        /// a trelica usa cantoneira dupla nas diagonais.
        /// </summary>
        public static bool EhCantoneira(string? nomeTipo)
        {
            if (string.IsNullOrWhiteSpace(nomeTipo)) return false;
            string s = nomeTipo!.Trim();
            return s.StartsWith("L ", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("L-", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("L", StringComparison.OrdinalIgnoreCase) && s.Length > 1 && char.IsDigit(s[1]);
        }
    }
}
