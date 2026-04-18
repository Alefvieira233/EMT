using System.Globalization;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfConsoloRebarWindow : Window
    {
        public PfConsoloRebarWindow(Document doc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbTirante.Items.Add(option);
                cmbSuspensao.Items.Add(option);
                cmbEstriboVertical.Items.Add(option);
                cmbEstriboHorizontal.Items.Add(option);
            }

            if (!PfRebarTypeCatalog.TrySelect(cmbTirante, "16 CA-50") && cmbTirante.Items.Count > 0)
                cmbTirante.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbSuspensao, "8 CA-50") && cmbSuspensao.Items.Count > 0)
                cmbSuspensao.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbEstriboVertical, "16 CA-50") && cmbEstriboVertical.Items.Count > 0)
                cmbEstriboVertical.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbEstriboHorizontal, "8 CA-50") && cmbEstriboHorizontal.Items.Count > 0)
                cmbEstriboHorizontal.SelectedIndex = 0;

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public PfConsoloRebarConfig BuildConfig()
        {
            return new PfConsoloRebarConfig
            {
                BarTypeTiranteName = (cmbTirante.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                NumeroTirantes = ParseInt(txtQtdTirante.Text, 4),
                ComprimentoTiranteCm = ParseDouble(txtCompTirante.Text, 100.0),
                BarTypeSuspensaoName = (cmbSuspensao.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                NumeroSuspensoes = ParseInt(txtQtdSuspensao.Text, 4),
                ComprimentoSuspensaoCm = ParseDouble(txtCompSuspensao.Text, 60.0),
                BarTypeEstriboVerticalName = (cmbEstriboVertical.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                QuantidadeEstribosVerticais = ParseInt(txtQtdEstriboVertical.Text, 5),
                BarTypeEstriboHorizontalName = (cmbEstriboHorizontal.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                QuantidadeEstribosHorizontais = ParseInt(txtQtdEstriboHorizontal.Text, 5)
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfConsoloRebarConfig config = BuildConfig();
            if (string.IsNullOrWhiteSpace(config.BarTypeTiranteName) ||
                string.IsNullOrWhiteSpace(config.BarTypeSuspensaoName) ||
                string.IsNullOrWhiteSpace(config.BarTypeEstriboVerticalName) ||
                string.IsNullOrWhiteSpace(config.BarTypeEstriboHorizontalName))
            {
                AppDialogService.ShowWarning("PF - Acos Consolo", "Selecione todos os tipos de vergalhao.", "Dados incompletos");
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
            return NumberParsing.ParseDoubleOrDefault(text, fallback);
        }
    }
}
