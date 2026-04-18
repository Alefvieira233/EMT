#nullable enable
using System;
using System.Windows;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Conexoes;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class ConexaoConfigWindow : Window
    {
        public ConexaoConfigWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            cmbTipoConexao.SelectedIndex = 0;

            btnOk.Click += (_, __) => DialogResult = true;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        private void CmbTipoConexao_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Logger.Debug("[ConexaoConfigWindow] Tipo de conexão alterado");

            // Ocultar todos os painéis
            gbChapaPonta.Visibility = Visibility.Collapsed;
            gbCantoneira.Visibility = Visibility.Collapsed;
            gbGusset.Visibility = Visibility.Collapsed;

            // Mostrar o painel correspondente
            int tipoSelecionado = cmbTipoConexao.SelectedIndex;
            switch (tipoSelecionado)
            {
                case 0: // Chapa de Ponta
                    gbChapaPonta.Visibility = Visibility.Visible;
                    break;
                case 1: // Dupla Cantoneira
                    gbCantoneira.Visibility = Visibility.Visible;
                    break;
                case 2: // Chapa Gusset
                    gbGusset.Visibility = Visibility.Visible;
                    break;
            }
        }

        public ConexaoConfig? BuildConfig()
        {
            Logger.Info("[ConexaoConfigWindow] Construindo configuração");

            try
            {
                var config = new ConexaoConfig
                {
                    Tipo = (TipoConexao)cmbTipoConexao.SelectedIndex,
                    GerarFurosDstv = chkGerarFurosDstv.IsChecked == true
                };

                // Chapa de Ponta
                if (double.TryParse(txtCPEspessura.Text, out double espCP) &&
                    double.TryParse(txtCPLargura.Text, out double largCP) &&
                    double.TryParse(txtCPAltura.Text, out double altCP) &&
                    int.TryParse(txtCPNumParafusos.Text, out int numCP))
                {
                    config.ChapaPonta = new ConfiguracaoChapaPonta
                    {
                        EspessuraMm = espCP,
                        LarguraMm = largCP,
                        AlturaMm = altCP,
                        NumParafusos = numCP
                    };
                }

                // Dupla Cantoneira
                if (double.TryParse(txtDCEspessura.Text, out double espDC) &&
                    double.TryParse(txtDCLargura.Text, out double largDC) &&
                    double.TryParse(txtDCAltura.Text, out double altDC) &&
                    int.TryParse(txtDCNumParafusos.Text, out int numDC))
                {
                    config.Cantoneira = new ConfiguracaoCantoneira
                    {
                        EspessuraMm = espDC,
                        LarguraMm = largDC,
                        AlturaMm = altDC,
                        NumParafusosPorCantoneira = numDC
                    };
                }

                // Chapa Gusset
                if (double.TryParse(txtGSEspessura.Text, out double espGS) &&
                    double.TryParse(txtGSLargura.Text, out double largGS) &&
                    double.TryParse(txtGSAltura.Text, out double altGS) &&
                    double.TryParse(txtGSAngulo.Text, out double angGS))
                {
                    config.Gusset = new ConfiguracaoGusset
                    {
                        EspessuraMm = espGS,
                        LarguraMm = largGS,
                        AlturaMm = altGS,
                        AnguloDiagonalDeg = angGS
                    };
                }

                Logger.Info("[ConexaoConfigWindow] Configuração construída com sucesso: tipo {Tipo}", config.Tipo);
                return config;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ConexaoConfigWindow] Erro ao construir configuração");
                AppDialogService.ShowError(
                    "Configuração de Conexão",
                    "Erro ao validar os parâmetros. Verifique se todos os valores são números válidos.",
                    "Erro de validação");
                return null;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
