using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// <see cref="IExternalEventHandler"/> que executa a conversao IFC -> Perfis
    /// Nativos a partir do click em "Converter" no <c>ConverterPerfilIfcWindow</c>
    /// modeless (v2.7.1).
    ///
    /// Necessario pq modeless WPF nao pode abrir <c>Transaction</c> diretamente
    /// — <c>service.Executar</c> abre transaction internamente, entao chamar
    /// daqui (via ExternalEvent.Raise) garante que rodamos no thread API.
    ///
    /// Window seta <see cref="Doc"/>, <see cref="Config"/> e
    /// <see cref="OnFinished"/> callback antes de <c>_event.Raise()</c>.
    /// </summary>
    public class IfcConversionHandler : IExternalEventHandler
    {
        public Document Doc { get; set; }
        public ConverterPerfilIfcConfig Config { get; set; }

        /// <summary>
        /// Callback invocado apos service.Executar completar. Argumentos:
        /// (convertidos, ignorados). Sera chamado no thread Revit API — a
        /// Window deve fazer <c>Dispatcher.Invoke</c> internamente se for
        /// tocar UI WPF.
        /// </summary>
        public Action<int, int> OnFinished { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = Doc;
            ConverterPerfilIfcConfig config = Config;
            Action<int, int> cb = OnFinished;

            if (doc == null || config == null)
            {
                Logger.Warn("[IfcConversionHandler] sem Doc/Config — abortando");
                cb?.Invoke(0, 0);
                return;
            }

            int convertidos = 0;
            int ignorados = 0;
            try
            {
                var service = new ConverterPerfilIfcService();
                (convertidos, ignorados) = service.Executar(doc, config);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[IfcConversionHandler] falha durante conversao");
            }
            finally
            {
                cb?.Invoke(convertidos, ignorados);
            }
        }

        public string GetName() => "SteelBIM.IfcConversion";
    }
}
