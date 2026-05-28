using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace SteelBIM.Utils
{
    /// <summary>
    /// v2.8.2: filtro de seleção que aceita apenas <see cref="FamilyInstance"/>
    /// da categoria <see cref="BuiltInCategory.OST_StructuralFraming"/>
    /// (vigas, terças, banzos).
    ///
    /// <para>Conviver com <see cref="StructuralFramingSelectionFilter"/>
    /// — o filter "Beam" exige especificamente <c>FamilyInstance</c>
    /// (linha-based com LocationCurve), enquanto o filter "Framing"
    /// generico aceita qualquer elemento da categoria.</para>
    ///
    /// <para>Usado pelo <see cref="SteelBIM.Commands.CmdInserirConexaoTercas"/>
    /// no segundo pick (vigas de apoio).</para>
    /// </summary>
    public sealed class StructuralBeamSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is not FamilyInstance)
                return false;
            return elem.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;
        }

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
