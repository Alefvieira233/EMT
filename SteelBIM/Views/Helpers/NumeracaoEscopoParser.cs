using System;
using SteelBIM.Models;

namespace SteelBIM.Views.Helpers
{
    /// <summary>
    /// v2.8.0 F11 (Wave 3 — Strangler Fig): logica de parsing de escopo
    /// extraida do code-behind de <c>NumeracaoItensWindow</c>. Antes era
    /// <c>private static</c> na Window, inacessivel pra teste. Agora
    /// <c>public static</c> aqui — Window delega.
    ///
    /// <para>
    /// Default robusto: input nao reconhecido → <see cref="NumeracaoEscopo.VistaAtiva"/>
    /// (escopo mais seguro/restrito; nao numera o projeto inteiro por engano).
    /// </para>
    /// </summary>
    public static class NumeracaoEscopoParser
    {
        /// <summary>
        /// Parseia string → <see cref="NumeracaoEscopo"/>. Case-insensitive.
        /// Fallback eh sempre <c>VistaAtiva</c> (mais conservador).
        /// </summary>
        public static NumeracaoEscopo Parse(string value)
        {
            return Enum.TryParse(value, ignoreCase: true, out NumeracaoEscopo escopo)
                ? escopo
                : NumeracaoEscopo.VistaAtiva;
        }
    }
}
