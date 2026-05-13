#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Core;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.ModelCheck;
using FerramentaEMT.Services.ModelCheck.ModelCheckRules;

namespace FerramentaEMT.Services.ModelCheck
{
    /// <summary>
    /// Orquestrador de verificacao de modelo.
    /// Coleta o escopo, executa as regras habilitadas, constroi relatorio e opcionalmente exporta para Excel.
    ///
    /// Contrato ADR-003:
    /// - Retorna <see cref="Result{ModelCheckReport}"/> — Fail carrega mensagem para dialogo.
    /// - Aceita <see cref="IProgress{ProgressReport}"/> opcional para UI nao congelar.
    /// - Aceita <see cref="CancellationToken"/> para o usuario poder interromper.
    /// - Falhas de exportacao Excel sao reportadas via <see cref="ModelCheckReport.ExportError"/>
    ///   e NAO invalidam o relatorio de analise — callsite decide como apresentar.
    /// </summary>
    public class ModelCheckService
    {
        private const string Titulo = "Verificacao de Modelo";

        /// <summary>
        /// Executa a verificacao de modelo de acordo com a configuracao fornecida.
        /// </summary>
        /// <param name="uidoc">UIDocument ativo (nao pode ser null).</param>
        /// <param name="config">Configuracao com regras habilitadas e opcao de export.</param>
        /// <param name="progress">Reporter opcional — recebe 1 evento por regra executada.</param>
        /// <param name="ct">Token de cancelamento. Cancelamento gera <see cref="OperationCanceledException"/>
        /// que o comando chamador trata como Result.Cancelled.</param>
        public Result<ModelCheckReport> Executar(
            UIDocument uidoc,
            ModelCheckConfig config,
            IProgress<ProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (uidoc?.Document == null)
                return Result<ModelCheckReport>.Fail("Documento do Revit nao disponivel.");

            if (config == null)
                return Result<ModelCheckReport>.Fail("Configuracao de verificacao nao informada.");

            Document doc = uidoc.Document;
            ProgressReporter reporter = new ProgressReporter(progress, throttleMs: 100, ct);
            Stopwatch sw = Stopwatch.StartNew();
            ModelCheckReport report = new ModelCheckReport();

            // --- 1. Coletar escopo ---
            Logger.Info("[{Cmd}] coletando escopo", Titulo);
            IList<ElementId> scopeIds = ColetarEscopo(uidoc, config.ScopeViewOnly);
            report.TotalElementsAnalyzed = scopeIds.Count;
            Logger.Info("[{Cmd}] escopo contem {Total} elementos", Titulo, scopeIds.Count);

            // --- 2. Criar lista de regras habilitadas ---
            List<IModelCheckRule> regras = CriarRegras(config);
            Logger.Info("[{Cmd}] {RulesCount} regras habilitadas", Titulo, regras.Count);

            bool possuiRegrasDeCarimbo =
                config.RunTitleBlockParameters &&
                config.TitleBlockParameters != null &&
                config.TitleBlockParameters.Count > 0;

            if (regras.Count == 0 && !possuiRegrasDeCarimbo)
                return Result<ModelCheckReport>.Fail(
                    "Nenhuma regra habilitada. Selecione ao menos uma regra na configuracao.");

            // --- 3. Executar cada regra ---
            for (int i = 0; i < regras.Count; i++)
            {
                reporter.ThrowIfCancellationRequested();

                IModelCheckRule regra = regras[i];
                Stopwatch swRegra = Stopwatch.StartNew();

                try
                {
                    Logger.Info("[{Cmd}] executando regra: {Rule} ({Index}/{Total})",
                        Titulo, regra.Name, i + 1, regras.Count);

                    IEnumerable<ModelCheckIssue> problemas = regra.Check(doc, scopeIds);
                    List<ModelCheckIssue> problemasList = problemas.ToList();

                    swRegra.Stop();

                    ModelCheckRuleResult resultado = new ModelCheckRuleResult
                    {
                        RuleName = regra.Name,
                        Description = regra.Description,
                        ElapsedMs = swRegra.ElapsedMilliseconds,
                        Issues = problemasList
                    };
                    report.Results.Add(resultado);

                    Logger.Info("[{Cmd}] {Rule} encontrou {Count} problemas em {Elapsed}ms",
                        Titulo, regra.Name, problemasList.Count, swRegra.ElapsedMilliseconds);

                    reporter.Report(i + 1, regras.Count,
                        $"Regra {regra.Name}: {problemasList.Count} problema(s)");
                }
                catch (OperationCanceledException)
                {
                    // Propaga — nao e erro da regra, e cancelamento do usuario.
                    throw;
                }
                catch (Exception ex)
                {
                    swRegra.Stop();
                    Logger.Error(ex, "[{Cmd}] erro ao executar regra {Rule} — pulando",
                        Titulo, regra.Name);
                    // Uma regra com defeito nao invalida o batch.
                }
            }

            // --- 3.5. Verificar carimbos (TitleBlock) ---
            if (possuiRegrasDeCarimbo)
            {
                reporter.Report(regras.Count, regras.Count, "Verificando carimbos...");

                try
                {
                    AdicionarResultadosDeCarimbo(uidoc, doc, config, report.Results, reporter, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[{Cmd}] erro na verificacao de carimbos — pulando", Titulo);
                    report.Results.Add(new ModelCheckRuleResult
                    {
                        RuleName = "Carimbo",
                        Description = "Verifica o preenchimento dos atributos selecionados no carimbo da folha.",
                        Issues = new List<ModelCheckIssue>
                        {
                            new ModelCheckIssue
                            {
                                RuleName = "Carimbo",
                                Severity = ModelCheckSeverity.Warning,
                                Description = $"Erro ao verificar parametros do carimbo: {ex.Message}",
                                Suggestion = "Revise a configuracao de carimbo e execute novamente.",
                                IsSheetIssue = true
                            }
                        }
                    });
                }
            }

            // --- 4. Exportar para Excel se configurado ---
            // Falha de export NAO invalida o relatorio de analise; usuario pode
            // abrir o report window mesmo assim. Command apresenta dois dialogos
            // separados caso necessario.
            if (config.ExportExcel && !string.IsNullOrWhiteSpace(config.ExcelPath))
            {
                reporter.ThrowIfCancellationRequested();

                try
                {
                    Logger.Info("[{Cmd}] exportando relatorio para Excel: {Path}",
                        Titulo, config.ExcelPath);

                    ExportarExcel(report, config.ExcelPath!);
                    report.ExportedToPath = config.ExcelPath;

                    Logger.Info("[{Cmd}] relatorio Excel exportado com sucesso", Titulo);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[{Cmd}] erro ao exportar Excel — analise preservada", Titulo);
                    report.ExportError = ex.Message;
                }
            }

            sw.Stop();
            report.Duration = sw.ElapsedMilliseconds;

            Logger.Info("[{Cmd}] verificacao concluida em {Elapsed}ms — {Total} problemas encontrados",
                Titulo, sw.ElapsedMilliseconds, report.TotalIssues);

            reporter.ReportFinal(regras.Count, regras.Count,
                $"Verificacao concluida — {report.TotalIssues} problema(s) em {regras.Count} regra(s)");

            return Result<ModelCheckReport>.Ok(report);
        }

        private IList<ElementId> ColetarEscopo(UIDocument uidoc, bool viewOnly)
        {
            if (viewOnly && uidoc.ActiveView != null)
            {
                // Coletar apenas elementos visiveis na vista ativa
                FilteredElementCollector collector = new FilteredElementCollector(uidoc.Document, uidoc.ActiveView.Id)
                    .OfClass(typeof(FamilyInstance));

                return collector.ToElementIds().ToList();
            }
            else
            {
                // Coletar modelo inteiro
                FilteredElementCollector collector = new FilteredElementCollector(uidoc.Document)
                    .OfClass(typeof(FamilyInstance));

                return collector.ToElementIds().ToList();
            }
        }

        private List<IModelCheckRule> CriarRegras(ModelCheckConfig config)
        {
            List<IModelCheckRule> regras = new List<IModelCheckRule>();

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

        // ---------------------------------------------------------------
        // Verificacao de carimbo (TitleBlock)
        // Origem: projeto Victor (incorporado e adaptado na Onda 3, Miniciclo 4).
        // ---------------------------------------------------------------

        /// <summary>
        /// Verifica os parametros de carimbo (TitleBlock) nas folhas do projeto.
        /// Para cada parametro listado em <see cref="ModelCheckConfig.TitleBlockParameters"/>,
        /// cria um <see cref="ModelCheckIssue"/> com <see cref="ModelCheckIssue.IsSheetIssue"/> = true
        /// quando o parametro estiver ausente, vazio ou invalido.
        /// </summary>
        /// <remarks>
        /// O parametro e buscado em 3 niveis: ViewSheet → instancia do TitleBlock → FamilySymbol do TitleBlock.
        /// Basta existir em qualquer um dos 3 para ser considerado preenchido.
        /// </remarks>
        private void AdicionarResultadosDeCarimbo(
            UIDocument uidoc,
            Document doc,
            ModelCheckConfig config,
            List<ModelCheckRuleResult> results,
            ProgressReporter reporter,
            CancellationToken ct)
        {
            if (!config.RunTitleBlockParameters ||
                config.TitleBlockParameters == null ||
                config.TitleBlockParameters.Count == 0)
            {
                return;
            }

            // --- Logging dos filtros (AJUSTE 3) ---
            string familiaDesc = string.IsNullOrWhiteSpace(config.TitleBlockFamilyName)
                ? "<qualquer>"
                : config.TitleBlockFamilyName;
            string tipoDesc = string.IsNullOrWhiteSpace(config.TitleBlockTypeName)
                ? "<qualquer>"
                : config.TitleBlockTypeName;
            string escopoDesc = config.TitleBlockScopeActiveSheetOnly
                ? "folha ativa"
                : "todas as folhas";

            Logger.Info(
                "[ModelCheck] Verificacao de carimbo: familia=\"{0}\", tipo=\"{1}\", {2} parametros, escopo={3}",
                familiaDesc, tipoDesc, config.TitleBlockParameters.Count, escopoDesc);

            // --- Resolver symbol do carimbo ---
            FamilySymbol? titleBlockSymbol = ResolverTitleBlockSymbol(
                doc,
                config.TitleBlockFamilyName,
                config.TitleBlockTypeName);

            // --- Coletar instancias de TitleBlock ---
            List<FamilyInstance> titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(instance => titleBlockSymbol == null || instance.Symbol?.Id == titleBlockSymbol.Id)
                .ToList();

            // --- Filtrar por escopo (folha ativa ou todas) ---
            if (config.TitleBlockScopeActiveSheetOnly && uidoc.ActiveView is ViewSheet activeSheet)
                titleBlocks = titleBlocks.Where(instance => instance.OwnerViewId == activeSheet.Id).ToList();

            // --- Nenhum titleblock encontrado ---
            if (titleBlocks.Count == 0)
            {
                Logger.Warn("[ModelCheck] Nenhuma folha com o carimbo selecionado foi encontrada");
                results.Add(new ModelCheckRuleResult
                {
                    RuleName = "Carimbo",
                    Description = "Verifica o preenchimento dos atributos selecionados no carimbo da folha.",
                    Issues = new List<ModelCheckIssue>
                    {
                        new ModelCheckIssue
                        {
                            RuleName = "Carimbo",
                            Severity = ModelCheckSeverity.Warning,
                            Description = "Nenhuma folha com o carimbo selecionado foi encontrada no projeto.",
                            Suggestion = "Revise a familia da folha escolhida ou execute a verificacao em um projeto com folhas carregadas.",
                            IsSheetIssue = true
                        }
                    }
                });
                return;
            }

            // --- Verificar cada parametro × cada titleblock ---
            List<string> parametrosUnicos = config.TitleBlockParameters
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int p = 0; p < parametrosUnicos.Count; p++)
            {
                ct.ThrowIfCancellationRequested();

                string parameterName = parametrosUnicos[p];
                List<ModelCheckIssue> issues = new List<ModelCheckIssue>();

                for (int t = 0; t < titleBlocks.Count; t++)
                {
                    FamilyInstance titleBlock = titleBlocks[t];
                    ViewSheet? sheet = doc.GetElement(titleBlock.OwnerViewId) as ViewSheet;

                    if (ParametroPossuiValor(sheet, parameterName) ||
                        ParametroPossuiValor(titleBlock, parameterName) ||
                        ParametroPossuiValor(titleBlock.Symbol, parameterName))
                    {
                        continue;
                    }

                    string identificacaoFolha = sheet != null
                        ? $"{sheet.SheetNumber} - {sheet.Name}"
                        : $"Elemento {titleBlock.Id.Value}";

                    issues.Add(new ModelCheckIssue
                    {
                        RuleName = $"Carimbo: {parameterName}",
                        Severity = ModelCheckSeverity.Warning,
                        ElementId = titleBlock.Id.Value,
                        IsSheetIssue = true,
                        Description = $"A folha '{identificacaoFolha}' esta sem valor para '{parameterName}'.",
                        Suggestion = $"Preencha o parametro '{parameterName}' no carimbo da folha {identificacaoFolha}."
                    });
                }

                results.Add(new ModelCheckRuleResult
                {
                    RuleName = $"Carimbo: {parameterName}",
                    Description = $"Verifica o preenchimento do atributo '{parameterName}' no carimbo da folha.",
                    Issues = issues
                });

                // Progress sem alterar contadores (AJUSTE 1)
                reporter.Report(0, 0,
                    $"Carimbo: parametro {p + 1}/{parametrosUnicos.Count} — {parameterName}");
            }

            int totalIssuesCarimbo = results
                .Where(r => r.RuleName.StartsWith("Carimbo:", StringComparison.Ordinal))
                .Sum(r => r.IssuesCount);

            Logger.Info("[ModelCheck] Verificacao de carimbo concluida — {0} problemas em {1} folhas",
                totalIssuesCarimbo, titleBlocks.Count);
        }

        // ---------------------------------------------------------------
        // Helpers de carimbo
        // ---------------------------------------------------------------

        /// <summary>
        /// Resolve o <see cref="FamilySymbol"/> do carimbo com base nos filtros de familia e tipo.
        /// Retorna <c>null</c> quando os filtros estao vazios (aceita qualquer carimbo)
        /// ou quando nao encontra correspondencia exata.
        /// </summary>
        private FamilySymbol? ResolverTitleBlockSymbol(Document doc, string familyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(typeName))
                return null;

            IEnumerable<FamilySymbol> symbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            FamilySymbol? exact = symbols.FirstOrDefault(symbol =>
                string.Equals(symbol.Family?.Name, familyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(symbol.Name, typeName, StringComparison.OrdinalIgnoreCase));

            return exact;
        }

        /// <summary>
        /// Verifica se o elemento possui um parametro com valor preenchido (nao vazio, nao nulo,
        /// nao <see cref="ElementId.InvalidElementId"/>).
        /// Trata os 4 tipos de armazenamento da Revit API: String, ElementId, Integer, Double.
        /// </summary>
        private static bool ParametroPossuiValor(Element? element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            Parameter? parameter = element.LookupParameter(parameterName);
            if (parameter == null)
                return false;

            if (parameter.StorageType == StorageType.String)
                return !string.IsNullOrWhiteSpace(parameter.AsString());

            if (parameter.StorageType == StorageType.ElementId)
            {
                ElementId value = parameter.AsElementId();
                return value != null && value != ElementId.InvalidElementId;
            }

            string? readableValue = parameter.AsValueString();
            if (!string.IsNullOrWhiteSpace(readableValue))
                return true;

            return parameter.StorageType switch
            {
                StorageType.Integer => parameter.AsInteger() != 0,
                StorageType.Double => Math.Abs(parameter.AsDouble()) > 1e-9,
                _ => false
            };
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
            using (XLWorkbook workbook = new XLWorkbook())
            {
                // Aba 1: Issues consolidadas
                IXLWorksheet wsIssues = workbook.Worksheets.Add("Issues");

                // Cabecalho
                wsIssues.Cell(1, 1).Value = "Severidade";
                wsIssues.Cell(1, 2).Value = "Regra";
                wsIssues.Cell(1, 3).Value = "Element ID";
                wsIssues.Cell(1, 4).Value = "Descricao";
                wsIssues.Cell(1, 5).Value = "Sugestao";

                // Formatar cabecalho
                IXLRange headerRange = wsIssues.Range(1, 1, 1, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Preencher dados
                int row = 2;
                foreach (ModelCheckRuleResult resultado in report.Results)
                {
                    foreach (ModelCheckIssue issue in resultado.Issues)
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
                IXLWorksheet wsResumo = workbook.Worksheets.Add("Resumo");

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
