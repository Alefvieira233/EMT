#nullable enable
using System.Globalization;
using System.Windows;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    /// <summary>
    /// v2.8.26: janela do comando "Área de Pintura". Coleta escopo, categorias e desconto;
    /// produz um <see cref="AreaPinturaConfig"/>.
    /// </summary>
    public partial class AreaPinturaWindow : Window
    {
        public AreaPinturaWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public AreaPinturaConfig BuildConfig()
        {
            return new AreaPinturaConfig
            {
                SomenteSelecao = rbSelecao.IsChecked == true,
                IncluirVigas = chkVigas.IsChecked == true,
                IncluirPilares = chkPilares.IsChecked == true,
                DescontarTopos = chkDescontarTopos.IsChecked == true,
                PercentualDesconto = ParseD(txtDesconto.Text, 5.0),
                CriarTabela = chkCriarTabela.IsChecked == true
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (chkVigas.IsChecked != true && chkPilares.IsChecked != true)
            {
                AppDialogService.ShowWarning("Área de Pintura",
                    "Marque ao menos uma categoria (vigas ou pilares).", "Dados incompletos");
                return;
            }

            double desc = ParseD(txtDesconto.Text, -1.0);
            if (desc < 0.0 || desc > 100.0)
            {
                AppDialogService.ShowWarning("Área de Pintura",
                    "Desconto deve estar entre 0 e 100%.", "Dados inválidos");
                return;
            }

            DialogResult = true;
        }

        private static double ParseD(string text, double fallback)
        {
            string n = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }
    }
}
