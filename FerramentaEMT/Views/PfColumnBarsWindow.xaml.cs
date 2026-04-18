using System.Globalization;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfColumnBarsWindow : Window
    {
        public PfColumnBarsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
                cmbBarType.Items.Add(option);

            if (!PfRebarTypeCatalog.TrySelect(cmbBarType, "10 CA-50") && cmbBarType.Items.Count > 0)
                cmbBarType.SelectedIndex = 0;

            txtAmostra.Text = sampleElement == null
                ? "Selecione um pilar para usar a geometria real da secao."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfColumnBarsConfig BuildConfig()
        {
            return new PfColumnBarsConfig
            {
                BarTypeName = (cmbBarType.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                QuantidadeLargura = ParseInt(txtQtdLargura.Text, 2),
                QuantidadeProfundidade = ParseInt(txtQtdProfundidade.Text, 2)
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfColumnBarsConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeName))
            {
                AppDialogService.ShowWarning("PM - Acos Pilar", "Selecione um tipo de vergalhao.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Acos Pilar", "Informe um cobrimento maior que zero.", "Dados invalidos");
                return;
            }

            if (config.QuantidadeLargura <= 0 || config.QuantidadeProfundidade <= 0)
            {
                AppDialogService.ShowWarning("PM - Acos Pilar", "Informe quantidades maiores que zero.", "Dados invalidos");
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
