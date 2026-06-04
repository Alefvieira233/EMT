#nullable enable
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using SteelBIM.Models.Bloco;
using SteelBIM.Services.PF;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    /// <summary>
    /// v2.8.21 (Fase 1): janela do comando "Armadura de Fundacao - Coroamento (Gaiola)".
    /// Coleta os vergalhoes da malha de fundo/topo, estribo e pele, alem de cobrimento,
    /// desconto do topo da estaca e ganchos. Produz um <see cref="CoroamentoConfig"/>.
    /// </summary>
    public partial class ArmaduraCoroamentoWindow : Window
    {
        public ArmaduraCoroamentoWindow(Document doc, Element? sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbMalhaFundo.Items.Add(option);
                cmbMalhaTopo.Items.Add(option);
                cmbEstribo.Items.Add(option);
                cmbPele.Items.Add(option);
            }

            SelectDefault(cmbMalhaFundo, "12.5 CA-50");
            SelectDefault(cmbMalhaTopo, "10 CA-50");
            SelectDefault(cmbEstribo, "8 CA-50");
            SelectDefault(cmbPele, "8 CA-50");

            txtElementInfo.Text = sampleElement == null
                ? "Selecione um bloco de coroamento para usar a geometria real."
                : PfElementService.GetHostPreview(sampleElement);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        public CoroamentoConfig BuildConfig()
        {
            return new CoroamentoConfig
            {
                CobrimentoBlocoCm = ParseD(txtCobrimento.Text, 5.0),
                TopoEstacaEmbutidoCm = ParseD(txtTopoEstaca.Text, 5.0),
                FecharGaiola = chkFecharGaiola.IsChecked == true,
                GanchoMalhaCm = ParseD(txtGanchoMalha.Text, 10.0),

                MalhaFundoBarType = BarName(cmbMalhaFundo),
                MalhaFundoEspacamentoCm = ParseD(txtMalhaFundoEsp.Text, 15.0),

                LancarMalhaTopo = chkMalhaTopo.IsChecked == true,
                MalhaTopoBarType = BarName(cmbMalhaTopo),
                MalhaTopoEspacamentoCm = ParseD(txtMalhaTopoEsp.Text, 20.0),

                EstriboBarType = BarName(cmbEstribo),
                EstriboEspacamentoCm = ParseD(txtEstriboEsp.Text, 20.0),

                LancarPeleLateral = chkPeleLateral.IsChecked == true,
                PeleBarType = BarName(cmbPele),
                PeleEspacamentoCm = ParseD(txtPeleEsp.Text, 20.0)
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            CoroamentoConfig c = BuildConfig();

            if (string.IsNullOrWhiteSpace(c.MalhaFundoBarType))
            {
                AppDialogService.ShowWarning("Armadura de Coroamento",
                    "Malha de fundo: selecione o vergalhao (e a armadura principal do bloco).", "Dados invalidos");
                return;
            }

            if (c.FecharGaiola && string.IsNullOrWhiteSpace(c.EstriboBarType))
            {
                AppDialogService.ShowWarning("Armadura de Coroamento",
                    "Para fechar a gaiola, selecione o vergalhao do estribo perimetral (ou desmarque \"Fechar gaiola\").",
                    "Dados invalidos");
                return;
            }

            if (c.LancarMalhaTopo && string.IsNullOrWhiteSpace(c.MalhaTopoBarType))
            {
                AppDialogService.ShowWarning("Armadura de Coroamento",
                    "Malha de topo: selecione o vergalhao (ou desmarque \"Lancar malha de topo\").", "Dados invalidos");
                return;
            }

            if (c.LancarPeleLateral && string.IsNullOrWhiteSpace(c.PeleBarType))
            {
                AppDialogService.ShowWarning("Armadura de Coroamento",
                    "Pele lateral: selecione o vergalhao (ou desmarque \"Lancar pele lateral\").", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        private static string BarName(ComboBox cmb)
            => (cmb.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty;

        private static void SelectDefault(ComboBox cmb, string preferred)
        {
            if (!PfRebarTypeCatalog.TrySelect(cmb, preferred) && cmb.Items.Count > 0)
                cmb.SelectedIndex = 0;
        }

        private static double ParseD(string text, double fallback)
        {
            string n = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }
    }
}
