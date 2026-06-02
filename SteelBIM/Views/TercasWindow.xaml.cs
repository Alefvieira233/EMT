using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class TercasWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;
        private readonly AppSettings _settings;
        // v2.8.1 (Victor): valores pre-preenchidos pelo CmdGerarTercasPlano.
        // _inclinacaoGraus vem do pick da viga de referencia (ou angulo do plano se Esc).
        // _vaoRefCm vem do pick antecipado da linha limite inicial.
        private readonly double _inclinacaoGraus;
        private readonly double _vaoRefCm;

        // v2.8.1: estado do espacamento manual (persiste entre aberturas da Spacing window)
        private bool _usarEspacamentoManual;
        private List<double> _espacamentosCm = new List<double>();

        public TercasWindow(List<FamilySymbol> symbols, AppSettings settings,
            double inclinacaoGraus = 0.0, double vaoRefCm = 600.0)
        {
            try
            {
                InitializeComponent();
                RevitWindowThemeService.Attach(this);
            }
            catch (System.Exception ex)
            {
                // v2.8.7 (auditoria UX/security): ex.Message em vez de ex.ToString()
                // — antes o stack trace inteiro com paths do dev vazava ao usuario
                // final (P3-3 security + finding UX). Stack trace fica no Logger.
                SteelBIM.Infrastructure.Logger.Error(ex, "[TercasWindow] Falha ao inicializar janela");
                AppDialogService.ShowError("Gerar Terças", ex.Message, "Erro ao inicializar janela");
                throw;
            }
            _symbols = symbols;
            _settings = settings;
            _inclinacaoGraus = inclinacaoGraus;
            _vaoRefCm = vaoRefCm > 0 ? vaoRefCm : 600.0;
            LoadData();
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            btnEspacamentos.Click += BtnEspacamentos_Click;
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
            // v2.8.1 (Victor): default mudou de "Topo" (idx 2) pra "Inferior" (idx 3).
            // Justificativa: base da secao na linha de ref -> terca apoia sobre o topo
            // da viga, comportamento esperado em telhado tipico.
            cmbZJust.SelectedIndex = 3;

            // v2.8.11 (A3): justificacao lateral Y. Y_JUSTIFICATION: 0=Esquerda..3=Direita.
            // Default Esquerda (padrao EMT: terca alinhada pela esquerda/costa).
            cmbYJust.Items.Add(new ZJustificationItem(0, "Esquerda"));
            cmbYJust.Items.Add(new ZJustificationItem(1, "Centro"));
            cmbYJust.Items.Add(new ZJustificationItem(2, "Origem"));
            cmbYJust.Items.Add(new ZJustificationItem(3, "Direita"));
            cmbYJust.SelectedIndex = 0;

            // default numeric values
            numBeiralIni.Text = "0";
            numBeiralFim.Text = "0";
            numOffset.Text = "0";
            // v2.8.1 (Victor): rotacao pre-preenchida pelo angulo extraido da viga
            // de referencia (pick adicional no CmdGerarTercasPlano). Default 0 se
            // usuario apertou Esc no pick (mantem inclinacao do plano de trabalho).
            numRotacao.Text = _inclinacaoGraus.ToString("F2", CultureInfo.InvariantCulture);
            numQtde.Text = "5";

        }

        /// <summary>
        /// v2.8.1 (Victor): abre TercasSpacingWindow com vao pre-preenchido pelo
        /// pick da linha limite inicial. Persiste valores entre aberturas.
        /// </summary>
        private void BtnEspacamentos_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(numQtde.Text, out int qtde) || qtde < 1)
                qtde = 1;

            // Vao inicial = soma dos valores ja definidos (caso o usuario tenha
            // editado antes), ou o vao da linha limite inicial pickada.
            double vaoInicial = _usarEspacamentoManual && _espacamentosCm.Count > 0
                ? _espacamentosCm.Sum()
                : _vaoRefCm;

            FamilySymbol simboloAtual = (cmbPerfil.SelectedItem as SymbolItem)?.Symbol;

            var wnd = new TercasSpacingWindow(qtde, vaoInicial,
                _usarEspacamentoManual, _espacamentosCm, simboloAtual);
            if (wnd.ShowDialog() == true)
            {
                _usarEspacamentoManual = wnd.UsarEspacamentoManual;
                _espacamentosCm = wnd.GetValues();
            }
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
                    YJustificationValue = cmbYJust.SelectedItem is ZJustificationItem yItem ? yItem.Value : 0,
                    DividirNosBanzos = chkDividir.IsChecked == true,
                    // v2.8.1 (Victor): persiste estado do espacamento manual.
                    UsarEspacamentoManual = _usarEspacamentoManual,
                    EspacamentosCm = new List<double>(_espacamentosCm),
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
