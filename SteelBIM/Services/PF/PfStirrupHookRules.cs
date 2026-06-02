#nullable enable
namespace SteelBIM.Services.PF
{
    /// <summary>
    /// Regras NBR 6118 secao 9.4.6.1 para o comprimento reto (rabo)
    /// do gancho de estribo. Multiplicador aplicado sobre o diametro
    /// da barra (rabo = multiplier x diametro). Helper puro, sem
    /// dependencia de Autodesk.Revit.DB — testavel via xUnit.
    /// </summary>
    public static class PfStirrupHookRules
    {
        /// <summary>
        /// Retorna o multiplicador do rabo reto do gancho conforme o
        /// angulo de dobra. 90->12, 135->10, 180->5. Qualquer outro
        /// angulo usa 10 (conservador, NBR-safe para estribo).
        /// </summary>
        public static double NbrStirrupHookMultiplier(int angleDegrees)
        {
            if (angleDegrees == 90)
                return 12.0;
            if (angleDegrees == 180)
                return 5.0;
            return 10.0;
        }

        /// <summary>
        /// Verifica se um RebarHookType ja existente (com `currentMultiplier`)
        /// esta em conformidade com a NBR 6118 secao 9.4.6.1 para o angulo
        /// informado. Use ANTES de reusar um hook ja presente no projeto,
        /// para nao silenciosamente herdar um multiplier insuficiente
        /// (regressao classe v2.4.0: hook 135 com rabo 6.O em vez de 10.O).
        ///
        /// Tolerancia padrao 0.01 absorve arredondamento de double sem
        /// afrouxar a regra.
        ///
        /// Adicionado em v2.6.1 (hotfix P0 NBR-1/NBR-2) apos auditoria
        /// senior 2026-05-19 detectar dois call sites no codigo
        /// (Bloco/RebarCreationService e PF/PfRebarService.GetHookTypeByAngle)
        /// que filtravam hooks por angulo sem checar multiplier.
        /// </summary>
        public static bool IsCompliantWithNbr(int angleDegrees, double currentMultiplier, double tolerance = 0.01)
        {
            double required = NbrStirrupHookMultiplier(angleDegrees);
            return currentMultiplier >= required - tolerance;
        }
    }
}
