using FerramentaEMT.Utils;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FerramentaEMT.Views
{
    public partial class TercasSpacingWindow : Window
    {
        private readonly List<TextBox> _inputs = new List<TextBox>();

        public TercasSpacingWindow(int quantidade, double vaoRefCm, bool usarManual, IList<double> valoresAtuais)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            chkEspacamentoManual.IsChecked = usarManual;

            int required = quantidade + 1;
            double defaultSpacing = vaoRefCm / required;

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
                    Width = 80,
                    Text = Format(value),
                    IsEnabled = usarManual
                };

                row.Children.Add(input);
                row.Children.Add(new TextBlock
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "cm"
                });

                panelEspacamentosEditor.Children.Add(row);
                _inputs.Add(input);
            }

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

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (UsarEspacamentoManual && GetValues().Any(x => x <= 0.0))
            {
                AppDialogService.ShowWarning("Gerar Terças", "Informe distâncias manuais maiores que zero para todas as faixas.", "Espaçamentos inválidos");
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
        }

        private static string BuildSpacingLabel(int index, int quantidade)
        {
            if (index == 0)
                return "Linha inicial -> T1";
            if (index == quantidade)
                return $"T{quantidade} -> Linha final";

            return $"T{index} -> T{index + 1}";
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
