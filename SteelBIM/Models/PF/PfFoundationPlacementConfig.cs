namespace SteelBIM.Models.PF
{
    public enum PfFoundationPlacementScope
    {
        SelecaoAtual = 0,
        VistaAtiva = 1
    }

    public sealed class PfFoundationPlacementConfig
    {
        public Autodesk.Revit.DB.ElementId SymbolId { get; set; } = Autodesk.Revit.DB.ElementId.InvalidElementId;

        public PfFoundationPlacementScope Escopo { get; set; } = PfFoundationPlacementScope.SelecaoAtual;

        public bool OrientarPeloPilar { get; set; } = true;

        public bool IgnorarSeJaExisteFundacao { get; set; } = true;

        public double ToleranciaCentroMm { get; set; } = 150.0;
    }
}
