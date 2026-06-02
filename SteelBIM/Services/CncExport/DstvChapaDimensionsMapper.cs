#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Services.CncExport
{
    /// <summary>
    /// v2.8.10 (Etapa D) — Helper puro pra mapear as 6 dimensoes lineares de
    /// uma CHAPA (DstvProfileType.B) para os campos do DstvFile na ordem que
    /// o <see cref="DstvFileWriter"/> espera (validado byte-a-byte contra
    /// CH02/CH03/CH04 do fabricante).
    ///
    /// Ordem dos campos numericos do bloco ST (chapa):
    ///   1. CutLengthMm        = comprimento da chapa (ex: 620)
    ///   2. ProfileHeightMm    = LARGURA da chapa  (ex: 520)
    ///   3. FlangeWidthMm      = ESPESSURA          (ex: 12.70)
    ///   4. WebThicknessMm     = ESPESSURA          (ex: 12.70)
    ///   5. FlangeThicknessMm  = ESPESSURA          (ex: 12.70)
    ///   6. FilletRadiusMm     = 0 (chapa nao tem raio de filete)
    ///
    /// 100% sem dependencia de Autodesk.Revit.DB — testavel via xUnit.
    /// </summary>
    public static class DstvChapaDimensionsMapper
    {
        /// <summary>
        /// Resultado do mapeamento — 6 dimensoes na ordem dos campos do ST.
        /// </summary>
        public readonly struct ChapaDimensions
        {
            public double CutLengthMm { get; }
            public double ProfileHeightMm { get; }
            public double FlangeWidthMm { get; }
            public double WebThicknessMm { get; }
            public double FlangeThicknessMm { get; }
            public double FilletRadiusMm { get; }

            public ChapaDimensions(
                double cutLengthMm,
                double profileHeightMm,
                double flangeWidthMm,
                double webThicknessMm,
                double flangeThicknessMm,
                double filletRadiusMm)
            {
                CutLengthMm = cutLengthMm;
                ProfileHeightMm = profileHeightMm;
                FlangeWidthMm = flangeWidthMm;
                WebThicknessMm = webThicknessMm;
                FlangeThicknessMm = flangeThicknessMm;
                FilletRadiusMm = filletRadiusMm;
            }
        }

        /// <summary>
        /// Mapeia (comprimento, largura, espessura) em mm para as 6 dimensoes do ST.
        /// </summary>
        /// <param name="comprimentoMm">Maior dimensao no plano da chapa (ex: 620).</param>
        /// <param name="larguraMm">Menor dimensao no plano da chapa (ex: 520).</param>
        /// <param name="espessuraMm">Espessura fora do plano (ex: 12.70).</param>
        /// <exception cref="ArgumentOutOfRangeException">Se qualquer dimensao for &lt;= 0.</exception>
        public static ChapaDimensions Map(double comprimentoMm, double larguraMm, double espessuraMm)
        {
            if (comprimentoMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(comprimentoMm), "Comprimento da chapa deve ser maior que zero.");
            if (larguraMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(larguraMm), "Largura da chapa deve ser maior que zero.");
            if (espessuraMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(espessuraMm), "Espessura da chapa deve ser maior que zero.");

            return new ChapaDimensions(
                cutLengthMm: comprimentoMm,
                profileHeightMm: larguraMm,
                flangeWidthMm: espessuraMm,
                webThicknessMm: espessuraMm,
                flangeThicknessMm: espessuraMm,
                filletRadiusMm: 0.0);
        }

        /// <summary>
        /// Dado um bounding box em mm (dx, dy, dz), identifica
        /// (comprimento=maior horizontal, largura=menor horizontal, espessura=menor).
        ///
        /// Trade-off conhecido: para chapas inclinadas o bbox mundial nao reflete
        /// as dimensoes nominais. Resultado: chapa horizontal com normal Z e' OK;
        /// chapa em parede precisa que o caller projete antes (ou use plano local
        /// da Sketch da familia).
        /// </summary>
        public static ChapaDimensions FromBoundingBox(double dxMm, double dyMm, double dzMm)
        {
            // Espessura = menor das 3
            double espessura = Math.Min(dxMm, Math.Min(dyMm, dzMm));
            // Comprimento = maior das 3
            double comprimento = Math.Max(dxMm, Math.Max(dyMm, dzMm));
            // Largura = a sobrando (soma - max - min)
            double largura = (dxMm + dyMm + dzMm) - comprimento - espessura;

            return Map(comprimento, largura, espessura);
        }

        /// <summary>
        /// Deriva as dimensoes da chapa a partir do contorno 2D ja extraido/normalizado
        /// (no plano da face principal) + a espessura. Comprimento = extensao em X do
        /// contorno; largura = extensao em Y. Diferente de <see cref="FromBoundingBox"/>,
        /// funciona para chapa em QUALQUER orientacao (inclusive inclinada), pois o
        /// contorno ja esta no frame local da face — nao depende do bounding box world.
        /// </summary>
        /// <param name="contorno">Pontos (X, Y, Raio) em mm no plano da chapa (>= 3 pontos).</param>
        /// <param name="espessuraMm">Espessura da chapa em mm (&gt; 0).</param>
        /// <exception cref="ArgumentException">Se o contorno tiver menos de 3 pontos.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Se alguma dimensao resultante for &lt;= 0.</exception>
        public static ChapaDimensions FromContorno(
            IReadOnlyList<(double X, double Y, double Raio)> contorno, double espessuraMm)
        {
            if (contorno == null || contorno.Count < 3)
                throw new ArgumentException("Contorno da chapa precisa de pelo menos 3 pontos.", nameof(contorno));

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            foreach ((double X, double Y, double Raio) p in contorno)
            {
                if (p.X < minX)
                    minX = p.X;
                if (p.X > maxX)
                    maxX = p.X;
                if (p.Y < minY)
                    minY = p.Y;
                if (p.Y > maxY)
                    maxY = p.Y;
            }

            return Map(maxX - minX, maxY - minY, espessuraMm);
        }
    }
}
