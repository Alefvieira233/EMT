using System;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using Microsoft.Win32;

namespace FerramentaEMT.Views
{
    public partial class ExportarListaMateriaisWindow : Window
    {
        private readonly UIDocument _uidoc;
        private bool _atualizando;

        public ExportarListaMateriaisWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;

            btnProcurar.Click += BtnProcurar_Click;
            btnCancelar.Click += (_, __) => DialogResult = false;
            btnExportar.Click += BtnExportar_Click;

            CarregarEstadoInicial();
        }

        public ExportarListaMateriaisConfig BuildConfig()
        {
            return new ExportarListaMateriaisConfig
            {
                Escopo = ObterEscopoSelecionado(),
                IncluirVigas = chkVigas.IsChecked == true,
                IncluirPilares = chkPilares.IsChecked == true,
                IncluirFundacoes = chkFundacoes.IsChecked == true,
                IncluirContraventamentos = chkContraventamentos.IsChecked == true,
                IncluirChapasConexoes = chkChapasConexoes.IsChecked == true,
                ExportarPerfisLineares = chkAbaPerfis.IsChecked == true,
                ExportarChapas = chkAbaChapas.IsChecked == true,
                ExportarResumo = chkAbaResumo.IsChecked == true,
                CaminhoArquivo = txtCaminhoArquivo.Text?.Trim() ?? string.Empty
            };
        }

        private void CarregarEstadoInicial()
        {
            _atualizando = true;

            rbVistaAtiva.IsChecked = true;
            chkVigas.IsChecked = true;
            chkPilares.IsChecked = true;
            chkFundacoes.IsChecked = true;
            chkContraventamentos.IsChecked = true;
            chkChapasConexoes.IsChecked = true;
            chkAbaPerfis.IsChecked = true;
            chkAbaChapas.IsChecked = true;
            chkAbaResumo.IsChecked = true;

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            txtCaminhoArquivo.Text = Path.Combine(desktop, $"ListaMateriais_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

            _atualizando = false;
            AtualizarResumo();
        }

        private void BtnProcurar_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Salvar Lista de Materiais",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = Path.GetFileName(txtCaminhoArquivo.Text)
            };

            string diretorio = Path.GetDirectoryName(txtCaminhoArquivo.Text);
            if (!string.IsNullOrWhiteSpace(diretorio) && Directory.Exists(diretorio))
                dialog.InitialDirectory = diretorio;

            if (dialog.ShowDialog(this) == true)
            {
                txtCaminhoArquivo.Text = dialog.FileName;
            }
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            ExportarListaMateriaisConfig config = BuildConfig();
            if (!config.TemCategoriaSelecionada())
            {
                AppDialogService.ShowWarning("Exportar Lista de Materiais", "Selecione ao menos uma categoria para exportar.", "Seleção incompleta");
                return;
            }

            if (!config.TemAbaSelecionada())
            {
                AppDialogService.ShowWarning("Exportar Lista de Materiais", "Selecione ao menos uma aba de saída.", "Seleção incompleta");
                return;
            }

            if (string.IsNullOrWhiteSpace(config.CaminhoArquivo))
            {
                AppDialogService.ShowWarning("Exportar Lista de Materiais", "Informe o caminho do arquivo Excel.", "Arquivo de saída ausente");
                return;
            }

            if (!config.CaminhoArquivo.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                AppDialogService.ShowWarning("Exportar Lista de Materiais", "O arquivo de saída deve usar a extensão .xlsx.", "Extensão inválida");
                return;
            }

            DialogResult = true;
        }

        private void Configuracao_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarResumo();
        }

        private void Configuracao_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarResumo();
        }

        private void AtualizarResumo()
        {
            ExportarListaMateriaisConfig config = BuildConfig();

            txtPreviewArquivo.Text = $"Arquivo de saída: {config.CaminhoArquivo}";

            string escopo = config.Escopo switch
            {
                ListaMateriaisEscopo.ModeloInteiro => "Modelo inteiro",
                ListaMateriaisEscopo.VistaAtiva => "Vista ativa",
                ListaMateriaisEscopo.SelecaoAtual => "Seleção atual",
                _ => "Desconhecido"
            };

            string categorias = string.Join(", ", new[]
            {
                config.IncluirVigas ? "Vigas" : null,
                config.IncluirPilares ? "Pilares" : null,
                config.IncluirFundacoes ? "Fundações" : null,
                config.IncluirContraventamentos ? "Contraventamentos" : null,
                config.IncluirChapasConexoes ? "Conexões/Perfis de Conexão" : null
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            string abas = string.Join(", ", new[]
            {
                config.ExportarPerfisLineares ? "Elementos Estruturais" : null,
                config.ExportarChapas ? "Conexões" : null,
                config.ExportarResumo ? "Resumo" : null
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            txtResumo.Text =
                $"Documento: {_uidoc?.Document?.Title ?? "<sem documento>"}\n" +
                $"Escopo: {escopo}\n" +
                $"Categorias: {(string.IsNullOrWhiteSpace(categorias) ? "Nenhuma" : categorias)}\n" +
                $"Abas do Excel: {(string.IsNullOrWhiteSpace(abas) ? "Nenhuma" : abas)}\n\n" +
                "Colunas V1: marca(s), categoria, família, tipo/perfil, material, comprimento de corte, área, volume, quantidade, peso, origem do peso e detalhe do agrupamento.\n" +
                "Resumo: totais por categoria e por material/perfil.\n" +
                "Conexões: a exportação gera abas separadas para perfis de conexão e demais conexões quando essa saída estiver habilitada.";

            btnExportar.IsEnabled = config.TemCategoriaSelecionada() &&
                                    config.TemAbaSelecionada() &&
                                    !string.IsNullOrWhiteSpace(config.CaminhoArquivo);
        }

        private ListaMateriaisEscopo ObterEscopoSelecionado()
        {
            if (rbModeloInteiro.IsChecked == true)
                return ListaMateriaisEscopo.ModeloInteiro;
            if (rbSelecaoAtual.IsChecked == true)
                return ListaMateriaisEscopo.SelecaoAtual;

            return ListaMateriaisEscopo.VistaAtiva;
        }
    }
}
