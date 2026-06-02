#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Utils;
using WpfGrid = System.Windows.Controls.Grid;
using WpfVisibility = System.Windows.Visibility;

namespace SteelBIM.Views
{
    /// <summary>
    /// Janela do comando "Inserir Conexao de Terca" (v2.8.1, Victor).
    ///
    /// <para>Popula combos Familia + Tipo a partir das FamilySymbols recebidos.
    /// Quando o usuario escolhe um Tipo, o expander "Parametros da familia"
    /// é populado dinamicamente com TextBoxes para cada parametro Double
    /// editavel da familia. Tipos detectados via SpecTypeId: Length (mm),
    /// Angle (°), ou generico (sem sufixo).</para>
    /// </summary>
    public partial class ConexaoTercasWindow : Window
    {
        private readonly List<FamilySymbol> _symbols;

        // (nome do parametro, TextBox de edicao, é comprimento em mm, é angulo em graus)
        private readonly List<(string Name, TextBox Tb, bool IsLength, bool IsAngle)> _paramRows
            = new List<(string, TextBox, bool, bool)>();

        public ConexaoTercasWindow(List<FamilySymbol> symbols, int qtdeTercas, int qtdeVigas = 0)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _symbols = symbols;
            txtSubtitulo.Text = qtdeVigas > 0
                ? $"{qtdeTercas} terça(s) e {qtdeVigas} viga(s) selecionadas. Escolha a família e o tipo de conexão."
                : $"{qtdeTercas} terça(s) selecionada(s). Escolha a família e o tipo de conexão.";
            CarregarFamilias();
            // v2.8.4: handlers btnOk/btnCancel ja sao registrados via XAML
            // (Click="BtnOk_Click" e Click="BtnCancel_Click"). NAO duplicar
            // aqui via "+= BtnOk_Click" — isso faz o handler rodar 2x ao
            // clicar, e a segunda chamada lanca exception
            // "DialogResult somente pode ser definido apos Window ser criado
            // e exibido como caixa de dialogo" porque a primeira ja fechou
            // a janela. Bug encontrado em v2.8.3 (logs Alef 28/05 noite).
        }

        private void CarregarFamilias()
        {
            var familias = _symbols.Select(s => s.FamilyName).Distinct().OrderBy(f => f).ToList();
            foreach (string f in familias)
                cmbFamilia.Items.Add(f);

            if (cmbFamilia.Items.Count > 0)
                cmbFamilia.SelectedIndex = 0;
        }

        private void CmbFamilia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cmbTipo.Items.Clear();
            if (cmbFamilia.SelectedItem is string familia)
            {
                foreach (var s in _symbols.Where(x => x.FamilyName == familia))
                    cmbTipo.Items.Add(new SymbolItem(s));

                if (cmbTipo.Items.Count > 0)
                    cmbTipo.SelectedIndex = 0;
            }
        }

        private void CmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTipo.SelectedItem is SymbolItem item)
                CarregarParametrosTipo(item.Symbol);
        }

        private void CarregarParametrosTipo(FamilySymbol symbol)
        {
            stkParametros.Children.Clear();
            _paramRows.Clear();

            // Mostra apenas parametros definidos pelo usuario (Id > 0).
            // Parametros built-in do Revit (Custo, Elevacao-padrao, etc.) tem Id negativo.
            var plist = symbol.Parameters
                .Cast<Parameter>()
                .Where(p => !p.IsReadOnly
                         && p.StorageType == StorageType.Double
                         && p.Definition != null
                         && !string.IsNullOrWhiteSpace(p.Definition.Name)
                         && p.Id.Value > 0)
                .OrderBy(p => p.Definition.Name)
                .ToList();

            grpParametros.Visibility = plist.Count > 0 ? WpfVisibility.Visible : WpfVisibility.Collapsed;

            foreach (var p in plist)
            {
                // Detecta comprimento via SpecTypeId — mais confiavel que AsValueString()
                // cujo retorno varia por localidade e pode ser null em parametros de tipo.
                ForgeTypeId? dataType = null;
                try
                { dataType = p.Definition.GetDataType(); }
                catch { /* fallback pra generico */ }

                bool isLength = dataType != null && dataType.Equals(SpecTypeId.Length);
                bool isAngle = dataType != null && dataType.Equals(SpecTypeId.Angle);

                double displayVal;
                string suffix;
                if (isLength)
                {
                    displayVal = Math.Round(
                        UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters), 2);
                    suffix = "mm";
                }
                else if (isAngle)
                {
                    displayVal = Math.Round(
                        UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Degrees), 3);
                    suffix = "°";
                }
                else
                {
                    displayVal = Math.Round(p.AsDouble(), 6);
                    suffix = "";
                }

                var row = new WpfGrid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock
                {
                    Text = p.Definition.Name + ":",
                    VerticalAlignment = VerticalAlignment.Center
                };
                WpfGrid.SetColumn(lbl, 0);

                var tb = new TextBox
                {
                    Text = displayVal.ToString(),
                    TextAlignment = TextAlignment.Right,
                    MinWidth = 70
                };
                WpfGrid.SetColumn(tb, 1);

                var sufLbl = new TextBlock
                {
                    Text = suffix,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                WpfGrid.SetColumn(sufLbl, 2);

                row.Children.Add(lbl);
                row.Children.Add(tb);
                row.Children.Add(sufLbl);
                stkParametros.Children.Add(row);

                _paramRows.Add((p.Definition.Name, tb, isLength, isAngle));
            }
        }

        public ConexaoTercasConfig? BuildConfig()
        {
            if (cmbTipo.SelectedItem is not SymbolItem item)
                return null;

            NumberParsing.TryParseDouble(numRotacaoOffset.Text, out double rot);
            NumberParsing.TryParseDouble(numOffsetVertical.Text, out double offV);
            NumberParsing.TryParseDouble(numOffsetLateral.Text, out double offL);

            // Converte valores editados de volta para unidades internas Revit (pes / radianos)
            var parametros = new Dictionary<string, double>();
            foreach (var (name, tb, isLength, isAngle) in _paramRows)
            {
                if (!NumberParsing.TryParseDouble(tb.Text, out double val))
                    continue;
                double internalVal = isLength ? UnitUtils.ConvertToInternalUnits(val, UnitTypeId.Millimeters)
                                  : isAngle ? UnitUtils.ConvertToInternalUnits(val, UnitTypeId.Degrees)
                                              : val;
                parametros[name] = internalVal;
            }

            return new ConexaoTercasConfig
            {
                SymbolSelecionado = item.Symbol,
                ColocarExtremidades = chkExtremidades.IsChecked == true,
                ColocarMeio = chkMeio.IsChecked == true,
                ModoCompleto = chkModoCompleto.IsChecked == true,
                VigaTipoI = chkVigaTipoI.IsChecked == true,
                InverterFace = chkInverterFace.IsChecked == true,
                OffsetRotacaoGraus = rot,
                OffsetVerticalAdicionalMm = offV,
                OffsetLateralMm = offL,
                Referencia = cmbReferenciaChapa.SelectedIndex == 1
                    ? ReferenciaChapa.OrigemTerca
                    : ReferenciaChapa.Cruzamento,
                ParametrosInternos = parametros
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTipo.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Conexão de Terça", "Selecione uma família de conexão.", "Dados incompletos");
                return;
            }
            if (chkExtremidades.IsChecked != true && chkMeio.IsChecked != true)
            {
                AppDialogService.ShowWarning("Conexão de Terça", "Selecione ao menos uma posição de inserção.", "Dados incompletos");
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
