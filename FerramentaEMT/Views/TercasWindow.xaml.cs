using Autodesk.Revit.DB;
using FerramentaEMT.Models;
using FerramentaEMT.Forms;
using FerramentaEMT.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FerramentaEMT.Views
{
    public partial class TercasWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly AppSettings _settings;

        public TercasWindow(List<FamilySymbol> symbols, AppSettings settings)
        {
            try
            {
                InitializeComponent();
                RevitWindowThemeService.Attach(this);
            }
            catch (System.Exception ex)
            {
                AppDialogService.ShowError("Gerar Terças", ex.ToString(), "Erro ao inicializar janela");
                throw;
            }
            _symbols = symbols;
            _settings = settings;
            LoadData();
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        private void LoadData()
        {
            // Populate family combo
            var families = _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f).ToList();
            foreach (string family in families)
            {
                cmbFamilia.Items.Add(family);
            }

            // Select last used family if available
            if (!string.IsNullOrEmpty(_settings.LastSelectedProfileFamilyName))
            {
                for (int i = 0; i < cmbFamilia.Items.Count; i++)
                {
                    if (cmbFamilia.Items[i] as string == _settings.LastSelectedProfileFamilyName)
                    {
                        cmbFamilia.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbFamilia.SelectedIndex == -1 && cmbFamilia.Items.Count > 0)
                cmbFamilia.SelectedIndex = 0;

            // Populate profile combo based on selected family
            PopulateProfiles();

            cmbZJust.Items.Add(new ZJustificationItem(0, "Origem"));
            cmbZJust.Items.Add(new ZJustificationItem(2, "Topo"));
            cmbZJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbZJust.Items.Add(new ZJustificationItem(3, "Inferior"));
            cmbZJust.SelectedIndex = 2;

            // default numeric values
            numBeiralIni.Text = "0";
            numBeiralFim.Text = "0";
            numOffset.Text = "0";
            numRotacao.Text = "0";
            numQtde.Text = "5";

        }

        private void PopulateProfiles()
        {
            cmbPerfil.Items.Clear();
            if (cmbFamilia.SelectedItem is string selectedFamily)
            {
                var filteredSymbols = _symbols.Where(s => s.FamilyName == selectedFamily).ToList();
                foreach (FamilySymbol s in filteredSymbols)
                {
                    cmbPerfil.Items.Add(new SymbolItem(s));
                }

                // Select last used profile if it matches the family
                if (!string.IsNullOrEmpty(_settings.LastSelectedProfileName) && !string.IsNullOrEmpty(_settings.LastSelectedProfileFamilyName) && _settings.LastSelectedProfileFamilyName == selectedFamily)
                {
                    for (int i = 0; i < cmbPerfil.Items.Count; i++)
                    {
                        if (cmbPerfil.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedProfileName)
                        {
                            cmbPerfil.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (cmbPerfil.SelectedIndex == -1 && cmbPerfil.Items.Count > 0)
                    cmbPerfil.SelectedIndex = 0;
            }
        }

        private void CmbFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PopulateProfiles();
        }

        public TercasConfig BuildConfig()
        {
            if (cmbPerfil.SelectedItem is SymbolItem item && cmbZJust.SelectedItem is ZJustificationItem zItem)
            {
                int qtde = 1;
                double beiralIni = 0, beiralFim = 0, offset = 0, rot = 0;
                int.TryParse(numQtde.Text, out qtde);
                NumberParsing.TryParseDouble(numBeiralIni.Text, out beiralIni);
                NumberParsing.TryParseDouble(numBeiralFim.Text, out beiralFim);
                NumberParsing.TryParseDouble(numOffset.Text, out offset);
                NumberParsing.TryParseDouble(numRotacao.Text, out rot);

                return new TercasConfig
                {
                    SymbolSelecionado = item.Symbol,
                    Quantidade = qtde <= 0 ? 1 : qtde,
                    BeiralInicialCm = beiralIni,
                    BeiralFinalCm = beiralFim,
                    OffsetMm = offset,
                    RotacaoSecaoGraus = rot,
                    InverterSentido = chkInverter.IsChecked == true,
                    ZJustificationValue = zItem.Value,
                    DividirNosBanzos = chkDividir.IsChecked == true
                };
            }
            return null;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPerfil.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Gerar Terças", "Selecione um perfil.", "Dados incompletos");
                return;
            }
            if (cmbZJust.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Gerar Terças", "Selecione a justificação em Z.", "Dados incompletos");
                return;
            }

            // Save selected profile to settings
            if (cmbPerfil.SelectedItem is SymbolItem item)
            {
                _settings.LastSelectedProfileName = item.Symbol.Name;
                _settings.LastSelectedProfileFamilyName = item.Symbol.FamilyName;
                _settings.Save();
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
