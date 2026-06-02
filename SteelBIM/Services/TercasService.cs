#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SteelBIM.Models;
using SteelBIM.Utils;
using SteelBIM.Views.Helpers;
using IUIDecisionService = SteelBIM.Core.IUIDecisionService;

namespace SteelBIM.Services
{
    public class TercasService
    {
        private const string Titulo = "Gerar Terças por Plano";

        private readonly IUIDecisionService _ui;

        // v2.7.11 F6 (ADR-003 Wave 2): construtor com IUIDecisionService injetavel.
        // Sem injecao cai em AppDialogUIDecisionService — backward compat com
        // callers que faziam new TercasService() sem argumentos.
        public TercasService(IUIDecisionService? ui = null)
        {
            _ui = ui ?? new AppDialogUIDecisionService();
        }

        public Result Executar(UIDocument uidoc, Document doc, TercasConfig config, Plane plane)
            => ExecutarInterno(uidoc, doc, config, plane, prePickedLimA: null, prePickedLineAraw: null);

        /// <summary>
        /// v2.8.1 (Victor): overload que aceita a linha limite inicial ja
        /// pre-pickada pelo command. Evita pick duplicado quando o command
        /// precisa do vao da linha pra pre-preencher a TercasSpacingWindow.
        /// </summary>
        public Result Executar(UIDocument uidoc, Document doc, TercasConfig config, Plane plane,
            Element? prePickedLimA, Line? prePickedLineAraw)
            => ExecutarInterno(uidoc, doc, config, plane, prePickedLimA, prePickedLineAraw);

        private Result ExecutarInterno(UIDocument uidoc, Document doc, TercasConfig config, Plane plane,
            Element? prePickedLimA, Line? prePickedLineAraw)
        {
            if (plane == null)
            {
                _ui.Error(Titulo, "Nao foi possivel obter um plano valido para gerar as tercas.");
                return Result.Failed;
            }
            // Guard: config.Quantidade >= 1 (evita div/0 em step = 1.0/(Quantidade+1))
            if (config == null || config.Quantidade < 1)
            {
                _ui.Error(
                    Titulo,
                    "A quantidade de terças intermediárias precisa ser pelo menos 1.",
                    "Configuração inválida");
                return Result.Failed;
            }

            Element elLimA;
            Line lineAraw;
            // v2.8.1 (Victor): usa pre-pick se disponivel; senao pickea normalmente.
            if (prePickedLimA != null && prePickedLineAraw != null)
            {
                elLimA = prePickedLimA;
                lineAraw = prePickedLineAraw;
            }
            else
            {
                if (!RevitUtils.TryGetLineFromPickedElement(uidoc, "Selecione a LINHA LIMITE INICIAL", out elLimA, out lineAraw))
                {
                    _ui.Error(Titulo, "Nao foi possivel obter a linha limite inicial.");
                    return Result.Failed;
                }
            }

            Element elLimB;
            Line lineBraw;
            if (!RevitUtils.TryGetLineFromPickedElement(uidoc, "Selecione a LINHA LIMITE FINAL", out elLimB, out lineBraw))
            {
                _ui.Error(Titulo, "Nao foi possivel obter a linha limite final.");
                return Result.Failed;
            }

            Line lineA = RevitUtils.ProjectLineOntoPlane(lineAraw, plane);
            Line lineB = RevitUtils.ProjectLineOntoPlane(lineBraw, plane);
            if (lineA == null || lineB == null)
            {
                _ui.Error(Titulo, "Uma das linhas limite nao pode ser projetada corretamente no plano.");
                return Result.Failed;
            }

            lineB = RevitUtils.EnsureSameDirection(lineA, lineB);
            if (config.InverterSentido)
            {
                lineA = RevitUtils.ReverseLine(lineA);
                lineB = RevitUtils.ReverseLine(lineB);
            }

            List<Curve> curvasBanzos = new List<Curve>();
            if (config.DividirNosBanzos)
            {
                IList<Reference>? refsBanzos = null;
                try
                {
                    refsBanzos = uidoc.Selection.PickObjects(ObjectType.Element, "Selecione os BANZOS que devem dividir as terças");
                }
                catch
                {
                    return Result.Cancelled;
                }
                if (refsBanzos == null || refsBanzos.Count == 0)
                {
                    _ui.Warn(Titulo, "Nenhum banzo foi selecionado para divisao.", "Selecao vazia");
                    return Result.Failed;
                }
                foreach (Reference r in refsBanzos)
                {
                    Element el = doc.GetElement(r);
                    Curve? c = RevitUtils.GetElementCurve(el);
                    if (c != null)
                        curvasBanzos.Add(c);
                }
                if (curvasBanzos.Count == 0)
                {
                    _ui.Error(Titulo, "Nao foi possivel obter curvas validas dos banzos selecionados.");
                    return Result.Failed;
                }
            }

            Level nivel = RevitUtils.GetElementLevel(doc, elLimA);
            if (nivel == null)
            {
                _ui.Error(Titulo, "Nao foi possivel determinar o nivel de referencia.");
                return Result.Failed;
            }

            double beiralIniFt = config.BeiralInicialCm * RevitUtils.FT_PER_CM;
            double beiralFimFt = config.BeiralFinalCm * RevitUtils.FT_PER_CM;
            double offsetFt = config.OffsetMm * RevitUtils.FT_PER_MM;
            double rotacaoRad = RevitUtils.DegToRad(config.RotacaoSecaoGraus);

            // v2.8.1 (Victor): calcula parametros (0..1) ao longo da lineA/lineB.
            // Delega ao helper puro (testavel sem Revit). Quando UsarEspacamentoManual
            // + EspacamentosCm.Count == Quantidade+1, usa distancias customizadas;
            // senao cai pra distribuicao uniforme.
            List<double> parametros = TercasSpacingCalculator.CalcularParametrosPosicao(
                config.Quantidade, config.UsarEspacamentoManual, config.EspacamentosCm, lineA.Length);

            using (Transaction t = new Transaction(doc, "Criar Terças por Plano"))
            {
                t.Start();
                // TODO(nullable): SymbolSelecionado e' FamilySymbol? mas o command/window
                // ja' valida que esta setado antes de chamar Executar. Mantido ! pra preservar
                // comportamento; caso a validacao do caller falhe, ainda NRE como antes.
                if (!config.SymbolSelecionado!.IsActive)
                    config.SymbolSelecionado.Activate();
                doc.Regenerate();

                foreach (double par in parametros)
                {
                    XYZ ptA = lineA.Evaluate(par, true);
                    XYZ ptB = lineB.Evaluate(par, true);
                    XYZ dirSpan = RevitUtils.SafeNormalize(ptB - ptA);
                    if (RevitUtils.IsZeroVector(dirSpan))
                        continue;
                    XYZ start = ptA - dirSpan * beiralIniFt;
                    XYZ end = ptB + dirSpan * beiralFimFt;
                    if (System.Math.Abs(offsetFt) > RevitUtils.EPS)
                    {
                        XYZ n = plane.Normal;
                        start = start + n * offsetFt;
                        end = end + n * offsetFt;
                    }
                    if (start.DistanceTo(end) < RevitUtils.EPS)
                        continue;
                    Line eixoTerca = Line.CreateBound(start, end);
                    CreateTercaSegments(doc, eixoTerca, plane, curvasBanzos, config.DividirNosBanzos, config.SymbolSelecionado, nivel, config.ZJustificationValue, config.YJustificationValue, rotacaoRad);
                }
                t.Commit();
            }
            _ui.Info(Titulo, "Tercas criadas por plano com sucesso.", "Lancamento concluido");
            return Result.Succeeded;
        }


        private void CreateTercaSegments(
            Document doc,
            Line eixoTerca,
            Plane plane,
            List<Curve> curvasBanzos,
            bool dividirNosBanzos,
            FamilySymbol perfil,
            Level nivel,
            int zJustificationValue,
            int yJustificationValue,
            double rotacaoRad)
        {
            if (eixoTerca == null || perfil == null || nivel == null)
                return;

            List<XYZ> nodes = new List<XYZ>();
            nodes.Add(eixoTerca.GetEndPoint(0));

            if (dividirNosBanzos && curvasBanzos != null && curvasBanzos.Count > 0)
            {
                List<XYZ> cuts = RevitUtils.GetCutPointsOnTerca(eixoTerca, plane, curvasBanzos);
                nodes.AddRange(cuts);
            }

            nodes.Add(eixoTerca.GetEndPoint(1));

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                XYZ a = nodes[i];
                XYZ b = nodes[i + 1];

                if (a.DistanceTo(b) < RevitUtils.EPS)
                    continue;

                Line seg = Line.CreateBound(a, b);
                FamilyInstance fi = doc.Create.NewFamilyInstance(seg, perfil, nivel, StructuralType.Beam);

                if (fi != null)
                {
                    // v2.8.11 (Onda 3): alem do Z (inferior, default da janela), aplicar a
                    // justificacao lateral Y (esquerda, default 0) — antes a terça ficava
                    // centralizada lateralmente por nunca setar Y_JUSTIFICATION.
                    RevitUtils.SetZJustification(fi, zJustificationValue);
                    RevitUtils.SetYJustification(fi, yJustificationValue);
                    RevitUtils.SetYZOffsets(fi, 0.0, 0.0);
                    RevitUtils.SetSectionRotation(fi, rotacaoRad);
                }
            }
        }
    }
}
