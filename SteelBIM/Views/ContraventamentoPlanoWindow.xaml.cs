using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class ContraventamentoPlanoWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly AppSettings _settings;
        private bool _isInitializing;

        public ContraventamentoPlanoWindow(List<FamilySymbol> symbols, AppSettings settings)
        {
            _symbols = symbols ?? new List<FamilySymbol>();
            _settings = settings ?? new AppSettings();
            _isInitializing = true;

            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            LoadData();
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;

            _isInitializing = false;
        }

        private void LoadData()
        {
            List<string> families = _symbols
                .Select(s => s.FamilyName)
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            foreach (string family in families)
                cmbFamilia.Items.Add(family);

            if (!string.IsNullOrEmpty(_settings.LastSelectedContraventamentoPlanoFamilyName))
            {
                for (int i = 0; i < cmbFamilia.Items.Count; i++)
                {
                    if ((cmbFamilia.Items[i] as string) == _settings.LastSelectedContraventamentoPlanoFamilyName)
                    {
                        cmbFamilia.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbFamilia.SelectedIndex == -1 && cmbFamilia.Items.Count > 0)
                cmbFamilia.SelectedIndex = 0;

            cmbZJust.Items.Add(new ZJustificationItem(0, "Origem"));
            cmbZJust.Items.Add(new ZJustificationItem(2, "Topo"));
            cmbZJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbZJust.Items.Add(new ZJustificationItem(3, "Inferior"));
            cmbZJust.SelectedIndex = 2;

            numOffsetSegundaDiagonal.Text = "30";
            chkDesabilitarUniao.IsChecked = true;

            PopularPerfis();
        }

        private void CmbFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            PopularPerfis();
        }

        private void PopularPerfis()
        {
            cmbPerfil.Items.Clear();

            if (cmbFamilia.SelectedItem is not string selectedFamily)
                return;

            List<FamilySymbol> filteredSymbols = _symbols
                .Where(s => s.FamilyName == selectedFamily)
                .OrderBy(s => s.Name)
                .ToList();

            foreach (FamilySymbol symbol in filteredSymbols)
                cmbPerfil.Items.Add(new SymbolItem(symbol));

            if (!string.IsNullOrEmpty(_settings.LastSelectedContraventamentoPlanoName) &&
                !string.IsNullOrEmpty(_settings.LastSelectedContraventamentoPlanoFamilyName) &&
                _settings.LastSelectedContraventamentoPlanoFamilyName == selectedFamily)
            {
                for (int i = 0; i < cmbPerfil.Items.Count; i++)
                {
                    if (cmbPerfil.Items[i] is SymbolItem item &&
                        item.Symbol.Name == _settings.LastSelectedContraventamentoPlanoName)
                    {
                        cmbPerfil.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbPerfil.SelectedIndex == -1 && cmbPerfil.Items.Count > 0)
                cmbPerfil.SelectedIndex = 0;
        }

        public ContraventamentoPlanoConfig BuildConfig()
        {
            if (cmbPerfil.SelectedItem is not SymbolItem profileItem ||
                cmbZJust.SelectedItem is not ZJustificationItem zItem)
            {
                return null;
            }

            double offsetSegundaDiagonal = 30.0;
            double.TryParse(numOffsetSegundaDiagonal.Text, out offsetSegundaDiagonal);

            return new ContraventamentoPlanoConfig
            {
                SymbolSelecionado = profileItem.Symbol,
                ZJustificationValue = zItem.Value,
                OffsetSegundaDiagonalMm = offsetSegundaDiagonal,
                DesabilitarUniao = chkDesabilitarUniao.IsChecked == true
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPerfil.SelectedItem is not SymbolItem profileItem)
            {
                AppDialogService.ShowWarning("Contraventamento", "Selecione um perfil estrutural.", "Dados incompletos");
                return;
            }

            _settings.LastSelectedContraventamentoPlanoName = profileItem.Symbol.Name;
            _settings.LastSelectedContraventamentoPlanoFamilyName = profileItem.Symbol.FamilyName;
            _settings.Save();

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
