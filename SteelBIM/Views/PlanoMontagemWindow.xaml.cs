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
                    "Plano de Montagem",
                    "Por favor, insira um numero de etapa valido (inteiro positivo).",
                    "Entrada invalida");
                return;
            }

            if (_idsPreSelecionados.Count == 0)
            {
                // Defesa em profundidade — o comando ja validou, mas garantir aqui tambem
                AppDialogService.ShowError(
                    "Plano de Montagem",
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
                        "Plano de Montagem",
                        resultado.Mensagem ?? "Etapa atribuida com sucesso.",
                        "Sucesso");
                    Logger.Info(
                        "[PlanoMontagemWindow] Etapa {Etapa} atribuida a {Count} elementos",
                        numEtapa, resultado.ElementosProcessados);
                }
                else
                {
                    AppDialogService.ShowError(
                        "Plano de Montagem",
                        resultado.Mensagem ?? "Falha ao atribuir etapa.",
                        "Erro");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao atribuir etapa");
                AppDialogService.ShowError(
                    "Plano de Montagem",
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
                        "Plano de Montagem",
                        "Nenhuma etapa encontrada. Atribua etapas aos elementos primeiro.",
                        "Sem dados");
                    return;
                }

                // Aplicar destaque visual
                if (_config.AplicarDestaqueVisual)
                {
                    _service.AplicarDestaqueVisual(_uidoc.Document, _uidoc.ActiveView, relatorio.Etapas);
                }

                // Exibir no DataGrid
                var dados = new ObservableCollection<dynamic>();
                foreach (var etapa in relatorio.Etapas)
                {
                    dynamic row = new
                    {
                        Etapa = etapa.Numero,
                        Descricao = etapa.Descricao,
                        DataPlanejada = etapa.DataPlanejada?.ToString("dd/MM/yyyy") ?? "-",
                        Quantidade = etapa.ElementIds.Count
                    };
                    dados.Add(row);
                }

                dgEtapas.ItemsSource = dados;

                AppDialogService.ShowInfo(
                    "Plano de Montagem",
                    $"Plano gerado: {relatorio.TotalEtapas} etapa(s), {relatorio.TotalElementos} elemento(s). Destaque visual aplicado.",
                    "Sucesso");

                Logger.Info("[PlanoMontagemWindow] Plano gerado com {Etapas} etapas", relatorio.TotalEtapas);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao gerar plano");
                AppDialogService.ShowError(
                    "Plano de Montagem",
                    ex.Message,
                    "Erro");
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Salvar Plano de Montagem",
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
                    "Plano de Montagem",
                    "Por favor, selecione um caminho para o arquivo.",
                    "Caminho ausente");
                return;
            }

            try
            {
                var relatorio = _service.GerarRelatorio(_uidoc.Document, _config);
                _service.ExportarRelatorioExcel(relatorio, caminho);

                AppDialogService.ShowInfo(
                    "Plano de Montagem",
                    $"Relatório exportado com sucesso:\n{caminho}",
                    "Sucesso");

                Logger.Info("[PlanoMontagemWindow] Relatório exportado: {Path}", caminho);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagemWindow] Erro ao exportar");
                AppDialogService.ShowError(
                    "Plano de Montagem",
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
