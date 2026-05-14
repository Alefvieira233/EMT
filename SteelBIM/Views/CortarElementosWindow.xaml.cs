using System.Windows;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class CortarElementosWindow : Window
    {
        public CortarElementosWindow(bool temSelecaoAtiva)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            if (temSelecaoAtiva)
                rbSelecao.IsChecked = true;
            else
            {
                rbSelecao.IsEnabled = false;
                rbVista.IsChecked = true;
            }

            AtualizarDica();

            rbSelecao.Checked += (_, __) => AtualizarDica();
            rbVista.Checked += (_, __) => AtualizarDica();
            rbModelo.Checked += (_, __) => AtualizarDica();

            btnCortar.Click += BtnCortar_Click;
            btnCancelar.Click += (_, __) => DialogResult = false;
        }

        public CortarElementosConfig BuildConfig()
        {
            CortarElementosEscopo escopo = rbSelecao.IsChecked == true
                ? CortarElementosEscopo.Selecao
                : rbModelo.IsChecked == true
                    ? CortarElementosEscopo.Modelo
                    : CortarElementosEscopo.VistaAtiva;

            return new CortarElementosConfig
            {
                Escopo = escopo,
                IncluirPisos = chkPisos.IsChecked == true,
                IncluirQuadroEstrutural = chkQuadro.IsChecked == true,
                IncluirParedes = chkParedes.IsChecked == true,
                IncluirPilaresEstruturais = chkPilares.IsChecked == true,
                IncluirColunas = chkColunas.IsChecked == true
            };
        }

        private void BtnCortar_Click(object sender, RoutedEventArgs e)
        {
            bool semHost = chkPisos.IsChecked != true &&
                           chkQuadro.IsChecked != true &&
                           chkParedes.IsChecked != true;

            bool semCortador = chkPilares.IsChecked != true &&
                               chkColunas.IsChecked != true;

            if (semHost)
            {
                AppDialogService.ShowWarning("Cortar Elementos",
                    "Selecione ao menos um tipo de host (Pisos, Quadro Estrutural ou Paredes).",
                    "Configuracao inválida");
                return;
            }

            if (semCortador)
            {
                AppDialogService.ShowWarning("Cortar Elementos",
                    "Selecione ao menos um tipo de cortador (Pilares Estruturais ou Colunas).",
                    "Configuracao inválida");
                return;
            }

            DialogResult = true;
        }

        private void AtualizarDica()
        {
            if (rbSelecao.IsChecked == true)
                txtDica.Text = "Processa apenas os elementos pre-selecionados no modelo. " +
                               "Pode-se selecionar so pilares ou so pisos — a ferramenta encontra os pares automaticamente.";
            else if (rbModelo.IsChecked == true)
                txtDica.Text = "Varre todos os elementos do modelo, independente de vista. " +
                               "Pode ser lento em projetos grandes.";
            else
                txtDica.Text = "Varre todos os elementos visíveis na vista ativa. " +
                               "Recomendado para processar uma planta ou corte completo de uma vez.";
        }
    }
}
