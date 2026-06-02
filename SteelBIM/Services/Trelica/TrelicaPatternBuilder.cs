#nullable enable
using System.Collections.Generic;
using SteelBIM.Models;

namespace SteelBIM.Services.Trelica
{
    /// <summary>Banzo de referencia de um no da treliça.</summary>
    public enum TrussChord
    {
        Superior,
        Inferior
    }

    /// <summary>Tipo de membro gerado.</summary>
    public enum TrussMemberKind
    {
        Banzo,
        Montante,
        Diagonal
    }

    /// <summary>No abstrato da treliça: banzo (superior/inferior) + indice da estacao ao longo do vao.</summary>
    public readonly struct TrussNode
    {
        public TrussChord Chord { get; }
        public int Estacao { get; }

        public TrussNode(TrussChord chord, int estacao)
        {
            Chord = chord;
            Estacao = estacao;
        }
    }

    /// <summary>Segmento abstrato (de -> para) com o tipo de membro. Sem coordenadas — o
    /// <c>TrelicaService</c> resolve os XYZ a partir das estacoes.</summary>
    public readonly struct TrussSegment
    {
        public TrussNode De { get; }
        public TrussNode Para { get; }
        public TrussMemberKind Tipo { get; }

        public TrussSegment(TrussNode de, TrussNode para, TrussMemberKind tipo)
        {
            De = de;
            Para = para;
            Tipo = tipo;
        }
    }

    /// <summary>Opcoes de montagem do treliçado.</summary>
    public sealed class TrussBuildOptions
    {
        public TrussPattern Padrao { get; set; } = TrussPattern.Warren;

        /// <summary>Modo "treliça completa": gera tambem os 2 banzos (superior/inferior).</summary>
        public bool IncluirBanzos { get; set; }

        /// <summary>Cria montantes nas estacoes intermediarias (alem dos exigidos pelo padrao).</summary>
        public bool MontantesIntermediarios { get; set; }

        /// <summary>Cria montantes nas estacoes das extremidades (apoios).</summary>
        public bool MontantesExtremidade { get; set; } = true;

        /// <summary>Cria diagonais nos paineis das extremidades.</summary>
        public bool DiagonaisExtremidade { get; set; } = true;

        /// <summary>Inverte o sentido das diagonais (espelha o padrao).</summary>
        public bool Espelhar { get; set; }
    }

    /// <summary>
    /// v2.8.11 (Onda 2 — Treliça): helper PURO que decide QUAIS membros (banzos, montantes,
    /// diagonais) compoem a treliça segundo o <see cref="TrussPattern"/>, dado o numero de
    /// estacoes ao longo do vao. 100% sem Revit — testavel por matriz de padroes.
    ///
    /// Convencao de diagonal: "subindo" = Inferior(p) -> Superior(p+1); "descendo" =
    /// Superior(p) -> Inferior(p+1). Estacoes 0..nEstacoes-1; paineis = nEstacoes-1.
    /// </summary>
    public static class TrelicaPatternBuilder
    {
        public static List<TrussSegment> Construir(int nEstacoes, TrussBuildOptions opcoes)
        {
            var segs = new List<TrussSegment>();
            if (opcoes == null || nEstacoes < 2)
                return segs;

            int ultima = nEstacoes - 1;
            int paineis = nEstacoes - 1;

            // 1) Banzos (modo treliça completa): um membro continuo por banzo.
            if (opcoes.IncluirBanzos)
            {
                segs.Add(new TrussSegment(
                    new TrussNode(TrussChord.Superior, 0), new TrussNode(TrussChord.Superior, ultima), TrussMemberKind.Banzo));
                segs.Add(new TrussSegment(
                    new TrussNode(TrussChord.Inferior, 0), new TrussNode(TrussChord.Inferior, ultima), TrussMemberKind.Banzo));
            }

            // 2) Montantes (verticais Superior(k) -> Inferior(k)).
            bool padraoUsaMontantesInternos = PadraoExigeMontantesInternos(opcoes.Padrao);
            for (int k = 0; k <= ultima; k++)
            {
                bool isPonta = (k == 0 || k == ultima);
                bool criar = isPonta
                    ? opcoes.MontantesExtremidade
                    : (opcoes.MontantesIntermediarios || padraoUsaMontantesInternos);
                if (criar)
                    segs.Add(new TrussSegment(
                        new TrussNode(TrussChord.Superior, k), new TrussNode(TrussChord.Inferior, k), TrussMemberKind.Montante));
            }

            // 3) Diagonais por painel.
            if (opcoes.Padrao != TrussPattern.SoMontantes)
            {
                double meio = paineis / 2.0;
                for (int p = 0; p < paineis; p++)
                {
                    bool isPainelPonta = (p == 0 || p == paineis - 1);
                    if (isPainelPonta && !opcoes.DiagonaisExtremidade)
                        continue;

                    if (opcoes.Padrao == TrussPattern.EmX)
                    {
                        segs.Add(Diagonal(true, p));
                        segs.Add(Diagonal(false, p));
                        continue;
                    }

                    bool subindo = DecideSubindo(opcoes.Padrao, p, meio);
                    if (opcoes.Espelhar)
                        subindo = !subindo;
                    segs.Add(Diagonal(subindo, p));
                }
            }

            return segs;
        }

        /// <summary>
        /// v2.8.11: altura da treliça (unidade consistente) na posicao normalizada t in [0,1]
        /// ao longo do vao, interpolando LINEARMENTE de H nas extremidades ate B no centro
        /// (duas aguas / tesoura). t=0 e t=1 -> H; t=0.5 -> B. H==B -> altura constante (banzos
        /// paralelos). Pura/testavel.
        /// </summary>
        public static double AlturaNaPosicao(double t, double alturaExtremidade, double alturaCentral)
        {
            double tc = System.Math.Clamp(t, 0.0, 1.0);
            double fatorCentro = 1.0 - System.Math.Abs((2.0 * tc) - 1.0);
            return alturaExtremidade + ((alturaCentral - alturaExtremidade) * fatorCentro);
        }

        private static TrussSegment Diagonal(bool subindo, int painel)
        {
            return subindo
                ? new TrussSegment(
                    new TrussNode(TrussChord.Inferior, painel), new TrussNode(TrussChord.Superior, painel + 1), TrussMemberKind.Diagonal)
                : new TrussSegment(
                    new TrussNode(TrussChord.Superior, painel), new TrussNode(TrussChord.Inferior, painel + 1), TrussMemberKind.Diagonal);
        }

        private static bool PadraoExigeMontantesInternos(TrussPattern padrao)
        {
            return padrao == TrussPattern.Pratt
                || padrao == TrussPattern.Howe
                || padrao == TrussPattern.Alternada
                || padrao == TrussPattern.SoMontantes;
        }

        private static bool DecideSubindo(TrussPattern padrao, int painel, double meio)
        {
            switch (padrao)
            {
                case TrussPattern.Pratt:
                    return painel >= meio;     // esq descendo, dir subindo (diagonais ao centro embaixo)
                case TrussPattern.Howe:
                    return painel < meio;      // inverso do Pratt
                case TrussPattern.DiagonalDireita:
                    return true;               // todas subindo
                case TrussPattern.DiagonalEsquerda:
                    return false;              // todas descendo
                default:
                    return (painel % 2) == 0;  // Warren / Alternada: zigue-zague
            }
        }
    }
}
