using System.Collections.Generic;
using FerramentaEMT.Models.PF;

namespace FerramentaEMT.Services.PF
{
    internal static class PfTwoPileCapBarCatalog
    {
        public static IReadOnlyList<PfTwoPileCapBarPosition> Tipo4 { get; } = new List<PfTwoPileCapBarPosition>
        {
            Position(1, 12.5, 48, 290, 12, PfTwoPileCapBarShape.FormaEspecial, "barra superior longitudinal conforme croqui N1 do PDF"),
            Position(2, 8.0, 48, 149, 20, PfTwoPileCapBarShape.U, "forma U 20+109+20"),
            Position(3, 6.3, 15, 205, 15, PfTwoPileCapBarShape.EstriboVertical, "estribo/linha de distribuicao inferior do bloco"),
            Position(4, 16.0, 15, 407, 0, PfTwoPileCapBarShape.FormaEspecial, "barra principal inferior com dobras R=4"),
            Position(5, 12.5, 12, 325, 0, PfTwoPileCapBarShape.FormaEspecial, "barra longitudinal inferior N5"),
            Position(6, 12.5, 12, 414, 0, PfTwoPileCapBarShape.FormaEspecial, "barra longitudinal inferior N6"),
            Position(7, 6.3, 15, 205, 15, PfTwoPileCapBarShape.EstriboVertical, "estribo/linha de distribuicao inferior do bloco"),
            Position(8, 8.0, 9, 764, 17, PfTwoPileCapBarShape.FormaEspecial, "barra vertical lateral N8"),
            Position(9, 6.3, 18, 311, 20, PfTwoPileCapBarShape.RetanguloFechado, "estribo horizontal do bloco"),
            Position(10, 6.3, 18, 822, 16, PfTwoPileCapBarShape.EstriboVertical, "estribo vertical longitudinal do bloco"),
            Position(11, 8.0, 9, 768, 17, PfTwoPileCapBarShape.FormaEspecial, "barra vertical lateral N11"),
            Position(12, 6.3, 27, 398, 20, PfTwoPileCapBarShape.RetanguloFechado, "estribo horizontal do calice"),
            Position(13, 10.0, 30, 762, 5, PfTwoPileCapBarShape.CaliceVertical, "armadura vertical do calice 2x5"),
            Position(14, 8.0, 12, 454, 20, PfTwoPileCapBarShape.FormaEspecial, "barra vertical/transversal do calice"),
        };

        public static PfTwoPileCapBarPosition Get(int position)
        {
            foreach (PfTwoPileCapBarPosition item in Tipo4)
            {
                if (item.Posicao == position)
                    return item;
            }

            return null;
        }

        private static PfTwoPileCapBarPosition Position(
            int position,
            double diameterMm,
            int totalPdf,
            double lengthCm,
            double spacingCm,
            PfTwoPileCapBarShape shape,
            string description)
        {
            return new PfTwoPileCapBarPosition
            {
                Posicao = position,
                DiametroMm = diameterMm,
                QuantidadeTotalPdf = totalPdf,
                QuantidadePorBloco = totalPdf / 3,
                ComprimentoCm = lengthCm,
                EspacamentoCm = spacingCm,
                Forma = shape,
                DescricaoForma = description
            };
        }
    }
}
