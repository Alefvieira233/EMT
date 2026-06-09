#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.Bloco;
using SteelBIM.Services.Bloco;
using SteelBIM.Services.PF;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    /// <summary>
    /// v2.8.21 (Fase 1): comando dedicado que monta a GAIOLA FECHADA do bloco de coroamento —
    /// malha de fundo (acima das estacas, com gancho para cima), malha de topo, estribos
    /// perimetrais que fecham a gaiola e pele lateral. Resolve a queixa do detalhamento
    /// fragmentado ("U soltos") gerando uma armacao 3D continua.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdArmaduraCoroamento : FerramentaCommandBase
    {
        protected override string CommandName => "Armadura de Fundacao - Coroamento (Gaiola)";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            SteelBIM.Utils.DisclaimerService.MostrarUmaVezPorSessao(
                "pf-armadura", CommandName, SteelBIM.Utils.DisclaimerTexts.Armadura);

            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                e => BlockGeometryService.CanHostRebar(e),
                "Selecione os blocos de coroamento para montar a gaiola fechada.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            ArmaduraCoroamentoWindow window = new ArmaduraCoroamentoWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            CoroamentoConfig config = window.BuildConfig();
            return new CoroamentoCageService().Executar(uidoc, hosts, config).result;
        }
    }
}
