using Autodesk.Revit.DB;
using FerramentaEMT.Forms;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FerramentaEMT.Views
{
    public partial class PipeRackWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly List<Level> _levels;
        private readonly AppSettings _settings;

        public PipeRackWindow(List<FamilySymbol> symbols, List<Level> levels, AppSettings settings)
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
            PopularFamilias(cmbFamiliaPilar, _settings.LastSelectedPipeRackPilarFamilyName, somenteColunas: true);
            PopularFamilias(cmbFamiliaViga, _settings.LastSelectedPipeRackVigaFamilyName, somenteFraming: true);
            PopularFamilias(cmbFamiliaMontante, _settings.LastSelectedPipeRackMontanteFamilyName, somenteFraming: true);
            PopularFamilias(cmbFamiliaDiagonal, _settings.LastSelectedPipeRackDiagonalFamilyName, somenteFraming: true);

            PopulateTipos(cmbFamiliaPilar, cmbTipoPilar, _settings.LastSelectedPipeRackPilarName, _settings.LastSelectedPipeRackPilarFamilyName);
            PopulateTipos(cmbFamiliaViga, cmbTipoViga, _settings.LastSelectedPipeRackVigaName, _settings.LastSelectedPipeRackVigaFamilyName);
            PopulateTipos(cmbFamiliaMontante, cmbTipoMontante, _settings.LastSelectedPipeRackMontanteName, _settings.LastSelectedPipeRackMontanteFamilyName);
            PopulateTipos(cmbFamiliaDiagonal, cmbTipoDiagonal, _settings.LastSelectedPipeRackDiagonalName, _settings.LastSelectedPipeRackDiagonalFamilyName);

            foreach (Level level in _levels.OrderBy(l => l.Elevation))
            {
                cmbNivelBase.Items.Add(new LevelItem(level));
                cmbNivelTopo.Items.Add(new LevelItem(level));
            }

            SelecionarNivelPreferido(cmbNivelBase, _settings.LastSelectedPipeRackLevelName);
            SelecionarNivelPreferido(cmbNivelTopo, _settings.LastSelectedPipeRackTopLevelName);

            if (cmbNivelBase.SelectedIndex < 0 && cmbNivelBase.Items.Count > 0)
                cmbNivelBase.SelectedIndex = 0;
            if (cmbNivelTopo.SelectedIndex < 0 && cmbNivelTopo.Items.Count > 0)
                cmbNivelTopo.SelectedIndex = cmbNivelTopo.Items.Count - 1;

            cmbTipoTrelica.Items.Add("Pratt");
            cmbTipoTrelica.Items.Add("Howe");
            cmbTipoTrelica.Items.Add("Warren");
            cmbTipoTrelica.Items.Add("X");
            cmbTipoTrelica.Items.Add("Diagonal simples");
            cmbTipoTrelica.SelectedIndex = 0;

            cmbModulos.Items.Add("2");
            cmbModulos.Items.Add("3");
            cmbModulos.SelectedIndex = 0;

            cmbPadraoDiagonais.Items.Add("Alternadas");
            cmbPadraoDiagonais.Items.Add("Sempre subindo");
            cmbPadraoDiagonais.Items.Add("Sempre descendo");
            cmbPadraoDiagonais.SelectedIndex = 0;

            txtVaos.Text = "6000;6000;6000";
            txtAlturaModulo.Text = "3000";
            txtQuantidadeModulos.Text = "2";
            txtLargura.Text = "3000";
            chkDesabilitarUniao.IsChecked = true;
        }

        private void PopularFamilias(ComboBox comboFamilia, string familiaPreferida, bool somenteFraming = false, bool somenteColunas = false)
        {
            IEnumerable<FamilySymbol> symbols = FiltrarSymbols(somenteFraming, somenteColunas);
            foreach (string family in symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f))
                comboFamilia.Items.Add(family);

            if (!string.IsNullOrWhiteSpace(familiaPreferida))
            {
                for (int i = 0; i < comboFamilia.Items.Count; i++)
                {
                    if ((string)comboFamilia.Items[i] == familiaPreferida)
                    {
                        comboFamilia.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (comboFamilia.SelectedIndex < 0 && comboFamilia.Items.Count > 0)
                comboFamilia.SelectedIndex = 0;
        }

        private void PopulateTipos(ComboBox comboFamilia, ComboBox comboTipo, string nomePreferido, string familiaPreferida, bool somenteFraming = false, bool somenteColunas = false)
        {
            comboTipo.Items.Clear();
            if (comboFamilia.SelectedItem is not string familia)
                return;

            foreach (FamilySymbol symbol in FiltrarSymbols(somenteFraming, somenteColunas).Where(s => s.FamilyName == familia).OrderBy(s => s.Name))
                comboTipo.Items.Add(new SymbolItem(symbol));

            if (!string.IsNullOrWhiteSpace(nomePreferido) && familia == familiaPreferida)
            {
                for (int i = 0; i < comboTipo.Items.Count; i++)
                {
                    if (comboTipo.Items[i] is SymbolItem item && item.Symbol.Name == nomePreferido)
                    {
                        comboTipo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (comboTipo.SelectedIndex < 0 && comboTipo.Items.Count > 0)
                comboTipo.SelectedIndex = 0;
        }

        private IEnumerable<FamilySymbol> FiltrarSymbols(bool somenteFraming, bool somenteColunas)
        {
            IEnumerable<FamilySymbol> symbols = _symbols;
            if (somenteFraming)
                symbols = symbols.Where(s => s.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming);
            if (somenteColunas)
                symbols = symbols.Where(s => s.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns);
            return symbols;
        }

        private void SelecionarNivelPreferido(ComboBox combo, string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is LevelItem item && item.Level.Name == nome)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void CmbFamiliaPilar_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            PopulateTipos(cmbFamiliaPilar, cmbTipoPilar, _settings.LastSelectedPipeRackPilarName, _settings.LastSelectedPipeRackPilarFamilyName, somenteColunas: true);

        private void CmbFamiliaViga_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            PopulateTipos(cmbFamiliaViga, cmbTipoViga, _settings.LastSelectedPipeRackVigaName, _settings.LastSelectedPipeRackVigaFamilyName, somenteFraming: true);

        private void CmbFamiliaMontante_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            PopulateTipos(cmbFamiliaMontante, cmbTipoMontante, _settings.LastSelectedPipeRackMontanteName, _settings.LastSelectedPipeRackMontanteFamilyName, somenteFraming: true);

        private void CmbFamiliaDiagonal_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            PopulateTipos(cmbFamiliaDiagonal, cmbTipoDiagonal, _settings.LastSelectedPipeRackDiagonalName, _settings.LastSelectedPipeRackDiagonalFamilyName, somenteFraming: true);

        public PipeRackConfig BuildConfig()
        {
            if (cmbNivelBase.SelectedItem is not LevelItem nivelBase ||
                cmbNivelTopo.SelectedItem is not LevelItem nivelTopo ||
                cmbTipoPilar.SelectedItem is not SymbolItem pilarItem ||
                cmbTipoViga.SelectedItem is not SymbolItem vigaItem ||
                cmbTipoMontante.SelectedItem is not SymbolItem montanteItem ||
                cmbTipoDiagonal.SelectedItem is not SymbolItem diagonalItem)
                return null;

            if (nivelTopo.Level.Elevation <= nivelBase.Level.Elevation)
                return null;

            List<double> vaos = ParseListaNumerica(txtVaos.Text);
            if (vaos.Count == 0)
                return null;

            return new PipeRackConfig
            {
                NivelBase = nivelBase.Level,
                NivelTopoPilares = nivelTopo.Level,
                SymbolPilar = pilarItem.Symbol,
                SymbolViga = vigaItem.Symbol,
                SymbolMontante = montanteItem.Symbol,
                SymbolDiagonal = diagonalItem.Symbol,
                VaosMm = vaos,
                AlturaModuloMm = ParseDouble(txtAlturaModulo.Text, 3000),
                QuantidadeModulos = ParseInt(txtQuantidadeModulos.Text, 2),
                LarguraEstruturaMm = ParseDouble(txtLargura.Text, 3000),
                NumeroModulosLargura = int.TryParse(cmbModulos.SelectedItem as string, out int m) ? m : 2,
                TipoTrelica = cmbTipoTrelica.SelectedItem as string ?? "Pratt",
                PadraoDiagonais = cmbPadraoDiagonais.SelectedItem as string ?? "Alternadas",
                DesabilitarUniaoMembros = chkDesabilitarUniao.IsChecked == true
            };
        }

        private List<double> ParseListaNumerica(string texto)
        {
            return texto.Split(new[] { ';', ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(t => ParseDouble(t, -1))
                .Where(v => v > 0)
                .ToList();
        }

        private double ParseDouble(string texto, double padrao)
        {
            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double valor))
                return valor;

            if (double.TryParse(texto, NumberStyles.Float, new CultureInfo("pt-BR"), out valor))
                return valor;

            return padrao;
        }

        private int ParseInt(string texto, int padrao)
        {
            if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor) && valor > 0)
                return valor;

            if (int.TryParse(texto, NumberStyles.Integer, new CultureInfo("pt-BR"), out valor) && valor > 0)
                return valor;

            return padrao;
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            PipeRackConfig config = BuildConfig();
            if (config == null)
            {
                AppDialogService.ShowWarning("Pipe Rack", "Preencha os perfis, os níveis e os dados obrigatórios do pipe rack.", "Dados incompletos");
                return;
            }

            _settings.LastSelectedPipeRackPilarName = config.SymbolPilar.Name;
            _settings.LastSelectedPipeRackPilarFamilyName = config.SymbolPilar.FamilyName;
            _settings.LastSelectedPipeRackVigaName = config.SymbolViga.Name;
            _settings.LastSelectedPipeRackVigaFamilyName = config.SymbolViga.FamilyName;
            _settings.LastSelectedPipeRackMontanteName = config.SymbolMontante.Name;
            _settings.LastSelectedPipeRackMontanteFamilyName = config.SymbolMontante.FamilyName;
            _settings.LastSelectedPipeRackDiagonalName = config.SymbolDiagonal.Name;
            _settings.LastSelectedPipeRackDiagonalFamilyName = config.SymbolDiagonal.FamilyName;
            _settings.LastSelectedPipeRackLevelName = config.NivelBase.Name;
            _settings.LastSelectedPipeRackTopLevelName = config.NivelTopoPilares.Name;
            _settings.Save();

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
