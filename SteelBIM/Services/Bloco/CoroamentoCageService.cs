#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Models.Bloco;

namespace SteelBIM.Services.Bloco
{
    /// <summary>
    /// v2.8.21 (Fase 1): monta a GAIOLA FECHADA do bloco de coroamento orquestrando os serviços de
    /// armadura existentes — malha de fundo (acima das estacas, com gancho para cima), malha de topo,
    /// estribos perimetrais (fecham a gaiola) e pele lateral. Aplica cobrimento efetivo
    /// (cobrimento do bloco + Ø do estribo) nas longitudinais e desconto do topo da estaca via OffsetZ.
    /// </summary>
    internal sealed class CoroamentoCageService
    {
        public (Result result, int hostsOk, int totalCriados) Executar(
            UIDocument uidoc, IReadOnlyList<Element> hosts, CoroamentoConfig c)
        {
            Document doc = uidoc.Document;
            double diametroEstriboCm = DiametroBarraCm(doc, c.EstriboBarType);
            double cobrEfetivaCm = c.CobrimentoBlocoCm + (c.FecharGaiola ? diametroEstriboCm : 0.0);

            BlocoFundacaoRebarConfig cfg = MontarGaiola(c, cobrEfetivaCm);
            return new BlocoFundacaoRebarOrchestrator().Execute(uidoc, hosts, cfg, mostrarResumo: false);
        }

        private static BlocoFundacaoRebarConfig MontarGaiola(CoroamentoConfig c, double cobrEfetivaCm)
        {
            BlocoFundacaoRebarConfig cfg = new BlocoFundacaoRebarConfig();

            // ----- malha de fundo (tracao): acima das estacas, com gancho para cima (fecha o "U") -----
            cfg.LancarArmaduraInferior = !string.IsNullOrWhiteSpace(c.MalhaFundoBarType);
            foreach (BlocoBarraDirecaoConfig dir in new[] { cfg.ArmaduraInferior.BarsX, cfg.ArmaduraInferior.BarsY })
            {
                dir.Ativo = true;
                dir.BarTypeName = c.MalhaFundoBarType;
                dir.CobrimentoCm = cobrEfetivaCm;
                dir.ModoQuantidade = BlocoModoQuantidade.PorEspacamento;
                dir.EspacamentoCm = c.MalhaFundoEspacamentoCm;
                dir.OffsetZCm = c.TopoEstacaEmbutidoCm;        // sobe a malha acima das estacas
                if (c.FecharGaiola)
                {
                    dir.Dobra.HaDobraInicial = true;
                    dir.Dobra.HaDobraFinal = true;
                    dir.Dobra.ComprimentoDobraInicialCm = c.GanchoMalhaCm;
                    dir.Dobra.ComprimentoDobraFinalCm = c.GanchoMalhaCm;
                    dir.Dobra.DobraInicialParaCima = true;
                    dir.Dobra.DobraFinalParaCima = true;
                }
            }

            // ----- malha de topo (opcional) -----
            cfg.LancarArmaduraSuperior = c.LancarMalhaTopo && !string.IsNullOrWhiteSpace(c.MalhaTopoBarType);
            foreach (BlocoBarraDirecaoConfig dir in new[] { cfg.ArmaduraSuperior.BarsX, cfg.ArmaduraSuperior.BarsY })
            {
                dir.Ativo = true;
                dir.BarTypeName = c.MalhaTopoBarType;
                dir.CobrimentoCm = cobrEfetivaCm;
                dir.ModoQuantidade = BlocoModoQuantidade.PorEspacamento;
                dir.EspacamentoCm = c.MalhaTopoEspacamentoCm;
            }

            // ----- estribos perimetrais verticais: fecham a gaiola -----
            cfg.LancarEstriboVertical = c.FecharGaiola && !string.IsNullOrWhiteSpace(c.EstriboBarType);
            cfg.EstriboVertical.BarTypeName = c.EstriboBarType;
            cfg.EstriboVertical.CobrimentoCm = c.CobrimentoBlocoCm;
            foreach (BlocoEstriboVerticalDirConfig d in new[] { cfg.EstriboVertical.DirX, cfg.EstriboVertical.DirY })
            {
                d.Ativo = true;
                d.ModoQuantidade = BlocoModoQuantidade.PorEspacamento;
                d.EspacamentoCm = c.EstriboEspacamentoCm;
            }

            // ----- pele lateral (opcional) -----
            cfg.LancarArmaduraLateral = c.LancarPeleLateral && !string.IsNullOrWhiteSpace(c.PeleBarType);
            cfg.ArmaduraLateral.BarTypeName = c.PeleBarType;
            cfg.ArmaduraLateral.CobrimentoCm = cobrEfetivaCm;
            cfg.ArmaduraLateral.ModoQuantidade = BlocoModoQuantidade.PorEspacamento;
            cfg.ArmaduraLateral.EspacamentoCm = c.PeleEspacamentoCm;

            return cfg;
        }

        private static double DiametroBarraCm(Document doc, string barTypeName)
        {
            if (string.IsNullOrWhiteSpace(barTypeName))
                return 0.0;
            try
            {
                RebarBarType bt = RebarCreationService.GetBarType(doc, barTypeName);
                return UnitUtils.ConvertFromInternalUnits(bt.BarModelDiameter, UnitTypeId.Centimeters);
            }
            catch
            {
                return 0.0;
            }
        }
    }
}
