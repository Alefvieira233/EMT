using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class MarcarPecasWindow : Window
    {
        private readonly UIDocument _uidoc;
        private bool _atualizando;

        public MarcarPecasWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            _atualizando = true;
            AtualizarExemplo();
            _atualizando = false;

            AtualizarResumo();
        }

        public MarcarPecasConfig BuildConfig()
        {
            int numInicial = 1;
            if (int.TryParse(txtNumInicial.Text, out int parsed) && parsed >= 0)
                numInicial = parsed;

            int digitos = 3;
            if (cmbDigitos.SelectedItem is ComboBoxItem item
                && item.Tag is string tagStr
                && int.TryParse(tagStr, out int digitsParsed))
            {
                digitos = digitsParsed;
            }

            double tolerancia = 1.0;
            if (double.TryParse(txtTolerancia.Text, out double tolParsed) && tolParsed >= 0)
                tolerancia = tolParsed;

            var destino = DestinoMarca.ParametroMark;
            if (rbParamComments.IsChecked == true) destino = DestinoMarca.ParametroComments;
            if (rbParamCustom.IsChecked == true) destino = DestinoMarca.ParametroCustomizado;

            var escopo = EscopoMarcacao.VistaAtiva;
            if (rbModeloInteiro.IsChecked == true) escopo = EscopoMarcacao.ModeloInteiro;
            if (rbSelecaoManual.IsChecked == true) escopo = EscopoMarcacao.SelecaoManual;

            return new MarcarPecasConfig
            {
                Escopo = escopo,
                MarcarVigas = chkVigas.IsChecked == true,
                MarcarPilares = chkPilares.IsChecked == true,
                MarcarContraventamentos = chkContrav.IsChecked == true,
                PrefixoVigas = txtPrefixoVigas.Text?.Trim() ?? "V",
                PrefixoPilares = txtPrefixoPilares.Text?.Trim() ?? "P",
                PrefixoContraventamentos = txtPrefixoContrav.Text?.Trim() ?? "C",
                NumeroInicial = numInicial,
                Digitos = digitos,
                Destino = destino,
                NomeParametroCustomizado = txtParamCustom.Text?.Trim() ?? "",
                SobrescreverExistentes = chkSobrescrever.IsChecked == true,
                DestaqueVisual = chkDestaqueVisual.IsChecked == true,
                ToleranciaComprimentoMm = tolerancia
            };
        }

        // ================================================================
        //  Eventos
        // ================================================================

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            MarcarPecasConfig config = BuildConfig();

            if (!config.TemCategoriaSelecionada())
            {
                AppDialogService.ShowWarning("Marcação de Peças",
                    "Selecione ao menos uma categoria para marcar.",
                    "Seleção incompleta");
                return;
            }

            if (config.Destino == DestinoMarca.ParametroCustomizado
                && string.IsNullOrWhiteSpace(config.NomeParametroCustomizado))
            {
                AppDialogService.ShowWarning("Marcação de Peças",
                    "Informe o nome do parâmetro customizado.",
                    "Parâmetro ausente");
                return;
            }

            DialogResult = true;
        }

        private void Configuracao_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarResumo();
        }

        private void TxtPrefixo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarExemplo();
            AtualizarResumo();
        }

        private void TxtNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarExemplo();
            AtualizarResumo();
        }

        private void CmbDigitos_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarExemplo();
            AtualizarResumo();
        }

        private void RbParamCustom_Changed(object sender, RoutedEventArgs e)
        {
            if (txtParamCustom != null)
                txtParamCustom.IsEnabled = rbParamCustom.IsChecked == true;

            if (!_atualizando)
                AtualizarResumo();
        }

        // ================================================================
        //  Atualizacao de UI
        // ================================================================

        private void AtualizarExemplo()
        {
            if (txtExemplo == null) return;

            string prefixo = txtPrefixoVigas?.Text?.Trim() ?? "V";
            int numInicial = 1;
            if (txtNumInicial != null && int.TryParse(txtNumInicial.Text, out int parsed) && parsed >= 0)
                numInicial = parsed;

            int digitos = 3;
            if (cmbDigitos?.SelectedItem is ComboBoxItem item
                && item.Tag is string tagStr
                && int.TryParse(tagStr, out int dp))
            {
                digitos = dp;
            }

            string exemplo1 = $"{prefixo}-{numInicial.ToString($"D{digitos}")}";
            string exemplo2 = $"{prefixo}-{(numInicial + 1).ToString($"D{digitos}")}";
            txtExemplo.Text = $"Exemplo: {exemplo1}, {exemplo2}, ...";
        }

        private void AtualizarResumo()
        {
            if (txtResumo == null) return;

            string escopo = "Vista ativa";
            if (rbModeloInteiro?.IsChecked == true) escopo = "Modelo inteiro";
            if (rbSelecaoManual?.IsChecked == true) escopo = "Seleção manual";

            string categorias = string.Join(", ", new[]
            {
                chkVigas?.IsChecked == true ? $"Vigas ({txtPrefixoVigas?.Text ?? "V"})" : null,
                chkPilares?.IsChecked == true ? $"Pilares ({txtPrefixoPilares?.Text ?? "P"})" : null,
                chkContrav?.IsChecked == true ? $"Contrav. ({txtPrefixoContrav?.Text ?? "C"})" : null,
            }.Where(x => x != null));

            if (string.IsNullOrEmpty(categorias))
                categorias = "Nenhuma";

            string destino = "Mark";
            if (rbParamComments?.IsChecked == true) destino = "Comments";
            if (rbParamCustom?.IsChecked == true) destino = $"Customizado ({txtParamCustom?.Text ?? ""})";

            string sobrescrever = chkSobrescrever?.IsChecked == true ? "Sim" : "Não (pula existentes)";
            string destaque = chkDestaqueVisual?.IsChecked == true ? "Sim" : "Não";

            // Contar elementos no escopo atual para dar uma previa
            string previa = "";
            try
            {
                if (_uidoc?.Document != null)
                {
                    int count = ContarElementosNoEscopo(_uidoc.Document);
                    previa = $"\nElementos no escopo: ~{count}";
                }
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao contar elementos para previa"); }

            txtResumo.Text =
                $"Escopo: {escopo}{previa}\n" +
                $"Categorias: {categorias}\n" +
                $"Destino: parâmetro {destino}\n" +
                $"Sobrescrever: {sobrescrever}\n" +
                $"Destaque visual: {destaque}\n\n" +
                "Peças com mesmo perfil, material e comprimento de corte " +
                "recebem a mesma marca. Cada categoria tem numeração independente.";
        }

        private int ContarElementosNoEscopo(Document doc)
        {
            bool usarVista = rbVistaAtiva?.IsChecked == true && doc.ActiveView != null;
            bool usarModelo = rbModeloInteiro?.IsChecked == true;

            if (!usarVista && !usarModelo)
                return 0; // Selecao manual: nao temos como prever

            int count = 0;
            var cats = new[]
            {
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns
            };

            foreach (var cat in cats)
            {
                FilteredElementCollector collector = usarVista
                    ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                    : new FilteredElementCollector(doc);

                count += collector
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }

            return count;
        }
    }
}
