using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfColumnStirrupsWindow : Window
    {
        public PfColumnStirrupsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
                cmbBarType.Items.Add(option);

            if (!PfRebarTypeCatalog.TrySelect(cmbBarType, "6.3 CA-50") && cmbBarType.Items.Count > 0)
                cmbBarType.SelectedIndex = 0;

            txtAmostra.Text = sampleElement == null
                ? "Selecione um pilar para a rotina usar a geometria real como base."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfColumnStirrupsConfig BuildConfig()
        {
            return new PfColumnStirrupsConfig
            {
                BarTypeName = (cmbBarType.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                EspacamentoInferiorCm = ParseDouble(txtEspInferior.Text, 12.0),
                EspacamentoCentralCm = ParseDouble(txtEspCentral.Text, 20.0),
                EspacamentoSuperiorCm = ParseDouble(txtEspSuperior.Text, 12.0),
                AlturaZonaExtremidadeCm = ParseDouble(txtAlturaZona.Text, 60.0)
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfColumnStirrupsConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeName))
            {
                AppDialogService.ShowWarning("PM - Estribos Pilar", "Selecione um tipo de vergalhao.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0 || config.AlturaZonaExtremidadeCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Pilar", "Informe cobrimento e altura de zona maiores que zero.", "Dados invalidos");
                return;
            }

            if (config.EspacamentoInferiorCm <= 0 && config.EspacamentoCentralCm <= 0 && config.EspacamentoSuperiorCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Pilar", "Informe ao menos um espacamento maior que zero.", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        private static double ParseDouble(string text, double fallback)
        {
            return NumberParsing.ParseDoubleOrDefault(text, fallback);
        }
    }
}
