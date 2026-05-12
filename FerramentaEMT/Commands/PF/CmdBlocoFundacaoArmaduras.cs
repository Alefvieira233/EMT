#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.Bloco;
using FerramentaEMT.Services.Bloco;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdBlocoFundacaoArmaduras : FerramentaCommandBase
    {
        protected override string CommandName => "Bloco Fundacao - Armaduras Manuais";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                e => BlockGeometryService.CanHostRebar(e),
                "Selecione os blocos de fundacao para lancar a armadura.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            BlocoFundacaoArmaduraWindow window = new BlocoFundacaoArmaduraWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            BlocoFundacaoRebarConfig config = window.BuildConfig();
            return new BlocoFundacaoRebarOrchestrator().Execute(uidoc, hosts, config);
        }
    }
}
