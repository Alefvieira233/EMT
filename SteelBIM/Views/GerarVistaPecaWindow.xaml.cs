using System;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class GerarVistaPecaWindow : Window
    {
        private readonly UIDocument _uidoc;
        private bool _atualizando;

        public GerarVistaPecaWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            CarregarDados();
            AtualizarResumo();
        }

        public GerarVistaPecaConfig BuildConfig()
        {
            int escala = 20;
            if (cmbEscala.SelectedItem is System.Windows.Controls.ComboBoxItem item
                && item.Tag is string tagStr
                && int.TryParse(tagStr, out int parsed))
            {
                escala = parsed;
            }

            double margem = 150;
            if (NumberParsing.TryParseDouble(txtMargem.Text, out double margemParsed) && margemParsed > 0)
                margem = margemParsed;

            string titleBlockFamily = "";
            string titleBlockType = "";
            if (chkCriarFolha.IsChecked == true && cmbTitleBlock.SelectedItem is TitleBlockItem tb)
            {
                titleBlockFamily = tb.FamilyName;
                titleBlockType = tb.TypeName;
            }

            return new GerarVistaPecaConfig
            {
                Escopo = rbVistaAtiva.IsChecked == true
                    ? EscopoSelecaoPeca.VistaAtiva
                    : EscopoSelecaoPeca.SelecaoManual,
                CriarVistaLongitudinal = chkLongitudinal.IsChecked == true,
                CriarCorteTransversal = chkTransversal.IsChecked == true,
                EscalaVista = escala,
                MargemMm = margem,
                PrefixoNome = txtPrefixo.Text?.Trim() ?? "SD",
                CriarFolha = chkCriarFolha.IsChecked == true,
                FamiliaFolhaTitulo = titleBlockFamily,
                TipoFolhaTitulo = titleBlockType
            };
        }

        private void CarregarDados()
        {
            _atualizando = true;

            // Carregar title blocks disponiveis
            if (_uidoc?.Document != null)
            {
                var titleBlocks = new FilteredElementCollector(_uidoc.Document)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Select(fs => new TitleBlockItem
                    {
                        FamilyName = fs.Family.Name,
                        TypeName = fs.Name,
                        DisplayName = $"{fs.Family.Name} : {fs.Name}"
                    })
                    .ToList();

                cmbTitleBlock.ItemsSource = titleBlocks;
                cmbTitleBlock.DisplayMemberPath = "DisplayName";
                if (titleBlocks.Count > 0)
                    cmbTitleBlock.SelectedIndex = 0;
            }

            _atualizando = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            GerarVistaPecaConfig config = BuildConfig();

            if (!config.TemVistasSelecionadas())
            {
                AppDialogService.ShowWarning("Auto-Vista de Peça",
                    "Selecione ao menos um tipo de vista (longitudinal ou transversal).",
                    "Seleção incompleta");
                return;
            }

            DialogResult = true;
        }

        private void Configuracao_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarResumo();
        }

        private void ChkCriarFolha_Changed(object sender, RoutedEventArgs e)
        {
            if (gridFolha != null)
            {
                gridFolha.Visibility = chkCriarFolha.IsChecked == true
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }

            if (!_atualizando)
                AtualizarResumo();
        }

        private void AtualizarResumo()
        {
            if (txtResumo == null) return;

            string escopo = rbVistaAtiva?.IsChecked == true
                ? "Todos os elementos da vista ativa"
                : "Seleção manual";

            string vistas = string.Join(", ", new[]
            {
                chkLongitudinal?.IsChecked == true ? "Longitudinal" : null,
                chkTransversal?.IsChecked == true ? "Transversal" : null
            }.Where(x => x != null));

            if (string.IsNullOrEmpty(vistas))
                vistas = "Nenhuma";

            string escala = "1:20";
            if (cmbEscala?.SelectedItem is System.Windows.Controls.ComboBoxItem item
                && item.Tag is string tag)
            {
                escala = $"1:{tag}";
            }

            string folha = chkCriarFolha?.IsChecked == true ? "Sim" : "Não";

            txtResumo.Text =
                $"Escopo: {escopo}\n" +
                $"Vistas: {vistas}\n" +
                $"Escala: {escala}\n" +
                $"Prefixo: {txtPrefixo?.Text ?? "SD"}\n" +
                $"Criar folha: {folha}\n\n" +
                "Para cada peça selecionada será criada uma vista de corte " +
                "com crop ajustado ao elemento. Os nomes seguem o padrão: " +
                "[Prefixo]-[Marca]-[Tipo].";
        }

        // Classe auxiliar para ComboBox de title blocks
        private class TitleBlockItem
        {
            public string FamilyName { get; set; } = "";
            public string TypeName { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }
    }
}
