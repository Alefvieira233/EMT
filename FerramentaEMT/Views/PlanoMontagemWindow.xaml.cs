using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Montagem;
using FerramentaEMT.Services.Montagem;
using FerramentaEMT.Utils;
using Microsoft.Win32;

namespace FerramentaEMT.Views
{
    public partial class PlanoMontagemWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly PlanoMontagemService _service;
        private PlanoMontagemConfig _config;

        public PlanoMontagemWindow(UIDocument uidoc)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _service = new PlanoMontagemService();
            _config = new PlanoMontagemConfig();
        }

        private void BtnAtribuir_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("[PlanoMontagemWindow] Atribuindo etapa");

            if (!int.TryParse(txtNumeroEtapa.Text, out int numEtapa) || numEtapa <= 0)
            {
                AppDialogService.ShowError(
                    "Plano de Montagem",
                    "Por favor, insira um número de etapa válido (inteiro positivo).",
                    "Entrada inválida");
                return;
            }

            string descricao = txtDescricao.Text?.Trim() ?? "";

            // ETAPA 1: tentar usar seleção pré-existente do Revit (antes de abrir a janela)
            var ids = new List<ElementId>();
            var preSelecao = _uidoc.Selection.GetElementIds();
            if (preSelecao != null && preSelecao.Count > 0)
            {
                foreach (ElementId id in preSelecao)
                    ids.Add(id);
                Logger.Info("[PlanoMontagemWindow] Usando pre-selecao: {Count} elementos", ids.Count);
            }

            try
            {
                // ETAPA 2: se nao havia pre-selecao, pedir pick interativo.
                // CRITICO: WPF modal bloqueia a UI do Revit. Precisa Hide() antes do PickObjects.
                if (ids.Count == 0)
                {
                    AppDialogService.ShowInfo(
                        "Plano de Montagem",
                        "A janela sera minimizada. Clique nos elementos no Revit e pressione Enter (ou ESC para cancelar).",
                        "Selecao");

                    this.Hide();
                    try
                    {
                        IList<Reference> refs = _uidoc.Selection.PickObjects(
                            Autodesk.Revit.UI.Selection.ObjectType.Element,
                            "Selecione elementos para atribuir a etapa");

                        if (refs == null || refs.Count == 0)
                        {
                            Logger.Info("[PlanoMontagemWindow] Nenhum elemento selecionado");
                            return;
                        }

                        foreach (Reference r in refs)
                            ids.Add(r.ElementId);
                    }
                    finally
                    {
                        // Reabrir a janela (nao-modal Show dentro do ShowDialog original)
                        this.Show();
                        this.Activate();
                    }
                }

                var resultado = _service.AtribuirEtapa(
                    _uidoc,
                    ids,
                    numEtapa,
                    _config.NomeParametroEtapa);

                if (resultado.Sucesso)
                {
                    AppDialogService.ShowInfo(
                        "Plano de Montagem",
                        resultado.Mensagem ?? "Etapa atribuída com sucesso.",
                        "Sucesso");
                    Logger.Info("[PlanoMontagemWindow] Etapa {Etapa} atribuída a {Count} elementos", numEtapa, resultado.ElementosProcessados);
                }
                else
                {
                    AppDialogService.ShowError(
                        "Plano de Montagem",
                        resultado.Mensagem ?? "Falha ao atribuir etapa.",
                        "Erro");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("[PlanoMontagemWindow] Seleção cancelada pelo usuário");
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
