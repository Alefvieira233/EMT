namespace FerramentaEMT.Models.CncExport
{
    /// <summary>
    /// Codigo de face DSTV onde o furo (ou contorno) esta posicionado.
    /// </summary>
    /// <remarks>
    /// v = web front (face frontal da alma)
    /// h = web back  (face traseira da alma)
    /// o = upper flange (mesa superior, "oben")
    /// u = lower flange (mesa inferior, "unten")
    /// s = side / extremidade
    /// </remarks>
    public enum DstvFace
    {
        WebFront = 0,
        WebBack = 1,
        TopFlange = 2,
        BottomFlange = 3,
        Side = 4
    }

    public static class DstvFaceExtensions
    {
        public static string ToDstvCode(this DstvFace face)
        {
            return face switch
            {
                DstvFace.WebFront => "v",
                DstvFace.WebBack => "h",
                DstvFace.TopFlange => "o",
                DstvFace.BottomFlange => "u",
                DstvFace.Side => "s",
                _ => "v"
            };
        }
    }

    /// <summary>
    /// Furo individual em uma peca, posicionado no sistema local DSTV
    /// (X ao longo do comprimento, Y ao longo da face).
    /// Coordenadas e diametro em milimetros.
    /// </summary>
    public sealed class DstvHole
    {
        public DstvFace Face { get; set; } = DstvFace.WebFront;

        /// <summary>Posicao X em mm (ao longo do comprimento da peca).</summary>
        public double XMm { get; set; }

        /// <summary>Posicao Y em mm (ao longo da largura da face).</summary>
        public double YMm { get; set; }

        /// <summary>Diametro do furo em mm.</summary>
        public double DiameterMm { get; set; }

        /// <summary>Profundidade do furo em mm. 0 = passante.</summary>
        public double DepthMm { get; set; } = 0.0;
    }
}
