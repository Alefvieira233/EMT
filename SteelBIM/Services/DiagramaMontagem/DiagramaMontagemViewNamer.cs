#nullable enable
using SteelBIM.Models.DiagramaMontagem;

namespace SteelBIM.Services.DiagramaMontagem
{
    /// <summary>
    /// v2.8.0 F12 (Wave 3 — Strangler Fig): logica de naming contextual
    /// extraida do <see cref="DiagramaMontagemService.Executar"/>. Antes
    /// inline na linha 51-53; agora pura + testavel.
    ///
    /// <para>
    /// Regra: vistas <see cref="OrientacaoDiagrama.Superior"/> ganham sufixo
    /// "(Planta)" pra facilitar identificacao no Project Browser do Revit
    /// (onde varias vistas podem ter nomes parecidos). Outras orientacoes
    /// mantem o nome base sem sufixo.
    /// </para>
    /// </summary>
    public static class DiagramaMontagemViewNamer
    {
        /// <summary>
        /// Constroi o nome contextual da vista a partir do nome base + orientacao.
        /// </summary>
        public static string BuildContextualName(string? nomeBase, OrientacaoDiagrama orientacao)
        {
            string baseSanitized = nomeBase ?? string.Empty;
            return orientacao == OrientacaoDiagrama.Superior
                ? $"{baseSanitized} (Planta)"
                : baseSanitized;
        }
    }
}
