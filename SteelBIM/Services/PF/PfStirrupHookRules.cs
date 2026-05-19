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
    }
}
