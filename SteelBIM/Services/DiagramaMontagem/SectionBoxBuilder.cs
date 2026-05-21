using System;

namespace SteelBIM.Services.DiagramaMontagem
{
    /// <summary>
    /// Dados puros (sem dependencia de Autodesk.Revit.DB) da SectionBox calculada
    /// pelo <see cref="SectionBoxBuilder"/>. O caller (DiagramaMontagemService)
    /// materializa em <c>Transform</c> + <c>BoundingBoxXYZ</c> do Revit.
    /// </summary>
    public readonly struct SectionBoxData
    {
        /// <summary>Origem do Transform (centro da regiao dos elementos, em coords globais).</summary>
        public readonly Vec3 OrigemTransform;

        /// <summary>Direcao "direita" da vista (BasisX do Transform).</summary>
        public readonly Vec3 BasisX;

        /// <summary>Direcao "up" da vista (BasisY do Transform).</summary>
        public readonly Vec3 BasisY;

        /// <summary>Direcao "para a frente do observador" (BasisZ do Transform — aponta PARA FORA do plano da vista).</summary>
        public readonly Vec3 BasisZ;

        /// <summary>Min da BoundingBox, em coordenadas locais ao Transform.</summary>
        public readonly Vec3 BboxMin;

        /// <summary>Max da BoundingBox, em coordenadas locais ao Transform.</summary>
        public readonly Vec3 BboxMax;

        public SectionBoxData(Vec3 origem, Vec3 bx, Vec3 by, Vec3 bz, Vec3 min, Vec3 max)
        {
            OrigemTransform = origem;
            BasisX = bx;
            BasisY = by;
            BasisZ = bz;
            BboxMin = min;
            BboxMax = max;
        }
    }

    /// <summary>
    /// Calculadora pura da SectionBox (Transform + bbox local) do Diagrama de
    /// Montagem. Sem dependencia de Autodesk.Revit.DB — 100% testavel via xUnit.
    ///
    /// Suporta 2 modos:
    /// <list type="bullet">
    /// <item><see cref="CalcularElevacao"/>: vista lateral (Paralela ao eixo X ou Y), comportamento original v2.3.0+.</item>
    /// <item><see cref="CalcularPlanta"/>: vista superior (planta) — v2.6.9. Observador olha -Z.</item>
    /// </list>
    ///
    /// O caller decide qual chamar com base no <c>OrientacaoDiagrama</c> da config
    /// e materializa o <see cref="SectionBoxData"/> retornado em
    /// <c>Autodesk.Revit.DB.Transform</c> + <c>BoundingBoxXYZ</c>.
    /// </summary>
    public static class SectionBoxBuilder
    {
        /// <summary>
        /// Calcula SectionBox de elevacao lateral (Paralela ao eixo X ou Y).
        /// Comportamento original v2.3.0+ preservado — apenas extraido pra
        /// helper testavel em v2.6.9.
        ///
        /// Quando <paramref name="paraleloAoX"/>=true: observador em Y- olhando para Y+.
        /// Quando false: observador em X- olhando para X+.
        /// Em ambos os casos UpDirection = +Z.
        /// </summary>
        /// <param name="bbMin">Canto minimo do bbox combinado dos elementos (coords globais, ft).</param>
        /// <param name="bbMax">Canto maximo do bbox combinado (coords globais, ft).</param>
        /// <param name="margemFt">Margem (ft) aplicada em todas as direcoes do bbox local.</param>
        /// <param name="paraleloAoX">true = secao paralela a X (vista frontal); false = paralela a Y (vista lateral).</param>
        public static SectionBoxData CalcularElevacao(
            Vec3 bbMin,
            Vec3 bbMax,
            double margemFt,
            bool paraleloAoX)
        {
            Vec3 origem = new Vec3(
                (bbMin.X + bbMax.X) / 2.0,
                (bbMin.Y + bbMax.Y) / 2.0,
                (bbMin.Z + bbMax.Z) / 2.0);

            double extX = bbMax.X - bbMin.X;
            double extY = bbMax.Y - bbMin.Y;
            double extZ = bbMax.Z - bbMin.Z;

            Vec3 rightDir, viewDir;
            if (paraleloAoX)
            {
                rightDir = new Vec3(1, 0, 0);       // +X
                viewDir = new Vec3(0, -1, 0);       // -Y (observador olha para Y+)
            }
            else
            {
                rightDir = new Vec3(0, 1, 0);       // +Y
                viewDir = new Vec3(1, 0, 0);        // +X (observador em X-)
            }

            Vec3 upDir = new Vec3(0, 0, 1);         // +Z

            double largura = (paraleloAoX ? extX : extY) + 2 * margemFt;
            double altura = extZ + 2 * margemFt;
            double profundidade = (paraleloAoX ? extY : extX) + 2 * margemFt;

            Vec3 min = new Vec3(-largura / 2.0, -altura / 2.0, -profundidade / 2.0);
            Vec3 max = new Vec3(largura / 2.0, altura / 2.0, profundidade / 2.0);

            return new SectionBoxData(origem, rightDir, upDir, viewDir, min, max);
        }

        /// <summary>
        /// Calcula SectionBox de vista superior (planta) — v2.6.9. Observador
        /// olha de cima pra baixo (-Z), com up da vista = +Y mundial (norte).
        /// Ideal para detalhamento de planos de cobertura, mezanino, lajes e
        /// fundacoes — qualquer caso onde a vista de planta e mais informativa
        /// que elevacao lateral.
        ///
        /// Trade-off conhecido (aceito): bbox local Z e centralizado no centroZ
        /// dos elementos. Em modelos com peca muito alta/baixa pode mostrar
        /// "ar" entre o observador e o topo do elemento mais alto. Considerado
        /// minor — smoke v2.6.9 valida se incomoda visualmente.
        /// </summary>
        /// <param name="bbMin">Canto minimo do bbox combinado dos elementos (coords globais, ft).</param>
        /// <param name="bbMax">Canto maximo do bbox combinado (coords globais, ft).</param>
        /// <param name="margemFt">Margem (ft) aplicada em todas as direcoes do bbox local.</param>
        public static SectionBoxData CalcularPlanta(
            Vec3 bbMin,
            Vec3 bbMax,
            double margemFt)
        {
            Vec3 origem = new Vec3(
                (bbMin.X + bbMax.X) / 2.0,
                (bbMin.Y + bbMax.Y) / 2.0,
                (bbMin.Z + bbMax.Z) / 2.0);

            double extX = bbMax.X - bbMin.X;
            double extY = bbMax.Y - bbMin.Y;
            double extZ = bbMax.Z - bbMin.Z;

            // Transform: olhar de cima pra baixo.
            // direita = +X mundial; up da vista = +Y mundial; observador em +Z olhando para -Z.
            Vec3 bx = new Vec3(1, 0, 0);
            Vec3 by = new Vec3(0, 1, 0);
            Vec3 bz = new Vec3(0, 0, -1);

            double largura = extX + 2 * margemFt;
            double altura = extY + 2 * margemFt;
            double profundidade = extZ + 2 * margemFt;

            Vec3 min = new Vec3(-largura / 2.0, -altura / 2.0, -profundidade / 2.0);
            Vec3 max = new Vec3(largura / 2.0, altura / 2.0, profundidade / 2.0);

            return new SectionBoxData(origem, bx, by, bz, min, max);
        }
    }
}
