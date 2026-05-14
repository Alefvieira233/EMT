using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.PF;
using SteelBIM.Services;
using SteelBIM.Utils;

namespace SteelBIM.Services.PF
{
    internal sealed class PfNamingService
    {
        // Tolerancia em pes para o snap de ordenacao geometrica. ~10 cm em
        // grids estruturais — vigas dentro desse raio do mesmo eixo logico
        // caem no mesmo bucket numerico, eliminando dependencia de qual
        // viga "ancorou" o cluster greedy anterior.
        private const double EIXO_TOLERANCIA_FT = 0.328084;

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
                .ThenBy(x => OrderHorizontalSnapped(x, view, config))
                .ThenBy(x => OrderVerticalSnapped(x, view, config))
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

            if (config.Alvo == PfNamingTarget.Vigas)
            {
                int diagonais = ordenados.Count(e => PfElementService.GetBeamAxisGroup(e, view) == 2);
                if (diagonais > 0)
                {
                    resumo += $"\n{diagonais} viga(s) sem eixo definido foram numeradas por Id ao final da sequencia.";
                }
            }

            if (falhas.Count > 0)
                resumo += "\n\nSem parametro editavel em alguns elementos:\n- " + string.Join("\n- ", falhas.Take(8));

            AppDialogService.ShowInfo(commandName, resumo, "Numeracao concluida");
            return Result.Succeeded;
        }

        private static double OrderHorizontalSnapped(Element x, View view, PfNamingConfig config)
        {
            bool vigaHorizontalNoEixoX =
                config.Alvo == PfNamingTarget.Vigas &&
                PfElementService.GetBeamAxisGroup(x, view) == 1;

            XYZ p = PfElementService.GetRepresentativePoint(x, view);
            double raw = vigaHorizontalNoEixoX
                ? PfElementService.GetHorizontalOrder(view, p)
                : -PfElementService.GetVerticalOrder(view, p);

            return PfElementService.GetSnappedOrder(raw, EIXO_TOLERANCIA_FT);
        }

        private static double OrderVerticalSnapped(Element x, View view, PfNamingConfig config)
        {
            bool vigaHorizontalNoEixoX =
                config.Alvo == PfNamingTarget.Vigas &&
                PfElementService.GetBeamAxisGroup(x, view) == 1;

            XYZ p = PfElementService.GetRepresentativePoint(x, view);
            double raw = vigaHorizontalNoEixoX
                ? -PfElementService.GetVerticalOrder(view, p)
                : PfElementService.GetHorizontalOrder(view, p);

            return PfElementService.GetSnappedOrder(raw, EIXO_TOLERANCIA_FT);
        }
    }
}
