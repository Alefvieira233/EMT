using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Services;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// Detecta e aplica automaticamente corte (JoinGeometry ou SolidSolidCut)
    /// entre pisos/quadro estrutural (hosts) e colunas/pilares (cortadores)
    /// que estejam interferindo.
    /// </summary>
    /// <remarks>
    /// Origem: trabalho do Victor (snapshot 2026-04-14).
    /// Adaptado para ADR-003: servico "mudo" (<see cref="CortarElementosService"/>)
    /// retorna <c>Result&lt;CortarElementosResultado&gt;</c>; este comando gerencia
    /// a transacao externa e monta a UX usando os helpers de <see cref="FerramentaCommandBase"/>.
    ///
    /// Onda 5 (Victor Final): abre <see cref="CortarElementosWindow"/> antes do
    /// PickObjects para coletar config (escopo + filtros de categoria) e despacha
    /// a coleta conforme o escopo escolhido (Selecao / VistaAtiva / Modelo).
    /// </remarks>
    [Transaction(TransactionMode.Manual)]
    public class CmdCortarElementos : FerramentaCommandBase
    {
        protected override string CommandName => "Cortar Elementos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Detecta pre-selecao para habilitar o radio "Selecao" na Window.
            List<Element> preSelecionados = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(CortarElementosService.EhElementoValidoParaEscopo)
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();

            bool temSelecaoAtiva = preSelecionados.Count > 0;

            CortarElementosWindow window = new CortarElementosWindow(temSelecaoAtiva);
            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            CortarElementosConfig config = window.BuildConfig();
            if (config == null)
                return Result.Cancelled;

            List<Element> elementosEscopo = ColetarEscopo(uidoc, doc, config, preSelecionados);
            if (elementosEscopo.Count == 0)
            {
                ShowWarning(
                    "Nenhum elemento valido encontrado no escopo selecionado com os filtros de categoria atuais.",
                    "Sem resultado");
                return Result.Cancelled;
            }

            CortarElementosService service = new CortarElementosService();
            FerramentaEMT.Core.Result<CortarElementosResultado> retorno;
            bool comitou = false;

            using (Transaction t = new Transaction(doc, CommandName))
            {
                t.Start();
                retorno = service.Executar(doc, elementosEscopo);

                if (retorno.IsSuccess && retorno.Value != null && retorno.Value.HouveAlteracao)
                {
                    t.Commit();
                    comitou = true;
                }
                else
                {
                    t.RollBack();
                }
            }

            if (retorno.IsFailure)
            {
                Logger.Warn("[{Cmd}] servico retornou falha: {Err}", CommandName, retorno.Error);
                ShowWarning(retorno.Error ?? "Nao foi possivel cortar os elementos.", "Nada a fazer");
                return Result.Cancelled;
            }

            CortarElementosResultado resultado = retorno.Value;

            List<ElementId> idsSelecaoFinal = resultado.ElementosRelacionados.Count > 0
                ? resultado.ElementosRelacionados.Select(v => new ElementId(v)).ToList()
                : elementosEscopo.Select(x => x.Id).Distinct().ToList();

            try
            {
                uidoc.Selection.SetElementIds(idsSelecaoFinal);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[{Cmd}] falha ao restaurar selecao (ignorando)", CommandName);
            }

            string resumo =
                $"Elementos selecionados: {resultado.TotalSelecionados}" +
                $"\nHosts analisados: {resultado.HostsAnalisados}" +
                $"\nColunas/pilares analisados: {resultado.CuttersAnalisados}" +
                $"\nPares com interferencia: {resultado.ParesIntersectando}" +
                $"\nPares com alteracao: {resultado.AlteracoesAplicadas}" +
                $"\nPares ja conformes: {resultado.JaConformes}" +
                $"\nFalhas: {resultado.Falhas}";

            string mensagem = resumo + FormatarDiagnostico(resultado.Diagnostico);

            if (comitou && resultado.HouveAlteracao)
            {
                ShowSuccess(mensagem, "Corte concluido");
                return Result.Succeeded;
            }

            if (resultado.JaConformes > 0)
            {
                ShowInfo(mensagem, "Nada novo para cortar");
                return Result.Succeeded;
            }

            ShowWarning(mensagem, "Nenhum corte aplicado");
            return Result.Cancelled;
        }

        private List<Element> ColetarEscopo(
            UIDocument uidoc,
            Document doc,
            CortarElementosConfig config,
            List<Element> preSelecionados)
        {
            IEnumerable<Element> brutos;

            switch (config.Escopo)
            {
                case CortarElementosEscopo.Selecao:
                    brutos = preSelecionados;
                    break;

                case CortarElementosEscopo.Modelo:
                    brutos = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(CortarElementosService.EhElementoValidoParaEscopo);
                    break;

                case CortarElementosEscopo.VistaAtiva:
                default:
                    View vistaAtiva = doc?.ActiveView;
                    if (vistaAtiva == null)
                        return new List<Element>();
                    brutos = new FilteredElementCollector(doc, vistaAtiva.Id)
                        .WhereElementIsNotElementType()
                        .Where(CortarElementosService.EhElementoValidoParaEscopo);
                    break;
            }

            return brutos
                .Where(e => AceitaCategoria(e, config))
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();
        }

        private static bool AceitaCategoria(Element element, CortarElementosConfig config)
        {
            if (element?.Category == null)
                return false;

            BuiltInCategory cat = element.Category.BuiltInCategory;
            switch (cat)
            {
                case BuiltInCategory.OST_Floors:
                    return config.IncluirPisos;
                case BuiltInCategory.OST_StructuralFraming:
                    return config.IncluirQuadroEstrutural;
                case BuiltInCategory.OST_Walls:
                    return config.IncluirParedes;
                case BuiltInCategory.OST_StructuralColumns:
                    return config.IncluirPilaresEstruturais;
                case BuiltInCategory.OST_Columns:
                    return config.IncluirColunas;
                default:
                    // Se a categoria nao for nenhuma das filtradas mas o service aceita,
                    // permite passar para nao perder nada inesperado.
                    return true;
            }
        }

        private static string FormatarDiagnostico(IReadOnlyList<string> diagnostico)
        {
            if (diagnostico == null || diagnostico.Count == 0)
                return string.Empty;

            return "\n\nDiagnostico:\n" + string.Join("\n", diagnostico.Take(16));
        }
    }
}
