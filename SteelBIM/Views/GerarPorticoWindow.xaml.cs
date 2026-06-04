using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    /// <summary>
    /// v2.8.14: janela de "Gerar Projeto Completo (Pórtico)". Combos planos (Família : Tipo) por
    /// categoria — pilar de OST_StructuralColumns, demais de OST_StructuralFraming. Devolve um
    /// <see cref="GerarPorticoConfig"/> via <see cref="BuildConfig"/>.
    /// </summary>
    public partial class GerarPorticoWindow : Window
    {
        public GerarPorticoWindow(List<FamilySymbol> colunas, List<FamilySymbol> perfis, List<FamilySymbol> ligacoes, List<FamilySymbol> fundacoes)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            cmbPadrao.ItemsSource = Enum.GetValues(typeof(TrussPattern));
            cmbPadrao.SelectedItem = TrussPattern.Warren;

            cmbDistribContravCob.Items.Add("Só extremidades");
            cmbDistribContravCob.Items.Add("Extremidades + centro");
            cmbDistribContravCob.Items.Add("Todos os vãos");
            cmbDistribContravCob.SelectedIndex = 0;

            Popular(cmbPilar, colunas, "2U300");
            Popular(cmbBanzoSup, perfis, "U150");
            Popular(cmbBanzoInf, perfis, "U150");
            Popular(cmbDiagonal, perfis, "L38");
            Popular(cmbMontante, perfis, "L38");
            Popular(cmbViga, perfis, string.Empty);
            Popular(cmbTerca, perfis, "UE150");
            Popular(cmbContravCob, perfis, "BARRA");
            Popular(cmbContravPil, perfis, "BARRA");
            Popular(cmbLinha, perfis, "BARRA");
            Popular(cmbLigacaoTerca, ligacoes, string.Empty);
            Popular(cmbFundacao, fundacoes, string.Empty);

            rbTrelica.Checked += (_, __) => AtualizarCobertura();
            rbViga.Checked += (_, __) => AtualizarCobertura();
            chkTercas.Checked += (_, __) => AtualizarHabilitacao();
            chkTercas.Unchecked += (_, __) => AtualizarHabilitacao();
            chkContravCob.Checked += (_, __) => AtualizarHabilitacao();
            chkContravCob.Unchecked += (_, __) => AtualizarHabilitacao();
            chkContravPil.Checked += (_, __) => AtualizarHabilitacao();
            chkContravPil.Unchecked += (_, __) => AtualizarHabilitacao();
            chkLinha.Checked += (_, __) => AtualizarHabilitacao();
            chkLinha.Unchecked += (_, __) => AtualizarHabilitacao();
            chkLigacaoTerca.Checked += (_, __) => AtualizarHabilitacao();
            chkLigacaoTerca.Unchecked += (_, __) => AtualizarHabilitacao();
            chkFundacoes.Checked += (_, __) => AtualizarHabilitacao();
            chkFundacoes.Unchecked += (_, __) => AtualizarHabilitacao();

            txtNumPorticos.TextChanged += (_, __) => AtualizarResumoGeo();
            txtEspacamento.TextChanged += (_, __) => AtualizarResumoGeo();
            txtVao.TextChanged += (_, __) => AtualizarResumoGeo();

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            AtualizarCobertura();
            AtualizarHabilitacao();
            AtualizarResumoGeo();
        }

        public GerarPorticoConfig? BuildConfig()
        {
            bool usarTrelica = rbTrelica.IsChecked == true;
            return new GerarPorticoConfig
            {
                NumeroPorticos = ParseInt(txtNumPorticos.Text, 7),
                EspacamentoPorticosMm = NumberParsing.ParseDoubleOrDefault(txtEspacamento.Text, 5000.0),
                VaoGalpaoMm = NumberParsing.ParseDoubleOrDefault(txtVao.Text, 15010.0),
                AlturaPilarMm = NumberParsing.ParseDoubleOrDefault(txtAlturaPilar.Text, 4000.0),
                SymbolPilar = Sym(cmbPilar),
                PilarCentral = chkPilarCentral.IsChecked == true,
                UsarTrelica = usarTrelica,
                PadraoTrelica = cmbPadrao.SelectedItem is TrussPattern tp ? tp : TrussPattern.Warren,
                AlturaExtremidadeMm = NumberParsing.ParseDoubleOrDefault(txtH.Text, 600.0),
                AlturaCentralMm = NumberParsing.ParseDoubleOrDefault(txtB.Text, 1600.0),
                DivisoesTrelica = ParseInt(txtDivisoes.Text, 8),
                SymbolBanzoSuperior = Sym(cmbBanzoSup),
                SymbolBanzoInferior = Sym(cmbBanzoInf),
                SymbolDiagonal = Sym(cmbDiagonal),
                SymbolMontante = Sym(cmbMontante),
                RotacaoBanzoSuperiorGraus = NumberParsing.ParseDoubleOrDefault(txtRotBanzoSup.Text, 0.0),
                RotacaoBanzoInferiorGraus = NumberParsing.ParseDoubleOrDefault(txtRotBanzoInf.Text, 0.0),
                RotacaoDiagonalGraus = NumberParsing.ParseDoubleOrDefault(txtRotDiagonal.Text, 0.0),
                RotacaoMontanteGraus = NumberParsing.ParseDoubleOrDefault(txtRotMontante.Text, 0.0),
                SymbolViga = Sym(cmbViga),
                AlturaCumeeiraMm = NumberParsing.ParseDoubleOrDefault(txtCumeeira.Text, 1500.0),
                LancarTercas = chkTercas.IsChecked == true,
                SymbolTerca = Sym(cmbTerca),
                EspacamentoTercasMm = NumberParsing.ParseDoubleOrDefault(txtEspTercas.Text, 1500.0),
                ElevacaoTercasMm = NumberParsing.ParseDoubleOrDefault(txtElevTercas.Text, 150.0),
                InserirLigacaoTerca = chkLigacaoTerca.IsChecked == true,
                SymbolLigacaoTerca = Sym(cmbLigacaoTerca),
                LigacaoOffsetZmm = NumberParsing.ParseDoubleOrDefault(txtLigOffsetZ.Text, 0.0),
                LigacaoOffsetXmm = NumberParsing.ParseDoubleOrDefault(txtLigOffsetX.Text, 0.0),
                LigacaoInverterFace = chkLigInverterFace.IsChecked == true,
                ContravCobertura = chkContravCob.IsChecked == true,
                SymbolContravCobertura = Sym(cmbContravCob),
                TercasPorXCobertura = ParseInt(txtNumXCobertura.Text, 2),
                DistribuicaoContravCobertura = cmbDistribContravCob.SelectedIndex switch
                {
                    1 => DistribuicaoContravCobertura.ExtremidadesECentro,
                    2 => DistribuicaoContravCobertura.Todos,
                    _ => DistribuicaoContravCobertura.Extremidades
                },
                ContravPilares = chkContravPil.IsChecked == true,
                SymbolContravPilares = Sym(cmbContravPil),
                NumeroXPilares = ParseInt(txtNumXPilares.Text, 2),
                LancarLinhaCorrente = chkLinha.IsChecked == true,
                SymbolLinhaCorrente = Sym(cmbLinha),
                NumeroLinhasCorrente = ParseInt(txtNumLinhasCorrente.Text, 3),
                CriarEixos = chkEixos.IsChecked == true,
                LancarPlacasBase = chkPlacas.IsChecked == true,
                LancarFundacoes = chkFundacoes.IsChecked == true,
                SymbolFundacao = Sym(cmbFundacao),
                LancarArmaduraFundacao = chkArmaduraFundacao.IsChecked == true
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (ParseInt(txtNumPorticos.Text, 0) < 2)
            {
                AppDialogService.ShowWarning("Gerar Pórtico", "Informe pelo menos 2 pórticos.", "Dados incompletos");
                return;
            }
            if (cmbPilar.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Gerar Pórtico", "Selecione o perfil do pilar.", "Dados incompletos");
                return;
            }
            if (rbTrelica.IsChecked == true)
            {
                if (cmbBanzoSup.SelectedItem == null || cmbBanzoInf.SelectedItem == null
                    || cmbDiagonal.SelectedItem == null || cmbMontante.SelectedItem == null)
                {
                    AppDialogService.ShowWarning("Gerar Pórtico", "Selecione os perfis da treliça (banzo superior, banzo inferior, diagonal e montante).", "Dados incompletos");
                    return;
                }
            }
            else if (cmbViga.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Gerar Pórtico", "Selecione o perfil da viga.", "Dados incompletos");
                return;
            }

            // validação numérica de faixa: evita que texto inválido caia em fallback silencioso
            // (ex.: B="xyz" geraria treliça com 1600 mm sem o usuário perceber).
            var erros = new List<string>();
            void ReqPos(string texto, string nome)
            {
                if (!TryNum(texto, out double v) || v <= 0.0)
                    erros.Add(nome);
            }
            ReqPos(txtEspacamento.Text, "espaçamento entre pórticos (> 0)");
            ReqPos(txtVao.Text, "vão do galpão (> 0)");
            ReqPos(txtAlturaPilar.Text, "altura do pilar (> 0)");
            if (chkTercas.IsChecked == true)
                ReqPos(txtEspTercas.Text, "espaçamento das terças (> 0)");
            if (rbTrelica.IsChecked == true)
            {
                bool okB = TryNum(txtB.Text, out double bb) && bb > 0.0;
                bool okH = TryNum(txtH.Text, out double hh) && hh >= 0.0;
                if (!okB)
                    erros.Add("altura na cumeeira / B (> 0)");
                if (!okH)
                    erros.Add("altura no apoio / H (≥ 0)");
                if (okB && okH && bb < hh)
                    erros.Add("altura na cumeeira (B) deve ser ≥ altura no apoio (H)");
                if (ParseInt(txtDivisoes.Text, 0) < 1)
                    erros.Add("divisões da treliça (≥ 1)");
            }
            else
            {
                ReqPos(txtCumeeira.Text, "altura da cumeeira da viga (> 0)");
            }
            if (chkContravCob.IsChecked == true && ParseInt(txtNumXCobertura.Text, 0) < 1)
                erros.Add("X de cobertura a cada N terças (≥ 1)");
            if (chkContravPil.IsChecked == true && ParseInt(txtNumXPilares.Text, 0) < 1)
                erros.Add("nº de vãos com X nos pilares (≥ 1)");
            if (chkLinha.IsChecked == true && ParseInt(txtNumLinhasCorrente.Text, 0) < 1)
                erros.Add("nº de fileiras de linha de corrente (≥ 1)");
            if (erros.Count > 0)
            {
                AppDialogService.ShowWarning("Gerar Pórtico",
                    "Corrija os campos numéricos:\n- " + string.Join("\n- ", erros), "Dados inválidos");
                return;
            }

            // seções marcadas porém sem família selecionada (combo vazio): avisa antes de gerar.
            var faltando = new List<string>();
            if (chkContravCob.IsChecked == true && cmbContravCob.SelectedItem == null)
                faltando.Add("contraventamento da cobertura");
            if (chkContravPil.IsChecked == true && cmbContravPil.SelectedItem == null)
                faltando.Add("contraventamento dos pilares");
            if (chkLinha.IsChecked == true && cmbLinha.SelectedItem == null)
                faltando.Add("linha de corrente");
            if (chkLigacaoTerca.IsChecked == true && cmbLigacaoTerca.SelectedItem == null)
                faltando.Add("ligação de terça");
            if (chkFundacoes.IsChecked == true && cmbFundacao.SelectedItem == null)
                faltando.Add("fundações");
            if (faltando.Count > 0 && !AppDialogService.ShowConfirmation(
                "Gerar Pórtico",
                "Estas seções estão marcadas mas sem família selecionada (não serão geradas):\n- "
                    + string.Join("\n- ", faltando) + "\n\nGerar mesmo assim?",
                "Seções sem família"))
            {
                return;
            }

            DialogResult = true;
        }

        private void AtualizarCobertura()
        {
            bool trelica = rbTrelica.IsChecked == true;
            painelTrelica.IsEnabled = trelica;
            painelViga.IsEnabled = !trelica;
        }

        private void AtualizarHabilitacao()
        {
            cmbTerca.IsEnabled = chkTercas.IsChecked == true;
            txtEspTercas.IsEnabled = chkTercas.IsChecked == true;
            txtElevTercas.IsEnabled = chkTercas.IsChecked == true;
            cmbContravCob.IsEnabled = chkContravCob.IsChecked == true;
            cmbDistribContravCob.IsEnabled = chkContravCob.IsChecked == true;
            txtNumXCobertura.IsEnabled = chkContravCob.IsChecked == true;
            cmbContravPil.IsEnabled = chkContravPil.IsChecked == true;
            txtNumXPilares.IsEnabled = chkContravPil.IsChecked == true;
            cmbLinha.IsEnabled = chkLinha.IsChecked == true;
            txtNumLinhasCorrente.IsEnabled = chkLinha.IsChecked == true;
            cmbLigacaoTerca.IsEnabled = chkLigacaoTerca.IsChecked == true;
            txtLigOffsetZ.IsEnabled = chkLigacaoTerca.IsChecked == true;
            txtLigOffsetX.IsEnabled = chkLigacaoTerca.IsChecked == true;
            chkLigInverterFace.IsEnabled = chkLigacaoTerca.IsChecked == true;
            cmbFundacao.IsEnabled = chkFundacoes.IsChecked == true;
            chkArmaduraFundacao.IsEnabled = chkFundacoes.IsChecked == true;
        }

        private void AtualizarResumoGeo()
        {
            if (lblResumoGeo == null)
                return;

            int n = ParseInt(txtNumPorticos.Text, 0);
            double esp = NumberParsing.ParseDoubleOrDefault(txtEspacamento.Text, 0.0);
            double vao = NumberParsing.ParseDoubleOrDefault(txtVao.Text, 0.0);
            double comprimento = n > 1 ? (n - 1) * esp : 0.0;
            lblResumoGeo.Text = $"Galpão ≈ {comprimento:0} × {vao:0} mm  (comprimento × largura)";
        }

        private static void Popular(ComboBox combo, List<FamilySymbol> simbolos, string hint)
        {
            foreach (FamilySymbol s in simbolos)
                combo.Items.Add(new SymbolItem(s));

            if (!string.IsNullOrEmpty(hint))
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    if (combo.Items[i] is SymbolItem item &&
                        item.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        combo.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static FamilySymbol? Sym(ComboBox combo) =>
            (combo.SelectedItem as SymbolItem)?.Symbol;

        private static int ParseInt(string texto, int padrao)
        {
            return int.TryParse(texto?.Trim(), out int v) && v > 0 ? v : padrao;
        }

        private static bool TryNum(string texto, out double valor)
        {
            string n = (texto ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(n, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out valor);
        }
    }
}
