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
    public partial class GuardaCorpoWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly List<Level> _levels;
        private readonly AppSettings _settings;

        public GuardaCorpoWindow(List<FamilySymbol> symbols, List<Level> levels, AppSettings settings)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _symbols = symbols;
            _levels = levels;
            _settings = settings;
            LoadData();
            btnCreate.Click += BtnCreate_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        private void LoadData()
        {
            foreach (string family in _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f))
                cmbFamilia.Items.Add(family);

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedGuardaCorpoFamilyName))
            {
                for (int i = 0; i < cmbFamilia.Items.Count; i++)
                {
                    if ((string)cmbFamilia.Items[i] == _settings.LastSelectedGuardaCorpoFamilyName)
                    {
                        cmbFamilia.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbFamilia.SelectedIndex < 0 && cmbFamilia.Items.Count > 0)
                cmbFamilia.SelectedIndex = 0;

            PopulateTipos();

            foreach (Level level in _levels.OrderBy(l => l.Elevation))
                cmbNivel.Items.Add(new LevelItem(level));

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedGuardaCorpoLevelName))
            {
                for (int i = 0; i < cmbNivel.Items.Count; i++)
                {
                    if (cmbNivel.Items[i] is LevelItem item && item.Level.Name == _settings.LastSelectedGuardaCorpoLevelName)
                    {
                        cmbNivel.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbNivel.SelectedIndex < 0 && cmbNivel.Items.Count > 0)
                cmbNivel.SelectedIndex = 0;

            cmbZJust.Items.Add(new ZJustificationItem(0, "Origem"));
            cmbZJust.Items.Add(new ZJustificationItem(2, "Topo"));
            cmbZJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbZJust.Items.Add(new ZJustificationItem(3, "Inferior"));
            cmbZJust.SelectedIndex = 2;

            numAltura.Text = "110";
            numOffsetLateral.Text = "0";
            numEspacamentoMaximo.Text = "150";
            numQuantidadeTravessas.Text = "6";
            chkUnirGeometrias.IsChecked = true;
            chkCriarPostes.IsChecked = true;
            chkCriarTravessas.IsChecked = true;
            AtualizarEstadoTravessas();
        }

        private void PopulateTipos()
        {
            cmbTipo.Items.Clear();
            if (cmbFamilia.SelectedItem is not string family)
                return;

            foreach (FamilySymbol symbol in _symbols.Where(s => s.FamilyName == family).OrderBy(s => s.Name))
                cmbTipo.Items.Add(new SymbolItem(symbol));

            if (!string.IsNullOrWhiteSpace(_settings.LastSelectedGuardaCorpoProfileName) &&
                !string.IsNullOrWhiteSpace(_settings.LastSelectedGuardaCorpoFamilyName) &&
                family == _settings.LastSelectedGuardaCorpoFamilyName)
            {
                for (int i = 0; i < cmbTipo.Items.Count; i++)
                {
                    if (cmbTipo.Items[i] is SymbolItem item && item.Symbol.Name == _settings.LastSelectedGuardaCorpoProfileName)
                    {
                        cmbTipo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbTipo.SelectedIndex < 0 && cmbTipo.Items.Count > 0)
                cmbTipo.SelectedIndex = 0;
        }

        private void CmbFamilia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PopulateTipos();
        }

        public GuardaCorpoConfig BuildConfig()
        {
            if (cmbTipo.SelectedItem is not SymbolItem symbolItem ||
                cmbNivel.SelectedItem is not LevelItem levelItem ||
                cmbZJust.SelectedItem is not ZJustificationItem zItem)
                return null;

            double altura = 110.0;
            double offsetLateral = 0.0;
            double espacamento = 150.0;
            int quantidadeTravessas = 6;

            NumberParsing.TryParseDouble(numAltura.Text, out altura);
            NumberParsing.TryParseDouble(numOffsetLateral.Text, out offsetLateral);
            NumberParsing.TryParseDouble(numEspacamentoMaximo.Text, out espacamento);
            int.TryParse(numQuantidadeTravessas.Text, out quantidadeTravessas);

            if (altura <= 0.0)
                altura = 110.0;
            if (espacamento <= 0.0)
                espacamento = 150.0;
            if (quantidadeTravessas < 1)
                quantidadeTravessas = 1;

            return new GuardaCorpoConfig
            {
                SymbolSelecionado = symbolItem.Symbol,
                NivelReferencia = levelItem.Level,
                AlturaCorrimaoCm = altura,
                OffsetLateralCm = offsetLateral,
                ZJustificationValue = zItem.Value,
                UnirGeometrias = chkUnirGeometrias.IsChecked == true,
                CriarPostes = chkCriarPostes.IsChecked == true,
                CriarTravessasIntermediarias = chkCriarTravessas.IsChecked == true,
                QuantidadeTravessasIntermediarias = quantidadeTravessas,
                EspacamentoMaximoPostesCm = espacamento
            };
        }

        private void ChkCriarTravessas_Changed(object sender, RoutedEventArgs e)
        {
            AtualizarEstadoTravessas();
        }

        private void AtualizarEstadoTravessas()
        {
            bool habilitarQuantidade = chkCriarTravessas.IsChecked == true;
            numQuantidadeTravessas.IsEnabled = habilitarQuantidade;
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            GuardaCorpoConfig config = BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning("Guarda-Corpo", "Preencha família, tipo, nível e justificação.", "Dados incompletos");
                return;
            }

            _settings.LastSelectedGuardaCorpoProfileName = config.SymbolSelecionado.Name;
            _settings.LastSelectedGuardaCorpoFamilyName = config.SymbolSelecionado.FamilyName;
            _settings.LastSelectedGuardaCorpoLevelName = config.NivelReferencia?.Name ?? string.Empty;
            _settings.Save();

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
