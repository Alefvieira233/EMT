using System;
using System.ComponentModel;
using System.Windows;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class NumeracaoItensControleWindow : Window
    {
        private readonly NumeracaoItensService.NumeracaoItensSessao _sessao;
        private bool _atualizando;

        internal NumeracaoItensControleWindow(NumeracaoItensService.NumeracaoItensSessao sessao)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _sessao = sessao;

            btnRetroceder.Click += (_, __) => _sessao.AjustarNumero(-1);
            btnAvancar.Click += (_, __) => _sessao.AjustarNumero(1);
            btnDesfazer.Click += (_, __) => _sessao.SolicitarDesfazer();
            btnRetomar.Click += (_, __) => _sessao.SolicitarRetomar();
            btnConcluir.Click += (_, __) => _sessao.SolicitarConcluir();

            chkAutoIncremento.Checked += ChkAutoIncremento_Changed;
            chkAutoIncremento.Unchecked += ChkAutoIncremento_Changed;
            Closing += NumeracaoItensControleWindow_Closing;
            Loaded += NumeracaoItensControleWindow_Loaded;
        }

        public void AtualizarEstado(
            string valorAtual,
            string proximoAutomatico,
            string parametroNome,
            string status,
            int totalProcessado,
            int totalElegivel,
            bool autoIncremento,
            bool isSelecionando,
            bool podeDesfazer,
            bool podeRetomar)
        {
            _atualizando = true;

            txtValorAtual.Text = $"Valor atual: {valorAtual}";
            txtProximo.Text = $"Próximo automático: {proximoAutomatico}";
            txtParametro.Text = $"Campo: {parametroNome}";
            txtContagem.Text = $"{totalProcessado} de {totalElegivel} itens numerados";
            txtStatus.Text = status;

            chkAutoIncremento.IsChecked = autoIncremento;
            btnRetroceder.IsEnabled = true;
            btnAvancar.IsEnabled = true;
            btnDesfazer.IsEnabled = podeDesfazer && !isSelecionando;
            btnRetomar.IsEnabled = podeRetomar && !isSelecionando && totalProcessado < totalElegivel;
            btnRetomar.Content = totalProcessado == 0 ? "Selecionar" : "Retomar";
            btnConcluir.Content = isSelecionando ? "Concluir (ESC)" : "Concluir";

            _atualizando = false;
        }

        private void NumeracaoItensControleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AjustarAoEspacoUtil();
        }

        private void ChkAutoIncremento_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizando)
                return;

            _sessao.DefinirAutoIncremento(chkAutoIncremento.IsChecked == true);
        }

        private void NumeracaoItensControleWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_sessao.PodeFecharJanela())
                return;

            e.Cancel = true;
            _sessao.SolicitarFechamentoPelaJanela();
        }

        private void AjustarAoEspacoUtil()
        {
            Rect area = SystemParameters.WorkArea;
            MaxHeight = area.Height - 24;
            MaxWidth = area.Width - 24;

            UpdateLayout();

            double largura = ActualWidth > 0 ? ActualWidth : Width;
            double altura = ActualHeight > 0 ? ActualHeight : Height;

            Left = Math.Max(area.Left + 12, Math.Min(Left, area.Right - largura - 12));
            Top = Math.Max(area.Top + 12, Math.Min(Top, area.Bottom - altura - 12));
        }
    }
}
