#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models.Bloco;
using SteelBIM.Utils;

namespace SteelBIM.Services.Bloco
{
    internal sealed class BlocoFundacaoRebarOrchestrator
    {
        public Result Execute(UIDocument uidoc, IReadOnlyList<Element> hosts, BlocoFundacaoRebarConfig config, bool mostrarResumo = true)
        {
            Document doc = uidoc.Document;
            int hostsOk = 0;
            int totalCriados = 0;
            List<string> avisos = new List<string>();

            using (Transaction tx = new Transaction(doc, "Bloco Fundacao - Armaduras Manuais"))
            {
                tx.Start();

                foreach (Element host in hosts)
                {
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            if (!BlockGeometryService.CanHostRebar(host))
                                throw new InvalidOperationException("Elemento nao suporta armaduras no Revit.");

                            BlocoHostFrame frame = BlockGeometryService.BuildFrame(host);
                            int criados = 0;

                            if (config.LancarArmaduraInferior)
                                criados += BottomRebarService.Generate(doc, host, frame, config.ArmaduraInferior);

                            if (config.LancarArmaduraSuperior)
                                criados += TopRebarService.Generate(doc, host, frame, config.ArmaduraSuperior);

                            if (config.LancarArmaduraLateral && !string.IsNullOrWhiteSpace(config.ArmaduraLateral.BarTypeName))
                                criados += SideSkinRebarService.Generate(doc, host, frame, config.ArmaduraLateral);

                            if (config.LancarEstriboVertical && !string.IsNullOrWhiteSpace(config.EstriboVertical.BarTypeName))
                                criados += VerticalStirrupService.Generate(doc, host, frame, config.EstriboVertical);

                            if (config.LancarEstriboHorizontal && !string.IsNullOrWhiteSpace(config.EstriboHorizontal.BarTypeName))
                                criados += HorizontalStirrupService.Generate(doc, host, frame, config.EstriboHorizontal);

                            if (config.LancarFaixaTransversal && !string.IsNullOrWhiteSpace(config.FaixaTransversal.BarTypeName))
                                criados += TransverseBandService.Generate(doc, host, frame, config.FaixaTransversal);

                            if (criados == 0)
                            {
                                avisos.Add($"Id {host.Id.Value}: nenhuma barra gerada (verifique dimensoes e cobrimento).");
                                sub.RollBack();
                                continue;
                            }

                            hostsOk++;
                            totalCriados += criados;
                            sub.Commit();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[Bloco Fundacao] falha no host {HostId}", host.Id.Value);
                            avisos.Add($"Id {host.Id.Value}: {ex.Message.Replace("\n", " ").Trim()}");
                            sub.RollBack();
                        }
                    }
                }

                tx.Commit();
            }

            if (mostrarResumo)
            {
                uidoc.Selection.SetElementIds(hosts.Select(h => h.Id).ToList());

                string resumo =
                    $"Blocos processados: {hosts.Count}\n" +
                    $"Blocos com sucesso: {hostsOk}\n" +
                    $"Armaduras criadas: {totalCriados}";

                if (avisos.Count > 0)
                    resumo += "\n\nOcorrencias:\n- " + string.Join("\n- ", avisos.Take(10));

                AppDialogService.ShowInfo("Bloco Fundacao - Armaduras", resumo, "Processamento concluido");
            }

            return hostsOk > 0 ? Result.Succeeded : Result.Failed;
        }
    }
}
