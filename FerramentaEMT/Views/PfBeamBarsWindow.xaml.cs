using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace FerramentaEMT.Views
{
    public partial class PfBeamBarsWindow : Window
    {
        private const double PreviewPadding = 24.0;
        private readonly Document _doc;
        private readonly PfRebarSectionPreview _sectionPreview;
        private bool _previewReady;

        public string LastCoordinateError { get; private set; } = string.Empty;

        public PfBeamBarsWindow(Document doc, Element sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _doc = doc;

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbBarSup.Items.Add(option);
                cmbBarInf.Items.Add(option);
                cmbBarLat.Items.Add(option);
            }

            cmbModoPonta.Items.Add("Reta");
            cmbModoPonta.Items.Add("Dobra interna");
            cmbModoPonta.SelectedIndex = 1;
            tabModo.SelectedIndex = 0;
            ConfigureLapInputs();

            if (!PfRebarTypeCatalog.TrySelect(cmbBarSup, "8 CA-50") && cmbBarSup.Items.Count > 0)
                cmbBarSup.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarInf, "8 CA-50") && cmbBarInf.Items.Count > 0)
                cmbBarInf.SelectedIndex = 0;
            if (!PfRebarTypeCatalog.TrySelect(cmbBarLat, "8 CA-50") && cmbBarLat.Items.Count > 0)
                cmbBarLat.SelectedIndex = 0;

            if (sampleElement is FamilyInstance sampleBeam)
                _sectionPreview = PfRebarService.BuildBeamSectionPreview(sampleBeam);

            txtAmostra.Text = sampleElement == null
                ? "Selecione uma viga para usar a geometria real da secao."
                : PfElementService.GetHostPreview(sampleElement);

            txtCover.TextChanged += (_, __) => UpdatePreview();
            txtQtdSup.TextChanged += (_, __) => UpdatePreview();
            txtQtdInf.TextChanged += (_, __) => UpdatePreview();
            txtQtdLat.TextChanged += (_, __) => UpdatePreview();
            txtCoordenadas.TextChanged += (_, __) => UpdatePreview();
            chkTraspasse.Checked += (_, __) => UpdatePreview();
            chkTraspasse.Unchecked += (_, __) => UpdatePreview();
            cmbFck.SelectionChanged += (_, __) => UpdatePreview();
            cmbSteel.SelectionChanged += (_, __) => UpdatePreview();
            cmbBondZone.SelectionChanged += (_, __) => UpdatePreview();
            cmbAnchorage.SelectionChanged += (_, __) => UpdatePreview();
            txtSplicePercent.TextChanged += (_, __) => UpdatePreview();
            txtMaxBarLength.TextChanged += (_, __) => UpdatePreview();
            txtBarSpacing.TextChanged += (_, __) => UpdatePreview();
            tabModo.SelectionChanged += (_, __) => UpdatePreview();

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            _previewReady = true;
            UpdatePreview();
        }

        public PfBeamBarsConfig BuildConfig()
        {
            PfBeamBarsConfig config = new PfBeamBarsConfig
            {
                BarTypeSuperiorName = (cmbBarSup.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeInferiorName = (cmbBarInf.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                BarTypeLateralName = (cmbBarLat.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty,
                CobrimentoCm = ParseDouble(txtCover.Text, 3.0),
                ModoLancamento = tabModo.SelectedIndex == 1
                    ? PfRebarPlacementMode.Coordenadas
                    : PfRebarPlacementMode.Automatico,
                QuantidadeSuperior = ParseInt(txtQtdSup.Text, 2),
                QuantidadeInferior = ParseInt(txtQtdInf.Text, 2),
                QuantidadeLateral = ParseInt(txtQtdLat.Text, 0),
                ComprimentoGanchoCm = ParseDouble(txtGancho.Text, 10.0),
                ModoPonta = cmbModoPonta.SelectedIndex == 0 ? PfBeamBarEndMode.Reta : PfBeamBarEndMode.DobraInterna
            };

            FillLapConfig(config.Traspasse);

            config.Coordenadas.AddRange(ParseCoordinates(config, txtCoordenadas.Text, out string error));
            LastCoordinateError = error;
            return config;
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

            string lapError = ValidateLapConfig(config.Traspasse);
            if (!string.IsNullOrWhiteSpace(lapError))
            {
                AppDialogService.ShowWarning("PM - Acos Viga", lapError, "Traspasse invalido");
                return;
            }

            lapError = ValidateLapCalculation(config.Traspasse, GetMaxSelectedBarDiameterMm());
            if (!string.IsNullOrWhiteSpace(lapError))
            {
                AppDialogService.ShowWarning("PM - Acos Viga", lapError, "Traspasse invalido");
                return;
            }

            if (config.ModoLancamento == PfRebarPlacementMode.Automatico &&
                config.QuantidadeSuperior <= 0 && config.QuantidadeInferior <= 0 && config.QuantidadeLateral <= 0)
            {
                AppDialogService.ShowWarning("PM - Acos Viga", "Informe ao menos uma quantidade maior que zero.", "Dados invalidos");
                return;
            }

            if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas)
            {
                if (!string.IsNullOrWhiteSpace(LastCoordinateError))
                {
                    AppDialogService.ShowWarning("PM - Acos Viga", LastCoordinateError, "Coordenadas invalidas");
                    return;
                }

                if (config.Coordenadas.Count == 0)
                {
                    AppDialogService.ShowWarning("PM - Acos Viga", "Informe ao menos uma coordenada de barra.", "Dados invalidos");
                    return;
                }

                string coordinateBoundsError = ValidateCoordinateBounds(config);
                if (!string.IsNullOrWhiteSpace(coordinateBoundsError))
                {
                    AppDialogService.ShowWarning("PM - Acos Viga", coordinateBoundsError, "Coordenadas fora da secao");
                    return;
                }
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
            return TryParseDouble(text, out double value)
                ? value
                : fallback;
        }

        private static List<PfBeamBarCoordinate> ParseCoordinates(PfBeamBarsConfig config, string text, out string error)
        {
            error = string.Empty;
            List<PfBeamBarCoordinate> coordinates = new List<PfBeamBarCoordinate>();

            if (string.IsNullOrWhiteSpace(text))
                return coordinates;

            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts.Length > 3)
                {
                    error = $"Linha {i + 1}: use o formato X(cm); Y(cm); Tipo.";
                    return new List<PfBeamBarCoordinate>();
                }

                if (!TryParseDouble(parts[0].Trim(), out double xCm) ||
                    !TryParseDouble(parts[1].Trim(), out double yCm))
                {
                    error = $"Linha {i + 1}: X e Y precisam ser numeros em centimetros.";
                    return new List<PfBeamBarCoordinate>();
                }

                string typeName = ResolveCoordinateBarType(
                    config,
                    parts.Length == 3 ? parts[2].Trim() : "Superior",
                    out string posicao);
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    error = $"Linha {i + 1}: tipo deve ser Superior, Inferior ou Lateral.";
                    return new List<PfBeamBarCoordinate>();
                }

                coordinates.Add(new PfBeamBarCoordinate
                {
                    BarTypeName = typeName,
                    Posicao = posicao,
                    XCm = xCm,
                    YCm = yCm
                });
            }

            return coordinates;
        }

        private static string ResolveCoordinateBarType(PfBeamBarsConfig config, string value, out string posicao)
        {
            posicao = string.Empty;
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized == "s" ||
                normalized == "sup" ||
                normalized == "superior")
            {
                posicao = "Superior";
                return config.BarTypeSuperiorName;
            }

            if (normalized == "i" ||
                normalized == "inf" ||
                normalized == "inferior")
            {
                posicao = "Inferior";
                return config.BarTypeInferiorName;
            }

            if (normalized == "l" ||
                normalized == "lat" ||
                normalized == "lateral")
            {
                posicao = "Lateral";
                return config.BarTypeLateralName;
            }

            return string.Empty;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return double.TryParse(
                text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
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
                return "Informe um aco valido.";
            if (lap.SplicePercentage <= 0.0 || lap.SplicePercentage > 100.0)
                return "Informe uma porcentagem de barras emendadas entre 0 e 100.";
            if (lap.BarSpacingCm <= 0.0)
                return "Informe um espacamento entre barras maior que zero.";
            if (lap.MaxBarLengthCm <= 0.0)
                return "Informe um comprimento maximo de barra maior que zero.";

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

            if (_sectionPreview == null || _sectionPreview.WidthCm <= 0 || _sectionPreview.HeightCm <= 0)
            {
                txtPreviewBounds.Text = "Secao nao detectada.";
                txtPreviewStatus.Text = "Selecione uma viga antes de abrir a janela.";
                return;
            }

            PfBeamBarsConfig config = BuildConfig();
            DrawSection(config);
            UpdateLapPreview(config);
        }

        private string ValidateCoordinateBounds(PfBeamBarsConfig config)
        {
            if (_sectionPreview == null)
                return string.Empty;

            double cover = Math.Max(0.0, config.CobrimentoCm);
            double minX = cover;
            double maxX = _sectionPreview.WidthCm - cover;
            double minY = cover;
            double maxY = _sectionPreview.HeightCm - cover;

            if (maxX <= minX || maxY <= minY)
                return "O cobrimento informado elimina a area util da secao.";

            for (int i = 0; i < config.Coordenadas.Count; i++)
            {
                PfBeamBarCoordinate coordinate = config.Coordenadas[i];
                if (coordinate.XCm < minX ||
                    coordinate.XCm > maxX ||
                    coordinate.YCm < minY ||
                    coordinate.YCm > maxY)
                {
                    return $"Linha {i + 1}: X={Format(coordinate.XCm)} cm, Y={Format(coordinate.YCm)} cm fora da area util.";
                }
            }

            return string.Empty;
        }

        private void DrawSection(PfBeamBarsConfig config)
        {
            double canvasWidth = canvasSecao.Width;
            double canvasHeight = canvasSecao.Height;
            double scale = Math.Min(
                (canvasWidth - (PreviewPadding * 2.0)) / Math.Max(1.0, _sectionPreview.WidthCm),
                (canvasHeight - (PreviewPadding * 2.0)) / Math.Max(1.0, _sectionPreview.HeightCm));

            double sectionLeft = ToCanvasX(_sectionPreview.MinXCm, scale);
            double sectionTop = ToCanvasY(_sectionPreview.MaxYCm, scale);
            double sectionWidth = _sectionPreview.WidthCm * scale;
            double sectionHeight = _sectionPreview.HeightCm * scale;

            canvasSecao.Children.Add(CreateRectangle(sectionLeft, sectionTop, sectionWidth, sectionHeight, Brushes.Transparent, "#3A4450", 2.0));

            double cover = Math.Max(0.0, config.CobrimentoCm);
            double usefulMinX = _sectionPreview.MinXCm + cover;
            double usefulMaxX = _sectionPreview.MaxXCm - cover;
            double usefulMinY = _sectionPreview.MinYCm + cover;
            double usefulMaxY = _sectionPreview.MaxYCm - cover;
            bool hasUsefulArea = usefulMaxX > usefulMinX && usefulMaxY > usefulMinY;

            if (hasUsefulArea)
            {
                canvasSecao.Children.Add(CreateRectangle(
                    ToCanvasX(usefulMinX, scale),
                    ToCanvasY(usefulMaxY, scale),
                    (usefulMaxX - usefulMinX) * scale,
                    (usefulMaxY - usefulMinY) * scale,
                    Brushes.Transparent,
                    "#D28B00",
                    1.0));
            }

            DrawAxis(scale);

            List<PreviewBar> bars = BuildPreviewBars(config, usefulMinX, usefulMaxX, usefulMinY, usefulMaxY);
            int invalidCount = 0;
            for (int i = 0; i < bars.Count; i++)
            {
                PreviewBar bar = bars[i];
                bool insideUsefulArea = hasUsefulArea &&
                    bar.XCm >= usefulMinX &&
                    bar.XCm <= usefulMaxX &&
                    bar.YCm >= usefulMinY &&
                    bar.YCm <= usefulMaxY;
                bool insideSection =
                    bar.XCm >= _sectionPreview.MinXCm &&
                    bar.XCm <= _sectionPreview.MaxXCm &&
                    bar.YCm >= _sectionPreview.MinYCm &&
                    bar.YCm <= _sectionPreview.MaxYCm;

                if (!insideUsefulArea)
                    invalidCount++;

                DrawBar(bar, i + 1, scale, insideSection && insideUsefulArea);
            }

            txtPreviewBounds.Text =
                $"Secao {Format(_sectionPreview.WidthCm)} x {Format(_sectionPreview.HeightCm)} cm | " +
                $"X 0 a {Format(_sectionPreview.WidthCm)} | " +
                $"Y 0 a {Format(_sectionPreview.HeightCm)}";

            if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas &&
                !string.IsNullOrWhiteSpace(LastCoordinateError))
                txtPreviewStatus.Text = LastCoordinateError;
            else if (!hasUsefulArea)
                txtPreviewStatus.Text = "Cobrimento maior que a secao util.";
            else if (bars.Count == 0)
                txtPreviewStatus.Text = "Nenhuma barra para visualizar.";
            else if (invalidCount > 0)
                txtPreviewStatus.Text = $"{invalidCount} barra(s) fora do cobrimento.";
            else
                txtPreviewStatus.Text = $"{bars.Count} barra(s) posicionada(s) no corte.";
        }

        private void DrawAxis(double scale)
        {
            double originX = ToCanvasX(_sectionPreview.MinXCm, scale);
            double originY = ToCanvasY(_sectionPreview.MinYCm, scale);
            double endX = ToCanvasX(_sectionPreview.MaxXCm, scale);
            double endY = ToCanvasY(_sectionPreview.MaxYCm, scale);

            canvasSecao.Children.Add(CreateLine(originX, originY, endX, originY, "#1D4ED8", 2.0));
            canvasSecao.Children.Add(CreateLine(originX, originY, originX, endY, "#D14343", 2.0));
            AddAxisLabel("X", endX + 4.0, originY - 14.0, "#1D4ED8");
            AddAxisLabel("Y", originX + 4.0, endY - 2.0, "#D14343");
        }

        private void DrawBar(PreviewBar bar, int index, double scale, bool valid)
        {
            double radius = valid ? 5.0 : 6.0;
            double cx = ToCanvasX(bar.XCm, scale);
            double cy = ToCanvasY(bar.YCm, scale);
            Brush fill = valid ? GetBarBrush(bar.Posicao) : BrushFromHex("#D14343");
            Brush stroke = BrushFromHex("#FFFFFF");

            WpfEllipse circle = new WpfEllipse
            {
                Width = radius * 2.0,
                Height = radius * 2.0,
                Fill = fill,
                Stroke = stroke,
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

        private List<PreviewBar> BuildPreviewBars(
            PfBeamBarsConfig config,
            double usefulMinX,
            double usefulMaxX,
            double usefulMinY,
            double usefulMaxY)
        {
            if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas)
            {
                return config.Coordenadas
                    .Select(x => new PreviewBar(ToSectionX(x.XCm), ToSectionY(x.YCm), x.Posicao))
                    .ToList();
            }

            List<PreviewBar> bars = new List<PreviewBar>();
            foreach (double x in DistributePositions(config.QuantidadeSuperior, usefulMinX, usefulMaxX))
                bars.Add(new PreviewBar(x, usefulMaxY, "Superior"));

            foreach (double x in DistributePositions(config.QuantidadeInferior, usefulMinX, usefulMaxX))
                bars.Add(new PreviewBar(x, usefulMinY, "Inferior"));

            foreach (double y in DistributePositions(config.QuantidadeLateral, usefulMinY, usefulMaxY))
            {
                bars.Add(new PreviewBar(usefulMinX, y, "Lateral"));
                bars.Add(new PreviewBar(usefulMaxX, y, "Lateral"));
            }

            return bars;
        }

        private static List<double> DistributePositions(int count, double min, double max)
        {
            if (count <= 0 || max < min)
                return new List<double>();

            if (count == 1 || max - min <= 0.001)
                return new List<double> { (min + max) / 2.0 };

            List<double> values = new List<double>();
            double step = (max - min) / (count - 1);
            for (int i = 0; i < count; i++)
                values.Add(min + (step * i));

            return values;
        }

        private double ToCanvasX(double xCm, double scale)
        {
            return PreviewPadding + ((xCm - _sectionPreview.MinXCm) * scale);
        }

        private double ToCanvasY(double yCm, double scale)
        {
            return PreviewPadding + ((_sectionPreview.MaxYCm - yCm) * scale);
        }

        private double ToSectionX(double coordinateXCm)
        {
            return _sectionPreview.MinXCm + coordinateXCm;
        }

        private double ToSectionY(double coordinateYCm)
        {
            return _sectionPreview.MinYCm + coordinateYCm;
        }

        private static WpfRectangle CreateRectangle(double left, double top, double width, double height, Brush fill, string stroke, double thickness)
        {
            WpfRectangle rectangle = new WpfRectangle
            {
                Width = Math.Max(0.0, width),
                Height = Math.Max(0.0, height),
                Fill = fill,
                Stroke = BrushFromHex(stroke),
                StrokeThickness = thickness
            };
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            return rectangle;
        }

        private static WpfLine CreateLine(double x1, double y1, double x2, double y2, string stroke, double thickness)
        {
            return new WpfLine
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = BrushFromHex(stroke),
                StrokeThickness = thickness
            };
        }

        private void AddAxisLabel(string text, double left, double top, string color)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromHex(color)
            };
            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, top);
            canvasSecao.Children.Add(label);
        }

        private static Brush GetBarBrush(string posicao)
        {
            switch ((posicao ?? string.Empty).ToLowerInvariant())
            {
                case "superior":
                    return BrushFromHex("#2F80ED");
                case "inferior":
                    return BrushFromHex("#27AE60");
                case "lateral":
                    return BrushFromHex("#9B51E0");
                default:
                    return BrushFromHex("#3A4450");
            }
        }

        private static Brush BrushFromHex(string value)
        {
            return (Brush)new BrushConverter().ConvertFromString(value);
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void UpdateLapPreview(PfBeamBarsConfig config)
        {
            if (config == null || !config.Traspasse.Enabled)
            {
                txtTraspassePreview.Text = string.Empty;
                return;
            }

            try
            {
                double diameterMm = GetSelectedBarDiameterMm(cmbBarSup);
                PfAnchorageResult result = PfNbr6118AnchorageService.Calculate(diameterMm, config.Traspasse);
                txtTraspassePreview.Text = $"l0 {Format(result.SpliceLengthCm)} cm | lb,nec {Format(result.RequiredAnchorageCm)} cm";
            }
            catch
            {
                txtTraspassePreview.Text = "Nao foi possivel calcular.";
            }
        }

        private double GetSelectedBarDiameterMm(ComboBox combo)
        {
            if (combo?.SelectedItem is PfRebarBarTypeOption option &&
                _doc?.GetElement(option.Id) is RebarBarType barType)
            {
                return UnitUtils.ConvertFromInternalUnits(barType.BarNominalDiameter, UnitTypeId.Millimeters);
            }

            return 10.0;
        }

        private double GetMaxSelectedBarDiameterMm()
        {
            return Math.Max(
                GetSelectedBarDiameterMm(cmbBarSup),
                Math.Max(GetSelectedBarDiameterMm(cmbBarInf), GetSelectedBarDiameterMm(cmbBarLat)));
        }

        private sealed class PreviewBar
        {
            public PreviewBar(double xCm, double yCm, string posicao)
            {
                XCm = xCm;
                YCm = yCm;
                Posicao = posicao ?? string.Empty;
            }

            public double XCm { get; }
            public double YCm { get; }
            public string Posicao { get; }
        }
    }
}
