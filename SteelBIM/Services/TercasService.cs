using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System.Collections.Generic;

namespace FerramentaEMT.Services
{
    public class TercasService
    {
        public Result Executar(UIDocument uidoc, Document doc, TercasConfig config, Plane plane)
        {
            if (plane == null)
            {
                AppDialogService.ShowError("Gerar Terças por Plano", "Nao foi possivel obter um plano valido para gerar as tercas.");
                return Result.Failed;
            }
            // Guard: config.Quantidade >= 1 (evita div/0 em step = 1.0/(Quantidade+1))
            if (config == null || config.Quantidade < 1)
            {
                AppDialogService.ShowError(
                    "Gerar Terças por Plano",
                    "A quantidade de terças intermediárias precisa ser pelo menos 1.",
                    "Configuração inválida");
                return Result.Failed;
            }

            Element elLimA, elLimB;
            Line lineAraw, lineBraw;
            if (!RevitUtils.TryGetLineFromPickedElement(uidoc, "Selecione a LINHA LIMITE INICIAL", out elLimA, out lineAraw))
            {
                AppDialogService.ShowError("Gerar Terças por Plano", "Nao foi possivel obter a linha limite inicial.");
                return Result.Failed;
            }
            if (!RevitUtils.TryGetLineFromPickedElement(uidoc, "Selecione a LINHA LIMITE FINAL", out elLimB, out lineBraw))
            {
                AppDialogService.ShowError("Gerar Terças por Plano", "Nao foi possivel obter a linha limite final.");
                return Result.Failed;
            }

            Line lineA = RevitUtils.ProjectLineOntoPlane(lineAraw, plane);
            Line lineB = RevitUtils.ProjectLineOntoPlane(lineBraw, plane);
            if (lineA == null || lineB == null)
            {
                AppDialogService.ShowError("Gerar Terças por Plano", "Uma das linhas limite nao pode ser projetada corretamente no plano.");
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
                IList<Reference> refsBanzos = null;
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
                    AppDialogService.ShowWarning("Gerar Terças por Plano", "Nenhum banzo foi selecionado para divisao.", "Selecao vazia");
                    return Result.Failed;
                }
                foreach (Reference r in refsBanzos)
                {
                    Element el = doc.GetElement(r);
                    Curve c = RevitUtils.GetElementCurve(el);
                    if (c != null) curvasBanzos.Add(c);
                }
                if (curvasBanzos.Count == 0)
                {
                    AppDialogService.ShowError("Gerar Terças por Plano", "Nao foi possivel obter curvas validas dos banzos selecionados.");
                    return Result.Failed;
                }
            }

            Level nivel = RevitUtils.GetElementLevel(doc, elLimA);
            if (nivel == null)
            {
                AppDialogService.ShowError("Gerar Terças por Plano", "Nao foi possivel determinar o nivel de referencia.");
                return Result.Failed;
            }

            double beiralIniFt = config.BeiralInicialCm * RevitUtils.FT_PER_CM;
            double beiralFimFt = config.BeiralFinalCm * RevitUtils.FT_PER_CM;
            double offsetFt = config.OffsetMm * RevitUtils.FT_PER_MM;
            double rotacaoRad = RevitUtils.DegToRad(config.RotacaoSecaoGraus);

            using (Transaction t = new Transaction(doc, "Criar Terças por Plano"))
            {
                t.Start();
                if (!config.SymbolSelecionado.IsActive) config.SymbolSelecionado.Activate();
                doc.Regenerate();

                double step = 1.0 / (config.Quantidade + 1);
                for (int i = 1; i <= config.Quantidade; i++)
                {
                    double par = step * i;
                    XYZ ptA = lineA.Evaluate(par, true);
                    XYZ ptB = lineB.Evaluate(par, true);
                    XYZ dirSpan = RevitUtils.SafeNormalize(ptB - ptA);
                    if (RevitUtils.IsZeroVector(dirSpan)) continue;
                    XYZ start = ptA - dirSpan * beiralIniFt;
                    XYZ end = ptB + dirSpan * beiralFimFt;
                    if (System.Math.Abs(offsetFt) > RevitUtils.EPS)
                    {
                        XYZ n = plane.Normal;
                        start = start + n * offsetFt;
                        end = end + n * offsetFt;
                    }
                    if (start.DistanceTo(end) < RevitUtils.EPS) continue;
                    Line eixoTerca = Line.CreateBound(start, end);
                    CreateTercaSegments(doc, eixoTerca, plane, curvasBanzos, config.DividirNosBanzos, config.SymbolSelecionado, nivel, config.ZJustificationValue, rotacaoRad);
                }
                t.Commit();
            }
            AppDialogService.ShowInfo("Gerar Terças por Plano", "Tercas criadas por plano com sucesso.", "Lancamento concluido");
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
            double rotacaoRad)
        {
            if (eixoTerca == null || perfil == null || nivel == null) return;

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
                    RevitUtils.SetZJustification(fi, zJustificationValue);
                    RevitUtils.SetYZOffsets(fi, 0.0, 0.0);
                    RevitUtils.SetSectionRotation(fi, rotacaoRad);
                }
            }
        }
    }
}
