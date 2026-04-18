using System.Globalization;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfBeamBarsWindow : Window
    {
        public PfBeamBarsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbBarSup.Items.Add(option);
                cmbBarInf.Items.Add(option);
                cmbBarLat.Items.Add(option);
            }

            cmbModoPonta.Items.Add("Reta");
            cmbModoPonta.Items.Add("Dobra interna");
            cmbModoPonta.SelectedIndex = 1;

            if (!PfRebarTypeCatalog.TrySelect(cmbBarSup, "8 CA-50") && cmbBarSup.Items.Count > 0)
                cmbBarSup.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarInf, "8 CA-50") && cmbBarInf.Items.Count > 0)
                cmbBarInf.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarLat, "8 CA-50") && cmbBarLat.Items.Count > 0)
                cmbBarLat.SelectedIndex = 0;

            txtAmostra.Text = sampleElement == null
                ? "Selecione uma viga para usar a geometria real da secao."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfBeamBarsConfig BuildConfig()
        {
            return new PfBeamBarsConfig
            {
                BarTypeSuperiorName = (cmbBarSup.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeInferiorName = (cmbBarInf.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeLateralName = (cmbBarLat.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                QuantidadeSuperior = ParseInt(txtQtdSup.Text, 2),
                QuantidadeInferior = ParseInt(txtQtdInf.Text, 2),
                QuantidadeLateral = ParseInt(txtQtdLat.Text, 0),
                ComprimentoGanchoCm = ParseDouble(txtGancho.Text, 10.0),
                ModoPonta = cmbModoPonta.SelectedIndex == 0 ? PfBeamBarEndMode.Reta : PfBeamBarEndMode.DobraInterna
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfBeamBarsConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeSuperiorName) ||
                string.IsNullOrWhiteSpace(config.BarTypeInferiorName) ||
                string.IsNullOrWhiteSpace(config.BarTypeLateralName))
            {
                AppDialogService.ShowWarning("PM - Acos Viga", "Selecione todos os tipos de vergalhao.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Acos Viga", "Informe um cobrimento maior que zero.", "Dados invalidos");
                return;
            }

            if (config.QuantidadeSuperior <= 0 && config.QuantidadeInferior <= 0 && config.QuantidadeLateral <= 0)
            {
                AppDialogService.ShowWarning("PM - Acos Viga", "Informe ao menos uma quantidade maior que zero.", "Dados invalidos");
                return;
            }

            if (config.ComprimentoGanchoCm < 0)
            {
                AppDialogService.ShowWarning("PM - Acos Viga", "O comprimento da dobra nao pode ser negativo.", "Dados invalidos");
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
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }
    }
}
