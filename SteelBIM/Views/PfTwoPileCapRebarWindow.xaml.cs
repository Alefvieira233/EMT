using System.Globalization;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfTwoPileCapRebarWindow : Window
    {
        public PfTwoPileCapRebarWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbBarSup.Items.Add(option);
                cmbBarInf.Items.Add(option);
                cmbBarLat.Items.Add(option);
            }

            if (!PfRebarTypeCatalog.TrySelect(cmbBarSup, "12.5 CA-50") && cmbBarSup.Items.Count > 0)
                cmbBarSup.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarInf, "12.5 CA-50") && cmbBarInf.Items.Count > 0)
                cmbBarInf.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarLat, "8 CA-50") && cmbBarLat.Items.Count > 0)
                cmbBarLat.SelectedIndex = 0;

            cmbModoPonta.Items.Add("Reta");
            cmbModoPonta.Items.Add("Dobra interna");
            cmbModoPonta.SelectedIndex = 1;

            txtAmostra.Text = sampleElement == null
                ? "Selecione um bloco de fundacao para usar a geometria real."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfTwoPileCapRebarConfig BuildConfig()
        {
            return new PfTwoPileCapRebarConfig
            {
                BarTypeSuperiorName = (cmbBarSup.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeInferiorName = (cmbBarInf.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeLateralName = (cmbBarLat.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 5.0),
                QuantidadeSuperior = ParseInt(txtQtdSup.Text, 4),
                QuantidadeInferior = ParseInt(txtQtdInf.Text, 4),
                QuantidadeLateral = ParseInt(txtQtdLat.Text, 0),
                ComprimentoGanchoCm = ParseDouble(txtGancho.Text, 10.0),
                ModoPonta = cmbModoPonta.SelectedIndex == 0 ? PfBeamBarEndMode.Reta : PfBeamBarEndMode.DobraInterna
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfTwoPileCapRebarConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeSuperiorName) ||
                string.IsNullOrWhiteSpace(config.BarTypeInferiorName) ||
                string.IsNullOrWhiteSpace(config.BarTypeLateralName))
            {
                AppDialogService.ShowWarning("PF - Acos Bloco 2 Estacas", "Selecione todos os tipos de vergalhao.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0)
            {
                AppDialogService.ShowWarning("PF - Acos Bloco 2 Estacas", "Informe um cobrimento maior que zero.", "Dados invalidos");
                return;
            }

            if (config.QuantidadeSuperior <= 0 &&
                config.QuantidadeInferior <= 0 &&
                config.QuantidadeLateral <= 0)
            {
                AppDialogService.ShowWarning("PF - Acos Bloco 2 Estacas", "Informe ao menos uma quantidade maior que zero.", "Dados invalidos");
                return;
            }

            if (config.ComprimentoGanchoCm < 0)
            {
                AppDialogService.ShowWarning("PF - Acos Bloco 2 Estacas", "O comprimento da dobra nao pode ser negativo.", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        private static int ParseInt(string text, int fallback)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private static double ParseDouble(string text, double fallback)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }
    }
}
