using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// <see cref="IExternalEventHandler"/> que destaca elementos IFC na vista
    /// ativa do Revit quando o usuario clica numa linha do DataGrid do
    /// <c>ConverterPerfilIfcWindow</c> (modeless dialog v2.7.1).
    ///
    /// Combina <c>Selection.SetElementIds</c> (highlight azul) +
    /// <c>UIDocument.ShowElements</c> (zoom + isolate) — sem este combo, o
    /// usuario veria a peca enquadrada mas sem contorno de selecao,
    /// dificultando identificacao em pranchas densas.
    ///
    /// Modeless WPF NAO pode chamar Revit API direto — toda manipulacao
    /// precisa passar por <see cref="ExternalEvent.Raise"/>. A Window seta
    /// <see cref="PendingIds"/> e dispara o evento; o Revit invoca este
    /// <see cref="Execute"/> no thread API quando ocioso.
    /// </summary>
    public class IfcSelectionHandler : IExternalEventHandler
    {
        /// <summary>
        /// Ids a destacar na proxima invocacao de <see cref="Execute"/>.
        /// A Window seta isto antes de <c>_event.Raise()</c>; o Execute
        /// le e processa. Null/empty = nada faz.
        /// </summary>
        public List<ElementId> PendingIds { get; set; }

        public void Execute(UIApplication app)
        {
            List<ElementId> ids = PendingIds;
            if (ids == null || ids.Count == 0)
                return;

            try
            {
                UIDocument uidoc = app?.ActiveUIDocument;
                if (uidoc == null)
                    return;

                // SetElementIds: highlight azul de selecao
                uidoc.Selection.SetElementIds(ids);
                // ShowElements: zoom + isolate (Revit chooses best view)
                uidoc.ShowElements(ids);
            }
            catch (System.Exception ex)
            {
                Logger.Warn(ex, "[IfcSelectionHandler] falha ao destacar elementos");
            }
        }

        public string GetName() => "SteelBIM.IfcSelection";
    }
}
