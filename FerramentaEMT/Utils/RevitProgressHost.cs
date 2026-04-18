using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FerramentaEMT.Core;
using FerramentaEMT.Views;

namespace FerramentaEMT.Utils
{
    /// <summary>
    /// Host para executar operacoes longas dentro do Revit com feedback visual
    /// (progress bar + botao Cancelar) — SEM violar o contrato de thread unica
    /// da Revit API.
    ///
    /// <para>
    /// Contrato de threading (ADR-004):
    /// <list type="bullet">
    ///   <item>O <paramref name="work"/> corre no mesmo thread que chamou este host
    ///   (tipicamente o thread principal do Revit). Chamadas a FilteredElementCollector,
    ///   Transaction, Parameter, etc. permanecem seguras.</item>
    ///   <item>Entre cada evento de <see cref="IProgress{ProgressReport}"/>, o host
    ///   bombeia o Dispatcher com prioridade <c>Background</c>. Isso faz com que a
    ///   ProgressBar atualize e o click em Cancelar chegue ao CTS — sem threads extras.</item>
    ///   <item>Quando o usuario clica Cancelar, o CancellationToken e sinalizado. O servico
    ///   ve no proximo <c>ThrowIfCancellationRequested()</c> e lanca OperationCanceledException,
    ///   que propaga ate o callsite.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Uso tipico:
    /// <code>
    /// try
    /// {
    ///     Result&lt;int&gt; r = RevitProgressHost.Run(
    ///         title: "Verificacao de Modelo",
    ///         headline: "Verificando regras...",
    ///         work: (prog, ct) =&gt; service.Executar(uidoc, config, prog, ct));
    /// }
    /// catch (OperationCanceledException) { return Result.Cancelled; }
    /// </code>
    /// </para>
    /// </summary>
    public static class RevitProgressHost
    {
        /// <summary>
        /// Executa <paramref name="work"/> com feedback visual. O delegate recebe um
        /// IProgress que atualiza a janela e um CancellationToken que o botao Cancelar dispara.
        /// </summary>
        /// <typeparam name="TResult">Tipo do retorno do trabalho.</typeparam>
        /// <param name="title">Titulo do dialogo (barra de titulo).</param>
        /// <param name="headline">Texto de destaque ("Exportando DSTV...").</param>
        /// <param name="work">Funcao a executar. Recebe IProgress e CancellationToken.</param>
        /// <param name="owner">Janela pai opcional, para centralizar.</param>
        /// <returns>O resultado de <paramref name="work"/>.</returns>
        /// <exception cref="OperationCanceledException">Propaga se <paramref name="work"/>
        /// chamou <c>ThrowIfCancellationRequested</c> apos o usuario clicar Cancelar.</exception>
        public static TResult Run<TResult>(
            string title,
            string headline,
            Func<IProgress<ProgressReport>, CancellationToken, TResult> work,
            Window owner = null)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            ProgressWindow window = new ProgressWindow(title, headline);
            if (owner != null) window.Owner = owner;

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                window.Cancelled += (s, e) =>
                {
                    // Proteger contra exceptions de um CTS ja disposed
                    // (caso o usuario feche depois que o trabalho ja terminou).
                    try { cts.Cancel(); }
                    catch (ObjectDisposedException) { /* ignorado */ }
                };

                // Progress<T> captura o SynchronizationContext atual (UI thread).
                // Mas como o trabalho tambem corre na UI thread, o callback Post'd
                // fica na fila ate o thread retornar ao message loop. Por isso o
                // Action delegate tambem bombeia explicitamente o dispatcher.
                Action<ProgressReport> onProgress = report =>
                {
                    window.UpdateProgress(report);
                    DoEvents();
                };
                Progress<ProgressReport> progress = new Progress<ProgressReport>(onProgress);

                window.Show();
                DoEvents(); // dar tempo da janela renderizar antes do trabalho comecar

                try
                {
                    return work(progress, cts.Token);
                }
                finally
                {
                    // Fechar antes de sair, seja sucesso, fail ou cancel.
                    // Usar Dispatcher para evitar reentrada se Closing ainda esta rodando.
                    if (window.IsVisible)
                    {
                        try { window.Close(); }
                        catch (InvalidOperationException) { /* janela ja fechada */ }
                    }
                }
            }
        }

        /// <summary>
        /// Bombeia o Dispatcher com prioridade Background. Equivalente ao
        /// <c>Application.DoEvents()</c> do WinForms. Permite que a UI renderize
        /// atualizacoes pendentes e que mensagens de input (clique no botao Cancelar)
        /// cheguem aos handlers sem esperar o thread retornar ao message loop natural.
        /// </summary>
        private static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
