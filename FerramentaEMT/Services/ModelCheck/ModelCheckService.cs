#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Services.ModelCheck.ModelCheckRules;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.ModelCheck
{
    /// <summary>
    /// Orquestrador de verificacao de modelo.
    /// Coleta o escopo, executa as regras habilitadas, constroi relatorio e opcionalmente exporta para Excel.
    /// </summary>
    public class ModelCheckService
    {
        private const string Titulo = "Verificacao de Modelo";

        /// <summary>
        /// Executa a verificacao de modelo de acordo com a configuracao fornecida.
        /// </summary>
        public ModelCheckReport Executar(UIDocument uidoc, ModelCheckConfig config)
        {
            var sw = Stopwatch.StartNew();
            var report = new ModelCheckReport();

            if (uidoc?.Document == null)
            {
                Logger.Warn("[{Cmd}] uidoc nulo — abortando", Titulo);
                return report;
            }

            Document doc = uidoc.Document;

            try
            {
                // --- 1. Coletar escopo ---
                Logger.Info("[{Cmd}] coletando escopo", Titulo);
                IList<ElementId> scopeIds = ColetarEscopo(uidoc, config.ScopeViewOnly);
                report.TotalElementsAnalyzed = scopeIds.Count;

                Logger.Info("[{Cmd}] escopo contem {Total} elementos", Titulo, scopeIds.Count);

                // --- 2. Criar lista de regras habilitadas ---
                var regras = CriarRegras(config);
                Logger.Info("[{Cmd}] {RulesCount} regras habilitadas", Titulo, regras.Count);

                if (regras.Count == 0)
                {
                    Logger.Warn("[{Cmd}] nenhuma regra habilitada — abortando", Titulo);
                    return report;
                }

                // --- 3. Executar cada regra ---
                foreach (var regra in regras)
                {
                    var swRegra = Stopwatch.StartNew();

                    try
                    {
                        Logger.Info("[{Cmd}] executando regra: {Rule}", Titulo, regra.Name);

                        var problemas = regra.Check(doc, scopeIds);
                        var problemasList = problemas.ToList();

                        swRegra.Stop();

                        var resultado = new ModelCheckRuleResult
                        {
                            RuleName = regra.Name,
                            Description = regra.Description,
                            ElapsedMs = swRegra.ElapsedMilliseconds,
                            Issues = problemasList
                        };

                        report.Results.Add(resultado);

                        Logger.Info("[{Cmd}] {Rule} encontrou {Count} problemas em {Elapsed}ms",
                            Titulo, regra.Name, problemasList.Count, swRegra.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        swRegra.Stop();
                        Logger.Error(ex, "[{Cmd}] erro ao executar regra {Rule}",
                            Titulo, regra.Name);
                    }
                }

                // --- 4. Exportar para Excel se configurado ---
                if (config.ExportExcel && !string.IsNullOrWhiteSpace(config.ExcelPath))
                {
                    try
                    {
                        Logger.Info("[{Cmd}] exportando relatorio para Excel: {Path}",
                            Titulo, config.ExcelPath);

                        ExportarExcel(report, config.ExcelPath);

                        Logger.Info("[{Cmd}] relatorio Excel exportado com sucesso", Titulo);
                        AppDialogService.ShowInfo(
                            Titulo,
                            $"Relatorio exportado com sucesso:\n{config.ExcelPath}",
                            "Exportacao Concluida");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "[{Cmd}] erro ao exportar Excel", Titulo);
                        AppDialogService.ShowError(
                            Titulo,
                            $"Erro ao exportar relatorio: {ex.Message}",
                            "Erro de Exportacao");
                    }
                }

                sw.Stop();
                report.Duration = sw.ElapsedMilliseconds;

                Logger.Info("[{Cmd}] verificacao concluida em {Elapsed}ms — {Total} problemas encontrados",
                    Titulo, sw.ElapsedMilliseconds, report.TotalIssues);

                return report;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error(ex, "[{Cmd}] FALHA geral na verificacao apos {Elapsed}ms",
                    Titulo, sw.ElapsedMilliseconds);
                throw;
            }
        }

        private IList<ElementId> ColetarEscopo(UIDocument uidoc, bool viewOnly)
        {
            if (viewOnly && uidoc.ActiveView != null)
            {
                // Coletar apenas elementos visiveis na vista ativa
                var collector = new FilteredElementCollector(uidoc.Document, uidoc.ActiveView.Id)
                    .OfClass(typeof(FamilyInstance));

                return collector.ToElementIds().ToList();
            }
            else
            {
                // Coletar modelo inteiro
                var collector = new FilteredElementCollector(uidoc.Document)
                    .OfClass(typeof(FamilyInstance));

                return collector.ToElementIds().ToList();
            }
        }

        private List<IModelCheckRule> CriarRegras(ModelCheckConfig config)
        {
            var regras = new List<IModelCheckRule>();

            if (config.RunMissingMaterial)
                regras.Add(new MissingMaterialRule());

            if (config.RunMissingMark)
                regras.Add(new MissingMarkRule());

            if (config.RunDuplicateMark)
                regras.Add(new DuplicateMarkRule());

            if (config.RunOverlappingElements)
                regras.Add(new OverlappingElementsRule());

            if (config.RunMissingProfile)
                regras.Add(new MissingProfileRule());

            if (config.RunZeroLength)
                regras.Add(new ZeroLengthRule());

            if (config.RunMissingLevel)
                regras.Add(new MissingLevelRule());

            if (config.RunStructuralWithoutType)
                regras.Add(new StructuralWithoutTypeRule());

            if (config.RunMissingComment)
                regras.Add(new MissingCommentRule());

            if (config.RunOrphanGroup)
                regras.Add(new OrphanGroupRule());

            return regras;
        }

        private void ExportarExcel(ModelCheckReport report, string excelPath)
        {
            // Garantir que o diretorio existe
            string? diretorio = Path.GetDirectoryName(excelPath);
            if (!string.IsNullOrEmpty(diretorio) && !Directory.Exists(diretorio))
                Directory.CreateDirectory(diretorio);

            // Se arquivo ja existe, remover
            if (File.Exists(excelPath))
                File.Delete(excelPath);

            // Criar workbook
            using (var workbook = new XLWorkbook())
            {
                // Aba 1: Issues consolidadas
                var wsIssues = workbook.Worksheets.Add("Issues");

                // Cabecalho
                wsIssues.Cell(1, 1).Value = "Severidade";
                wsIssues.Cell(1, 2).Value = "Regra";
                wsIssues.Cell(1, 3).Value = "Element ID";
                wsIssues.Cell(1, 4).Value = "Descricao";
                wsIssues.Cell(1, 5).Value = "Sugestao";

                // Formatar cabecalho
                var headerRange = wsIssues.Range(1, 1, 1, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Preencher dados
                int row = 2;
                foreach (var resultado in report.Results)
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

                // Auto-ajustar largura das colunas
                wsIssues.Columns("A", "E").AdjustToContents();

                // Aba 2: Resumo
                var wsResumo = workbook.Worksheets.Add("Resumo");

                wsResumo.Cell(1, 1).Value = "Total de Elementos Analisados";
                wsResumo.Cell(1, 2).Value = report.TotalElementsAnalyzed;

                wsResumo.Cell(2, 1).Value = "Total de Problemas";
                wsResumo.Cell(2, 2).Value = report.TotalIssues;

                wsResumo.Cell(3, 1).Value = "Problemas de Erro";
                wsResumo.Cell(3, 2).Value = report.CountBySeverity(ModelCheckSeverity.Error);

                wsResumo.Cell(4, 1).Value = "Avisos";
                wsResumo.Cell(4, 2).Value = report.CountBySeverity(ModelCheckSeverity.Warning);

                wsResumo.Cell(5, 1).Value = "Informacoes";
                wsResumo.Cell(5, 2).Value = report.CountBySeverity(ModelCheckSeverity.Info);

                wsResumo.Cell(6, 1).Value = "Tempo Total (ms)";
                wsResumo.Cell(6, 2).Value = report.Duration;

                wsResumo.Cell(7, 1).Value = "Data/Hora";
                wsResumo.Cell(7, 2).Value = report.ExecutionTime;

                wsResumo.Column(1).AdjustToContents();
                wsResumo.Column(2).AdjustToContents();

                // Salvar
                workbook.SaveAs(excelPath);
            }
        }
    }
}
