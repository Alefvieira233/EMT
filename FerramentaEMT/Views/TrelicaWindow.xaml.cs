using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using FerramentaEMT.Forms;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class TrelicaWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly AppSettings _settings;
        private bool _isInitializing;

        public TrelicaWindow(List<FamilySymbol> symbols, AppSettings settings)
        {
            _symbols = symbols ?? new List<FamilySymbol>();
            _settings = settings ?? new AppSettings();
            _isInitializing = true;

            try
            {
                InitializeComponent();
                RevitWindowThemeService.Attach(this);
            }
            catch (System.Exception ex)
            {
                AppDialogService.ShowError("Treliça", ex.ToString(), "Erro ao inicializar janela");
                throw;
            }

            LoadData();
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            _isInitializing = false;
            AtualizarEstadoControles();
        }

        private void LoadData()
        {
            List<string> families = _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f).ToList();
            foreach (string family in families)
            {
                cmbFamiliaMontante.Items.Add(family);
                cmbFamiliaDiagonal.Items.Add(family);
            }

            SelecionarFamiliaSalva(cmbFamiliaMontante, _settings.LastSelectedTrelicaMontanteFamilyName);
            SelecionarFamiliaSalva(cmbFamiliaDiagonal, _settings.LastSelectedTrelicaDiagonalFamilyName);

            if (cmbFamiliaMontante.SelectedIndex == -1 && cmbFamiliaMontante.Items.Count > 0)
                cmbFamiliaMontante.SelectedIndex = 0;
            if (cmbFamiliaDiagonal.SelectedIndex == -1 && cmbFamiliaDiagonal.Items.Count > 0)
                cmbFamiliaDiagonal.SelectedIndex = 0;

            PopulateMontanteProfiles();
            PopulateDiagonalProfiles();

            cmbZJust.Items.Add(new ZJustificationItem(0, "Origem"));
            cmbZJust.Items.Add(new ZJustificationItem(2, "Topo"));
            cmbZJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbZJust.Items.Add(new ZJustificationItem(3, "Inferior"));
            cmbZJust.SelectedIndex = 2;

            numSubd.Text = "1";
            numZOffset.Text = "0";
            chkInverterSentido.IsChecked = false;
            AtualizarEstadoControles();
        }

        private void SelecionarFamiliaSalva(System.Windows.Controls.ComboBox combo, string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] as string == familyName)
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        private void PopulateMontanteProfiles()
        {
            cmbMontante.Items.Clear();
            if (cmbFamiliaMontante.SelectedItem is not string selectedFamily)
                return;

            foreach (FamilySymbol symbol in _symbols.Where(s => s.FamilyName == selectedFamily))
                cmbMontante.Items.Add(new SymbolItem(symbol));

            SelecionarPerfilSalvo(
                cmbMontante,
                selectedFamily,
                _settings.LastSelectedTrelicaMontanteFamilyName,
                _settings.LastSelectedTrelicaMontanteName);
        }

        private void PopulateDiagonalProfiles()
        {
            cmbDiagonal.Items.Clear();
            if (cmbFamiliaDiagonal.SelectedItem is not string selectedFamily)
                return;

            foreach (FamilySymbol symbol in _symbols.Where(s => s.FamilyName == selectedFamily))
                cmbDiagonal.Items.Add(new SymbolItem(symbol));

            SelecionarPerfilSalvo(
                cmbDiagonal,
                selectedFamily,
                _settings.LastSelectedTrelicaDiagonalFamilyName,
                _settings.LastSelectedTrelicaDiagonalName);
        }

        private void SelecionarPerfilSalvo(
            System.Windows.Controls.ComboBox combo,
            string selectedFamily,
            string savedFamilyName,
            string savedTypeName)
        {
            if (!string.IsNullOrEmpty(savedTypeName) &&
                !string.IsNullOrEmpty(savedFamilyName) &&
                savedFamilyName == selectedFamily)
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    if (combo.Items[i] is SymbolItem item && item.Symbol.Name == savedTypeName)
                    {
                        combo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (combo.SelectedIndex == -1 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void CmbFamiliaMontante_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing || cmbMontante == null)
                return;

            PopulateMontanteProfiles();
        }

        private void CmbFamiliaDiagonal_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing || cmbDiagonal == null)
                return;

            PopulateDiagonalProfiles();
        }

        private void ChkLancarMontante_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoControles();
        }

        private void ChkLancarDiagonal_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoControles();
        }

        private void AtualizarEstadoControles()
        {
            if (_isInitializing)
                return;

            if (chkLancarMontante == null || chkLancarDiagonal == null ||
                cmbFamiliaMontante == null || cmbMontante == null ||
                cmbFamiliaDiagonal == null || cmbDiagonal == null)
            {
                return;
            }

            bool lancarMontante = chkLancarMontante.IsChecked == true;
            bool lancarDiagonal = chkLancarDiagonal.IsChecked == true;

            cmbFamiliaMontante.IsEnabled = lancarMontante;
            cmbMontante.IsEnabled = lancarMontante;
            cmbFamiliaDiagonal.IsEnabled = lancarDiagonal;
            cmbDiagonal.IsEnabled = lancarDiagonal;
        }

        public TrelicaConfig BuildConfig()
        {
            if (cmbZJust.SelectedItem is not ZJustificationItem zItem)
                return null;

            bool lancarMontante = chkLancarMontante.IsChecked == true;
            bool lancarDiagonal = chkLancarDiagonal.IsChecked == true;
            SymbolItem montanteItem = cmbMontante.SelectedItem as SymbolItem;
            SymbolItem diagonalItem = cmbDiagonal.SelectedItem as SymbolItem;

            if ((lancarMontante && montanteItem == null) || (lancarDiagonal && diagonalItem == null))
                return null;

            int subd = 1;
            double zOffset = 0.0;
            int.TryParse(numSubd.Text, out subd);
            double.TryParse(numZOffset.Text, out zOffset);
            if (subd <= 0)
                subd = 1;

            return new TrelicaConfig
            {
                SymbolMontante = lancarMontante ? montanteItem.Symbol : null,
                SymbolDiagonal = lancarDiagonal ? diagonalItem.Symbol : null,
                LancarMontante = lancarMontante,
                LancarDiagonal = lancarDiagonal,
                Quantidade = subd,
                ZJustificationValue = zItem.Value,
                ZOffsetMm = zOffset,
                InverterSentido = chkInverterSentido.IsChecked == true
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            bool lancarMontante = chkLancarMontante.IsChecked == true;
            bool lancarDiagonal = chkLancarDiagonal.IsChecked == true;

            if (!lancarMontante && !lancarDiagonal)
            {
                AppDialogService.ShowWarning("Treliça", "Selecione ao menos uma opção: montante ou diagonal.", "Dados incompletos");
                return;
            }

            if ((lancarMontante && cmbMontante.SelectedItem == null) || (lancarDiagonal && cmbDiagonal.SelectedItem == null) || cmbZJust.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Treliça", "Selecione os perfis habilitados e a justificação em Z.", "Dados incompletos");
                return;
            }

            if (lancarMontante && cmbMontante.SelectedItem is SymbolItem montanteItem)
            {
                _settings.LastSelectedTrelicaMontanteName = montanteItem.Symbol.Name;
                _settings.LastSelectedTrelicaMontanteFamilyName = montanteItem.Symbol.FamilyName;
            }

            if (lancarDiagonal && cmbDiagonal.SelectedItem is SymbolItem diagonalItem)
            {
                _settings.LastSelectedTrelicaDiagonalName = diagonalItem.Symbol.Name;
                _settings.LastSelectedTrelicaDiagonalFamilyName = diagonalItem.Symbol.FamilyName;
            }

            _settings.Save();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
