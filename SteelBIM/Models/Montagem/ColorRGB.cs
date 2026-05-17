namespace SteelBIM.Models.Montagem
{
    /// <summary>
    /// Representa uma cor RGB independente de Autodesk.Revit.DB.
    /// Usado para persistir escolhas de cor do usuario sem
    /// dependencia direta da Revit API no Model.
    /// </summary>
    public sealed class ColorRGB
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public ColorRGB() { }
        public ColorRGB(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }
}
