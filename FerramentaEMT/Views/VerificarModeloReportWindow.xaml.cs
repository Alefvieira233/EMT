using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Services.ModelCheck;
using FerramentaEMT.Utils;
using Microsoft.Win32;

namespace FerramentaEMT.Views
{
    public partial class VerificarModeloReportWindow : Window
    {
        private sealed class IssueNodeData
        {
            public string Titulo { get; set; } = string.Empty;
            public string Descricao { get; set; } = string.Empty;
            public string Sugestao { get; set; } = string.Empty;
            public List<long> ElementIds { get; set; } = new List<long>();
            public bool IsSheetIssue { get; set; }
        }

        private readonly UIDocument _uidoc;
        private readonly ModelCheckReport _report;
        private readonly List<long> _elementIdsDoRelatorio;
        private readonly List<long> _elementIdsDoModelo;
        private readonly HashSet<long> _sheetElementIds;
        private readonly ModelCheckVisualizationService _visualizationService;
        private List<long> _elementIdsSelecionados;
        private bool _populandoRelatorio;

        public VerificarModeloReportWindow(UIDocument uidoc, ModelCheckReport report)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _report = report ?? new ModelCheckReport();
            _elementIdsDoRelatorio = new List<long>();
            _elementIdsDoModelo = new List<long>();
            _sheetElementIds = new HashSet<long>();
            _visualizationService = new ModelCheckVisualizationService(uidoc);
            _elementIdsSelecionados = new List<long>();

            treeProblemas.SelectedItemChanged += TreeProblemas_SelectedItemChanged;
            treeProblemas.MouseDoubleClick += TreeProblemas_MouseDoubleClick;
            btnIsolarNaVista.Click += BtnIsolarNaVista_Click;
            btnSelecionarElementos.Click += BtnSelecionarElementos_Click;
            btnExportarExcel.Click += BtnExportarExcel_Click;
            btnFechar.Click += (_, __) => Close();

            try
            {
                PopularRelatorio();
            }
            catch (Exception ex)
            {
                txtStatusNavegacao.Text = $"Erro ao carregar o relatório: {ex.Message}";
            }

            AbrirVista3DInicial();
        }

        private void PopularRelatorio()
        {
            _populandoRelatorio = true;
            try
            {
                lblTotalElementos.Text = _report.TotalElementsAnalyzed.ToString();
                lblTotalProblemas.Text = _report.TotalIssues.ToString();
                lblErros.Text = _report.CountBySeverity(ModelCheckSeverity.Error).ToString();
                lblAvisos.Text = _report.CountBySeverity(ModelCheckSeverity.Warning).ToString();

                treeProblemas.Items.Clear();
                _elementIdsDoRelatorio.Clear();
                _elementIdsDoModelo.Clear();
                _sheetElementIds.Clear();

                var porSeveridade = new Dictionary<ModelCheckSeverity, List<ModelCheckIssue>>
                {
                    { ModelCheckSeverity.Error, new List<ModelCheckIssue>() },
                    { ModelCheckSeverity.Warning, new List<ModelCheckIssue>() },
                    { ModelCheckSeverity.Info, new List<ModelCheckIssue>() }
                };

                foreach (var resultado in _report.Results)
                {
                    foreach (var issue in resultado.Issues)
                    {
                        if (!porSeveridade.ContainsKey(issue.Severity))
                            porSeveridade[issue.Severity] = new List<ModelCheckIssue>();

                        porSeveridade[issue.Severity].Add(issue);

                        if (issue.ElementId.HasValue)
                        {
                            _elementIdsDoRelatorio.Add(issue.ElementId.Value);
                            if (issue.IsSheetIssue)
                                _sheetElementIds.Add(issue.ElementId.Value);
                            else
                                _elementIdsDoModelo.Add(issue.ElementId.Value);
                        }
                    }
                }

                var severidades = new[] { ModelCheckSeverity.Error, ModelCheckSeverity.Warning, ModelCheckSeverity.Info };
                foreach (var sev in severidades)
                {
                    var problemas = porSeveridade[sev];
                    if (problemas.Count == 0)
                        continue;

                    var setorIds = problemas
                        .Where(issue => issue.ElementId.HasValue)
                        .Select(issue => issue.ElementId!.Value)
                        .Distinct()
                        .ToList();

                    var nodeSetor = CriarTreeItem(
                        $"{sev} ({problemas.Count})",
                        new IssueNodeData
                        {
                            Titulo = $"{sev} ({problemas.Count})",
                            Descricao = $"Seleciona todos os elementos classificados como {sev.ToString().ToLowerInvariant()} no relatório.",
                            Sugestao = "Use a seleção para revisar os elementos desta severidade em conjunto.",
                            ElementIds = setorIds,
                            IsSheetIssue = setorIds.Count > 0 && setorIds.All(id => _sheetElementIds.Contains(id))
                        },
                        true);

                    var porRegra = problemas.GroupBy(p => p.RuleName);

                    foreach (var regra in porRegra)
                    {
                        var regraIds = regra
                            .Where(issue => issue.ElementId.HasValue)
                            .Select(issue => issue.ElementId!.Value)
                            .Distinct()
                            .ToList();

                        bool regraEhDeFolha = regra.All(issue => issue.IsSheetIssue);

                        var nodeRegra = CriarTreeItem(
                            $"{regra.Key} ({regra.Count()})",
                            new IssueNodeData
                            {
                                Titulo = $"{regra.Key} ({regra.Count()})",
                                Descricao = regraEhDeFolha
                                    ? $"{regra.Count()} folha(s) com '{regra.Key.Replace("Carimbo: ", "")}' não preenchido."
                                    : $"Seleciona todos os elementos listados na regra '{regra.Key}'.",
                                Sugestao = regra.FirstOrDefault()?.Suggestion ?? "Revise os itens desta regra.",
                                ElementIds = regraIds,
                                IsSheetIssue = regraEhDeFolha
                            });

                        foreach (var issue in regra)
                        {
                            var descricao = issue.Description;
                            if (issue.ElementId.HasValue && !issue.IsSheetIssue)
                                descricao += $" [ID: {issue.ElementId}]";

                            nodeRegra.Items.Add(CriarTreeItem(
                                descricao,
                                new IssueNodeData
                                {
                                    Titulo = issue.RuleName,
                                    Descricao = issue.Description,
                                    Sugestao = issue.Suggestion,
                                    IsSheetIssue = issue.IsSheetIssue,
                                    ElementIds = issue.ElementId.HasValue
                                        ? new List<long> { issue.ElementId.Value }
                                        : new List<long>()
                                }));
                        }

                        nodeSetor.Items.Add(nodeRegra);
                    }

                    treeProblemas.Items.Add(nodeSetor);
                }

                SelecionarPrimeiroItem();
            }
            finally
            {
                _populandoRelatorio = false;
            }
        }

        private TreeViewItem CriarTreeItem(string texto, IssueNodeData data, bool expanded = false)
        {
            return new TreeViewItem
            {
                Header = texto,
                Tag = data,
                IsExpanded = expanded
            };
        }

        private void SelecionarPrimeiroItem()
        {
            if (treeProblemas.Items.Count == 0)
                return;

            TreeViewItem item = treeProblemas.Items[0] as TreeViewItem;
            if (item == null)
                return;

            item.IsSelected = true;
        }

        private void TreeProblemas_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_populandoRelatorio)
                return;

            TreeViewItem item = e.NewValue as TreeViewItem;
            IssueNodeData data = item?.Tag as IssueNodeData;

            if (data == null)
                return;

            _elementIdsSelecionados = data.ElementIds
                .Distinct()
                .ToList();

            txtSelecaoTitulo.Text = string.IsNullOrWhiteSpace(data.Titulo) ? "Sem título" : data.Titulo;
            txtSelecaoDescricao.Text = string.IsNullOrWhiteSpace(data.Descricao) ? "Selecione outro item para ver mais detalhes." : data.Descricao;
            txtSelecaoSugestao.Text = string.IsNullOrWhiteSpace(data.Sugestao) ? "Sem sugestão adicional." : data.Sugestao;
        }

        private void TreeProblemas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem item = treeProblemas.SelectedItem as TreeViewItem;
            IssueNodeData data = item?.Tag as IssueNodeData;

            if (data == null || data.ElementIds.Count == 0)
                return;

            e.Handled = true;

            if (data.ElementIds.Count == 1)
                NavegarParaIssue(data);
            else
                NavegarParaGrupo(data);

            this.WindowState = WindowState.Minimized;
        }

        private void NavegarParaIssue(IssueNodeData data)
        {
            try
            {
                if (data.IsSheetIssue)
                {
                    _visualizationService.FocusSheetElements(data.ElementIds);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? "Folha aberta para revisão do carimbo."
                        : _visualizationService.LastNavigationDescription;
                }
                else
                {
                    _visualizationService.FocusElements(data.ElementIds);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? "Elemento destacado no modelo."
                        : _visualizationService.LastNavigationDescription;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao navegar para issue individual");
                txtStatusNavegacao.Text = $"Não foi possível navegar até o elemento: {ex.Message}";
            }
        }

        private void NavegarParaGrupo(IssueNodeData data)
        {
            try
            {
                List<long> idsModelo = data.ElementIds
                    .Where(id => !_sheetElementIds.Contains(id))
                    .ToList();
                List<long> idsFolha = data.ElementIds
                    .Where(id => _sheetElementIds.Contains(id))
                    .ToList();

                if (idsModelo.Count > 0)
                {
                    _visualizationService.OpenResultsView(idsModelo);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? $"{idsModelo.Count} elemento(s) do modelo isolados na vista 3D."
                        : _visualizationService.LastNavigationDescription;
                }
                else if (idsFolha.Count > 0)
                {
                    _visualizationService.FocusSheetElements(idsFolha);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? $"{idsFolha.Count} folha(s) com problema de carimbo destacada(s)."
                        : _visualizationService.LastNavigationDescription;
                }
                else
                {
                    txtStatusNavegacao.Text = "Este grupo não possui elementos navegáveis no modelo.";
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao navegar para grupo de issues");
                txtStatusNavegacao.Text = $"Não foi possível navegar até os elementos: {ex.Message}";
            }
        }

        private void AbrirVista3DInicial()
        {
            if (_elementIdsDoModelo.Count == 0)
                return;

            try
            {
                if (_visualizationService.OpenResultsView(_elementIdsDoModelo))
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? "Vista aberta com os elementos do relatório."
                        : _visualizationService.LastNavigationDescription;
            }
            catch (Exception ex)
            {
                txtStatusNavegacao.Text = $"Não foi possível abrir a vista do relatório: {ex.Message}";
            }
        }

        private void BtnIsolarNaVista_Click(object sender, RoutedEventArgs e)
        {
            List<long> fonteIds = _elementIdsSelecionados.Count > 0 ? _elementIdsSelecionados : _elementIdsDoRelatorio;
            List<long> idsModelo = fonteIds.Where(id => !_sheetElementIds.Contains(id)).Distinct().ToList();

            if (idsModelo.Count == 0)
            {
                AppDialogService.ShowInfo("Isolar na Vista", "Não há elementos do modelo para exibir em 3D. Problemas de carimbo são exibidos navegando até a folha.", "Sem elementos 3D");
                return;
            }

            try
            {
                _visualizationService.OpenResultsView(idsModelo);
                txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                    ? "Vista reaberta e atualizada com os elementos do relatório."
                    : _visualizationService.LastNavigationDescription;
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError("Isolar na Vista", $"Erro ao abrir a vista do relatório: {ex.Message}", "Erro");
            }
        }

        private void BtnSelecionarElementos_Click(object sender, RoutedEventArgs e)
        {
            List<long> fonteIds = _elementIdsSelecionados.Count > 0 ? _elementIdsSelecionados : _elementIdsDoRelatorio;

            List<long> idsModelo = fonteIds.Where(id => !_sheetElementIds.Contains(id)).Distinct().ToList();
            List<long> idsFolha = fonteIds.Where(id => _sheetElementIds.Contains(id)).Distinct().ToList();

            if (idsModelo.Count == 0 && idsFolha.Count == 0)
            {
                AppDialogService.ShowInfo("Selecionar", "Nenhum elemento para selecionar.", "Sem elementos");
                return;
            }

            try
            {
                if (idsModelo.Count > 0)
                {
                    _visualizationService.FocusElements(idsModelo);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? $"{idsModelo.Count} elemento(s) do modelo selecionado(s)."
                        : _visualizationService.LastNavigationDescription;
                }
                else
                {
                    _visualizationService.FocusSheetElements(idsFolha);
                    txtStatusNavegacao.Text = string.IsNullOrWhiteSpace(_visualizationService.LastNavigationDescription)
                        ? $"{idsFolha.Count} folha(s) com problema de carimbo navegada(s)."
                        : _visualizationService.LastNavigationDescription;
                }

                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError("Selecionar", $"Erro ao selecionar elementos: {ex.Message}", "Erro");
            }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Arquivos Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = "modelo-verificacao.xlsx"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string caminhoExcel = dialog.FileName;

                // Garantir diretorio
                string dir = Path.GetDirectoryName(caminhoExcel);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Remover se existe
                if (File.Exists(caminhoExcel))
                    File.Delete(caminhoExcel);

                // Exportar
                using (var workbook = new XLWorkbook())
                {
                    var wsIssues = workbook.Worksheets.Add("Issues");

                    wsIssues.Cell(1, 1).Value = "Severidade";
                    wsIssues.Cell(1, 2).Value = "Regra";
                    wsIssues.Cell(1, 3).Value = "Element ID";
                    wsIssues.Cell(1, 4).Value = "Descricao";
                    wsIssues.Cell(1, 5).Value = "Sugestao";

                    var headerRange = wsIssues.Range(1, 1, 1, 5);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    int row = 2;
                    foreach (var resultado in _report.Results)
                    {
                        foreach (var issue in resultado.Issues)
                        {
                            wsIssues.Cell(row, 1).Value = issue.Severity.ToString();
                            wsIssues.Cell(row, 2).Value = issue.RuleName;
                            wsIssues.Cell(row, 3).Value = issue.ElementId?.ToString() ?? "N/A";
                            wsIssues.Cell(row, 4).Value = issue.Description;
                            wsIssues.Cell(row, 5).Value = issue.Suggestion;
                            row++;
                        }
                    }

                    wsIssues.Columns("A", "E").AdjustToContents();

                    var wsResumo = workbook.Worksheets.Add("Resumo");
                    wsResumo.Cell(1, 1).Value = "Total de Elementos";
                    wsResumo.Cell(1, 2).Value = _report.TotalElementsAnalyzed;

                    wsResumo.Cell(2, 1).Value = "Total de Problemas";
                    wsResumo.Cell(2, 2).Value = _report.TotalIssues;

                    wsResumo.Cell(3, 1).Value = "Erros";
                    wsResumo.Cell(3, 2).Value = _report.CountBySeverity(ModelCheckSeverity.Error);

                    wsResumo.Cell(4, 1).Value = "Avisos";
                    wsResumo.Cell(4, 2).Value = _report.CountBySeverity(ModelCheckSeverity.Warning);

                    wsResumo.Cell(5, 1).Value = "Tempo (ms)";
                    wsResumo.Cell(5, 2).Value = _report.Duration;

                    wsResumo.Column(1).AdjustToContents();
                    wsResumo.Column(2).AdjustToContents();

                    workbook.SaveAs(caminhoExcel);
                }

                AppDialogService.ShowInfo(
                    "Exportacao",
                    $"Relatorio exportado com sucesso:\n{caminhoExcel}",
                    "Sucesso");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Erro ao exportar Excel");
                AppDialogService.ShowError(
                    "Exportacao",
                    $"Erro ao exportar: {ex.Message}",
                    "Erro");
            }
        }
    }
}
