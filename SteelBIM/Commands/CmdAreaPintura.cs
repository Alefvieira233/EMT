#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models;
using SteelBIM.Services.PF;
using SteelBIM.Services.Pintura;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// v2.8.26: calcula a area de pintura (perimetro x comprimento) dos perfis metalicos pela
    /// geometria, grava no parametro EMT_Area_Pintura e cria/atualiza a tabela de quantitativo.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAreaPintura : FerramentaCommandBase
    {
        protected override string CommandName => "Area de Pintura";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            AreaPinturaWindow window = new AreaPinturaWindow();
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            AreaPinturaConfig cfg = window.BuildConfig();

            bool Filtro(Element e) =>
                (cfg.IncluirVigas && PfElementService.IsStructuralBeam(e)) ||
                (cfg.IncluirPilares && PfElementService.IsStructuralColumn(e));

            List<Element> alvos;
            if (cfg.SomenteSelecao)
            {
                alvos = uidoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .Where(x => x != null && Filtro(x))
                    .ToList();
                if (alvos.Count == 0)
                {
                    ShowWarning("Selecione vigas/pilares metalicos antes, ou escolha 'Todo o modelo'.");
                    return Result.Cancelled;
                }
            }
            else
            {
                ElementMulticategoryFilter cats = new ElementMulticategoryFilter(new[]
                {
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralColumns
                });
                alvos = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(cats)
                    .Where(Filtro)
                    .ToList();
            }

            if (alvos.Count == 0)
            {
                ShowWarning("Nenhuma viga/pilar metalico encontrado para o escopo escolhido.");
                return Result.Cancelled;
            }

            PinturaResultado r = new AreaPinturaService().Executar(uidoc, alvos, cfg);

            string msg = $"Peças processadas: {r.Processadas}";
            if (r.Falhas > 0)
                msg += $"\nSem área/parâmetro: {r.Falhas}";
            msg += $"\nÁrea total de pintura: {r.TotalM2:0.00} m²";
            if (cfg.CriarTabela)
            {
                msg += r.TabelaCriada
                    ? "\nTabela criada/atualizada (procure por \"EMT - Area de Pintura\")."
                    : $"\nTabela não criada{(string.IsNullOrEmpty(r.MotivoTabela) ? string.Empty : ": " + r.MotivoTabela)}.";
            }

            ShowInfo(msg, "Área de Pintura");
            return Result.Succeeded;
        }
    }
}
