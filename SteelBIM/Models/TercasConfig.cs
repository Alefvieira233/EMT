#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    public class TercasConfig
    {
        public FamilySymbol? SymbolSelecionado { get; set; }
        public int Quantidade { get; set; }
        public double BeiralInicialCm { get; set; }
        public double BeiralFinalCm { get; set; }
        public double OffsetMm { get; set; }
        public double RotacaoSecaoGraus { get; set; }
        public bool InverterSentido { get; set; }
        public int ZJustificationValue { get; set; }

        /// <summary>
        /// v2.8.11 (Onda 3): justificacao lateral (Y). 0=Esquerda (default), 1=Centro,
        /// 2=Origem, 3=Direita. Lança a terça "alinhada pela esquerda" em vez de centralizada.
        /// </summary>
        public int YJustificationValue { get; set; } = 0;
        public bool DividirNosBanzos { get; set; }

        // v2.8.1 (Victor): espacamento manual entre linhas.
        // EspacamentosCm tem (Quantidade+1) distancias em cm:
        // [linha_A → T1, T1→T2, ..., Tn → linha_B].
        // Quando UsarEspacamentoManual=false, usa distribuicao uniforme via Quantidade.
        // TercasService valida soma vs vao total; se invalido, cai pra uniforme.
        public bool UsarEspacamentoManual { get; set; } = false;
        public List<double> EspacamentosCm { get; set; } = new List<double>();
    }
}
