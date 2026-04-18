using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class ExportarDstvWindow : Window
    {
        private readonly UIDocument _uidoc;

        public ExportarDstvWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            // Pre-popular pasta com .\DSTV ao lado do arquivo Revit (se possivel)
            try
            {
                if (!string.IsNullOrWhiteSpace(_uidoc?.Document?.PathName))
                {
                    string pastaSugerida = Path.Combine(
                        Path.GetDirectoryName(_uidoc.Document.PathName) ?? "",
                        "DSTV");
                    txtPasta.Text = pastaSugerida;
                }
            }
            catch (Exception ex) { Logger.Warn(ex, "Falha ao sugerir pasta destino"); }
        }

        public ExportarDstvConfig BuildConfig()
        {
            var config = new ExportarDstvConfig
            {
                PastaDestino = txtPasta.Text?.Trim() ?? "",
                CodigoProjeto = txtCodigoProjeto.Text?.Trim() ?? "",
                Fase = string.IsNullOrWhiteSpace(txtFase.Text) ? "1" : txtFase.Text.Trim(),
                NomeParametroMarca = txtParamMarca.Text?.Trim() ?? "",
                ExportarVigas = chkVigas.IsChecked == true,
                ExportarPilares = chkPilares.IsChecked == true,
                ExportarContraventamentos = chkContrav.IsChecked == true,
                SobrescreverExistentes = chkSobrescrever.IsChecked == true,
                GerarRelatorio = chkRelatorio.IsChecked == true,
                AbrirPastaAposExportar = chkAbrirPasta.IsChecked == true,
                Agrupamento = cmbAgrupamento.SelectedIndex == 1
                    ? AgrupamentoArquivosDstv.UmPorInstancia
                    : AgrupamentoArquivosDstv.UmPorMarca
            };

            if (rbModelo.IsChecked == true)
                config.Escopo = EscopoExportacaoDstv.ModeloInteiro;
            else if (rbVistaAtiva.IsChecked == true)
                config.Escopo = EscopoExportacaoDstv.VistaAtiva;
            else
                config.Escopo = EscopoExportacaoDstv.SelecaoManual;

            return config;
        }

        private void BtnProcurar_Click(object sender, RoutedEventArgs e)
        {
            // System.Windows.Forms.FolderBrowserDialog requer referencia ao System.Windows.Forms.
            // Para evitar a referencia, fazemos um SaveFileDialog truque ou usamos OpenFolderDialog se .NET 8.
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Selecione a pasta destino para os arquivos NC1",
                    InitialDirectory = string.IsNullOrWhiteSpace(txtPasta.Text)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : txtPasta.Text
                };
                if (dialog.ShowDialog() == true)
                    txtPasta.Text = dialog.FolderName;
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError("Exportar DSTV/NC1",
                    $"Nao foi possivel abrir o seletor de pasta:\n{ex.Message}",
                    "Erro de UI");
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ExportarDstvConfig cfg = BuildConfig();

            if (string.IsNullOrWhiteSpace(cfg.PastaDestino))
            {
                AppDialogService.ShowWarning("Exportar DSTV/NC1",
                    "Informe a pasta de destino dos arquivos .nc1.",
                    "Pasta nao informada");
                return;
            }

            if (!cfg.ExportarVigas && !cfg.ExportarPilares && !cfg.ExportarContraventamentos)
            {
                AppDialogService.ShowWarning("Exportar DSTV/NC1",
                    "Selecione ao menos uma categoria.",
                    "Categorias nao selecionadas");
                return;
            }

            DialogResult = true;
        }
    }
}
