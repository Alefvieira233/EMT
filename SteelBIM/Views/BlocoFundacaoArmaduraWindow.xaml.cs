#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.Bloco;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class BlocoFundacaoArmaduraWindow : Window
    {
        public BlocoFundacaoArmaduraWindow(Document doc, Element? sampleElement = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            foreach (PfRebarBarTypeOption option in PfRebarTypeCatalog.Load(doc))
            {
                cmbInfXBarra.Items.Add(option);
                cmbInfYBarra.Items.Add(option);
                cmbSupXBarra.Items.Add(option);
                cmbSupYBarra.Items.Add(option);
                cmbLatBarra.Items.Add(option);
                cmbEstVertBarra.Items.Add(option);
                cmbEstHorizBarra.Items.Add(option);
                cmbFaixaTransBarra.Items.Add(option);
            }

            SelectDefault(cmbInfXBarra, "12.5 CA-50");
            SelectDefault(cmbInfYBarra, "12.5 CA-50");
            SelectDefault(cmbSupXBarra, "12.5 CA-50");
            SelectDefault(cmbSupYBarra, "12.5 CA-50");
            SelectDefault(cmbLatBarra, "8 CA-50");
            SelectDefault(cmbEstVertBarra, "8 CA-50");
            SelectDefault(cmbEstHorizBarra, "8 CA-50");
            SelectDefault(cmbFaixaTransBarra, "10 CA-50");

            txtElementInfo.Text = sampleElement == null
                ? "Selecione um bloco de fundacao para usar a geometria real."
                : PfElementService.GetHostPreview(sampleElement);

            WireRadioModeHandlers();

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        // -------------------------------------------------------------------
        // Build config
        // -------------------------------------------------------------------

        public BlocoFundacaoRebarConfig BuildConfig()
        {
            BlocoFundacaoRebarConfig c = new BlocoFundacaoRebarConfig
            {
                LancarArmaduraInferior = chkLancarInferior.IsChecked == true,
                LancarArmaduraSuperior = chkLancarSuperior.IsChecked == true,
                LancarArmaduraLateral  = chkLancarLateral.IsChecked == true,
                LancarEstriboVertical  = chkLancarEstVert.IsChecked == true,
                LancarEstriboHorizontal= chkLancarEstHoriz.IsChecked == true,
                LancarFaixaTransversal = chkLancarFaixaTrans.IsChecked == true
            };

            // --- Inferior ---
            FillBarDir(c.ArmaduraInferior.BarsX,
                chkInfXAtivo, cmbInfXBarra, txtInfXCob, txtInfXOffZ,
                rbInfXQtd, txtInfXQtd, txtInfXEsp,
                chkInfXDobI, txtInfXDobIComp, rbInfXDobICima,
                chkInfXDobF, txtInfXDobFComp, rbInfXDobFCima);

            FillBarDir(c.ArmaduraInferior.BarsY,
                chkInfYAtivo, cmbInfYBarra, txtInfYCob, txtInfYOffZ,
                rbInfYQtd, txtInfYQtd, txtInfYEsp,
                chkInfYDobI, txtInfYDobIComp, rbInfYDobICima,
                chkInfYDobF, txtInfYDobFComp, rbInfYDobFCima);

            // --- Superior ---
            FillBarDir(c.ArmaduraSuperior.BarsX,
                chkSupXAtivo, cmbSupXBarra, txtSupXCob, txtSupXOffZ,
                rbSupXQtd, txtSupXQtd, txtSupXEsp,
                chkSupXDobI, txtSupXDobIComp, rbSupXDobICima,
                chkSupXDobF, txtSupXDobFComp, rbSupXDobFCima);

            FillBarDir(c.ArmaduraSuperior.BarsY,
                chkSupYAtivo, cmbSupYBarra, txtSupYCob, txtSupYOffZ,
                rbSupYQtd, txtSupYQtd, txtSupYEsp,
                chkSupYDobI, txtSupYDobIComp, rbSupYDobICima,
                chkSupYDobF, txtSupYDobFComp, rbSupYDobFCima);

            // --- Lateral ---
            c.ArmaduraLateral.BarTypeName  = BarName(cmbLatBarra);
            c.ArmaduraLateral.CobrimentoCm = ParseD(txtLatCob.Text, 5.0);
            c.ArmaduraLateral.ModoQuantidade = rbLatQtd.IsChecked == true
                ? BlocoModoQuantidade.PorQuantidade : BlocoModoQuantidade.PorEspacamento;
            c.ArmaduraLateral.Quantidade   = ParseI(txtLatQtd.Text, 2);
            c.ArmaduraLateral.EspacamentoCm= ParseD(txtLatEsp.Text, 20.0);
            c.ArmaduraLateral.LancarNasFacesX = chkLatFacesX.IsChecked == true;
            c.ArmaduraLateral.LancarNasFacesY = chkLatFacesY.IsChecked == true;
            FillBend(c.ArmaduraLateral.Dobra,
                chkLatDobI, txtLatDobIComp, rbLatDobICima,
                chkLatDobF, txtLatDobFComp, rbLatDobFCima);

            // --- Estribo Vertical ---
            c.EstriboVertical.BarTypeName  = BarName(cmbEstVertBarra);
            c.EstriboVertical.CobrimentoCm = ParseD(txtEstVertCob.Text, 5.0);
            c.EstriboVertical.AnguloGarras = GarraAngle(cmbEstVertGarra);
            c.EstriboVertical.DirX.Ativo   = chkEstVertXAtivo.IsChecked == true;
            c.EstriboVertical.DirX.ModoQuantidade = rbEstVertXQtd.IsChecked == true
                ? BlocoModoQuantidade.PorQuantidade : BlocoModoQuantidade.PorEspacamento;
            c.EstriboVertical.DirX.Quantidade   = ParseI(txtEstVertXQtd.Text, 2);
            c.EstriboVertical.DirX.EspacamentoCm= ParseD(txtEstVertXEsp.Text, 20.0);
            c.EstriboVertical.DirY.Ativo   = chkEstVertYAtivo.IsChecked == true;
            c.EstriboVertical.DirY.ModoQuantidade = rbEstVertYQtd.IsChecked == true
                ? BlocoModoQuantidade.PorQuantidade : BlocoModoQuantidade.PorEspacamento;
            c.EstriboVertical.DirY.Quantidade   = ParseI(txtEstVertYQtd.Text, 2);
            c.EstriboVertical.DirY.EspacamentoCm= ParseD(txtEstVertYEsp.Text, 20.0);

            // --- Estribo Horizontal ---
            c.EstriboHorizontal.BarTypeName  = BarName(cmbEstHorizBarra);
            c.EstriboHorizontal.CobrimentoCm = ParseD(txtEstHorizCob.Text, 5.0);
            c.EstriboHorizontal.AnguloGarras = GarraAngle(cmbEstHorizGarra);
            c.EstriboHorizontal.ModoQuantidade = rbEstHorizQtd.IsChecked == true
                ? BlocoModoQuantidade.PorQuantidade : BlocoModoQuantidade.PorEspacamento;
            c.EstriboHorizontal.Quantidade   = ParseI(txtEstHorizQtd.Text, 2);
            c.EstriboHorizontal.EspacamentoCm= ParseD(txtEstHorizEsp.Text, 20.0);

            // --- Faixa Transversal ---
            c.FaixaTransversal.BarTypeName  = BarName(cmbFaixaTransBarra);
            c.FaixaTransversal.CobrimentoCm = ParseD(txtFaixaTransCob.Text, 5.0);
            c.FaixaTransversal.EspacamentoCm= ParseD(txtFaixaTransEsp.Text, 15.0);
            c.FaixaTransversal.PosicaoZCm   = ParseD(txtFaixaTransPosZ.Text, 10.0);
            c.FaixaTransversal.Direcao = rbFaixaTransX.IsChecked == true ? BlocoDirecao.ApenasX
                : rbFaixaTransY.IsChecked == true ? BlocoDirecao.ApenasY : BlocoDirecao.AmbosXeY;
            FillBend(c.FaixaTransversal.Dobra,
                chkFaixaTransDobI, txtFaixaTransDobIComp, rbFaixaTransDobICima,
                chkFaixaTransDobF, txtFaixaTransDobFComp, rbFaixaTransDobFCima);

            return c;
        }

        // -------------------------------------------------------------------
        // Validacao
        // -------------------------------------------------------------------

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            BlocoFundacaoRebarConfig c = BuildConfig();

            bool algumAtivo =
                c.LancarArmaduraInferior ||
                c.LancarArmaduraSuperior ||
                c.LancarArmaduraLateral  ||
                c.LancarEstriboVertical  ||
                c.LancarEstriboHorizontal||
                c.LancarFaixaTransversal;

            if (!algumAtivo)
            {
                AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                    "Marque ao menos um tipo de armadura para lancar.", "Dados invalidos");
                return;
            }

            if (c.LancarArmaduraInferior)
            {
                bool infSemBarra = (!c.ArmaduraInferior.BarsX.Ativo || string.IsNullOrWhiteSpace(c.ArmaduraInferior.BarsX.BarTypeName))
                    && (!c.ArmaduraInferior.BarsY.Ativo || string.IsNullOrWhiteSpace(c.ArmaduraInferior.BarsY.BarTypeName));
                if (infSemBarra)
                {
                    AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                        "Armadura Inferior: ative ao menos uma direcao (X ou Y) e selecione o vergalhao.", "Dados invalidos");
                    return;
                }
            }

            if (c.LancarArmaduraSuperior)
            {
                bool supSemBarra = (!c.ArmaduraSuperior.BarsX.Ativo || string.IsNullOrWhiteSpace(c.ArmaduraSuperior.BarsX.BarTypeName))
                    && (!c.ArmaduraSuperior.BarsY.Ativo || string.IsNullOrWhiteSpace(c.ArmaduraSuperior.BarsY.BarTypeName));
                if (supSemBarra)
                {
                    AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                        "Armadura Superior: ative ao menos uma direcao (X ou Y) e selecione o vergalhao.", "Dados invalidos");
                    return;
                }
            }

            if (c.LancarArmaduraLateral && string.IsNullOrWhiteSpace(c.ArmaduraLateral.BarTypeName))
            {
                AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                    "Armadura Lateral: selecione o vergalhao.", "Dados invalidos");
                return;
            }

            if (c.LancarEstriboVertical && string.IsNullOrWhiteSpace(c.EstriboVertical.BarTypeName))
            {
                AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                    "Estribos Verticais: selecione o vergalhao.", "Dados invalidos");
                return;
            }

            if (c.LancarEstriboHorizontal && string.IsNullOrWhiteSpace(c.EstriboHorizontal.BarTypeName))
            {
                AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                    "Estribos Horizontais: selecione o vergalhao.", "Dados invalidos");
                return;
            }

            if (c.LancarFaixaTransversal && string.IsNullOrWhiteSpace(c.FaixaTransversal.BarTypeName))
            {
                AppDialogService.ShowWarning("Bloco Fundacao - Armaduras",
                    "Faixa Transversal: selecione o vergalhao.", "Dados invalidos");
                return;
            }

            DialogResult = true;
        }

        // -------------------------------------------------------------------
        // Wire mode radio handlers
        // -------------------------------------------------------------------

        private void WireRadioModeHandlers()
        {
            WireMode(rbInfXQtd, rbInfXEsp, txtInfXQtd, txtInfXEsp);
            WireMode(rbInfYQtd, rbInfYEsp, txtInfYQtd, txtInfYEsp);
            WireMode(rbSupXQtd, rbSupXEsp, txtSupXQtd, txtSupXEsp);
            WireMode(rbSupYQtd, rbSupYEsp, txtSupYQtd, txtSupYEsp);
            WireMode(rbLatQtd, rbLatEsp, txtLatQtd, txtLatEsp);
            WireMode(rbEstVertXQtd, rbEstVertXEsp, txtEstVertXQtd, txtEstVertXEsp);
            WireMode(rbEstVertYQtd, rbEstVertYEsp, txtEstVertYQtd, txtEstVertYEsp);
            WireMode(rbEstHorizQtd, rbEstHorizEsp, txtEstHorizQtd, txtEstHorizEsp);
        }

        private static void WireMode(RadioButton rbQtd, RadioButton rbEsp, TextBox txtQtd, TextBox txtEsp)
        {
            rbQtd.Checked += (_, __) => { txtQtd.IsEnabled = true; txtEsp.IsEnabled = false; };
            rbEsp.Checked += (_, __) => { txtQtd.IsEnabled = false; txtEsp.IsEnabled = true; };
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static void FillBarDir(
            BlocoBarraDirecaoConfig d,
            CheckBox chkAtivo, ComboBox cmbBarra, TextBox txtCob, TextBox txtOffZ,
            RadioButton rbQtd, TextBox txtQtd, TextBox txtEsp,
            CheckBox chkDobI, TextBox txtDobIComp, RadioButton rbDobICima,
            CheckBox chkDobF, TextBox txtDobFComp, RadioButton rbDobFCima)
        {
            d.Ativo       = chkAtivo.IsChecked == true;
            d.BarTypeName = BarName(cmbBarra);
            d.CobrimentoCm= ParseD(txtCob.Text, 5.0);
            d.OffsetZCm   = ParseD(txtOffZ.Text, 0.0);
            d.ModoQuantidade = rbQtd.IsChecked == true
                ? BlocoModoQuantidade.PorQuantidade : BlocoModoQuantidade.PorEspacamento;
            d.Quantidade   = ParseI(txtQtd.Text, 4);
            d.EspacamentoCm= ParseD(txtEsp.Text, 15.0);
            FillBend(d.Dobra, chkDobI, txtDobIComp, rbDobICima, chkDobF, txtDobFComp, rbDobFCima);
        }

        private static void FillBend(
            BlocoRebarBendConfig b,
            CheckBox chkDobI, TextBox txtDobIComp, RadioButton rbDobICima,
            CheckBox chkDobF, TextBox txtDobFComp, RadioButton rbDobFCima)
        {
            b.HaDobraInicial = chkDobI.IsChecked == true;
            b.ComprimentoDobraInicialCm = ParseD(txtDobIComp.Text, 10.0);
            b.DobraInicialParaCima = rbDobICima.IsChecked == true;
            b.HaDobraFinal = chkDobF.IsChecked == true;
            b.ComprimentoDobraFinalCm = ParseD(txtDobFComp.Text, 10.0);
            b.DobraFinalParaCima = rbDobFCima.IsChecked == true;
        }

        private static string BarName(ComboBox cmb)
            => (cmb.SelectedItem as PfRebarBarTypeOption)?.Name ?? string.Empty;

        private static BlocoAnguloGancho GarraAngle(ComboBox cmb)
        {
            string tag = (cmb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "135°";
            if (tag.StartsWith("90")) return BlocoAnguloGancho.Graus90;
            if (tag.StartsWith("180")) return BlocoAnguloGancho.Graus180;
            return BlocoAnguloGancho.Graus135;
        }

        private static void SelectDefault(ComboBox cmb, string preferred)
        {
            if (!PfRebarTypeCatalog.TrySelect(cmb, preferred) && cmb.Items.Count > 0)
                cmb.SelectedIndex = 0;
        }

        private static int ParseI(string text, int fallback)
            => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        private static double ParseD(string text, double fallback)
        {
            string n = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }
    }
}
