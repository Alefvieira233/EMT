#nullable enable
namespace SteelBIM.Models
{
    /// <summary>
    /// v2.8.19: onde colocar o contraventamento de cobertura ao longo do comprimento do galpao.
    /// </summary>
    public enum DistribuicaoContravCobertura
    {
        /// <summary>Apenas os dois vaos de extremidade.</summary>
        Extremidades = 0,

        /// <summary>Extremidades + vaos centrais (4 vaos distribuidos uniformemente).</summary>
        ExtremidadesECentro = 1,

        /// <summary>Todos os vaos entre porticos.</summary>
        Todos = 2
    }
}
