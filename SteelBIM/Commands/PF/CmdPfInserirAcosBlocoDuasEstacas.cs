using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.PF;
using SteelBIM.Services.PF;
using SteelBIM.Views;

namespace SteelBIM.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfInserirAcosBlocoDuasEstacas : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Acos Bloco 2 Estacas";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> hosts = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsTwoPileCap,
                "Selecione os blocos de duas estacas para configurar e lancar a armadura.");

            if (hosts.Count == 0)
                return Result.Cancelled;

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            PfTwoPileCapRebarWindow window = new PfTwoPileCapRebarWindow(doc, hosts[0]);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            PfTwoPileCapRebarConfig config = window.BuildConfig();

            // ADR-003: service eh mudo. Pegamos o resultado e decidimos a UX aqui no Cmd.
            PfTwoPileCapResultado resultado;
            Result revitResult = new PfTwoPileCapRebarService().Execute(uidoc, config, out resultado);

            if (resultado.SelecaoVazia)
            {
                ShowWarning("Nenhum bloco de fundacao elegivel foi selecionado.", "Selecao vazia");
                return revitResult;
            }

            if (resultado.HostsComSucesso > 0)
                // v2.8.9 (auditoria): o bloco sobre 2 estacas lanca apenas a malha de
                // tirantes/pele — NAO inclui estribos de suspensao. Disclaimer explicito
                // (elemento biela-tirante exige verificacao do engenheiro).
                ShowInfo(resultado.ToResumo() +
                    "\n\nATENÇÃO: lançada a malha de tirantes — estribos de suspensão NÃO são " +
                    "gerados automaticamente. Verifique e complemente conforme o projeto estrutural.",
                    "Processamento concluido");
            else
                ShowWarning(resultado.ToResumo(), "Nenhuma armadura criada");

            return revitResult;
        }
    }
}
