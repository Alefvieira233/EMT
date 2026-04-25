using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class PfBeamStirrupsWindow : Window
    {
        private readonly Document _doc;

        public PfBeamStirrupsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _doc = doc;

            foreach (PfRebarShapeOption option in PfRebarShapeCatalog.LoadStirrupTieShapes(doc))
                cmbShape.Items.Add(option);

            cmbShape.SelectedIndex = 0;
            cmbShape.SelectionChanged += CmbShape_SelectionChanged;

            double comprimentoCm = sampleElement is FamilyInstance beam
                ? PfRebarService.GetBeamLengthCm(beam)
                : 0.0;
            txtComprimentoViga.Text = comprimentoCm > 0.0
                ? Format(comprimentoCm)
                : "Detectado apos selecionar";
            txtAmostra.Text = sampleElement == null
                ? "Selecione uma viga para usar a geometria real da secao."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
            UpdateShapePreview();
        }

        public PfBeamStirrupsConfig BuildConfig()
        {
            return new PfBeamStirrupsConfig
            {
                ShapeName = (cmbShape.SelectedItem as PfRebarShapeOption)?.Name ?? string.Empty,
                DiametroMm = ParseDouble(txtDiametro.Text, 6.3),
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                EspacamentoCm = ParseDouble(txtEspacamento.Text, 12.0),
                Dobra = cmbDobra.SelectedIndex == 1
                    ? PfStirrupHookAngle.Graus90
                    : PfStirrupHookAngle.Graus135
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfBeamStirrupsConfig config = BuildConfig();
            if (config.DiametroMm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Informe um diametro de barra maior que zero.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Informe cobrimento maior que zero.", "Dados invalidos");
                return;
            }

            if (config.EspacamentoCm <= 0)
            {
                AppDialogService.ShowWarning("PM - Estribos Viga", "Informe espacamento maior que zero.", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        private static double ParseDouble(string text, double fallback)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void CmbShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateShapePreview();
        }

        private void UpdateShapePreview()
        {
            PfRebarShapeOption option = cmbShape.SelectedItem as PfRebarShapeOption;
            if (option == null || option.IsAutomatic)
            {
                imgShapePreview.Source = null;
                panelShapePreviewFallback.Visibility = System.Windows.Visibility.Visible;
                txtShapePreviewTag.Text = "Automatico";
                txtShapePreviewCaption.Text = "O estribo sera montado pela geometria da peca e pela dobra selecionada.";
                txtShapePreviewTitle.Text = "Automatico";
                txtShapePreviewHint.Text = "O add-in cria o estribo pela geometria da peca e pela dobra selecionada.";
                return;
            }

            imgShapePreview.Source = PfRebarShapePreviewService.LoadPreview(_doc, option);
            panelShapePreviewFallback.Visibility = imgShapePreview.Source == null
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            txtShapePreviewTag.Text = option.DisplayName;
            txtShapePreviewCaption.Text = "Formato escolhido no projeto para tentar reaproveitar a dobra existente.";
            txtShapePreviewTitle.Text = $"Formato {option.DisplayName}";
            txtShapePreviewHint.Text = "Depois de criar o estribo automatico, o add-in tenta aplicar este formato do projeto.";
        }
    }
}
