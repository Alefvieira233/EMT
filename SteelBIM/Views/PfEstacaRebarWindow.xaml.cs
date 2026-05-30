using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Utils;
using WpfEllipse = System.Windows.Shapes.Ellipse;

namespace SteelBIM.Views
{
    public partial class PfEstacaRebarWindow : Window
    {
        private const double PreviewPadding = 24.0;
        private readonly Document _doc;
        private readonly PfRebarSectionPreview _sectionPreview;
        private bool _previewReady;

        public PfEstacaRebarWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _doc = doc;

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbBarType.Items.Add(option);
                cmbEstriboBarType.Items.Add(option);
            }

            foreach (PfRebarShapeOption option in PfRebarShapeCatalog.LoadStirrupTieShapes(doc))
                cmbEstriboShape.Items.Add(option);

            if (!PfRebarTypeCatalog.TrySelect(cmbBarType, "12.5 CA-50") && cmbBarType.Items.Count > 0)
                cmbBarType.SelectedIndex = 0;

            if (!PfRebarTypeCatalog.TrySelect(cmbEstriboBarType, "6.3 CA-60") &&
                !PfRebarTypeCatalog.TrySelect(cmbEstriboBarType, "6.3 CA-50") &&
                cmbEstriboBarType.Items.Count > 0)
                cmbEstriboBarType.SelectedIndex = 0;

            if (!PfRebarShapeCatalog.TrySelect(cmbEstriboShape, "33"))
                cmbEstriboShape.SelectedIndex = 0;
            cmbEstriboDobra.SelectedIndex = 0;
            ConfigureLapInputs();

            if (sampleElement is FamilyInstance samplePile)
            {
                try
                {
                    _sectionPreview = PfRebarService.BuildColumnSectionPreview(samplePile);
                }
                catch
                {
                    _sectionPreview = null;
                }

                double lengthCm = 0.0;
                try
                { lengthCm = PfRebarService.GetColumnLengthCm(samplePile); }
                catch { }

                string hostInfo = PfElementService.GetHostPreview(sampleElement);
                if (lengthCm > 0)
                    hostInfo += $" — comprimento detectado: {lengthCm:0.#} cm";
                txtAmostra.Text = hostInfo;

                if (lengthCm > 1200.0)
                    chkTraspasse.IsChecked = true;
            }
            else
            {
                txtAmostra.Text = "Selecione uma estaca antes de abrir a janela para usar a geometria real da seção.";
            }

            txtQuantidade.TextChanged += (_, __) => UpdatePreview();
            txtCover.TextChanged += (_, __) => UpdatePreview();
            cmbBarType.SelectionChanged += (_, __) => UpdatePreview();
            chkInserirEstribos.Checked += (_, __) => UpdatePreview();
            chkInserirEstribos.Unchecked += (_, __) => UpdatePreview();
            cmbEstriboBarType.SelectionChanged += (_, __) => UpdatePreview();
            txtEstriboEspacamento.TextChanged += (_, __) => UpdatePreview();
            cmbEstriboShape.SelectionChanged += (_, __) => UpdatePreview();
            cmbEstriboDobra.SelectionChanged += (_, __) => UpdatePreview();
            chkTraspasse.Checked += (_, __) => UpdatePreview();
            chkTraspasse.Unchecked += (_, __) => UpdatePreview();
            cmbFck.SelectionChanged += (_, __) => UpdatePreview();
            cmbSteel.SelectionChanged += (_, __) => UpdatePreview();
            cmbBondZone.SelectionChanged += (_, __) => UpdatePreview();
            cmbAnchorage.SelectionChanged += (_, __) => UpdatePreview();
            txtSplicePercent.TextChanged += (_, __) => UpdatePreview();
            txtMaxBarLength.TextChanged += (_, __) => UpdatePreview();
            txtBarSpacing.TextChanged += (_, __) => UpdatePreview();

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            _previewReady = true;
            UpdatePreview();
        }

        public PfEstacaRebarConfig BuildConfig()
        {
            PfEstacaRebarConfig config = new PfEstacaRebarConfig
            {
                BarTypeName = (cmbBarType.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 5.0),
                QuantidadeBarras = ParseInt(txtQuantidade.Text, 6),
                InserirEstribos = chkInserirEstribos.IsChecked == true
            };

            config.Estribos.ShapeName = (cmbEstriboShape.SelectedItem as PfRebarShapeOption)?.Name ?? string.Empty;
            config.Estribos.BarTypeName = (cmbEstriboBarType.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty;
            config.Estribos.DiametroMm = GetSelectedBarDiameterMm(cmbEstriboBarType);
            config.Estribos.CobrimentoCm = config.CobrimentoCm;
            config.Estribos.EspacamentoCm = ParseDouble(txtEstriboEspacamento.Text, 12.0);
            config.Estribos.Dobra = cmbEstriboDobra.SelectedIndex == 1
                ? PfStirrupHookAngle.Graus90
                : PfStirrupHookAngle.Graus135;

            FillLapConfig(config.Traspasse);
            return config;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            PfEstacaRebarConfig config = BuildConfig();

            if (string.IsNullOrWhiteSpace(config.BarTypeName))
            {
                AppDialogService.ShowWarning("PF - Armadura de Estaca", "Selecione um tipo de vergalhão.", "Dados incompletos");
                return;
            }

            if (config.CobrimentoCm <= 0)
            {
                AppDialogService.ShowWarning("PF - Armadura de Estaca", "Informe um cobrimento maior que zero.", "Dados inválidos");
                return;
            }

            if (config.QuantidadeBarras <= 0)
            {
                AppDialogService.ShowWarning("PF - Armadura de Estaca", "Informe uma quantidade de barras maior que zero.", "Dados inválidos");
                return;
            }

            if (config.InserirEstribos)
            {
                if (string.IsNullOrWhiteSpace(config.Estribos.BarTypeName))
                {
                    AppDialogService.ShowWarning("PF - Armadura de Estaca", "Selecione um tipo de vergalhão para o estribo.", "Dados incompletos");
                    return;
                }

                if (config.Estribos.EspacamentoCm <= 0)
                {
                    AppDialogService.ShowWarning("PF - Armadura de Estaca", "Informe um espaçamento de estribo maior que zero.", "Dados inválidos");
                    return;
                }

            }

            string lapError = ValidateLapConfig(config.Traspasse);
            if (!string.IsNullOrWhiteSpace(lapError))
            {
                AppDialogService.ShowWarning("PF - Armadura de Estaca", lapError, "Traspasse inválido");
                return;
            }

            lapError = ValidateLapCalculation(config.Traspasse, GetSelectedBarDiameterMm(cmbBarType));
            if (!string.IsNullOrWhiteSpace(lapError))
            {
                AppDialogService.ShowWarning("PF - Armadura de Estaca", lapError, "Traspasse inválido");
                return;
            }

            DialogResult = true;
        }

        private void ConfigureLapInputs()
        {
            foreach (int fck in new[] { 20, 25, 30, 35, 40, 45, 50 })
                cmbFck.Items.Add(fck.ToString(CultureInfo.InvariantCulture));

            cmbSteel.Items.Add("CA-50");
            cmbSteel.Items.Add("CA-60");
            cmbSteel.Items.Add("CA-25");

            cmbBondZone.Items.Add("Boa");
            cmbBondZone.Items.Add("Ruim");

            cmbAnchorage.Items.Add("Reta");
            cmbAnchorage.Items.Add("Gancho 90");
            cmbAnchorage.Items.Add("Gancho 180");
            cmbAnchorage.Items.Add("Gancho 45");

            cmbFck.SelectedIndex = 1;
            cmbSteel.SelectedIndex = 0;
            cmbBondZone.SelectedIndex = 0;
            cmbAnchorage.SelectedIndex = 0;
        }

        private void FillLapConfig(PfLapSpliceConfig lap)
        {
            lap.Enabled = chkTraspasse.IsChecked == true;
            lap.ConcreteFckMpa = ParseDouble(cmbFck.SelectedItem as string, 25.0);
            lap.SteelFykMpa = SelectedSteelFyk();
            lap.BarSurface = SelectedBarSurface();
            lap.BondZone = cmbBondZone.SelectedIndex == 1 ? PfBondZone.Ruim : PfBondZone.Boa;
            lap.AnchorageType = SelectedAnchorageType();
            lap.SplicePercentage = ParseDouble(txtSplicePercent.Text, 50.0);
            lap.MaxBarLengthCm = ParseDouble(txtMaxBarLength.Text, 1200.0);
            lap.BarSpacingCm = ParseDouble(txtBarSpacing.Text, 8.0);
        }

        private double SelectedSteelFyk()
        {
            string value = cmbSteel.SelectedItem as string ?? "CA-50";
            if (value.Contains("60"))
                return 600.0;
            if (value.Contains("25"))
                return 250.0;
            return 500.0;
        }

        private PfBarSurfaceType SelectedBarSurface()
        {
            string value = cmbSteel.SelectedItem as string ?? "CA-50";
            if (value.Contains("25"))
                return PfBarSurfaceType.Lisa;
            if (value.Contains("60"))
                return PfBarSurfaceType.Entalhada;
            return PfBarSurfaceType.Nervurada;
        }

        private PfAnchorageType SelectedAnchorageType()
        {
            switch (cmbAnchorage.SelectedIndex)
            {
                case 1:
                    return PfAnchorageType.Gancho90;
                case 2:
                    return PfAnchorageType.Gancho180;
                case 3:
                    return PfAnchorageType.Gancho45;
                default:
                    return PfAnchorageType.Reta;
            }
        }

        private static string ValidateLapConfig(PfLapSpliceConfig lap)
        {
            if (lap == null || !lap.Enabled)
                return string.Empty;
            if (lap.ConcreteFckMpa <= 0.0)
                return "Informe um fck maior que zero.";
            if (lap.SteelFykMpa <= 0.0)
                return "Informe um aço válido.";
            if (lap.SplicePercentage <= 0.0 || lap.SplicePercentage > 100.0)
                return "Informe uma porcentagem entre 0 e 100.";
            if (lap.BarSpacingCm <= 0.0)
                return "Informe um espaçamento entre barras maior que zero.";
            if (lap.MaxBarLengthCm <= 0.0)
                return "Informe um comprimento máximo de barra maior que zero.";
            return string.Empty;
        }

        private static string ValidateLapCalculation(PfLapSpliceConfig lap, double diameterMm)
        {
            if (lap == null || !lap.Enabled)
                return string.Empty;
            try
            {
                PfAnchorageResult result = PfNbr6118AnchorageService.Calculate(diameterMm, lap);
                if (lap.MaxBarLengthCm <= result.SpliceLengthCm + 30.0)
                    return $"A barra maxima precisa ser maior que o traspasse calculado ({Format(result.SpliceLengthCm)} cm).";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return string.Empty;
        }

        private void UpdatePreview()
        {
            if (!_previewReady)
                return;

            canvasSecao.Children.Clear();
            PfEstacaRebarConfig config = BuildConfig();
            DrawSection(config);
            UpdateLapPreview(config);
        }

        private void DrawSection(PfEstacaRebarConfig config)
        {
            double canvasSize = canvasSecao.Width;
            bool hasPreview = _sectionPreview != null && _sectionPreview.RadiusCm > 0;

            double radiusCm = hasPreview
                ? _sectionPreview.RadiusCm
                : canvasSize / 2.0 / 2.5;

            double scale = (canvasSize - (PreviewPadding * 2.0)) / Math.Max(1.0, radiusCm * 2.0);
            double radiusPx = radiusCm * scale;
            double centerPx = canvasSize / 2.0;

            canvasSecao.Children.Add(CreateCircle(centerPx, centerPx, radiusPx, Brushes.Transparent, "#3A4450", 2.0));

            double baseCover = Math.Max(0.0, config.CobrimentoCm);
            double stirrupDiameterCm = config.InserirEstribos ? config.Estribos.DiametroMm / 10.0 : 0.0;
            double stirrupRadiusCm = GetStirrupCenterRadiusCm(config, radiusCm, baseCover, stirrupDiameterCm);
            double barRadiusCm = GetSelectedBarDiameterMm(cmbBarType) / 20.0;
            double usefulRadiusCm = GetLongitudinalBarCenterRadiusCm(config, radiusCm, baseCover, stirrupDiameterCm, barRadiusCm);

            if (config.InserirEstribos && stirrupRadiusCm > 0)
                canvasSecao.Children.Add(CreateCircle(centerPx, centerPx, stirrupRadiusCm * scale, Brushes.Transparent, "#2D9CDB", 1.0));

            if (usefulRadiusCm > 0)
                canvasSecao.Children.Add(CreateCircle(centerPx, centerPx, usefulRadiusCm * scale, Brushes.Transparent, "#D28B00", 1.0));

            int count = Math.Max(0, config.QuantidadeBarras);
            int invalidCount = 0;
            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2.0 * i) / Math.Max(1, count);
                bool valid = usefulRadiusCm > 0;
                double dotRadiusCm = valid ? usefulRadiusCm : radiusCm * 0.6;
                double bx = centerPx + (Math.Cos(angle) * dotRadiusCm * scale);
                double by = centerPx + (Math.Sin(angle) * dotRadiusCm * scale);
                if (!valid)
                    invalidCount++;
                DrawBarDot(bx, by, i + 1, valid);
            }

            if (hasPreview)
                txtPreviewBounds.Text = $"Seção circular Ø {Format(radiusCm * 2.0)} cm | Raio útil {Format(Math.Max(0, usefulRadiusCm))} cm";
            else
                txtPreviewBounds.Text = "Seção estimada — selecione uma estaca para geometria real.";

            if (count == 0)
                txtPreviewStatus.Text = "Nenhuma barra configurada.";
            else if (invalidCount > 0)
                txtPreviewStatus.Text = $"{invalidCount} barra(s) fora do cobrimento.";
            else if (config.InserirEstribos)
                txtPreviewStatus.Text = $"{count} barra(s) com reserva automática para o estribo circular.";
            else
                txtPreviewStatus.Text = $"{count} barra(s) distribuídas polarmante.";
        }

        private static double GetStirrupCenterRadiusCm(PfEstacaRebarConfig config, double hostRadiusCm, double baseCoverCm, double stirrupDiameterCm)
        {
            if (config?.InserirEstribos != true)
                return hostRadiusCm - baseCoverCm;

            return hostRadiusCm - baseCoverCm;
        }

        private static double GetLongitudinalBarCenterRadiusCm(
            PfEstacaRebarConfig config,
            double hostRadiusCm,
            double baseCoverCm,
            double stirrupDiameterCm,
            double longitudinalBarRadiusCm)
        {
            double radius = hostRadiusCm - Math.Max(0.0, baseCoverCm) - Math.Max(0.0, longitudinalBarRadiusCm);
            if (config?.InserirEstribos == true)
                radius -= Math.Max(0.0, stirrupDiameterCm);

            return radius;
        }

        private void DrawBarDot(double cx, double cy, int index, bool valid)
        {
            double radius = valid ? 5.0 : 6.0;
            WpfEllipse circle = new WpfEllipse
            {
                Width = radius * 2.0,
                Height = radius * 2.0,
                Fill = BrushFromHex(valid ? "#2F80ED" : "#D14343"),
                Stroke = BrushFromHex("#FFFFFF"),
                StrokeThickness = 1.0
            };
            Canvas.SetLeft(circle, cx - radius);
            Canvas.SetTop(circle, cy - radius);
            canvasSecao.Children.Add(circle);

            TextBlock label = new TextBlock
            {
                Text = index.ToString(CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = BrushFromHex("#17212B")
            };
            Canvas.SetLeft(label, cx + radius + 2.0);
            Canvas.SetTop(label, cy - radius - 2.0);
            canvasSecao.Children.Add(label);
        }

        private void UpdateLapPreview(PfEstacaRebarConfig config)
        {
            if (config == null || !config.Traspasse.Enabled)
            {
                txtTraspassePreview.Text = string.Empty;
                return;
            }
            try
            {
                double diameterMm = GetSelectedBarDiameterMm(cmbBarType);
                PfAnchorageResult result = PfNbr6118AnchorageService.Calculate(diameterMm, config.Traspasse);
                txtTraspassePreview.Text = $"l0 {Format(result.SpliceLengthCm)} cm | lb,nec {Format(result.RequiredAnchorageCm)} cm";
            }
            catch
            {
                txtTraspassePreview.Text = "Não foi possível calcular.";
            }
        }

        private double GetSelectedBarDiameterMm(ComboBox combo)
        {
            if (combo?.SelectedItem is PfRebarBarTypeOption option &&
                _doc?.GetElement(option.Id) is RebarBarType barType)
            {
                return UnitUtils.ConvertFromInternalUnits(barType.BarNominalDiameter, UnitTypeId.Millimeters);
            }
            return 12.5;
        }

        private static WpfEllipse CreateCircle(double centerX, double centerY, double radiusPx, Brush fill, string stroke, double thickness)
        {
            double r = Math.Max(0.0, radiusPx);
            WpfEllipse ellipse = new WpfEllipse
            {
                Width = r * 2.0,
                Height = r * 2.0,
                Fill = fill,
                Stroke = BrushFromHex(stroke),
                StrokeThickness = thickness
            };
            Canvas.SetLeft(ellipse, centerX - r);
            Canvas.SetTop(ellipse, centerY - r);
            return ellipse;
        }

        private static int ParseInt(string text, int fallback)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private static double ParseDouble(string text, double fallback)
        {
            // v2.8.9 FIX: usar o helper oficial (Invariant primeiro, depois pt-BR). Antes
            // tentava CurrentCulture PRIMEIRO — viola a "regra de ouro" do projeto
            // (SteelBIM.Utils.NumberParsing): em PC pt-BR "1.5" digitado quebrava e "1,000"
            // virava ambiguo; alem disso text.Replace(',','.') lancava NRE se text fosse null.
            return SteelBIM.Utils.NumberParsing.ParseDoubleOrDefault(text, fallback);
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static Brush BrushFromHex(string value)
        {
            return (Brush)new BrushConverter().ConvertFromString(value);
        }
    }
}
