#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    public class TrelicaConfig
    {
        // ---- Perfis ----
        public FamilySymbol? SymbolMontante { get; set; }
        public FamilySymbol? SymbolDiagonal { get; set; }

        /// <summary>Perfil dos banzos — usado apenas no modo "treliça completa".</summary>
        public FamilySymbol? SymbolBanzo { get; set; }

        /// <summary>
        /// v2.8.14: perfil do banzo SUPERIOR (opcional). Quando nulo, cai em <see cref="SymbolBanzo"/>.
        /// Permite ao "Gerar Projeto Completo" usar perfis distintos para banzo superior/inferior.
        /// </summary>
        public FamilySymbol? SymbolBanzoSuperior { get; set; }

        /// <summary>v2.8.14: perfil do banzo INFERIOR (opcional). Quando nulo, cai em <see cref="SymbolBanzo"/>.</summary>
        public FamilySymbol? SymbolBanzoInferior { get; set; }

        // ---- Liga/desliga membros (filtro mestre sobre o padrao) ----
        public bool LancarMontante { get; set; }
        public bool LancarDiagonal { get; set; }

        // ---- Modulacao ----
        public int Quantidade { get; set; }

        /// <summary>v2.8.11: modo de espacamento dos montantes ao longo do vao.</summary>
        public TrussSpacingMode ModoEspacamento { get; set; } = TrussSpacingMode.Uniforme;

        /// <summary>
        /// v2.8.11: para <see cref="TrussSpacingMode.ListaEspacamentos"/> = distancias (cm)
        /// entre montantes consecutivos; para <see cref="TrussSpacingMode.Posicoes"/> =
        /// posicoes absolutas (cm) dos montantes a partir do inicio do vao.
        /// </summary>
        public List<double> EspacamentosCm { get; set; } = new List<double>();

        // ---- Treliçado ----
        /// <summary>v2.8.11: padrao de treliçado (Pratt/Howe/Warren/X/...).</summary>
        public TrussPattern Padrao { get; set; } = TrussPattern.Warren;

        /// <summary>v2.8.11: montantes nas estacoes intermediarias (alem dos exigidos pelo padrao).</summary>
        public bool MontantesIntermediarios { get; set; }

        /// <summary>v2.8.11: montantes nas extremidades (apoios).</summary>
        public bool MontantesExtremidade { get; set; } = true;

        /// <summary>v2.8.11: diagonais nos paineis das extremidades.</summary>
        public bool DiagonaisExtremidade { get; set; } = true;

        // ---- Modo "treliça completa" (gera tambem os banzos) ----
        /// <summary>v2.8.11: se true, gera banzos+montantes+diagonais a partir de 1 linha-base
        /// (banzo inferior) + altura; se false, preenche o miolo entre 2 banzos selecionados.</summary>
        public bool TrelicaCompleta { get; set; }

        /// <summary>v2.8.11: altura da treliça nas EXTREMIDADES (apoios), em mm — modo "treliça completa".</summary>
        public double AlturaExtremidadeMm { get; set; }

        /// <summary>v2.8.11: altura da treliça no CENTRO (cumeeira), em mm — modo "treliça completa".
        /// Igual à extremidade = banzos paralelos; maior = duas águas (tesoura).</summary>
        public double AlturaCentralMm { get; set; }

        // ---- Justificacao / offset ----
        public int ZJustificationValue { get; set; }
        public double ZOffsetMm { get; set; }
        public bool InverterSentido { get; set; }
    }
}
