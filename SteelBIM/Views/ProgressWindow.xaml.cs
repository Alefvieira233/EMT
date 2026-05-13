using System;
using System.Windows;
using FerramentaEMT.Core;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Dialogo reutilizavel de progresso para operacoes longas.
    /// Usado via <see cref="RevitProgressHost.Run{T}"/> — nao abrir diretamente.
    ///
    /// Assumindo execucao do servico no mesmo thread da UI (requisito da Revit API).
    /// O <see cref="RevitProgressHost"/> bombeia o dispatcher entre eventos de progresso
    /// para que esta janela atualize e o clique em Cancelar chegue ao servico.
    /// </summary>
    public partial class ProgressWindow : Window
    {
        /// <summary>
        /// True quando o usuario clicou Cancelar. O host consulta esta flag
        /// no handler de IProgress e dispara o CancellationTokenSource.
        /// </summary>
        public bool CancelRequested { get; private set; }

        /// <summary>
        /// Disparado quando o usuario clica Cancelar. O host assina este evento
        /// para cancelar o <see cref="System.Threading.CancellationTokenSource"/>.
        /// </summary>
        public event EventHandler Cancelled;

        internal ProgressWindow(string title, string headline)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            Title = string.IsNullOrWhiteSpace(title) ? "Processando..." : title;
            txtHeadline.Text = string.IsNullOrWhiteSpace(headline) ? Title : headline;

            btnCancel.Click += BtnCancel_Click;
            Closing += ProgressWindow_Closing;
        }

        /// <summary>
        /// Atualiza a UI com um novo <see cref="ProgressReport"/>.
        /// Chamado pelo host na thread da UI (Progress&lt;T&gt; captura o contexto).
        /// </summary>
        public void UpdateProgress(ProgressReport report)
        {
            if (report.Total > 0)
            {
                double pct = report.Percent;
                progressBar.IsIndeterminate = false;
                progressBar.Value = pct;
                txtPercent.Text = $"{pct:0}%";
                txtCounter.Text = $"{report.Current} / {report.Total}";
            }
            else
            {
                progressBar.IsIndeterminate = true;
                txtPercent.Text = "";
                txtCounter.Text = "";
            }

            if (!string.IsNullOrEmpty(report.Message))
                txtDetail.Text = report.Message;
        }

        /// <summary>
        /// Marca o dialogo como "nao cancelavel" — usado pelo host quando
        /// o trabalho entra em fase que nao pode ser interrompida com seguranca
        /// (ex.: gravacao final de arquivo).
        /// </summary>
        public void DisableCancel()
        {
            btnCancel.IsEnabled = false;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (CancelRequested) return;
            CancelRequested = true;
            btnCancel.IsEnabled = false;
            btnCancel.Content = "Cancelando...";
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        private void ProgressWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Fechar pelo X equivale a Cancelar.
            if (!CancelRequested)
            {
                CancelRequested = true;
                Cancelled?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
