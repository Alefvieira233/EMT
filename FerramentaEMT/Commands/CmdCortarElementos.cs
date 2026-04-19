using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Services;

namespace FerramentaEMT.Commands
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
    /// </remarks>
    [Transaction(TransactionMode.Manual)]
    public class CmdCortarElementos : FerramentaCommandBase
    {
        protected override string CommandName => "Cortar Elementos";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            List<Element> elementosEscopo = SelecionarEscopo(uidoc, doc);
            if (elementosEscopo.Count == 0)
                return Result.Cancelled;

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

        private List<Element> SelecionarEscopo(UIDocument uidoc, Document doc)
        {
            List<Element> preSelecionados = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(CortarElementosService.EhElementoValidoParaEscopo)
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();

            if (preSelecionados.Count > 0)
                return preSelecionados;

            IList<Reference> referencias;
            try
            {
                referencias = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroEscopo(),
                    "Selecione pisos, quadro estrutural e/ou colunas/pilares para cortar automaticamente");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new List<Element>();
            }

            List<Element> elementos = referencias
                .Select(r => doc.GetElement(r))
                .Where(CortarElementosService.EhElementoValidoParaEscopo)
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();

            if (elementos.Count == 0)
            {
                ShowWarning(
                    "Nenhum piso, elemento de quadro estrutural, coluna ou pilar estrutural foi selecionado.",
                    "Selecao invalida");
            }

            return elementos;
        }

        private static string FormatarDiagnostico(IReadOnlyList<string> diagnostico)
        {
            if (diagnostico == null || diagnostico.Count == 0)
                return string.Empty;

            return "\n\nDiagnostico:\n" + string.Join("\n", diagnostico.Take(16));
        }

        private sealed class FiltroEscopo : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return CortarElementosService.EhElementoValidoParaEscopo(elem);
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
