#nullable enable
namespace SteelBIM.Models
{
    /// <summary>
    /// v2.8.19: onde colocar o contraventamento ao longo do comprimento do galpao. Usado tanto
    /// pelo contraventamento do plano da cobertura quanto pelo dos pilares (paredes) — mesmo padrao.
    /// </summary>
    public enum DistribuicaoContrav
    {
        /// <summary>Apenas os dois vaos de extremidade.</summary>
        Extremidades = 0,

        /// <summary>Extremidades + vaos centrais (4 vaos distribuidos uniformemente).</summary>
        ExtremidadesECentro = 1,

        /// <summary>Todos os vaos entre porticos.</summary>
        Todos = 2
    }
}
