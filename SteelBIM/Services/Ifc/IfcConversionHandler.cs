using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Core;
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
    ///
    /// v2.7.7 (auditoria 2026-05-25 #008): props opcionais <see cref="Progress"/>
    /// e <see cref="CancellationToken"/> permitem que a Window opt-in pra UX
    /// de progresso/cancel sem quebrar call sites existentes. Se nao setadas,
    /// comportamento eh identico ao v2.7.6 (loop sem feedback).
    ///
    /// **Como wirar UI no futuro (Sprint 1/2 v2.8.0):**
    /// <code>
    /// // Em ConverterPerfilIfcWindow.BtnConverter_Click(...):
    /// var cts = new CancellationTokenSource();
    /// var progressWindow = new ProgressWindow { Owner = this };
    /// progressWindow.CancelClicked += (s, e) =&gt; cts.Cancel();
    /// _conversionHandler.Progress = new Progress&lt;ProgressReport&gt;(r =&gt; progressWindow.Update(r));
    /// _conversionHandler.CancellationToken = cts.Token;
    /// progressWindow.Show();
    /// _conversionEvent.Raise();
    /// </code>
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

        /// <summary>
        /// v2.7.7: opcional. Se setado, Window recebe reports de progresso
        /// durante a conversao. Use <c>new Progress&lt;ProgressReport&gt;(...)</c>
        /// criado no thread UI — o tipo nativo do .NET captura SynchronizationContext
        /// e marshalla callbacks automaticamente. Default null = sem reporting.
        /// </summary>
        public IProgress<ProgressReport> Progress { get; set; }

        /// <summary>
        /// v2.7.7: opcional. Se setado e cancelado pela Window (via CTS.Cancel),
        /// service captura <see cref="OperationCanceledException"/>, rollback da
        /// transaction (alteracoes desfeitas), retorna counts ate o ponto. Default
        /// <c>CancellationToken.None</c> = nunca cancela.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = Doc;
            ConverterPerfilIfcConfig config = Config;
            Action<int, int> cb = OnFinished;
            IProgress<ProgressReport> progress = Progress;
            CancellationToken ct = CancellationToken;

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
                (convertidos, ignorados) = service.Executar(doc, config, progress, ct);
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
