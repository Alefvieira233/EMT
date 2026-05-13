using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands.PF
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPfElevacaoFormaVigas : FerramentaCommandBase
    {
        protected override string CommandName => "PF - Elevação e Forma (Vigas)";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            GerarVistaPecaConfig config = new GerarVistaPecaConfig
            {
                Escopo = uidoc.Selection.GetElementIds().Any()
                    ? EscopoSelecaoPeca.SelecaoManual
                    : EscopoSelecaoPeca.VistaAtiva,
                CriarVistaLongitudinal = true,
                CriarCorteTransversal = true,
                EscalaVista = 20,
                MargemMm = 150,
                ProfundidadeCorteTransversalMm = 400,
                PrefixoNome = "PF-V",
                FiltroCategoria = VistaPecaCategoriaFiltro.Vigas
            };

            new AutoVistaService().Executar(uidoc, config);
            return Result.Succeeded;
        }
    }
}
