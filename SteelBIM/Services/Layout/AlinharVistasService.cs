#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Utils;

namespace SteelBIM.Services.Layout
{
    /// <summary>Acao de alinhamento/distribuicao escolhida na janela.</summary>
    public enum AcaoAlinhamentoVista
    {
        Esquerda,
        Direita,
        Topo,
        Base,
        CentroH,
        CentroV,
        DistribuirH,
        DistribuirV
    }

    /// <summary>
    /// v2.8.13 (Onda 2): alinha/distribui os VIEWPORTS selecionados numa folha (estilo
    /// PowerPoint). Le o footprint via Viewport.GetBoxOutline (papel, pes), delega a decisao
    /// ao nucleo PURO <see cref="LayoutVistasCalculator"/> e aplica via SetBoxCenter (por delta,
    /// pois GetBoxCenter pode diferir do centro do outline quando ha rotulo). Pula viewports
    /// fixados (pinned) e schedules (sem GetBoxOutline).
    /// </summary>
    public sealed class AlinharVistasService
    {
        public void Executar(UIDocument uidoc, AcaoAlinhamentoVista acao)
        {
            Document doc = uidoc.Document;

            List<Viewport> viewports = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id) as Viewport)
                .Where(vp => vp != null)
                .Cast<Viewport>()
                .ToList();

            bool distribuir = acao == AcaoAlinhamentoVista.DistribuirH || acao == AcaoAlinhamentoVista.DistribuirV;
            int minimo = distribuir ? 3 : 2;
            if (viewports.Count < minimo)
            {
                AppDialogService.ShowWarning(
                    "Alinhar Vistas",
                    $"Selecione pelo menos {minimo} vistas (viewports) na folha.",
                    "Seleção insuficiente");
                return;
            }

            // monta as caixas no espaco do papel (mm); pula pinados.
            var caixas = new List<CaixaVista>();
            var dadosPorId = new Dictionary<string, (Viewport Vp, double CentroXMm, double CentroYMm)>();
            int pinados = 0;
            foreach (Viewport vp in viewports)
            {
                if (vp.Pinned)
                {
                    pinados++;
                    continue;
                }

                Outline ol = vp.GetBoxOutline();
                double larguraMm = ToMm(ol.MaximumPoint.X - ol.MinimumPoint.X);
                double alturaMm = ToMm(ol.MaximumPoint.Y - ol.MinimumPoint.Y);
                double centroXMm = ToMm((ol.MinimumPoint.X + ol.MaximumPoint.X) / 2.0);
                double centroYMm = ToMm((ol.MinimumPoint.Y + ol.MaximumPoint.Y) / 2.0);

                string id = vp.Id.ToString();
                caixas.Add(new CaixaVista(id, larguraMm, alturaMm, centroXMm, centroYMm));
                dadosPorId[id] = (vp, centroXMm, centroYMm);
            }

            if (caixas.Count < minimo)
            {
                AppDialogService.ShowWarning(
                    "Alinhar Vistas",
                    $"Após descartar vistas fixadas, sobraram menos de {minimo}. Desafixe (unpin) e tente de novo.",
                    "Sem vistas suficientes");
                return;
            }

            IReadOnlyList<PosicaoVista> novas = distribuir
                ? LayoutVistasCalculator.Distribuir(caixas, acao == AcaoAlinhamentoVista.DistribuirH ? Eixo.Horizontal : Eixo.Vertical)
                : LayoutVistasCalculator.Alinhar(caixas, MapearModo(acao));

            int movidos = 0;
            using (Transaction t = new Transaction(doc, "Alinhar Vistas"))
            {
                t.Start();
                foreach (PosicaoVista pos in novas)
                {
                    if (!dadosPorId.TryGetValue(pos.Id, out var d))
                        continue;

                    double deltaXFt = ToFt(pos.CentroXMm - d.CentroXMm);
                    double deltaYFt = ToFt(pos.CentroYMm - d.CentroYMm);
                    if (System.Math.Abs(deltaXFt) < RevitUtils.EPS && System.Math.Abs(deltaYFt) < RevitUtils.EPS)
                        continue;

                    XYZ centroAtual = d.Vp.GetBoxCenter();
                    d.Vp.SetBoxCenter(new XYZ(centroAtual.X + deltaXFt, centroAtual.Y + deltaYFt, centroAtual.Z));
                    movidos++;
                }
                t.Commit();
            }

            string resumo = $"{movidos} vista(s) reposicionada(s).";
            if (pinados > 0)
                resumo += $"\n{pinados} vista(s) fixada(s) (pinned) foram ignoradas.";
            Logger.Info("[AlinharVistas] acao={Acao} movidos={Mov} pinados={Pin}", acao, movidos, pinados);
            AppDialogService.ShowInfo("Alinhar Vistas", resumo, "Concluído");
        }

        private static ModoAlinhamento MapearModo(AcaoAlinhamentoVista acao)
        {
            return acao switch
            {
                AcaoAlinhamentoVista.Esquerda => ModoAlinhamento.Esquerda,
                AcaoAlinhamentoVista.Direita => ModoAlinhamento.Direita,
                AcaoAlinhamentoVista.Topo => ModoAlinhamento.Topo,
                AcaoAlinhamentoVista.Base => ModoAlinhamento.Base,
                AcaoAlinhamentoVista.CentroH => ModoAlinhamento.CentroH,
                _ => ModoAlinhamento.CentroV
            };
        }

        private static double ToMm(double ft) => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);

        private static double ToFt(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
