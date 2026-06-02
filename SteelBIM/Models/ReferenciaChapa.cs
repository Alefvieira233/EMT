#nullable enable

namespace SteelBIM.Models
{
    /// <summary>
    /// v2.8.11 (P3 completo): referencia de posicionamento da chapa de ligacao da terça.
    /// </summary>
    public enum ReferenciaChapa
    {
        /// <summary>
        /// Default (comportamento provado): centra a chapa no cruzamento terça x viga pelo
        /// centroide ponderado por volume (corrige familias com origem fora do centro).
        /// </summary>
        Cruzamento = 0,

        /// <summary>
        /// Honra a ORIGEM nativa da familia da chapa (nao recentraliza) — alinha a chapa
        /// pela mesma referencia da terça (ex.: familia com origem no canto/costa). Combine
        /// com o offset lateral/vertical para o ajuste fino.
        /// </summary>
        OrigemTerca = 1
    }
}
