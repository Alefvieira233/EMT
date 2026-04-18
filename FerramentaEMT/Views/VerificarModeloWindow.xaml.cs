using System;
using System.Windows;
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
                ExcelPath = txtExcelPath.Text ?? string.Empty
            };

            return config;
        }
    }
}
