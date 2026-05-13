using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Utils;
using Microsoft.Win32;

namespace FerramentaEMT.Views
{
    public partial class VerificarModeloWindow : Window
    {
        private readonly UIDocument _uidoc;

        public VerificarModeloWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ChkExportarExcel_Checked(object sender, RoutedEventArgs e)
        {
            txtExcelPath.IsEnabled = true;
            btnBrowseExcel.IsEnabled = true;
        }

        private void ChkExportarExcel_Unchecked(object sender, RoutedEventArgs e)
        {
            txtExcelPath.IsEnabled = false;
            btnBrowseExcel.IsEnabled = false;
        }

        private void BtnBrowseExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Arquivos Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = "modelo-verificacao.xlsx"
            };

            if (dialog.ShowDialog() == true)
                txtExcelPath.Text = dialog.FileName;
        }

        private void Configuracao_Changed(object sender, RoutedEventArgs e)
        {
            // Hook para validacao futura, se necessario
        }

        // --- Carimbo (TitleBlock) ---

        private void ChkCarimbo_Checked(object sender, RoutedEventArgs e)
        {
            pnlCarimboOpcoes.IsEnabled = true;
        }

        private void ChkCarimbo_Unchecked(object sender, RoutedEventArgs e)
        {
            pnlCarimboOpcoes.IsEnabled = false;
        }

        private void BtnAddParametro_Click(object sender, RoutedEventArgs e)
        {
            var texto = txtNovoParametro.Text?.Trim();
            if (string.IsNullOrEmpty(texto))
                return;

            // Verificar duplicata (case-insensitive)
            foreach (var item in lstParametros.Items)
            {
                if (string.Equals(item as string, texto, StringComparison.OrdinalIgnoreCase))
                {
                    txtNovoParametro.SelectAll();
                    txtNovoParametro.Focus();
                    return;
                }
            }

            lstParametros.Items.Add(texto);
            txtNovoParametro.Clear();
            txtNovoParametro.Focus();
        }

        private void BtnRemoveParametro_Click(object sender, RoutedEventArgs e)
        {
            if (lstParametros.SelectedItem != null)
                lstParametros.Items.Remove(lstParametros.SelectedItem);
        }

        private void TxtNovoParametro_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddParametro_Click(sender, e);
        }

        /// <summary>
        /// Preenche os controles da Window a partir de uma ModelCheckConfig salva.
        /// Reservado para persistencia futura (atualmente nao chamado).
        /// </summary>
        private void AplicarConfig(ModelCheckConfig config)
        {
            if (config == null) return;
            // TODO (futuro): preencher controles a partir de config
            // Ex: chkCarimbo.IsChecked = config.RunTitleBlockParameters;
            //     txtCarimboFamilia.Text = config.TitleBlockFamilyName;
            //     (etc para todas as propriedades relevantes)
        }

        public ModelCheckConfig BuildConfig()
        {
            var config = new ModelCheckConfig
            {
                // Escopo
                ScopeViewOnly = rbVistaAtiva.IsChecked == true,

                // Regras
                RunMissingMaterial = chkMaterial.IsChecked == true,
                RunMissingMark = chkMarca.IsChecked == true,
                RunDuplicateMark = chkMarcaDuplicada.IsChecked == true,
                RunOverlappingElements = chkSobreposicao.IsChecked == true,
                RunMissingProfile = chkPerfil.IsChecked == true,
                RunZeroLength = chkComprimento.IsChecked == true,
                RunMissingLevel = chkNivel.IsChecked == true,
                RunStructuralWithoutType = chkTipo.IsChecked == true,
                RunMissingComment = chkComentario.IsChecked == true,
                RunOrphanGroup = chkGrupoOrfao.IsChecked == true,

                // Excel
                ExportExcel = chkExportarExcel.IsChecked == true,
                ExcelPath = txtExcelPath.Text ?? string.Empty,

                // Carimbo (TitleBlock)
                RunTitleBlockParameters = chkCarimbo.IsChecked == true,
                TitleBlockScopeActiveSheetOnly = chkCarimboFolhaAtiva.IsChecked == true,
                TitleBlockFamilyName = txtCarimboFamilia.Text?.Trim() ?? string.Empty,
                TitleBlockTypeName = txtCarimboTipo.Text?.Trim() ?? string.Empty,
                TitleBlockParameters = new List<string>(lstParametros.Items.Cast<string>())
            };

            return config;
        }
    }
}
