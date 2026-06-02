#nullable enable

namespace SteelBIM.Models
{
    /// <summary>
    /// Modo de espacamento dos montantes/estacoes da treliça ao longo do vao.
    /// Uniforme e ListaEspacamentos sao resolvidos por
    /// <c>TercasSpacingCalculator.CalcularParametrosPosicao</c> (helper puro ja existente);
    /// Posicoes mapeia posicoes absolutas (cm a partir da origem) em parametros 0..1.
    /// </summary>
    public enum TrussSpacingMode
    {
        /// <summary>Subdivisao uniforme do vao em N+1 partes iguais.</summary>
        Uniforme = 0,

        /// <summary>Lista de distancias entre montantes consecutivos (cm).</summary>
        ListaEspacamentos = 1,

        /// <summary>Lista de posicoes absolutas dos montantes (cm a partir do inicio do vao).</summary>
        Posicoes = 2
    }
}
