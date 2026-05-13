#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using SteelBIM.Models.Conexoes;
using SteelBIM.Services.Conexoes;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class PlacaBaseConfigWindow : Window
    {
        private readonly List<SymbolOption> _symbols;

        public PlacaBaseConfigWindow(Document doc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _symbols = PlacaBaseLancamentoService.CollectCompatibleSymbols(doc)
                .Select(s => new SymbolOption(s))
                .ToList();

            var familias = _symbols
                .GroupBy(s => s.FamilyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(s => s.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cmbFamilia.ItemsSource = familias;
            cmbFamilia.SelectedIndex = familias.Count > 0 ? 0 : -1;
        }

        public bool HasSymbols => _symbols.Count > 0;

        public PlacaBaseConfig? BuildConfig()
        {
            if (cmbTipo.SelectedItem is not SymbolOption symbol)
                return null;

            if (!TryParsePositive(txtComprimento.Text, out double comprimento) ||
                !TryParsePositive(txtLargura.Text, out double largura) ||
                !TryParsePositive(txtEspessura.Text, out double espessura) ||
                !TryParsePositive(txtFuroDiametro.Text, out double furoDiametro) ||
                !TryParsePositive(txtFuroOffsetX.Text, out double furoOffsetX) ||
                !TryParsePositive(txtFuroOffsetY.Text, out double furoOffsetY) ||
                !TryParsePositive(txtChumbadorDiametro.Text, out double chumbadorDiametro) ||
                !TryParsePositive(txtGrauteEspessura.Text, out double grauteEspessura))
            {
                return null;
            }

            return new PlacaBaseConfig
            {
                FamilyName = symbol.FamilyName,
                TypeName = symbol.TypeName,
                FamilySymbolId = symbol.Id,
                ComprimentoMm = comprimento,
                LarguraMm = largura,
                EspessuraMm = espessura,
                FuroDiametroMm = furoDiametro,
                FuroOffsetXmm = furoOffsetX,
                FuroOffsetYmm = furoOffsetY,
                ChumbadorDiametroMm = chumbadorDiametro,
                GrauteEspessuraMm = grauteEspessura,
                Solda = txtSolda.Text?.Trim() ?? string.Empty,
                SobrescreverParametros = chkSobrescreverParametros.IsChecked == true
            };
        }

        private void CmbFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbFamilia.SelectedItem is not SymbolOption selectedFamily)
            {
                cmbTipo.ItemsSource = null;
                return;
            }

            var types = _symbols
                .Where(s => string.Equals(s.FamilyName, selectedFamily.FamilyName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.TypeName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cmbTipo.ItemsSource = types;
            cmbTipo.SelectedIndex = types.Count > 0 ? 0 : -1;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSymbols)
            {
                AppDialogService.ShowWarning(
                    "Placas de Base",
                    "Nenhuma família face-based/work plane-based foi encontrada no modelo.",
                    "Família não encontrada");
                return;
            }

            if (BuildConfig() == null)
            {
                AppDialogService.ShowWarning(
                    "Placas de Base",
                    "Revise os campos numéricos e selecione um tipo de família válido.",
                    "Configuração inválida");
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static bool TryParsePositive(string? text, out double value)
        {
            return NumberParsing.TryParseDouble(text, out value) && value > 0;
        }

        private sealed class SymbolOption
        {
            public SymbolOption(FamilySymbol symbol)
            {
                Id = symbol.Id;
                FamilyName = symbol.Family?.Name ?? string.Empty;
                TypeName = symbol.Name;
            }

            public ElementId Id { get; }

            public string FamilyName { get; }

            public string TypeName { get; }
        }
    }
}
