using System.Globalization;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfBeamStirrupsWindow : Window
    {
        public PfBeamStirrupsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
                cmbBarType.Items.Add(option);

            if (!PfRebarTypeCatalog.TrySelect(cmbBarType, "6.3 CA-50") && cmbBarType.Items.Count > 0)
                cmbBarType.SelectedIndex = 0;

            txtAmostra.Text = sampleElement == null
                ? "Selecione uma viga para usar a geometria real da secao."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfBeamStirrupsConfig BuildConfig()
        {
            return new PfBeamStirrupsConfig
            {
                BarTypeName = (cmbBarType.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                EspacamentoApoioCm = ParseDouble(txtEspApoio.Text, 12.0),
                EspacamentoCentralCm = ParseDouble(txtEspCentral.Text, 20.0),
                ComprimentoZonaApoioCm = ParseDouble(txtZonaApoio.Text, 60.0)
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfBeamStirrupsConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeName))
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Selecione um tipo de vergalhao.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0 || config.ComprimentoZonaApoioCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Informe cobrimento e zona de apoio maiores que zero.", "Dados invalidos");
                return;
            }

            if (config.EspacamentoApoioCm <= 0 && config.EspacamentoCentralCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Informe ao menos um espacamento maior que zero.", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        private static double ParseDouble(string text, double fallback)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }
    }
}
