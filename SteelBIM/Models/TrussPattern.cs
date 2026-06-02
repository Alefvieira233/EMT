#nullable enable

namespace SteelBIM.Models
{
    /// <summary>
    /// Padrao de treliçado (disposicao das diagonais/montantes do miolo da treliça).
    /// Consumido por <see cref="Services.Trelica.TrelicaPatternBuilder"/> (logica pura)
    /// e materializado pelo <c>TrelicaService</c>.
    /// </summary>
    public enum TrussPattern
    {
        /// <summary>Diagonais alternadas em zigue-zague (/\/\), sem montantes intermediarios.</summary>
        Warren = 0,

        /// <summary>Diagonais apontando para o centro (embaixo) + montantes em todas as estacoes.</summary>
        Pratt = 1,

        /// <summary>Diagonais apontando para os apoios + montantes em todas as estacoes.</summary>
        Howe = 2,

        /// <summary>Warren com montantes intermediarios (zigue-zague + verticais).</summary>
        Alternada = 3,

        /// <summary>Todas as diagonais no mesmo sentido (subindo da esquerda para a direita).</summary>
        DiagonalDireita = 4,

        /// <summary>Todas as diagonais no mesmo sentido (descendo da esquerda para a direita).</summary>
        DiagonalEsquerda = 5,

        /// <summary>Duas diagonais cruzadas em cada painel (contraventamento em X).</summary>
        EmX = 6,

        /// <summary>Apenas montantes verticais, sem diagonais.</summary>
        SoMontantes = 7
    }
}
