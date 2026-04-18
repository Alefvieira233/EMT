using Autodesk.Revit.DB;
using FerramentaEMT.Models;
using FerramentaEMT.Forms;
using FerramentaEMT.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FerramentaEMT.Views
{
    public partial class TravamentoWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly AppSettings _settings;
        private bool _isInitializing;

        public TravamentoWindow(List<FamilySymbol> symbols, AppSettings settings)
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
                AppDialogService.ShowError("Travamentos", ex.ToString(), "Erro ao inicializar janela");
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
            // Populate family combos
            var families = _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f).ToList();
            foreach (string family in families)
            {
                cmbFamiliaT.Items.Add(family);
                cmbFamiliaF.Items.Add(family);
            }

            // Select last used tirante family
            if (!string.IsNullOrEmpty(_settings.LastSelectedTiranteFamilyName))
            {
                for (int i = 0; i < cmbFamiliaT.Items.Count; i++)
                {
                    if (cmbFamiliaT.Items[i] as string == _settings.LastSelectedTiranteFamilyName)
                    {
                        cmbFamiliaT.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbFamiliaT.SelectedIndex == -1 && cmbFamiliaT.Items.Count > 0)
                cmbFamiliaT.SelectedIndex = 0;

            // Select last used frechal family
            if (!string.IsNullOrEmpty(_settings.LastSelectedFrechalFamilyName))
            {
                for (int i = 0; i < cmbFamiliaF.Items.Count; i++)
                {
                    if (cmbFamiliaF.Items[i] as string == _settings.LastSelectedFrechalFamilyName)
                    {
                        cmbFamiliaF.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbFamiliaF.SelectedIndex == -1 && cmbFamiliaF.Items.Count > 0)
                cmbFamiliaF.SelectedIndex = 0;

            // Populate profiles
            PopulateTiranteProfiles();
            PopulateFrechalProfiles();

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

        private void PopulateTiranteProfiles()
        {
            cmbT.Items.Clear();
            if (cmbFamiliaT.SelectedItem is string selectedFamily)
            {
                var filteredSymbols = _symbols.Where(s => s.FamilyName == selectedFamily).ToList();
                foreach (FamilySymbol s in filteredSymbols)
                {
                    cmbT.Items.Add(new SymbolItem(s));
                }

                // Select last used tirante profile
                if (!string.IsNullOrEmpty(_settings.LastSelectedTiranteName) && !string.IsNullOrEmpty(_settings.LastSelectedTiranteFamilyName) && _settings.LastSelectedTiranteFamilyName == selectedFamily)
                {
                    for (int i = 0; i < cmbT.Items.Count; i++)
                    {
                        if (cmbT.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedTiranteName)
                        {
                            cmbT.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (cmbT.SelectedIndex == -1 && cmbT.Items.Count > 0)
                    cmbT.SelectedIndex = 0;
            }
        }

        private void PopulateFrechalProfiles()
        {
            cmbF.Items.Clear();
            if (cmbFamiliaF.SelectedItem is string selectedFamily)
            {
                var filteredSymbols = _symbols.Where(s => s.FamilyName == selectedFamily).ToList();
                foreach (FamilySymbol s in filteredSymbols)
                {
                    cmbF.Items.Add(new SymbolItem(s));
                }

                // Select last used frechal profile
                if (!string.IsNullOrEmpty(_settings.LastSelectedFrechalName) && !string.IsNullOrEmpty(_settings.LastSelectedFrechalFamilyName) && _settings.LastSelectedFrechalFamilyName == selectedFamily)
                {
                    for (int i = 0; i < cmbF.Items.Count; i++)
                    {
                        if (cmbF.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedFrechalName)
                        {
                            cmbF.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (cmbF.SelectedIndex == -1 && cmbF.Items.Count > 0)
                    cmbF.SelectedIndex = 0;
            }
        }

        private void CmbFamiliaT_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing || cmbT == null)
                return;

            PopulateTiranteProfiles();
        }

        private void CmbFamiliaF_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing || cmbF == null)
                return;

            PopulateFrechalProfiles();
        }

        private void ChkLancarTirante_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoControles();
        }

        private void ChkLancarFrechal_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoControles();
        }

        private void AtualizarEstadoControles()
        {
            if (_isInitializing)
                return;

            if (chkLancarTirante == null || chkLancarFrechal == null ||
                cmbFamiliaT == null || cmbT == null ||
                cmbFamiliaF == null || cmbF == null)
            {
                return;
            }

            bool lancarTirante = chkLancarTirante.IsChecked == true;
            bool lancarFrechal = chkLancarFrechal.IsChecked == true;

            cmbFamiliaT.IsEnabled = lancarTirante;
            cmbT.IsEnabled = lancarTirante;
            cmbFamiliaF.IsEnabled = lancarFrechal;
            cmbF.IsEnabled = lancarFrechal;
        }

        public TravamentoConfig BuildConfig()
        {
            if (cmbZJust.SelectedItem is ZJustificationItem zItem)
            {
                bool lancarTirante = chkLancarTirante.IsChecked == true;
                bool lancarFrechal = chkLancarFrechal.IsChecked == true;
                SymbolItem tItem = cmbT.SelectedItem as SymbolItem;
                SymbolItem fItem = cmbF.SelectedItem as SymbolItem;

                if ((lancarTirante && tItem == null) || (lancarFrechal && fItem == null))
                    return null;

                int subd = 1;
                double zOffset = 0.0;
                int.TryParse(numSubd.Text, out subd);
                NumberParsing.TryParseDouble(numZOffset.Text, out zOffset);
                if (subd <= 0) subd = 1;
                return new TravamentoConfig
                {
                    SymbolTirante = lancarTirante ? tItem.Symbol : null,
                    SymbolFrechal = lancarFrechal ? fItem.Symbol : null,
                    LancarTirante = lancarTirante,
                    LancarFrechal = lancarFrechal,
                    Quantidade = subd,
                    ZJustificationValue = zItem.Value,
                    ZOffsetMm = zOffset,
                    InverterSentido = chkInverterSentido.IsChecked == true
                };
            }
            return null;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            bool lancarTirante = chkLancarTirante.IsChecked == true;
            bool lancarFrechal = chkLancarFrechal.IsChecked == true;

            if (!lancarTirante && !lancarFrechal)
            {
                AppDialogService.ShowWarning("Travamentos", "Selecione ao menos uma opção: tirante ou frechal.", "Dados incompletos");
                return;
            }

            if ((lancarTirante && cmbT.SelectedItem == null) || (lancarFrechal && cmbF.SelectedItem == null) || cmbZJust.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Travamentos", "Selecione os perfis habilitados e a justificação em Z.", "Dados incompletos");
                return;
            }

            // Save selected profiles to settings
            if (lancarTirante && cmbT.SelectedItem is SymbolItem tItem)
            {
                _settings.LastSelectedTiranteName = tItem.Symbol.Name;
                _settings.LastSelectedTiranteFamilyName = tItem.Symbol.FamilyName;
            }
            if (lancarFrechal && cmbF.SelectedItem is SymbolItem fItem)
            {
                _settings.LastSelectedFrechalName = fItem.Symbol.Name;
                _settings.LastSelectedFrechalFamilyName = fItem.Symbol.FamilyName;
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
