#nullable enable
using System.Windows;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Dialogo simples de opcoes do comando "Cotar Treliça".
    /// O fluxo e: usuario pre-seleciona as barras -> clica botao ->
    /// esta janela abre -> usuario ajusta checkboxes -> OK -> executa.
    /// </summary>
    public partial class CotarTrelicaWindow : Window
    {
        public CotarTrelicaWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        /// <summary>Monta o DTO de configuracao a partir dos checkboxes da tela.</summary>
        public CotarTrelicaConfig BuildConfig()
        {
            return new CotarTrelicaConfig
            {
                CotarPaineisBanzoSuperior = chkPaineisSuperior.IsChecked == true,
                CotarVaosEntreApoios = chkVaosApoio.IsChecked == true,
                CotarPaineisBanzoInferior = chkPaineisInferior.IsChecked == true,
                CotarVaoTotal = chkVaoTotal.IsChecked == true,
                CotarAlturaMontantes = chkAlturaMontantes.IsChecked == true,
                IdentificarPerfis = chkIdentificarPerfis.IsChecked == true,
                CantoneiraDupla = chkCantoneiraDupla.IsChecked == true,
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
