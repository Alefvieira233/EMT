using Autodesk.Revit.DB;
using FerramentaEMT.Forms;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FerramentaEMT.Views
{
    public partial class EscadaWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly List<Level> _levels;
        private readonly AppSettings _settings;

        public EscadaWindow(List<FamilySymbol> symbols, List<Level> levels, AppSettings settings)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _symbols = symbols;
            _levels = levels;
            _settings = settings;
            LoadData();
            btnCreate.Click += BtnCreate_Click;
            btnCancel.Click += BtnCancel_Click;
            chkCriarDegraus.Checked += ChkCriarDegraus_Changed;
            chkCriarDegraus.Unchecked += ChkCriarDegraus_Changed;
        }

        private void LoadData()
        {
            IEnumerable<string> familias = _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f);

            foreach (string family in familias)
            {
                cmbLongarinaFamilia.Items.Add(family);
                cmbDegrauFamilia.Items.Add(family);
            }

            // Restore longarina family
            SelectComboItem(cmbLongarinaFamilia, _settings.LastSelectedEscadaLongarinaFamilyName);
            if (cmbLongarinaFamilia.SelectedIndex < 0 && cmbLongarinaFamilia.Items.Count > 0)
                cmbLongarinaFamilia.SelectedIndex = 0;

            PopulateTiposLongarina();

            // Restore degrau family
            SelectComboItem(cmbDegrauFamilia, _settings.LastSelectedEscadaDegrauFamilyName);
            if (cmbDegrauFamilia.SelectedIndex < 0 && cmbDegrauFamilia.Items.Count > 0)
                cmbDegrauFamilia.SelectedIndex = 0;

            PopulateTiposDegrau();

            // Levels
            foreach (Level level in _levels.OrderBy(l => l.Elevation))
                cmbNivel.Items.Add(new LevelItem(level));

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedEscadaLevelName))
            {
                for (int i = 0; i < cmbNivel.Items.Count; i++)
                {
                    if (cmbNivel.Items[i] is LevelItem item && item.Level.Name == _settings.LastSelectedEscadaLevelName)
                    {
                        cmbNivel.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbNivel.SelectedIndex < 0 && cmbNivel.Items.Count > 0)
                cmbNivel.SelectedIndex = 0;

            // Z-Justification
            // ZJustification no Revit API: Origin=0, Center=1, Top=2, Bottom=3
            cmbZJust.Items.Add(new ZJustificationItem(0, "Origem"));
            cmbZJust.Items.Add(new ZJustificationItem(2, "Topo"));
            cmbZJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbZJust.Items.Add(new ZJustificationItem(3, "Inferior"));
            cmbZJust.SelectedIndex = 1; // padrão: Topo (valor 2)

            cmbLadoInsercao.Items.Add(new EnumItem<EscadaLadoInsercao>(EscadaLadoInsercao.Centro, "Centro"));
            cmbLadoInsercao.Items.Add(new EnumItem<EscadaLadoInsercao>(EscadaLadoInsercao.Esquerda, "Esquerda"));
            cmbLadoInsercao.Items.Add(new EnumItem<EscadaLadoInsercao>(EscadaLadoInsercao.Direita, "Direita"));
            cmbLadoInsercao.SelectedIndex = 0;

            cmbTipoDegrauModelagem.Items.Add(new EnumItem<EscadaTipoDegrau>(EscadaTipoDegrau.PerfilLinear, "Perfil linear"));
            cmbTipoDegrauModelagem.Items.Add(new EnumItem<EscadaTipoDegrau>(EscadaTipoDegrau.Chapa, "Chapa"));
            cmbTipoDegrauModelagem.SelectedIndex = 0;

            // Defaults
            numLargura.Text = "100";
            numEspelho.Text = "19";
            numPisada.Text = "30";
            numQuantidadeDegraus.Text = "";
            chkCriarDegraus.IsChecked = true;
            chkUnirGeometrias.IsChecked = false;

            AtualizarEstadoDegraus();
        }

        private void SelectComboItem(System.Windows.Controls.ComboBox cmb, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if ((string)cmb.Items[i] == value)
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
        }

        private void PopulateTiposLongarina()
        {
            cmbLongarina.Items.Clear();
            if (cmbLongarinaFamilia.SelectedItem is not string family) return;

            foreach (FamilySymbol symbol in _symbols.Where(s => s.FamilyName == family).OrderBy(s => s.Name))
                cmbLongarina.Items.Add(new SymbolItem(symbol));

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedEscadaLongarinaNome) &&
                family == _settings.LastSelectedEscadaLongarinaFamilyName)
            {
                for (int i = 0; i < cmbLongarina.Items.Count; i++)
                {
                    if (cmbLongarina.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedEscadaLongarinaNome)
                    {
                        cmbLongarina.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbLongarina.SelectedIndex < 0 && cmbLongarina.Items.Count > 0)
                cmbLongarina.SelectedIndex = 0;
        }

        private void PopulateTiposDegrau()
        {
            cmbDegrau.Items.Clear();
            if (cmbDegrauFamilia.SelectedItem is not string family) return;

            foreach (FamilySymbol symbol in _symbols.Where(s => s.FamilyName == family).OrderBy(s => s.Name))
                cmbDegrau.Items.Add(new SymbolItem(symbol));

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedEscadaDegrauNome) &&
                family == _settings.LastSelectedEscadaDegrauFamilyName)
            {
                for (int i = 0; i < cmbDegrau.Items.Count; i++)
                {
                    if (cmbDegrau.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedEscadaDegrauNome)
                    {
                        cmbDegrau.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cmbDegrau.SelectedIndex < 0 && cmbDegrau.Items.Count > 0)
                cmbDegrau.SelectedIndex = 0;
        }

        private void CmbLongarinaFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PopulateTiposLongarina();
        }

        private void CmbDegrauFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PopulateTiposDegrau();
        }

        private void ChkCriarDegraus_Changed(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoDegraus();
        }

        private void CmbTipoDegrauModelagem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarEstadoDegraus();
        }

        private void AtualizarEstadoDegraus()
        {
            bool criar = chkCriarDegraus.IsChecked == true;
            EscadaTipoDegrau tipo = GetTipoDegrauSelecionado();
            bool usaPerfilLinear = criar && tipo == EscadaTipoDegrau.PerfilLinear;

            cmbTipoDegrauModelagem.IsEnabled = criar;
            cmbDegrauFamilia.IsEnabled = usaPerfilLinear;
            cmbDegrau.IsEnabled = usaPerfilLinear;
        }

        private EscadaTipoDegrau GetTipoDegrauSelecionado()
        {
            if (cmbTipoDegrauModelagem.SelectedItem is EnumItem<EscadaTipoDegrau> item)
                return item.Value;

            return EscadaTipoDegrau.PerfilLinear;
        }

        public EscadaConfig BuildConfig()
        {
            if (cmbLongarina.SelectedItem is not SymbolItem longarItem ||
                cmbNivel.SelectedItem is not LevelItem levelItem ||
                cmbZJust.SelectedItem is not ZJustificationItem zItem ||
                cmbLadoInsercao.SelectedItem is not EnumItem<EscadaLadoInsercao> ladoItem ||
                cmbTipoDegrauModelagem.SelectedItem is not EnumItem<EscadaTipoDegrau> tipoItem)
                return null;

            bool criarDegraus = chkCriarDegraus.IsChecked == true;
            FamilySymbol symbolDegrau = null;
            double espessuraChapa = 0.5;
            double pisada = 30.0;

            if (criarDegraus && tipoItem.Value == EscadaTipoDegrau.PerfilLinear)
            {
                if (cmbDegrau.SelectedItem is not SymbolItem degrauItem)
                    return null;
                symbolDegrau = degrauItem.Symbol;
            }

            double largura = 100.0;
            double espelho = 20.0;
            int quantidadeDegraus = 0;

            NumberParsing.TryParseDouble(numLargura.Text, out largura);
            NumberParsing.TryParseDouble(numEspelho.Text, out espelho);
            NumberParsing.TryParseDouble(numPisada.Text, out pisada);
            int.TryParse(numQuantidadeDegraus.Text, out quantidadeDegraus);

            if (largura <= 0.0) largura = 100.0;
            if (espelho <= 0.0) espelho = 19.0;
            if (pisada <= 0.0) pisada = 30.0;
            if (espessuraChapa <= 0.0) espessuraChapa = 0.5;

            return new EscadaConfig
            {
                SymbolLongarina = longarItem.Symbol,
                SymbolDegrau = symbolDegrau,
                NivelReferencia = levelItem.Level,
                LarguraCm = largura,
                AlturaEspelhoCm = espelho,
                PisadaCm = pisada,
                QuantidadeDegraus = quantidadeDegraus,
                LadoInsercao = ladoItem.Value,
                TipoDegrau = tipoItem.Value,
                PossuiExtensaoInicio = false,
                ExtensaoInicioCm = 0.0,
                PossuiExtensaoFim = false,
                ExtensaoFimCm = 0.0,
                EspessuraChapaDegrauCm = espessuraChapa,
                ZJustificationValue = zItem.Value,
                UnirGeometrias = chkUnirGeometrias.IsChecked == true,
                CriarDegraus = criarDegraus
            };
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            EscadaConfig config = BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning("Escada", "Preencha todos os campos obrigatórios.", "Dados incompletos");
                return;
            }

            _settings.LastSelectedEscadaLongarinaNome = config.SymbolLongarina.Name;
            _settings.LastSelectedEscadaLongarinaFamilyName = config.SymbolLongarina.FamilyName;

            if (config.SymbolDegrau != null)
            {
                _settings.LastSelectedEscadaDegrauNome = config.SymbolDegrau.Name;
                _settings.LastSelectedEscadaDegrauFamilyName = config.SymbolDegrau.FamilyName;
            }

            _settings.LastSelectedEscadaLevelName = config.NivelReferencia?.Name ?? string.Empty;
            _settings.Save();

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private sealed class EnumItem<T>
        {
            public EnumItem(T value, string label)
            {
                Value = value;
                Label = label;
            }

            public T Value { get; }
            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
