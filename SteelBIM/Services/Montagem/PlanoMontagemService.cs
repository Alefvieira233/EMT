#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using SteelBIM.Infrastructure;
using SteelBIM.Models.Montagem;

namespace SteelBIM.Services.Montagem
{
    /// <summary>
    /// Orquestrador do Plano de Montagem (Erection Plan).
    /// Gerencia atribuição de etapas, geração de relatórios, destaque visual e exportação Excel.
    /// </summary>
    public class PlanoMontagemService
    {
        private const string Titulo = "Sequenciamento BIM";

        // 5-color cyclic palette para destaque visual
        private static readonly Color[] CoresPadrao = new[]
        {
            new Color(0, 100, 200),     // Azul
            new Color(0, 180, 80),      // Verde
            new Color(255, 140, 0),     // Laranja
            new Color(200, 50, 50),     // Vermelho
            new Color(150, 50, 200),    // Roxo
        };

        /// <summary>Resultado da operação de atribuição de etapa.</summary>
        public sealed class ResultadoMontagem
        {
            public bool Sucesso { get; set; }
            public int ElementosProcessados { get; set; }
            public string? Mensagem { get; set; }
        }

        /// <summary>
        /// Garante que o parametro de projeto "EMT_Etapa_Montagem" exista
        /// como Integer aplicavel a Structural Framing, Structural Columns
        /// e Structural Foundations. Se nao existir, cria. Idempotente.
        /// </summary>
        private bool GarantirParametroEtapa(Document doc, string nomeParametro)
        {
            try
            {
                // Verifica se ja existe como projeto parameter
                BindingMap bindings = doc.ParameterBindings;
                DefinitionBindingMapIterator it = bindings.ForwardIterator();
                while (it.MoveNext())
                {
                    Definition? def = it.Key as Definition;
                    if (def != null && string.Equals(def.Name, nomeParametro, StringComparison.OrdinalIgnoreCase))
                        return true; // ja existe
                }

                // Nao existe — criar via SharedParameter temporario
                string sharedFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"SteelBIM_SharedParams_{DateTime.Now:yyyyMMddHHmmss}.txt");
                System.IO.File.WriteAllText(sharedFile, "# This is a Revit shared parameter file.\n*META\tVERSION\tMINVERSION\n*GROUP\tID\tNAME\n*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n");

                DefinitionFile defFile;
                string previousSharedFile = doc.Application.SharedParametersFilename;
                try
                {
                    doc.Application.SharedParametersFilename = sharedFile;
                    defFile = doc.Application.OpenSharedParameterFile();

                    DefinitionGroup group = defFile.Groups.Create("SteelBIM");
                    ExternalDefinitionCreationOptions opts = new ExternalDefinitionCreationOptions(
                        nomeParametro, SpecTypeId.Int.Integer);
                    opts.UserModifiable = true;
                    opts.Description = "Numero da etapa de montagem (Sequenciamento BIM)";
                    ExternalDefinition externalDef = (ExternalDefinition)group.Definitions.Create(opts);

                    // Bind a categorias estruturais relevantes
                    CategorySet categories = doc.Application.Create.NewCategorySet();
                    foreach (BuiltInCategory bic in new[] {
                        BuiltInCategory.OST_StructuralFraming,
                        BuiltInCategory.OST_StructuralColumns,
                        BuiltInCategory.OST_StructuralFoundation,
                        BuiltInCategory.OST_GenericModel })
                    {
                        Category cat = Category.GetCategory(doc, bic);
                        if (cat != null)
                            categories.Insert(cat);
                    }

                    InstanceBinding binding = doc.Application.Create.NewInstanceBinding(categories);
                    doc.ParameterBindings.Insert(externalDef, binding, GroupTypeId.Construction);

                    Logger.Info("[SequenciamentoBim] Parametro {Nome} criado automaticamente", nomeParametro);
                    return true;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(previousSharedFile))
                        doc.Application.SharedParametersFilename = previousSharedFile;
                    try
                    { System.IO.File.Delete(sharedFile); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[SequenciamentoBim] Falha ao criar parametro {Nome}", nomeParametro);
                return false;
            }
        }

        public ResultadoMontagem AtribuirEtapa(
            UIDocument uidoc,
            IList<ElementId> ids,
            int etapa,
            string nomeParametro)
        {
            Logger.Info("[SequenciamentoBim] Atribuindo etapa {Etapa} a {Count} elementos", etapa, ids.Count);

            Document doc = uidoc.Document;
            int processados = 0;
            int falhas = 0;
            List<string> motivosFalha = new List<string>();

            try
            {
                // GARANTIR que o parametro EMT_Etapa_Montagem existe
                // (cria automaticamente se necessario)
                using (Transaction txParam = new Transaction(doc, "Criar parametro Sequenciamento BIM"))
                {
                    txParam.Start();
                    GarantirParametroEtapa(doc, nomeParametro);
                    txParam.Commit();
                }

                using (Transaction tx = new Transaction(doc, $"Atribuir etapa {etapa}"))
                {
                    tx.Start();

                    foreach (ElementId eid in ids)
                    {
                        Element elem = doc.GetElement(eid);
                        if (elem == null)
                        {
                            falhas++;
                            motivosFalha.Add($"Id {eid.Value}: elemento nao encontrado");
                            continue;
                        }

                        // FALLBACK 1: parametro dedicado (criado automaticamente acima)
                        Parameter param = elem.LookupParameter(nomeParametro);
                        if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Integer)
                        {
                            param.Set(etapa);
                            processados++;
                            continue;
                        }

                        // FALLBACK 2: parametro built-in Comments (string)
                        Parameter comments = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comments != null && !comments.IsReadOnly && comments.StorageType == StorageType.String)
                        {
                            string valorAnterior = comments.AsString() ?? "";
                            string semEtapaAntiga = System.Text.RegularExpressions.Regex.Replace(
                                valorAnterior, @"Etapa:\d+\s*;?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                            string novoValor = string.IsNullOrEmpty(semEtapaAntiga)
                                ? $"Etapa:{etapa}"
                                : $"Etapa:{etapa}; {semEtapaAntiga}";
                            comments.Set(novoValor);
                            processados++;
                            continue;
                        }

                        // FALLBACK 3: parametro built-in Mark (string)
                        // So usar se Comments tambem nao funcionar — Mark eh marca de fabricacao
                        Parameter mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                        if (mark != null && !mark.IsReadOnly && mark.StorageType == StorageType.String)
                        {
                            // Anexar etapa no inicio da marca existente (nao destrutivo)
                            string valorAnterior = mark.AsString() ?? "";
                            string semEtapaAntiga = System.Text.RegularExpressions.Regex.Replace(
                                valorAnterior, @"^E\d+/", "");
                            mark.Set($"E{etapa}/{semEtapaAntiga}");
                            processados++;
                            continue;
                        }

                        // Nenhum fallback funcionou
                        falhas++;
                        string categoria = elem.Category?.Name ?? "(sem categoria)";
                        motivosFalha.Add($"Id {eid.Value} ({categoria}): nao tem parametro editavel");
                    }

                    tx.Commit();
                }

                string mensagem;
                if (processados > 0 && falhas == 0)
                {
                    mensagem = $"Atribuido a {processados} elemento(s) com sucesso.";
                }
                else if (processados > 0 && falhas > 0)
                {
                    mensagem = $"Atribuido a {processados} elemento(s). " +
                              $"{falhas} elemento(s) nao puderam receber a etapa " +
                              $"(sem parametro editavel). Verifique os tipos das familias.";
                }
                else
                {
                    mensagem = $"FALHA: nenhum dos {ids.Count} elemento(s) selecionado(s) " +
                              $"aceita atribuicao de etapa. Causa provavel: as familias " +
                              $"selecionadas nao tem parametros 'Comments' ou 'Mark' " +
                              $"editaveis, e nao foi possivel criar o parametro de " +
                              $"projeto automaticamente. Verifique permissoes do arquivo " +
                              $"e tente em familias com parametros nativos.";
                }

                return new ResultadoMontagem
                {
                    Sucesso = processados > 0,
                    ElementosProcessados = processados,
                    Mensagem = mensagem
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[SequenciamentoBim] Erro ao atribuir etapa");
                return new ResultadoMontagem
                {
                    Sucesso = false,
                    ElementosProcessados = processados,
                    Mensagem = ex.Message
                };
            }
        }

        /// <summary>
        /// Gera um relatório do plano de montagem coletando elementos e agrupando por etapa.
        /// </summary>
        public PlanoMontagemReport GerarRelatorio(Document doc, PlanoMontagemConfig config)
        {
            Logger.Info("[PlanoMontagem] Gerando relatório com escopo {Escopo}", config.Escopo);

            var sw = Stopwatch.StartNew();
            var dicEtapas = new Dictionary<int, EtapaMontagem>();

            try
            {
                // Filtro por escopo: define o escopo do collector ANTES de criar
                FilteredElementCollector collector;
                if (config.Escopo == EscopoMontagem.VistaAtiva && doc.ActiveView != null)
                {
                    // Coletar apenas elementos visiveis na vista ativa
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                }
                else
                {
                    // Modelo inteiro
                    collector = new FilteredElementCollector(doc);
                }

                // Excluir tipos (queremos so instancias)
                collector = collector.WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    int etapaNum = LerEtapaDoElemento(elem, config.NomeParametroEtapa);
                    if (etapaNum <= 0)
                        continue;

                    if (!dicEtapas.ContainsKey(etapaNum))
                    {
                        dicEtapas[etapaNum] = new EtapaMontagem
                        {
                            Numero = etapaNum,
                            Descricao = ""
                        };
                    }

                    dicEtapas[etapaNum].ElementIds.Add(elem.Id.Value);
                }

                // Ordena por número de etapa
                var etapasOrdenadas = dicEtapas.Values
                    .OrderBy(e => e.Numero)
                    .ToList();

                int totalElementos = dicEtapas.Values.Sum(e => e.ElementIds.Count);

                sw.Stop();

                return new PlanoMontagemReport
                {
                    TotalElementos = totalElementos,
                    TotalEtapas = etapasOrdenadas.Count,
                    Etapas = etapasOrdenadas,
                    Duracao = sw.Elapsed
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagem] Erro ao gerar relatório");
                sw.Stop();
                return new PlanoMontagemReport
                {
                    TotalElementos = 0,
                    TotalEtapas = 0,
                    Etapas = new(),
                    Duracao = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Le o numero da etapa de um elemento. Prioriza parametro Integer nomeado;
        /// cai em Comments (string "Etapa:N") se nao encontrar. Retorna 0 se nao houver etapa.
        ///
        /// CRITICO: precisa espelhar exatamente o que AtribuirEtapa escreve
        /// (fallback Comments). Antes, GerarRelatorio so lia Integer —
        /// por isso Plano de Montagem "nao criava" apesar de atribuicoes terem sucesso.
        /// </summary>
        private static int LerEtapaDoElemento(Element elem, string nomeParametroEtapa)
        {
            // 1) Parametro Integer nomeado (caminho ideal)
            Parameter paramInt = elem.LookupParameter(nomeParametroEtapa);
            if (paramInt != null && paramInt.StorageType == StorageType.Integer)
            {
                int v = paramInt.AsInteger();
                if (v > 0)
                    return v;
            }

            // 2) Fallback Comments com regex "Etapa:N"
            Parameter comments = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && comments.StorageType == StorageType.String)
            {
                int etapa = EtapaMontagemParser.Parse(comments.AsString());
                if (etapa > 0)
                    return etapa;
            }

            // 3) Fallback Mark com regex "E{N}/"
            Parameter mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (mark != null && mark.StorageType == StorageType.String)
            {
                string markValue = mark.AsString() ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(markValue, @"^E(\d+)/");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int v) && v > 0)
                    return v;
            }

            return 0;
        }

        /// <summary>
        /// Aplica destaque visual (colorização) aos elementos de cada etapa usando OverrideGraphicSettings.
        /// </summary>
        public void AplicarDestaqueVisual(
            Document doc,
            View view,
            List<EtapaMontagem> etapas,
            Dictionary<int, Models.Montagem.ColorRGB>? coresCustom = null)
        {
            Logger.Info("[SequenciamentoBim] Aplicando destaque visual a {Count} etapas", etapas.Count);

            try
            {
                using (Transaction tx = new Transaction(doc, "Destaque Visual - Sequenciamento BIM"))
                {
                    tx.Start();

                    for (int i = 0; i < etapas.Count; i++)
                    {
                        EtapaMontagem etapa = etapas[i];

                        // Usa cor custom se houver, senao paleta padrao ciclica
                        Color cor;
                        if (coresCustom != null && coresCustom.TryGetValue(etapa.Numero, out var custom))
                        {
                            cor = new Color(custom.R, custom.G, custom.B);
                        }
                        else
                        {
                            cor = CoresPadrao[i % CoresPadrao.Length];
                        }

                        foreach (long elemIdVal in etapa.ElementIds)
                        {
                            Element elem = doc.GetElement(new ElementId(elemIdVal));
                            if (elem == null)
                                continue;

                            var ogs = new OverrideGraphicSettings();
                            ogs.SetProjectionLineColor(cor);
                            ogs.SetSurfaceBackgroundPatternColor(cor);
                            ogs.SetSurfaceForegroundPatternColor(cor);

                            view.SetElementOverrides(elem.Id, ogs);
                        }
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[SequenciamentoBim] Erro ao aplicar destaque visual");
            }
        }

        /// <summary>
        /// Exporta o relatório para arquivo Excel com dois worksheets:
        /// "Etapas" (resumo) e "Elementos" (detalhe).
        /// </summary>
        public void ExportarRelatorioExcel(PlanoMontagemReport report, string caminhoSaida)
        {
            Logger.Info("[PlanoMontagem] Exportando relatório para {Path}", caminhoSaida);

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    // ===== Sheet 1: Etapas (sumário) =====
                    var wsEtapas = workbook.Worksheets.Add("Etapas");
                    wsEtapas.Cell("A1").Value = "Etapa";
                    wsEtapas.Cell("B1").Value = "Descricao";
                    wsEtapas.Cell("C1").Value = "Data Planejada";
                    wsEtapas.Cell("D1").Value = "Quantidade";

                    int linhaEtapa = 2;
                    foreach (var etapa in report.Etapas)
                    {
                        wsEtapas.Cell($"A{linhaEtapa}").Value = etapa.Numero;
                        wsEtapas.Cell($"B{linhaEtapa}").Value = etapa.Descricao;

                        if (etapa.DataPlanejada.HasValue)
                            wsEtapas.Cell($"C{linhaEtapa}").Value = etapa.DataPlanejada.Value.ToString("dd/MM/yyyy");

                        wsEtapas.Cell($"D{linhaEtapa}").Value = etapa.ElementIds.Count;

                        linhaEtapa++;
                    }

                    wsEtapas.Columns().AdjustToContents();

                    // ===== Sheet 2: Elementos (detalhe) =====
                    var wsElementos = workbook.Worksheets.Add("Elementos");
                    wsElementos.Cell("A1").Value = "Etapa";
                    wsElementos.Cell("B1").Value = "ElementID";

                    int linhaElem = 2;
                    foreach (var etapa in report.Etapas)
                    {
                        foreach (var elemId in etapa.ElementIds)
                        {
                            wsElementos.Cell($"A{linhaElem}").Value = etapa.Numero;
                            wsElementos.Cell($"B{linhaElem}").Value = elemId;
                            linhaElem++;
                        }
                    }

                    wsElementos.Columns().AdjustToContents();

                    workbook.SaveAs(caminhoSaida);
                }

                Logger.Info("[PlanoMontagem] Relatório exportado com sucesso: {Path}", caminhoSaida);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagem] Erro ao exportar relatório Excel");
            }
        }
    }
}
