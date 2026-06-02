#nullable enable
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Services;

namespace SteelBIM.Commands
{
    /// <summary>
    /// v2.8.11 (Onda 4 — P5): "Limpar Modelo" — remove os elementos auxiliares criados
    /// pelo SteelBIM (filtros de vista + grupos temporarios com prefixo EMT) antes da
    /// entrega ao cliente. Pede confirmacao e NAO remove elementos modelados.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdLimparModelo : FerramentaCommandBase
    {
        protected override string CommandName => "Limpar Modelo";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            bool ok = Confirm(
                "Desfazer os grupos temporários criados pelo SteelBIM (prefixo 'EMT_'), " +
                "preservando os membros?\n\n" +
                "Isto NÃO remove elementos modelados (pilares, vigas, armaduras). " +
                "Para remover famílias/tipos não utilizados, use também " +
                "'Gerenciar → Purgar Não Utilizados' do Revit.",
                "Preparar arquivo para entrega");
            if (!ok)
                return Result.Cancelled;

            LimparModeloService.Resultado r = LimparModeloService.Limpar(doc);

            string resumo = $"Grupos temporários desfeitos: {r.GruposDesfeitos}";

            if (r.Falhas.Count > 0)
                resumo += "\n\nOcorrências:\n• " + string.Join("\n• ", r.Falhas.Take(10));

            resumo += "\n\nDica: para deixar o arquivo mais leve, rode também " +
                      "'Gerenciar → Purgar Não Utilizados' (famílias e tipos não usados).";

            ShowSuccess(resumo, "Limpeza concluída");
            return Result.Succeeded;
        }
    }
}
