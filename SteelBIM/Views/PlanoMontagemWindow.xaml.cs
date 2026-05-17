using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Win32;
using SteelBIM.Infrastructure;
using SteelBIM.Models.Montagem;
using SteelBIM.Services.Montagem;
using SteelBIM.Utils;
using SDColor = System.Drawing.Color;

namespace SteelBIM.Views
{
    public partial class PlanoMontagemWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly PlanoMontagemService _service;
        private readonly List<ElementId> _idsPreSelecionados;
        private PlanoMontagemConfig _config;

        public PlanoMontagemWindow(UIDocument uidoc, List<ElementId> idsPreSelecionados)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _service = new PlanoMontagemService();
            _idsPreSelecionados = idsPreSelecionados ?? new List<ElementId>();
            _config = new PlanoMontagemConfig();
        }

        private void BtnAtribuir_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("[PlanoMontagemWindow] Atribuindo etapa");

            if (!int.TryParse(txtNumeroEtapa.Text, out int numEtapa) || numEtapa <= 0)
            {
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    "Por favor, insira um numero de etapa valido (inteiro positivo).",
                    "Entrada invalida");
                return;
            }

            if (_idsPreSelecionados.Count == 0)
            {
                // Defesa em profundidade — o comando ja validou, mas garantir aqui tambem
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    "Nenhum elemento foi pre-selecionado. Feche esta janela, selecione os " +
                    "elementos no Revit e reabra o comando.",
                    "Sem selecao");
                return;
            }

            string descricao = txtDescricao.Text?.Trim() ?? "";

            try
            {
                var resultado = _service.AtribuirEtapa(
                    _uidoc,
                    _idsPreSelecionados,
                    numEtapa,
                    _config.NomeParametroEtapa);

                if (resultado.Sucesso)
                {
                    AppDialogService.ShowInfo(
                        "Sequenciamento BIM",
                        resultado.Mensagem ?? "Etapa atribuida com sucesso.",
                        "Sucesso");
                    Logger.Info(
                        "[PlanoMontagemWindow] Etapa {Etapa} atribuida a {Count} elementos",
                        numEtapa, resultado.ElementosProcessados);
                }
                else
                {
                    AppDialogService.ShowError(
                        "Sequenciamento BIM",
                        resultado.Mensagem ?? "Falha ao atribuir etapa.",
                        "Erro");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao atribuir etapa");
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    ex.Message,
                    "Erro");
            }
        }

        private void BtnGerarPlano_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("[PlanoMontagemWindow] Gerando plano de montagem");

            try
            {
                var relatorio = _service.GerarRelatorio(_uidoc.Document, _config);

                if (relatorio.TotalEtapas == 0)
                {
                    AppDialogService.ShowWarning(
                        "Sequenciamento BIM",
                        "Nenhuma etapa encontrada. Atribua etapas aos elementos primeiro.",
                        "Sem dados");
                    return;
                }

                // Aplicar destaque visual
                if (_config.AplicarDestaqueVisual)
                {
                    _service.AplicarDestaqueVisual(
                        _uidoc.Document,
                        _uidoc.ActiveView,
                        relatorio.Etapas,
                        _config.CoresCustomPorEtapa);
                }

                // Exibir no DataGrid
                var dados = new ObservableCollection<dynamic>();
                foreach (var etapa in relatorio.Etapas)
                {
                    // Cor atual: custom ou paleta padrao
                    var paleta = new[] {
                        System.Windows.Media.Color.FromRgb(0, 100, 200),
                        System.Windows.Media.Color.FromRgb(0, 180, 80),
                        System.Windows.Media.Color.FromRgb(255, 140, 0),
                        System.Windows.Media.Color.FromRgb(200, 50, 50),
                        System.Windows.Media.Color.FromRgb(150, 50, 200)
                    };

                    System.Windows.Media.Color corMedia;
                    if (_config.CoresCustomPorEtapa.TryGetValue(etapa.Numero, out var custom))
                    {
                        corMedia = System.Windows.Media.Color.FromRgb(custom.R, custom.G, custom.B);
                    }
                    else
                    {
                        corMedia = paleta[(etapa.Numero - 1) % paleta.Length];
                    }
                    var brush = new System.Windows.Media.SolidColorBrush(corMedia);
                    brush.Freeze();

                    dynamic row = new
                    {
                        Etapa = etapa.Numero,
                        Descricao = etapa.Descricao ?? "",
                        DataPlanejada = etapa.DataPlanejada?.ToString("dd/MM/yyyy") ?? "-",
                        Quantidade = etapa.ElementIds.Count,
                        CorAtualBrush = brush
                    };
                    dados.Add(row);
                }

                dgEtapas.ItemsSource = dados;

                AppDialogService.ShowInfo(
                    "Sequenciamento BIM",
                    $"Plano gerado: {relatorio.TotalEtapas} etapa(s), {relatorio.TotalElementos} elemento(s). Destaque visual aplicado.",
                    "Sucesso");

                Logger.Info("[PlanoMontagemWindow] Plano gerado com {Etapas} etapas", relatorio.TotalEtapas);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao gerar plano");
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    ex.Message,
                    "Erro");
            }
        }

        private void BtnEscolherCor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is int etapaNum)
            {
                using (var dlg = new System.Windows.Forms.ColorDialog())
                {
                    // Cor inicial: a custom atual ou a paleta default
                    SDColor inicial;
                    if (_config.CoresCustomPorEtapa.TryGetValue(etapaNum, out var custom))
                    {
                        inicial = SDColor.FromArgb(custom.R, custom.G, custom.B);
                    }
                    else
                    {
                        var paleta = new[] {
                            SDColor.FromArgb(0, 100, 200),
                            SDColor.FromArgb(0, 180, 80),
                            SDColor.FromArgb(255, 140, 0),
                            SDColor.FromArgb(200, 50, 50),
                            SDColor.FromArgb(150, 50, 200)
                        };
                        inicial = paleta[(etapaNum - 1) % paleta.Length];
                    }
                    dlg.Color = inicial;
                    dlg.FullOpen = true;

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _config.CoresCustomPorEtapa[etapaNum] = new SteelBIM.Models.Montagem.ColorRGB(
                            dlg.Color.R, dlg.Color.G, dlg.Color.B);
                        AppDialogService.ShowInfo(
                            "Sequenciamento BIM",
                            $"Cor da etapa {etapaNum} atualizada. Clique em 'Gerar Plano' novamente para aplicar.",
                            "Cor selecionada");
                    }
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Salvar Sequenciamento BIM",
                Filter = "Arquivo Excel|*.xlsx",
                DefaultExt = ".xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                txtCaminhoRelatorio.Text = dlg.FileName;
            }
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("[PlanoMontagemWindow] Exportando relatório");

            string caminho = txtCaminhoRelatorio.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(caminho))
            {
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    "Por favor, selecione um caminho para o arquivo.",
                    "Caminho ausente");
                return;
            }

            try
            {
                var relatorio = _service.GerarRelatorio(_uidoc.Document, _config);
                _service.ExportarRelatorioExcel(relatorio, caminho);

                AppDialogService.ShowInfo(
                    "Sequenciamento BIM",
                    $"Relatório exportado com sucesso:\n{caminho}",
                    "Sucesso");

                Logger.Info("[PlanoMontagemWindow] Relatório exportado: {Path}", caminho);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao exportar");
                AppDialogService.ShowError(
                    "Sequenciamento BIM",
                    ex.Message,
                    "Erro");
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
