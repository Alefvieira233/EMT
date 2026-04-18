#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Core;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.CncExport
{
    /// <summary>
    /// Servico orquestrador: coleta elementos, monta os DstvFile e grava arquivos .nc1 na pasta destino.
    /// </summary>
    public sealed class DstvExportService
    {
        private const string Titulo = "Exportar DSTV/NC1";

        public sealed class ResultadoExport
        {
            public int TotalElementos { get; set; }
            public int ArquivosGerados { get; set; }
            public int ElementosPulados { get; set; }
            public List<string> Warnings { get; } = new();
            public List<string> ArquivosCriados { get; } = new();
            public string PastaDestino { get; set; } = "";
            public TimeSpan Duracao { get; set; }
            /// <summary>Distingue cancelamento (ESC no PickObjects) de "sem elementos".</summary>
            public bool Cancelado { get; set; }
            /// <summary>Contagem de arquivos que sairam com alguma dimensao critica zerada (perfil nao reconhecido). Sinaliza NC1 invalido mesmo se gravado.</summary>
            public int ArquivosComDimensaoZerada { get; set; }
        }

        /// <summary>
        /// Executa a exportacao DSTV/NC1. Primeira adocao do padrao ADR-003:
        /// retorna <see cref="Result{T}"/>, aceita <see cref="IProgress{T}"/> e
        /// <see cref="CancellationToken"/> opcionais para progresso e cancelamento.
        ///
        /// <para>
        /// <b>Contrato:</b> o servico ja logga e, em caso de sucesso, ainda chama
        /// <see cref="ExibirResumo"/> com o summary dialog nativo (responsabilidade
        /// do servico hoje; pode migrar para o comando no futuro). Os casos de
        /// falha de dominio (pasta nao informada, selecao vazia, etc.) voltam como
        /// <c>Result.Fail</c> com mensagem amigavel — o comando decide se exibe
        /// dialog ou nao.
        /// </para>
        /// </summary>
        public Result<ResultadoExport> Executar(
            UIDocument uidoc,
            ExportarDstvConfig config,
            IProgress<ProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));

            var sw = Stopwatch.StartNew();
            var resultado = new ResultadoExport { PastaDestino = config.PastaDestino ?? "" };
            var reporter = new ProgressReporter(progress, throttleMs: 100, ct);

            Document doc = uidoc.Document;

            // 1. Validar pasta destino
            if (string.IsNullOrWhiteSpace(config.PastaDestino))
                return Result<ResultadoExport>.Fail("Pasta de destino nao informada.");

            try
            {
                Directory.CreateDirectory(config.PastaDestino);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "DstvExportService: falha ao criar pasta {Path}", config.PastaDestino);
                return Result<ResultadoExport>.Fail(
                    $"Nao foi possivel criar a pasta de destino:\n{ex.Message}");
            }

            reporter.Report(0, 0, "Coletando elementos...");

            // 2. Coletar elementos
            bool cancelado = false;
            List<FamilyInstance> elementos = ColetarElementos(uidoc, doc, config, out cancelado);
            if (cancelado)
            {
                resultado.Cancelado = true;
                Logger.Info("[DstvExport] Usuario cancelou a selecao");
                return Result<ResultadoExport>.Ok(resultado); // cancelamento explicito e fluxo legitimo
            }
            if (elementos.Count == 0)
                return Result<ResultadoExport>.Fail("Nenhuma peca estrutural encontrada para exportar.");

            elementos = FiltrarPorCategoria(elementos, config);
            resultado.TotalElementos = elementos.Count;

            if (elementos.Count == 0)
                return Result<ResultadoExport>.Fail("Nenhum elemento corresponde as categorias selecionadas.");

            // 3. Agrupar por marca (se aplicavel)
            var arquivos = new Dictionary<string, DstvFile>(StringComparer.Ordinal);
            var contagemPorMarca = new Dictionary<string, int>(StringComparer.Ordinal);

            int processados = 0;
            foreach (FamilyInstance elem in elementos)
            {
                reporter.ThrowIfCancellationRequested();
                try
                {
                    var dstv = new DstvFile();
                    DstvHeaderBuilder.Build(doc, elem, config, dstv);

                    // VALIDACAO: se a altura do perfil ou o comprimento vieram zerados, o NC1 gerado
                    // sera invalido (maquina CNC rejeita). Sinaliza claramente ao usuario em vez de
                    // gravar arquivo silenciosamente corrompido. Bug reportado: "CNC nao consigo avaliar".
                    if (dstv.ProfileHeightMm <= 0 || dstv.CutLengthMm <= 0)
                    {
                        string tipo = dstv.ProfileHeightMm <= 0 ? "altura do perfil (h)" : "comprimento de corte";
                        resultado.Warnings.Add(
                            $"Elemento {elem.Id?.Value} ({dstv.ProfileName}): {tipo} = 0. " +
                            $"Verificar parametros da familia estrutural. NC1 gerado sera invalido.");
                        resultado.ArquivosComDimensaoZerada++;
                        Logger.Warn("[DstvExport] Elemento {Id} com dimensao zerada: h={H} len={L}",
                            elem.Id?.Value, dstv.ProfileHeightMm, dstv.CutLengthMm);
                    }

                    // Furos
                    foreach (DstvHole h in DstvHoleExtractor.Extract(doc, elem))
                        dstv.Holes.Add(h);

                    // Anguros de corte (placeholder — extracao geometrica fica para versao futura)

                    string marca = string.IsNullOrWhiteSpace(dstv.PieceMark)
                        ? $"ID-{elem.Id?.Value ?? 0}"
                        : dstv.PieceMark;

                    if (config.Agrupamento == AgrupamentoArquivosDstv.UmPorInstancia)
                    {
                        // Sufixar com ID para evitar colisao
                        string chave = $"{marca}_{elem.Id?.Value ?? 0}";
                        arquivos[chave] = dstv;
                    }
                    else
                    {
                        // UmPorMarca: primeira ocorrencia ganha; quantidade conta
                        if (!arquivos.ContainsKey(marca))
                            arquivos[marca] = dstv;

                        if (!contagemPorMarca.ContainsKey(marca))
                            contagemPorMarca[marca] = 0;
                        contagemPorMarca[marca]++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "DstvExportService: falha ao processar elemento {Id}", elem.Id?.Value);
                    resultado.Warnings.Add($"Elemento {elem.Id?.Value}: {ex.Message}");
                    resultado.ElementosPulados++;
                }

                processados++;
                reporter.Report(processados, elementos.Count,
                    $"Processando peca {processados}/{elementos.Count}");
            }

            // Ajustar Quantity para UmPorMarca
            if (config.Agrupamento == AgrupamentoArquivosDstv.UmPorMarca)
            {
                foreach (var kvp in contagemPorMarca)
                {
                    if (arquivos.TryGetValue(kvp.Key, out DstvFile? df) && df != null)
                        df.Quantity = kvp.Value;
                }
            }

            // 4. Gravar arquivos
            int gravados = 0;
            foreach (var kvp in arquivos)
            {
                reporter.ThrowIfCancellationRequested();
                try
                {
                    string nomeArquivo = SanitizarNomeArquivo(kvp.Key) + ".nc1";
                    string caminhoCompleto = Path.Combine(config.PastaDestino, nomeArquivo);

                    if (File.Exists(caminhoCompleto) && !config.SobrescreverExistentes)
                    {
                        resultado.Warnings.Add($"Pulado (ja existe): {nomeArquivo}");
                        continue;
                    }

                    DstvFileWriter.Save(kvp.Value, caminhoCompleto);
                    resultado.ArquivosGerados++;
                    resultado.ArquivosCriados.Add(nomeArquivo);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "DstvExportService: falha ao gravar {Nome}", kvp.Key);
                    resultado.Warnings.Add($"Falha ao gravar {kvp.Key}: {ex.Message}");
                }
                gravados++;
                reporter.Report(gravados, arquivos.Count,
                    $"Gravando {gravados}/{arquivos.Count} arquivos...");
            }

            // 5. Relatorio
            if (config.GerarRelatorio)
                GravarRelatorio(config.PastaDestino, resultado);

            sw.Stop();
            resultado.Duracao = sw.Elapsed;
            reporter.ReportFinal(resultado.ArquivosGerados, arquivos.Count, "Exportacao concluida");

            // 6. Resumo
            ExibirResumo(resultado);

            // 7. Abrir pasta
            if (config.AbrirPastaAposExportar && resultado.ArquivosGerados > 0)
                AbrirPastaNoExplorer(config.PastaDestino);

            return Result<ResultadoExport>.Ok(resultado);
        }

        // ============================================================
        //  Coleta
        // ============================================================

        private List<FamilyInstance> ColetarElementos(UIDocument uidoc, Document doc, ExportarDstvConfig config, out bool cancelado)
        {
            cancelado = false;
            switch (config.Escopo)
            {
                case EscopoExportacaoDstv.ModeloInteiro:
                    return ColetarDoModelo(doc);

                case EscopoExportacaoDstv.VistaAtiva:
                    return ColetarDaVista(doc);

                case EscopoExportacaoDstv.SelecaoManual:
                default:
                    return ColetarSelecao(uidoc, doc, out cancelado);
            }
        }

        private List<FamilyInstance> ColetarDoModelo(Document doc)
        {
            var result = new List<FamilyInstance>();
            foreach (BuiltInCategory cat in CategoriasEstruturais)
            {
                result.AddRange(new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>());
            }
            return result;
        }

        private List<FamilyInstance> ColetarDaVista(Document doc)
        {
            var result = new List<FamilyInstance>();
            View view = doc.ActiveView;
            if (view == null) return result;

            foreach (BuiltInCategory cat in CategoriasEstruturais)
            {
                result.AddRange(new FilteredElementCollector(doc, view.Id)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>());
            }
            return result;
        }

        private List<FamilyInstance> ColetarSelecao(UIDocument uidoc, Document doc, out bool cancelado)
        {
            cancelado = false;

            var ids = uidoc.Selection.GetElementIds();
            if (ids != null && ids.Count > 0)
            {
                var fromSel = ids
                    .Select(id => doc.GetElement(id))
                    .OfType<FamilyInstance>()
                    .Where(IsEstrutural)
                    .ToList();
                if (fromSel.Count > 0) return fromSel;
            }

            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroEstrutural(),
                    "Selecione as pecas para exportar DSTV e pressione Enter");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<FamilyInstance>()
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // distingue cancelamento explicito (ESC) de "selecao vazia" — UX claro pro usuario
                cancelado = true;
                return new List<FamilyInstance>();
            }
        }

        private List<FamilyInstance> FiltrarPorCategoria(
            List<FamilyInstance> elementos, ExportarDstvConfig config)
        {
            return elementos.Where(e =>
            {
                var cat = e.Category?.BuiltInCategory;
                if (cat == BuiltInCategory.OST_StructuralColumns) return config.ExportarPilares;
                if (cat == BuiltInCategory.OST_StructuralFraming)
                {
                    if (e.StructuralType == StructuralType.Brace)
                        return config.ExportarContraventamentos;
                    return config.ExportarVigas;
                }
                return false;
            }).ToList();
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private static readonly BuiltInCategory[] CategoriasEstruturais =
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns
        };

        private static bool IsEstrutural(FamilyInstance fi)
        {
            var cat = fi.Category?.BuiltInCategory;
            return cat == BuiltInCategory.OST_StructuralFraming
                || cat == BuiltInCategory.OST_StructuralColumns;
        }

        public static string SanitizarNomeArquivo(string nome) => DstvFileNameSanitizer.Sanitize(nome);

        private void GravarRelatorio(string pasta, ResultadoExport r)
        {
            try
            {
                string caminho = Path.Combine(pasta, $"_DSTV_export_{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                var sb = new StringBuilder();
                sb.AppendLine("=== Relatorio de Exportacao DSTV/NC1 ===");
                sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Pasta: {pasta}");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Duracao: {0:F2}s", r.Duracao.TotalSeconds));
                sb.AppendLine($"Total de elementos: {r.TotalElementos}");
                sb.AppendLine($"Arquivos gerados: {r.ArquivosGerados}");
                sb.AppendLine($"Elementos pulados: {r.ElementosPulados}");
                sb.AppendLine();
                sb.AppendLine("--- Arquivos ---");
                foreach (string a in r.ArquivosCriados.OrderBy(x => x))
                    sb.AppendLine($"  {a}");
                if (r.Warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- Warnings ---");
                    foreach (string w in r.Warnings)
                        sb.AppendLine($"  • {w}");
                }
                File.WriteAllText(caminho, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "DstvExportService: falha ao gravar relatorio");
            }
        }

        private static void ExibirResumo(ResultadoExport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Exportacao DSTV/NC1 concluida.");
            sb.AppendLine();
            sb.AppendLine($"Pasta: {r.PastaDestino}");
            sb.AppendLine($"Elementos processados: {r.TotalElementos}");
            sb.AppendLine($"Arquivos .nc1 gerados: {r.ArquivosGerados}");
            if (r.ElementosPulados > 0)
                sb.AppendLine($"Elementos pulados: {r.ElementosPulados}");
            sb.AppendLine($"Duracao: {r.Duracao.TotalSeconds:F2}s");
            if (r.ArquivosComDimensaoZerada > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ {r.ArquivosComDimensaoZerada} arquivo(s) com dimensao zerada (NC1 INVALIDO).");
                sb.AppendLine("   Verifique o parametro de secao da familia do Revit.");
            }
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Avisos: {r.Warnings.Count} (ver relatorio na pasta)");
            }

            if (r.ArquivosComDimensaoZerada > 0)
                AppDialogService.ShowWarning(Titulo, sb.ToString(), "Exportacao com avisos");
            else
                AppDialogService.ShowInfo(Titulo, sb.ToString(), "Exportacao concluida");
        }

        private static void AbrirPastaNoExplorer(string pasta)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{pasta}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "DstvExportService: falha ao abrir Explorer");
            }
        }

        private sealed class FiltroEstrutural : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                var cat = elem.Category?.BuiltInCategory;
                return cat == BuiltInCategory.OST_StructuralFraming
                    || cat == BuiltInCategory.OST_StructuralColumns;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
