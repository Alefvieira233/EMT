using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.PF
{
    internal sealed class PfNamingService
    {
        public Result Execute(
            UIDocument uidoc,
            string commandName,
            PfNamingConfig config)
        {
            if (uidoc == null || config == null)
                return Result.Failed;

            Document doc = uidoc.Document;
            List<NumeracaoElementoInfo> candidatos = PfNamingCatalog.ColetarCandidatos(uidoc, config.Escopo, config.Alvo);
            List<NumeracaoElementoInfo> filtrados = PfNamingCatalog.Filtrar(candidatos, config);
            if (filtrados.Count == 0)
            {
                AppDialogService.ShowWarning(
                    commandName,
                    "Nenhum elemento PM elegivel foi encontrado com os filtros atuais.",
                    "Nenhum item encontrado");
                return Result.Cancelled;
            }

            View view = doc.ActiveView;
            List<Element> ordenados = filtrados
                .Select(x => doc.GetElement(x.Id))
                .Where(x => x != null)
                .OrderBy(x => config.Alvo == PfNamingTarget.Vigas
                    ? PfElementService.GetBeamAxisGroup(x, view)
                    : 0)
                .ThenBy(x => config.Alvo == PfNamingTarget.Vigas &&
                             PfElementService.GetBeamAxisGroup(x, view) == 1
                    ? PfElementService.GetHorizontalOrder(view, PfElementService.GetRepresentativePoint(x, view))
                    : -PfElementService.GetVerticalOrder(view, PfElementService.GetRepresentativePoint(x, view)))
                .ThenBy(x => config.Alvo == PfNamingTarget.Vigas &&
                             PfElementService.GetBeamAxisGroup(x, view) == 1
                    ? -PfElementService.GetVerticalOrder(view, PfElementService.GetRepresentativePoint(x, view))
                    : PfElementService.GetHorizontalOrder(view, PfElementService.GetRepresentativePoint(x, view)))
                .ThenBy(x => x.Id.Value)
                .ToList();

            int numero = config.Inicio;
            int atualizados = 0;
            List<string> falhas = new List<string>();

            using (Transaction transaction = new Transaction(doc, commandName))
            {
                transaction.Start();

                foreach (Element elemento in ordenados)
                {
                    Parameter parametro = NumeracaoItensCatalog.EncontrarParametro(
                        elemento,
                        config.ParametroChave,
                        config.ParametroStorageType);

                    if (parametro == null || parametro.IsReadOnly)
                    {
                        falhas.Add($"Id {elemento.Id.Value}");
                        continue;
                    }

                    bool gravou = config.ParametroStorageType switch
                    {
                        StorageType.Integer => parametro.Set(numero),
                        StorageType.String => parametro.Set(config.MontarValor(numero)),
                        _ => false
                    };

                    if (gravou)
                    {
                        atualizados++;
                        numero += config.Degrau;
                    }
                    else
                    {
                        falhas.Add($"Id {elemento.Id.Value}");
                    }
                }

                transaction.Commit();
            }

            uidoc.Selection.SetElementIds(ordenados.Select(x => x.Id).ToList());

            string resumo = $"Elementos processados: {ordenados.Count}\n" +
                            $"Valores atualizados: {atualizados}\n" +
                            $"Alvo: {PfNamingCatalog.GetTargetDisplayName(config.Alvo)}\n" +
                            $"Parametro: {config.ParametroNome}\n" +
                            $"Inicio: {config.Inicio}\n" +
                            $"Degrau: {config.Degrau}";

            if (config.ParametroStorageType == StorageType.String)
                resumo += $"\nFormato inicial: {config.MontarValor(config.Inicio)}";

            if (config.Alvo == PfNamingTarget.Vigas)
                resumo += "\nOrdem aplicada nas vigas: horizontais/X primeiro, depois verticais/Y.";

            if (falhas.Count > 0)
                resumo += "\n\nSem parametro editavel em alguns elementos:\n- " + string.Join("\n- ", falhas.Take(8));

            AppDialogService.ShowInfo(commandName, resumo, "Numeracao concluida");
            return Result.Succeeded;
        }
    }
}
