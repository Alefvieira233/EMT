#nullable enable
using System;

namespace SteelBIM.Services.DiagramaMontagem
{
    /// <summary>
    /// Vetor 3D puro (sem dependencia de Autodesk.Revit.DB.XYZ) — usado pelo
    /// <see cref="DimensionPlanCalculator"/> para que toda a matematica vetorial
    /// fique testavel sem Document/View. O caller (DiagramaMontagemService)
    /// converte XYZ -> Vec3 antes de chamar e Vec3 -> XYZ depois.
    /// Imutavel por design — todas as operacoes retornam novo Vec3.
    /// </summary>
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalize()
        {
            double len = Length;
            if (len < 1e-9)
                throw new InvalidOperationException("Nao e possivel normalizar vetor nulo.");
            return new Vec3(X / len, Y / len, Z / len);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vec3 Cross(Vec3 other) => new Vec3(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);
    }

    /// <summary>
    /// Resultado puro do calculo do plano de uma cota (linha da cota).
    /// Origem = ponto pelo qual a Line passa (apos offset perpendicular do
    /// midpoint da peca); Direcao = vetor unitario ao longo do eixo de
    /// medicao (direcao da peca projetada). O caller monta a
    /// <c>Autodesk.Revit.DB.Line.CreateUnbound(origem, direcao)</c>.
    /// </summary>
    public readonly struct PlanoCotaResult
    {
        public readonly Vec3 Origem;
        public readonly Vec3 Direcao;

        public PlanoCotaResult(Vec3 origem, Vec3 direcao)
        {
            Origem = origem;
            Direcao = direcao;
        }
    }

    /// <summary>
    /// Calculadora pura do plano da cota individual de uma peca para o
    /// Diagrama de Montagem. Sem dependencia de Autodesk.Revit.DB,
    /// 100% testavel via xUnit Theory.
    ///
    /// Padrao perpendicular-a-peca (clone do
    /// <c>CotarPecaFabricacaoService.CriarCotaViaFamilyRefs</c>) em vez de
    /// perpendicular-a-vista — funciona corretamente para pecas com
    /// orientacoes variadas (terca horizontal, montante vertical, diagonal
    /// inclinada) na mesma Section View.
    ///
    /// v2.6.6: offset ADAPTATIVO baseado na seccao real do perfil
    /// (STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH lidos no caller a partir do
    /// FamilySymbol). Substitui offset fixo de 200mm + stagger da v2.6.5.
    /// Rejeitada abordagem bbox-based: AABB world-space de peca diagonal
    /// inclui projecao do COMPRIMENTO da peca, gerando half-section dezenas
    /// de vezes maior que a real (ex: diagonal U75 a 45 graus -> bbox 935mm
    /// em vez de 75mm).
    /// </summary>
    public static class DimensionPlanCalculator
    {
        /// <summary>
        /// Fallback de half-section (ft) usado quando ambos depth e width sao
        /// zero (familia sem parametros STRUCTURAL_SECTION_COMMON_*). 100mm
        /// e defensivo — gera offset visivel sem colidir com perfis comuns
        /// que estariam na faixa 50-300mm.
        /// </summary>
        public const double FallbackHalfSectionMm = 100.0;

        /// <summary>
        /// Conversao mm -> ft (Revit internal units). 1 ft = 304.8 mm.
        /// </summary>
        private const double FtToMm = 304.8;

        /// <summary>
        /// Calcula half-section perpendicular a partir das dimensoes da
        /// seccao bruta do perfil (lidas do FamilySymbol via
        /// BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH). Usa
        /// <c>Math.Max(depth, width)</c> para cobrir orientacao desconhecida
        /// do perfil — trade-off conservador: em alguns casos o offset sera
        /// ~10-30mm maior que o ideal preciso (otimizacao orientation-aware
        /// fica como follow-up v2.7.0+).
        /// </summary>
        /// <param name="sectionDepthFt">Section Depth — <c>STRUCTURAL_SECTION_COMMON_HEIGHT</c> (ft).</param>
        /// <param name="sectionWidthFt">Section Width — <c>STRUCTURAL_SECTION_COMMON_WIDTH</c> (ft).</param>
        /// <returns>Half-section em feet. Se ambos forem &lt;= 0, retorna fallback de <see cref="FallbackHalfSectionMm"/>mm em ft.</returns>
        public static double CalcularHalfSectionPerp(double sectionDepthFt, double sectionWidthFt)
        {
            if (sectionDepthFt <= 0 && sectionWidthFt <= 0)
                return FallbackHalfSectionMm / FtToMm;

            double maxDimFt = Math.Max(sectionDepthFt, sectionWidthFt);
            return maxDimFt / 2.0;
        }

        /// <summary>
        /// Calcula (origem, direcao) da Line de cota para uma peca cujos
        /// endpoints sao <paramref name="p1"/> e <paramref name="p2"/>, numa
        /// vista cujo plano tem normal <paramref name="viewNormal"/>.
        ///
        /// Offset total = <c>halfSectionPerp(depth, width) + clearance</c>.
        /// A linha da cota fica sempre a <c>clearance</c> mm da face externa
        /// do perfil — independente do tamanho da seccao.
        /// </summary>
        /// <param name="p1">Endpoint inicial (ft).</param>
        /// <param name="p2">Endpoint final (ft).</param>
        /// <param name="viewNormal">Normal do plano da vista (unitario).</param>
        /// <param name="sectionDepthFt">Section Depth do perfil (ft) — <c>STRUCTURAL_SECTION_COMMON_HEIGHT</c>.</param>
        /// <param name="sectionWidthFt">Section Width do perfil (ft) — <c>STRUCTURAL_SECTION_COMMON_WIDTH</c>.</param>
        /// <param name="clearanceFt">Folga (ft) entre face externa do perfil e linha da cota.</param>
        /// <returns>Origem e direcao da Line da cota.</returns>
        /// <exception cref="ArgumentException">p1 == p2 (peca degenerada).</exception>
        /// <exception cref="InvalidOperationException">Peca paralela ao viewNormal (cota colapsa).</exception>
        public static PlanoCotaResult CalcularPlanoCota(
            Vec3 p1,
            Vec3 p2,
            Vec3 viewNormal,
            double sectionDepthFt,
            double sectionWidthFt,
            double clearanceFt)
        {
            Vec3 delta = p2 - p1;
            if (delta.Length < 1e-9)
                throw new ArgumentException("p1 e p2 sao coincidentes — peca degenerada.", nameof(p2));

            Vec3 direcao = delta.Normalize();

            Vec3 perp = direcao.Cross(viewNormal);
            if (perp.Length < 1e-9)
                throw new InvalidOperationException(
                    "Peca paralela ao viewNormal — cota colapsaria em ponto. " +
                    "Caller deve pular esta peca e logar warn.");
            perp = perp.Normalize();

            double halfSectionPerpFt = CalcularHalfSectionPerp(sectionDepthFt, sectionWidthFt);
            double offsetTotalFt = halfSectionPerpFt + clearanceFt;

            Vec3 midpoint = (p1 + p2) * 0.5;
            Vec3 origem = midpoint + perp * offsetTotalFt;

            return new PlanoCotaResult(origem, direcao);
        }

        /// <summary>
        /// v2.8.8 — Projeta um ponto 3D no plano da Section View.
        /// O "plano" e definido por <paramref name="origemPlano"/> (ponto no plano)
        /// e <paramref name="normalPlano"/> (unitario, ortogonal ao plano —
        /// equivalente a <c>vista.ViewDirection</c> do Revit).
        ///
        /// Usado para colocar a linha de cota e endpoints na MESMA cota Y do plano
        /// da vista — sem isso, a Dimension fica com Line desalinhada das Refs e
        /// o Revit rejeita no commit (dialog "Excluir cotas").
        /// </summary>
        /// <param name="ponto">Ponto 3D arbitrario (em world-space, ft).</param>
        /// <param name="origemPlano">Ponto pertencente ao plano (tipicamente <c>vista.Origin</c>).</param>
        /// <param name="normalPlano">Normal unitaria do plano (tipicamente <c>vista.ViewDirection</c>).</param>
        /// <returns>Ponto projetado no plano. Se <paramref name="normalPlano"/> nao for unitaria, o resultado fica escalado proporcionalmente.</returns>
        public static Vec3 ProjetarPontoNoPlano(Vec3 ponto, Vec3 origemPlano, Vec3 normalPlano)
        {
            // d = (ponto - origemPlano) . normal  -> distancia signed do ponto ao plano
            double d = (ponto - origemPlano).Dot(normalPlano);
            // Subtrai a componente normal pra trazer o ponto pro plano
            return ponto - normalPlano * d;
        }

        /// <summary>
        /// v2.8.8 — Converte um ponto 3D pra coordenadas 2D do plano da vista
        /// (u = direcao RightDirection, v = direcao UpDirection). Util pra
        /// ordenar Grids da esquerda pra direita e calcular o topo dos eixos
        /// pra posicionar a linha de cota.
        /// </summary>
        /// <returns>Tuple (u, v) onde u = <c>(ponto - origem).Dot(right)</c> e v = <c>(ponto - origem).Dot(up)</c>.</returns>
        public static (double U, double V) ProjetarPontoEm2DDaVista(
            Vec3 ponto, Vec3 origem, Vec3 right, Vec3 up)
        {
            Vec3 delta = ponto - origem;
            return (delta.Dot(right), delta.Dot(up));
        }

        /// <summary>
        /// v2.8.8 — Reconstroi um ponto 3D world-space a partir das coordenadas
        /// 2D no plano da vista (inverso de <see cref="ProjetarPontoEm2DDaVista"/>).
        /// <c>origem + right*u + up*v</c>.
        /// </summary>
        public static Vec3 ReconstruirPonto3DDaVista(
            double u, double v, Vec3 origem, Vec3 right, Vec3 up)
        {
            return origem + right * u + up * v;
        }

        /// <summary>
        /// Decide se um <see cref="Dimension.ValueOverride"/> deve ser aplicado
        /// quando o comprimento geometrico (medido pelo Dimension) diverge do
        /// comprimento de fabricacao (STRUCTURAL_FRAME_CUT_LENGTH). Sugestao
        /// Cowork: aplicar override quando diferenca > 5mm.
        /// </summary>
        /// <param name="lengthGeomFt">Comprimento geometrico medido (ft).</param>
        /// <param name="lengthFabFt">Comprimento de fabricacao (ft).</param>
        /// <param name="thresholdMm">Tolerancia em mm (default 5).</param>
        /// <returns>true = aplicar override; false = deixar Dimension mostrar valor medido.</returns>
        public static bool DeveAplicarOverride(double lengthGeomFt, double lengthFabFt, double thresholdMm = 5.0)
        {
            if (lengthFabFt <= 0)
                return false;
            double divergenciaMm = Math.Abs(lengthGeomFt - lengthFabFt) * FtToMm;
            return divergenciaMm > thresholdMm;
        }
    }
}
