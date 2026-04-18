#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Montagem;

namespace FerramentaEMT.Services.Montagem
{
    /// <summary>
    /// Orquestrador do Plano de Montagem (Erection Plan).
    /// Gerencia atribuição de etapas, geração de relatórios, destaque visual e exportação Excel.
    /// </summary>
    public class PlanoMontagemService
    {
        private const string Titulo = "Plano de Montagem";

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
        /// Atribui um número de etapa de montagem a uma lista de elementos.
        /// Cria o parâmetro de projeto se não existir; fallback para Comments se necessário.
        /// </summary>
        public ResultadoMontagem AtribuirEtapa(
            UIDocument uidoc,
            IList<ElementId> ids,
            int etapa,
            string nomeParametro)
        {
            Logger.Info("[PlanoMontagem] Atribuindo etapa {Etapa} a {Count} elementos", etapa, ids.Count);

            Document doc = uidoc.Document;
            int processados = 0;

            try
            {
                using (Transaction tx = new Transaction(doc, $"Atribuir etapa {etapa}"))
                {
                    tx.Start();

                    foreach (ElementId eid in ids)
                    {
                        Element elem = doc.GetElement(eid);
                        if (elem == null) continue;

                        // Tenta escrever no parâmetro de projeto
                        Parameter? param = elem.LookupParameter(nomeParametro);
                        if (param != null && param.StorageType == StorageType.Integer)
                        {
                            param.Set(etapa);
                            processados++;
                        }
                        else
                        {
                            // Fallback: escreve em Comments. Sempre sobrescreve (remove "Etapa:X" anterior).
                            Parameter? comments = elem.LookupParameter("Comments");
                            if (comments != null && !comments.IsReadOnly)
                            {
                                string valorAnterior = comments.AsString() ?? "";
                                // Remove qualquer "Etapa:N" existente (caso o elemento ja tivesse etapa antiga)
                                string semEtapaAntiga = System.Text.RegularExpressions.Regex.Replace(
                                    valorAnterior, @"Etapa:\d+\s*;?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                                string novoValor = string.IsNullOrEmpty(semEtapaAntiga)
                                    ? $"Etapa:{etapa}"
                                    : $"Etapa:{etapa}; {semEtapaAntiga}";
                                comments.Set(novoValor);
                                processados++;
                            }
                        }
                    }

                    tx.Commit();
                }

                return new ResultadoMontagem
                {
                    Sucesso = true,
                    ElementosProcessados = processados,
                    Mensagem = $"Atribuído a {processados} elemento(s)."
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PlanoMontagem] Erro ao atribuir etapa");
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
                    if (etapaNum <= 0) continue;

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
            Parameter? paramInt = elem.LookupParameter(nomeParametroEtapa);
            if (paramInt != null && paramInt.StorageType == StorageType.Integer)
            {
                int v = paramInt.AsInteger();
                if (v > 0) return v;
            }

            // 2) Fallback Comments: string no formato "Etapa:N" (parsing delegado a EtapaMontagemParser)
            Parameter? comments = elem.LookupParameter("Comments");
            if (comments != null && comments.StorageType == StorageType.String)
            {
                return EtapaMontagemParser.Parse(comments.AsString());
            }

            return 0;
        }

        /// <summary>
        /// Aplica destaque visual (colorização) aos elementos de cada etapa usando OverrideGraphicSettings.
        /// </summary>
        public void AplicarDestaqueVisual(Document doc, View view, List<EtapaMontagem> etapas)
        {
            Logger.Info("[PlanoMontagem] Aplicando destaque visual a {Count} etapas", etapas.Count);

            try
            {
                using (Transaction tx = new Transaction(doc, "Destaque Visual - Plano de Montagem"))
                {
                    tx.Start();

                    for (int i = 0; i < etapas.Count; i++)
                    {
                        EtapaMontagem etapa = etapas[i];
                        Color cor = CoresPadrao[i % CoresPadrao.Length];

                        foreach (long elemIdVal in etapa.ElementIds)
                        {
                            Element? elem = doc.GetElement(new ElementId(elemIdVal));
                            if (elem == null) continue;

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
                Logger.Error(ex, "[PlanoMontagem] Erro ao aplicar destaque visual");
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
