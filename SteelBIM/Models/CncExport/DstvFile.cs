#nullable enable
using System.Collections.Generic;

namespace SteelBIM.Models.CncExport
{
    /// <summary>
    /// Representacao em memoria de um arquivo NC1 (DSTV) para uma peca.
    /// E construido por <see cref="Services.CncExport.DstvHeaderBuilder"/> e serializado
    /// por <see cref="Services.CncExport.DstvFileWriter"/>.
    /// </summary>
    /// <remarks>
    /// Todas as dimensoes lineares em milimetros.
    /// Todos os pesos em kg/m (peso linear).
    /// </remarks>
    public sealed class DstvFile
    {
        // ---------- Bloco ST (cabecalho) ----------

        /// <summary>Codigo do projeto / pedido.</summary>
        public string OrderNumber { get; set; } = "";

        /// <summary>Numero do desenho.</summary>
        public string DrawingNumber { get; set; } = "";

        /// <summary>Numero da fase / etapa de fabricacao.</summary>
        public string Phase { get; set; } = "1";

        /// <summary>Marca da peca (piece mark).</summary>
        public string PieceMark { get; set; } = "";

        /// <summary>Qualidade do aco (ex: A36, ASTM A992, S275).</summary>
        public string SteelQuality { get; set; } = "";

        /// <summary>Quantidade desta peca no projeto.</summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Codigo DSTV do tipo de perfil (I, U, L, B, RO, M, etc).</summary>
        public DstvProfileType ProfileType { get; set; } = DstvProfileType.SO;

        /// <summary>Designacao do perfil (ex: "W310X38.7", "HEA200").</summary>
        public string ProfileName { get; set; } = "";

        /// <summary>Altura do perfil (h) em mm.</summary>
        public double ProfileHeightMm { get; set; }

        /// <summary>Largura da mesa (bf) em mm.</summary>
        public double FlangeWidthMm { get; set; }

        /// <summary>Espessura da mesa (tf) em mm.</summary>
        public double FlangeThicknessMm { get; set; }

        /// <summary>Espessura da alma (tw) em mm.</summary>
        public double WebThicknessMm { get; set; }

        /// <summary>Raio de filete (r) em mm. 0 se desconhecido.</summary>
        public double FilletRadiusMm { get; set; }

        /// <summary>Peso linear em kg/m.</summary>
        public double WeightPerMeter { get; set; }

        /// <summary>
        /// Area de superficie de pintura em m2/m (campo 17 do bloco ST). 0 se nao calculado.
        /// Aparece logo apos o peso nos arquivos NC1 de referencia do fabricante.
        /// </summary>
        public double PaintingSurfacePerMeter { get; set; }

        /// <summary>
        /// Nome do arquivo NC1 escrito na linha-comentario de cabecalho ("** &lt;nome&gt;.nc1").
        /// Quando vazio, o writer deriva de <see cref="DrawingNumber"/> + ".nc1".
        /// </summary>
        public string OutputFileName { get; set; } = "";

        /// <summary>Tratamento de superficie (pintura, galvanizacao). Pode ser vazio.</summary>
        public string SurfaceTreatment { get; set; } = "";

        /// <summary>Comprimento de corte total da peca em mm.</summary>
        public double CutLengthMm { get; set; }

        /// <summary>
        /// Anguros de corte (start, end) em graus.
        /// 90,90 = cortes retos (omitidos no arquivo). Diferente disso gera bloco SC.
        /// </summary>
        public double CutAngleStartDeg { get; set; } = 90.0;
        public double CutAngleEndDeg { get; set; } = 90.0;

        /// <summary>
        /// Emitir o bloco AK (contorno externo retangular da face da alma) no NC1.
        /// DESLIGADO por padrao: embora muitos leitores DSTV exijam AK, o contorno exato
        /// varia por perfil/maquina e PRECISA ser validado contra um .nc1 real do fabricante
        /// (ou test-cut) antes de producao. Ligar somente apos validar na maquina alvo.
        /// </summary>
        public bool IncluirContornoAk { get; set; }

        // ---------- Bloco AK (contorno externo) ----------

        /// <summary>
        /// v2.8.10 — Pontos do contorno externo da CHAPA (face v), em mm: (X, Y, Raio).
        /// Raio &gt; 0 = arco; Raio &lt; 0 = arco invertido; Raio = 0 = canto reto.
        /// Usado pelo <c>DstvFileWriter</c> quando <see cref="IncluirContornoAk"/> = true.
        ///
        /// Quando vazio e <c>IncluirContornoAk</c> esta ligado, o writer pode gerar
        /// o retangulo padrao (comprimento x altura — fallback v2.8.0 do CHAPA).
        ///
        /// Validacao byte-a-byte contra .nc1 do fabricante pendente (Etapa D do plano
        /// Onda 3): aguardando arquivos CH02/CH03/CH04 do Alef pra rodar golden test.
        /// </summary>
        public List<(double X, double Y, double Raio)> ContornoAk { get; }
            = new List<(double X, double Y, double Raio)>();

        // ---------- Bloco BO (furos) ----------

        public List<DstvHole> Holes { get; } = new List<DstvHole>();

        // ---------- Bloco SI (informacoes adicionais, opcional) ----------

        public string Notes { get; set; } = "";

        /// <summary>
        /// True se algum corte na extremidade nao e perpendicular (90 graus).
        /// Usado pelo writer para decidir se grava bloco SC.
        /// </summary>
        public bool HasMiteredEnds()
        {
            const double tolerance = 0.5;
            return System.Math.Abs(CutAngleStartDeg - 90.0) > tolerance
                || System.Math.Abs(CutAngleEndDeg - 90.0) > tolerance;
        }
    }
}
