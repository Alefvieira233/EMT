#nullable enable
using System;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) para classificar barras de uma trelica
    /// em Banzo Superior, Banzo Inferior, Montante ou Diagonal.
    /// Extraido para permitir testes unitarios sem Revit API.
    /// </summary>
    /// <remarks>
    /// Criterios de classificacao (ver docs/PLANO-LAPIDACAO.md secao 6.2):
    /// - Banzo: barra "horizontal" (inclinacao absoluta &lt;= <see cref="TolInclinacaoBanzoRad"/>).
    ///   Banzo Superior = o de cota Z mais alta; Banzo Inferior = o de cota Z mais baixa.
    /// - Montante: barra "vertical" (inclinacao absoluta &gt;= <see cref="TolInclinacaoMontanteRad"/>).
    /// - Diagonal: tudo que nao e banzo nem montante.
    /// Tolerancias em radianos; 15 graus = 0.2618 rad.
    /// </remarks>
    public static class TrelicaClassificador
    {
        /// <summary>Ate 15 graus de inclinacao a barra ainda conta como banzo.</summary>
        public const double TolInclinacaoBanzoRad = 0.2618;

        /// <summary>A partir de 75 graus (ate 90) a barra conta como montante.</summary>
        public const double TolInclinacaoMontanteRad = 1.3090;

        /// <summary>Tolerancia Z (em pes) para desempate de banzo superior x inferior.
        /// 0.00328 pe ~ 1 mm. Abaixo disso, barra esta "na altura media" e vira Indefinido.</summary>
        public const double TolDesempateZPes = 0.00328;

        /// <summary>Tipos de membro em uma trelica plana de duas aguas/plana.</summary>
        public enum TipoMembro
        {
            Indefinido = 0,
            BanzoSuperior = 1,
            BanzoInferior = 2,
            /// <summary>Barra horizontal que ainda nao foi desambiguada entre superior/inferior.
            /// E' um estado intermediario devolvido por <see cref="ClassificarPorInclinacao"/>.</summary>
            BanzoIndefinido = 7,
            Montante = 3,
            Diagonal = 4,
            MaoFrancesa = 5,
            AgulhaCentral = 6
        }

        /// <summary>
        /// Classifica uma barra pela inclinacao absoluta (em radianos) da sua direcao.
        /// Nao decide entre Banzo Superior/Inferior — isso exige contexto da trelica
        /// inteira (cota Z maxima/minima dos banzos) e e feito por
        /// <see cref="ClassificarBanzoPorAltura"/>.
        /// </summary>
        /// <param name="inclinacaoAbsRad">Inclinacao absoluta da barra em relacao ao plano XY, em radianos. Sempre [0, pi/2].</param>
        /// <returns><see cref="TipoMembro.BanzoIndefinido"/>, <see cref="TipoMembro.Montante"/> ou <see cref="TipoMembro.Diagonal"/>.</returns>
        public static TipoMembro ClassificarPorInclinacao(double inclinacaoAbsRad)
        {
            if (double.IsNaN(inclinacaoAbsRad)) return TipoMembro.Indefinido;
            double abs = Math.Abs(inclinacaoAbsRad);
            if (abs <= TolInclinacaoBanzoRad) return TipoMembro.BanzoIndefinido;
            if (abs >= TolInclinacaoMontanteRad) return TipoMembro.Montante;
            return TipoMembro.Diagonal;
        }

        /// <summary>
        /// Dado um banzo ja identificado (pela inclinacao) e a cota Z media da trelica,
        /// retorna se e Banzo Superior, Inferior ou Indefinido (dentro da tolerancia).
        /// </summary>
        /// <param name="zMedioBarra">Z medio (meio da barra) em pes.</param>
        /// <param name="zMedioTrelica">Z medio da bounding box da trelica em pes.</param>
        /// <param name="tolZPes">Tolerancia (pes) — dentro dela retorna Indefinido.</param>
        public static TipoMembro ClassificarBanzoPorAltura(
            double zMedioBarra,
            double zMedioTrelica,
            double tolZPes = TolDesempateZPes)
        {
            double delta = zMedioBarra - zMedioTrelica;
            if (delta > tolZPes) return TipoMembro.BanzoSuperior;
            if (delta < -tolZPes) return TipoMembro.BanzoInferior;
            return TipoMembro.BanzoIndefinido;
        }
    }
}
