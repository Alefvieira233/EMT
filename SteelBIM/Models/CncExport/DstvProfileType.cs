namespace FerramentaEMT.Models.CncExport
{
    /// <summary>
    /// Codigo de tipo de perfil conforme especificacao DSTV (NC1).
    /// </summary>
    /// <remarks>
    /// Codigos oficiais DSTV (anexo A do padrao NC1):
    /// I = Perfis I/H (W, HEA, HEB, IPE)
    /// U = Perfil U / Channel
    /// L = Cantoneira (Angle)
    /// B = Chapa / Plate
    /// RO = Tubo redondo (HSS round)
    /// M = Tubo retangular (HSS rect)
    /// C = Perfil C
    /// T = Perfil T
    /// RU = Barra redonda
    /// SO = Perfil especial / desconhecido
    /// </remarks>
    public enum DstvProfileType
    {
        I = 0,
        U = 1,
        L = 2,
        B = 3,
        RO = 4,
        M = 5,
        C = 6,
        T = 7,
        RU = 8,
        SO = 9
    }

    /// <summary>
    /// Helpers de conversao para o codigo DSTV.
    /// </summary>
    public static class DstvProfileTypeExtensions
    {
        public static string ToDstvCode(this DstvProfileType type)
        {
            return type switch
            {
                DstvProfileType.I => "I",
                DstvProfileType.U => "U",
                DstvProfileType.L => "L",
                DstvProfileType.B => "B",
                DstvProfileType.RO => "RO",
                DstvProfileType.M => "M",
                DstvProfileType.C => "C",
                DstvProfileType.T => "T",
                DstvProfileType.RU => "RU",
                _ => "SO"
            };
        }
    }
}
