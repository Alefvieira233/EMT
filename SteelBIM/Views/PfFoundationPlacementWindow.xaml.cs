#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class PfFoundationPlacementWindow : Window
    {
        private readonly List<SymbolOption> _symbols;

        public PfFoundationPlacementWindow(Document doc, bool hasSelection)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _symbols = PfFoundationPlacementService.CollectFoundationSymbols(doc)
                .Select(x => new SymbolOption(x))
                .ToList();

            cmbFamilia.ItemsSource = _symbols
                .GroupBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cmbFamilia.SelectedIndex = cmbFamilia.Items.Count > 0 ? 0 : -1;
            rbSelecao.IsChecked = hasSelection;
            rbSelecao.IsEnabled = hasSelection;
            rbVista.IsChecked = !hasSelection;
        }

        public bool HasSymbols => _symbols.Count > 0;

        public PfFoundationPlacementConfig? BuildConfig()
        {
            if (cmbTipo.SelectedItem is not SymbolOption symbol)
                return null;

            if (!NumberParsing.TryParseDouble(txtTolerancia.Text, out double toleranciaMm) || toleranciaMm < 0.0)
                return null;

            return new PfFoundationPlacementConfig
            {
                SymbolId = symbol.Id,
                Escopo = rbVista.IsChecked == true ? PfFoundationPlacementScope.VistaAtiva : PfFoundationPlacementScope.SelecaoAtual,
                OrientarPeloPilar = chkOrientar.IsChecked == true,
                IgnorarSeJaExisteFundacao = chkIgnorarExistente.IsChecked == true,
                ToleranciaCentroMm = toleranciaMm
            };
        }

        private void CmbFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbFamilia.SelectedItem is not SymbolOption family)
            {
                cmbTipo.ItemsSource = null;
                return;
            }

            List<SymbolOption> tipos = _symbols
                .Where(x => string.Equals(x.FamilyName, family.FamilyName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.TypeName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cmbTipo.ItemsSource = tipos;
            cmbTipo.SelectedIndex = tipos.Count > 0 ? 0 : -1;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (BuildConfig() == null)
            {
                AppDialogService.ShowWarning("PF - Lançar Fundação", "Revise o tipo selecionado e a tolerância informada.", "Configuração inválida");
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private sealed class SymbolOption
        {
            public SymbolOption(FamilySymbol symbol)
            {
                Id = symbol.Id;
                FamilyName = symbol.FamilyName;
                TypeName = symbol.Name;
            }

            public ElementId Id { get; }
            public string FamilyName { get; }
            public string TypeName { get; }
        }
    }
}
