using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Utils;
using Microsoft.Win32;

namespace FerramentaEMT.Views
{
    public partial class VerificarModeloReportWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly ModelCheckReport _report;
        private List<long> _elementIdsDoRelatorio;

        public VerificarModeloReportWindow(UIDocument uidoc, ModelCheckReport report)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _report = report ?? new ModelCheckReport();
            _elementIdsDoRelatorio = new List<long>();

            btnIsolarNaVista.Click += BtnIsolarNaVista_Click;
            btnSelecionarElementos.Click += BtnSelecionarElementos_Click;
            btnExportarExcel.Click += BtnExportarExcel_Click;
            btnFechar.Click += (_, __) => Close();

            // Duplo-clique num item da arvore = selecionar e mostrar aquele elemento
            treeProblemas.MouseDoubleClick += TreeProblemas_MouseDoubleClick;

            PopularRelatorio();
        }

        private void PopularRelatorio()
        {
            // Atualizar resumo
            lblTotalElementos.Text = _report.TotalElementsAnalyzed.ToString();
            lblTotalProblemas.Text = _report.TotalIssues.ToString();
            lblErros.Text = _report.CountBySeverity(ModelCheckSeverity.Error).ToString();
            lblAvisos.Text = _report.CountBySeverity(ModelCheckSeverity.Warning).ToString();

            // Construir tree de problemas agrupados por severidade, depois por regra
            treeProblemas.Items.Clear();
            _elementIdsDoRelatorio.Clear();

            // Agrupar por severidade
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
                        _elementIdsDoRelatorio.Add(issue.ElementId.Value);
                }
            }

            // Criar nodes
            var severidades = new[] { ModelCheckSeverity.Error, ModelCheckSeverity.Warning, ModelCheckSeverity.Info };

            foreach (var sev in severidades)
            {
                var problemas = porSeveridade[sev];
                if (problemas.Count == 0)
                    continue;

                var nodeSetor = CriarTreeItem($"{sev} ({problemas.Count})", sev.ToString());

                // Agrupar por regra dentro da severidade
                var porRegra = problemas.GroupBy(p => p.RuleName);

                foreach (var regra in porRegra)
                {
                    var nodeRegra = CriarTreeItem($"{regra.Key} ({regra.Count()})", $"{sev}-{regra.Key}");

                    foreach (var issue in regra)
                    {
                        var descricao = issue.Description;
                        if (issue.ElementId.HasValue)
                            descricao += $" [ID: {issue.ElementId}]";

                        var nodeIssue = CriarTreeItem(descricao, $"issue-{issue.ElementId}");
                        nodeRegra.Items.Add(nodeIssue);
                    }

                    nodeSetor.Items.Add(nodeRegra);
                }

                treeProblemas.Items.Add(nodeSetor);
            }
        }

        private TreeViewItem CriarTreeItem(string texto, string tag)
        {
            var item = new TreeViewItem
            {
                Header = texto,
                Tag = tag,
                IsExpanded = false
            };

            return item;
        }

        /// <summary>
        /// Retorna o ElementId selecionado no TreeView se o node atual corresponde
        /// a uma issue individual (Tag no formato "issue-<id>"). Retorna null caso contrário.
        /// </summary>
        private long? ObterElementIdSelecionadoNaTree()
        {
            if (treeProblemas.SelectedItem is not TreeViewItem item) return null;
            if (item.Tag is not string tag) return null;
            if (!tag.StartsWith("issue-")) return null;
            string idStr = tag.Substring("issue-".Length);
            if (long.TryParse(idStr, out long id)) return id;
            return null;
        }

        /// <summary>
        /// Decide quais elementos aplicar (selecionado na tree, ou todos do relatório).
        /// </summary>
        private List<ElementId> ResolverElementIdsParaAcao()
        {
            // Senior pattern: sempre revalidar ElementIds antes de usar em operacoes Revit.
            // Usuario pode ter deletado elementos entre gerar relatorio e clicar acao — a engine
            // do Revit lanca ArgumentException em IsolateElementsTemporary/SetElementIds
            // se qualquer id estiver invalido. Filtra silenciosamente os stales.
            var doc = _uidoc?.Document;

            long? idSelecionado = ObterElementIdSelecionadoNaTree();
            if (idSelecionado.HasValue)
            {
                var id = new ElementId(idSelecionado.Value);
                return (doc != null && doc.GetElement(id) != null)
                    ? new List<ElementId> { id }
                    : new List<ElementId>();
            }

            return _elementIdsDoRelatorio
                .Select(id => new ElementId(id))
                .Where(id => doc == null || doc.GetElement(id) != null)
                .ToList();
        }

        private void TreeProblemas_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            long? id = ObterElementIdSelecionadoNaTree();
            if (!id.HasValue) return;

            try
            {
                var elementIds = new List<ElementId> { new ElementId(id.Value) };
                _uidoc.Selection.SetElementIds(elementIds);
                try { _uidoc.ShowElements(elementIds); } catch (Exception ex2) { Logger.Warn(ex2, "Falha ao focar elemento na vista"); }
                this.WindowState = WindowState.Minimized;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Erro no duplo-clique da arvore de problemas");
            }
        }

        private void BtnIsolarNaVista_Click(object sender, RoutedEventArgs e)
        {
            var elementIds = ResolverElementIdsParaAcao();
            if (elementIds.Count == 0)
            {
                AppDialogService.ShowInfo("Isolar na Vista", "Nenhum elemento para isolar.", "Sem elementos");
                return;
            }

            try
            {
                if (_uidoc.ActiveView != null)
                {
                    // Transaction necessaria para IsolateElementsTemporary
                    using (var t = new Transaction(_uidoc.Document, "Isolar Elementos"))
                    {
                        t.Start();
                        _uidoc.ActiveView.IsolateElementsTemporary(elementIds);
                        t.Commit();
                    }

                    // Minimiza a janela para o usuario ver o efeito na view do Revit
                    this.WindowState = WindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Erro ao isolar elementos");
                AppDialogService.ShowError(
                    "Isolar na Vista",
                    $"Erro ao isolar elementos: {ex.Message}",
                    "Erro");
            }
        }

        private void BtnSelecionarElementos_Click(object sender, RoutedEventArgs e)
        {
            var elementIds = ResolverElementIdsParaAcao();
            if (elementIds.Count == 0)
            {
                AppDialogService.ShowInfo("Selecionar", "Nenhum elemento para selecionar.", "Sem elementos");
                return;
            }

            try
            {
                _uidoc.Selection.SetElementIds(elementIds);

                // Se apenas 1 elemento, tenta mostrar/focar na vista ativa
                if (elementIds.Count == 1)
                {
                    try { _uidoc.ShowElements(elementIds); } catch (Exception ex2) { Logger.Warn(ex2, "Falha ao focar elemento apos selecao"); }
                }

                // Minimiza a janela para o usuario ver a selecao na view do Revit
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Erro ao selecionar elementos");
                AppDialogService.ShowError(
                    "Selecionar",
                    $"Erro ao selecionar elementos: {ex.Message}",
                    "Erro");
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
