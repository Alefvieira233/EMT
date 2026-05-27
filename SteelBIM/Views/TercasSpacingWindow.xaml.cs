#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using SteelBIM.Utils;
using SteelBIM.Views.Helpers;

namespace SteelBIM.Views
{
    /// <summary>
    /// Janela de configuracao de espaçamentos manuais entre terças
    /// (v2.8.1, Victor).
    ///
    /// <para>Comportamentos:</para>
    /// <list type="bullet">
    ///   <item>Vao total editavel: muda → escala todos proporcionalmente
    ///         via <see cref="TercasSpacingCalculator.ScaleProportionally"/>.</item>
    ///   <item>Editar um espaçamento individual (excluindo o ultimo): recalcula
    ///         o ultimo como remainder via
    ///         <see cref="TercasSpacingCalculator.RecalculateLastAsRemainder"/>.</item>
    ///   <item>Total mostrado em verde (bate) ou vermelho (nao bate) via
    ///         <see cref="TercasSpacingCalculator.IsTotalMatching"/>.</item>
    ///   <item>Mostra dimensao "d" do perfil pra contexto.</item>
    /// </list>
    /// </summary>
    public partial class TercasSpacingWindow : Window
    {
        private readonly List<TextBox> _inputs = new List<TextBox>();
        private double _totalRef;
        private bool _updatingFromCode;
        private TextBlock? _lblTotal;

        public TercasSpacingWindow(
            int quantidade,
            double vaoRefCm,
            bool usarManual,
            IList<double> valoresAtuais,
            FamilySymbol? perfil = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _totalRef = vaoRefCm > 0 ? vaoRefCm : 600.0;

            PreencherInfoPerfil(perfil);

            txtVaoTotal.Text = Format(_totalRef);
            txtVaoTotal.TextChanged += TxtVaoTotal_TextChanged;

            chkEspacamentoManual.IsChecked = usarManual;

            int required = quantidade + 1;
            double defaultSpacing = _totalRef / required;

            for (int i = 0; i < required; i++)
            {
                double value = i < valoresAtuais.Count ? valoresAtuais[i] : defaultSpacing;

                StackPanel row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                row.Children.Add(new TextBlock
                {
                    Width = 170,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = BuildSpacingLabel(i, quantidade)
                });

                TextBox input = new TextBox
                {
                    Width = 90,
                    TextAlignment = TextAlignment.Right,
                    Text = Format(value),
                    IsEnabled = usarManual,
                    Tag = i
                };

                input.TextChanged += AnyInput_TextChanged;

                row.Children.Add(input);
                row.Children.Add(new TextBlock
                {
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "cm"
                });

                panelEspacamentosEditor.Children.Add(row);
                _inputs.Add(input);
            }

            // Linha de total — feedback visual verde/vermelho.
            var totalRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };
            totalRow.Children.Add(new TextBlock
            {
                Width = 170,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Total:",
                FontWeight = FontWeights.SemiBold
            });
            _lblTotal = new TextBlock
            {
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Right
            };
            totalRow.Children.Add(_lblTotal);
            totalRow.Children.Add(new TextBlock
            {
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Text = "cm"
            });
            panelEspacamentosEditor.Children.Add(totalRow);

            AtualizarTotal();

            chkEspacamentoManual.Checked += ToggleInputs;
            chkEspacamentoManual.Unchecked += ToggleInputs;
        }

        public bool UsarEspacamentoManual => chkEspacamentoManual.IsChecked == true;

        public List<double> GetValues()
        {
            return _inputs
                .Select(x => NumberParsing.ParseDoubleOrDefault(x.Text, 0.0))
                .ToList();
        }

        // Quando o vao total muda: escala todos os campos proporcionalmente.
        private void TxtVaoTotal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingFromCode)
                return;
            if (!NumberParsing.TryParseDouble(txtVaoTotal.Text, out double novoTotal) || novoTotal <= 0)
                return;

            double oldTotal = _totalRef;
            if (Math.Abs(oldTotal) < 1e-9)
                return;

            _updatingFromCode = true;
            try
            {
                List<double> atuais = _inputs.Select(tb => NumberParsing.ParseDoubleOrDefault(tb.Text, 0.0)).ToList();
                List<double> escalados = TercasSpacingCalculator.ScaleProportionally(oldTotal, novoTotal, atuais);
                _totalRef = novoTotal;
                for (int i = 0; i < _inputs.Count && i < escalados.Count; i++)
                    _inputs[i].Text = Format(escalados[i]);
                AtualizarTotal();
            }
            finally
            {
                _updatingFromCode = false;
            }
        }

        // Quando qualquer campo de espaçamento muda: ajusta o ultimo campo como remainder.
        private void AnyInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingFromCode || !UsarEspacamentoManual || _inputs.Count < 2)
            {
                AtualizarTotal();
                return;
            }

            TextBox? changed = sender as TextBox;
            int idx = changed?.Tag is int tagValue ? tagValue : -1;

            // Soh redistribui se o campo alterado nao é o ultimo
            if (idx < 0 || idx == _inputs.Count - 1)
            {
                AtualizarTotal();
                return;
            }

            _updatingFromCode = true;
            try
            {
                List<double> atuais = _inputs.Select(tb => NumberParsing.ParseDoubleOrDefault(tb.Text, 0.0)).ToList();
                List<double> ajustados = TercasSpacingCalculator.RecalculateLastAsRemainder(_totalRef, atuais);
                _inputs[_inputs.Count - 1].Text = Format(ajustados[ajustados.Count - 1]);
                AtualizarTotal();
            }
            finally
            {
                _updatingFromCode = false;
            }
        }

        private void AtualizarTotal()
        {
            if (_lblTotal == null)
                return;
            List<double> atuais = _inputs.Select(tb => NumberParsing.ParseDoubleOrDefault(tb.Text, 0.0)).ToList();
            double soma = atuais.Sum();
            _lblTotal.Text = Format(soma);

            bool ok = TercasSpacingCalculator.IsTotalMatching(_totalRef, atuais);
            _lblTotal.Foreground = ok
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32))  // verde 700
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28)); // vermelho 700
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (UsarEspacamentoManual && GetValues().Any(x => x <= 0.0))
            {
                AppDialogService.ShowWarning(
                    "Gerar Terças",
                    "Informe distâncias manuais maiores que zero para todas as faixas.",
                    "Espaçamentos inválidos");
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ToggleInputs(object sender, RoutedEventArgs e)
        {
            bool enabled = UsarEspacamentoManual;
            foreach (TextBox input in _inputs)
                input.IsEnabled = enabled;
            AtualizarTotal();
        }

        private void PreencherInfoPerfil(FamilySymbol? perfil)
        {
            if (perfil == null)
            {
                lblPerfil.Text = "—";
                return;
            }

            string nome = string.IsNullOrWhiteSpace(perfil.Name)
                ? perfil.FamilyName
                : $"{perfil.FamilyName}  /  {perfil.Name}";

            // Tentar ler dimensao "d" (altura) para contexto
            string dimInfo = "";
            foreach (string param in new[] { "d", "H", "h", "Altura" })
            {
                Parameter p = perfil.LookupParameter(param);
                if (p?.StorageType == StorageType.Double)
                {
                    double dCm = p.AsDouble() / RevitUtils.FT_PER_CM;
                    if (dCm > 0.01)
                    {
                        dimInfo = $"  (d = {dCm:0.#} cm)";
                        break;
                    }
                }
            }

            lblPerfil.Text = nome + dimInfo;
        }

        private static string BuildSpacingLabel(int index, int quantidade)
        {
            if (index == 0)
                return "Linha inicial → T1";
            if (index == quantidade)
                return $"T{quantidade} → Linha final";
            return $"T{index} → T{index + 1}";
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
