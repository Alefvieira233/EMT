using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace SteelBIM.Utils
{
    /// <summary>
    /// Filtro de selecao que aceita apenas elementos da categoria
    /// <c>OST_StructuralFraming</c> (vigas, tercas, terras de cobertura, etc).
    ///
    /// <para>v2.8.1 — extraido do <c>ConexaoTercasService</c> (originado no
    /// trabalho do Victor sobre v2.7.3) para reuso futuro em outros comandos
    /// que pickem vigas/tercas via <c>PickObject</c>/<c>PickObjects</c>.</para>
    /// </summary>
    public sealed class StructuralFramingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
            => elem?.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
