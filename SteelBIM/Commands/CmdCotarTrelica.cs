#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando "Cotar Treliça" — aplica o padrao de cotagem EMT em 5 faixas
    /// + identificacao de perfis sobre uma elevacao/corte de trelica.
    ///
    /// Fluxo confirmado pelo usuario (ver docs/PLANO-LAPIDACAO.md secao 10):
    ///   1. Usuario PRE-SELECIONA as barras da trelica (filter: Structural Framing).
    ///   2. Clica no botao — abre CotarTrelicaWindow com opcoes (checkboxes).
    ///   3. Ao clicar OK, o comando valida, monta config e chama CotarTrelicaService.
    ///   4. Tudo roda dentro de uma transacao unica "EMT - Cotar Trelica".
    ///
    /// Requisitos de vista:
    ///   - Tem que ser uma Elevation ou Section (vista 2D lateral).
    ///   - Caso contrario, avisa o usuario e sai.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCotarTrelica : FerramentaCommandBase
    {
        protected override string CommandName => "Cotar Treliça";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // ===== 1. Valida tipo de vista =====
            View vistaAtiva = doc.ActiveView;
            if (vistaAtiva == null || !EhVistaLateralValida(vistaAtiva))
            {
                return NothingToDo(
                    "Abra uma elevacao ou corte da trelica antes de executar o comando.");
            }

            // ===== 2. Coleta barras pre-selecionadas =====
            var selIds = uidoc.Selection.GetElementIds();
            var barras = new List<FamilyInstance>();
            foreach (var id in selIds)
            {
                if (doc.GetElement(id) is FamilyInstance fi &&
                    fi.Category?.Id == new ElementId(BuiltInCategory.OST_StructuralFraming))
                {
                    barras.Add(fi);
                }
            }

            if (barras.Count < 3)
            {
                return NothingToDo(
                    "Selecione as barras da trelica (banzos, montantes e diagonais) antes de clicar no botao.");
            }

            // ===== 3. Abre dialogo de configuracao =====
            var wnd = new CotarTrelicaWindow();
            bool? ok = wnd.ShowDialog();
            if (ok != true) return Result.Cancelled;

            CotarTrelicaConfig config = wnd.BuildConfig();

            // ===== 4. Executa servico dentro de uma transacao =====
            var service = new Services.Trelica.CotarTrelicaService();
            Services.Trelica.CotarTrelicaReport report;

            using (var t = new Transaction(doc, "EMT - Cotar Treliça"))
            {
                t.Start();
                // P1.1 (2026-04-28): pipeline de 10 etapas cria muitas Dimensions/Tags/TextNotes;
                // sem swallow warnings comuns ("dimension outside view", "joined geometry...")
                // bloqueiam o commit com dialogo modal. Erros (Severity != Warning) seguem normais.
                FerramentaEMT.Utils.FailureHandlingHelper.SwallowWarnings(t);
                try
                {
                    Logger.Info("[{Cmd}] chamando service com {N} barras", CommandName, barras.Count);
                    report = service.Executar(uidoc, doc, vistaAtiva, barras, config);
                    t.Commit();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[{Cmd}] erro no service", CommandName);
                    if (t.HasStarted() && !t.HasEnded())
                        t.RollBack();
                    throw;
                }
            }

            // ===== 5. Exibe relatorio ao usuario =====
            if (report.WarningsCount == 0)
                ShowSuccess(report.Resumo, "Treliça cotada com sucesso");
            else
                ShowWarning(report.Resumo, "Treliça cotada (com avisos)");

            return Result.Succeeded;
        }

        private static bool EhVistaLateralValida(View v)
        {
            return v.ViewType == ViewType.Elevation ||
                   v.ViewType == ViewType.Section ||
                   v.ViewType == ViewType.Detail;
        }
    }
}
