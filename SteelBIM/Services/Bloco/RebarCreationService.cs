#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using SteelBIM.Models.Bloco;
using SteelBIM.Services.PF;

namespace SteelBIM.Services.Bloco
{
    internal static class RebarCreationService
    {
        private const double MinSegFt = 0.04;  // ~12 mm

        // -------------------------------------------------------------------
        // Barras abertas (barras longitudinais)
        // -------------------------------------------------------------------

        public static Rebar CreateOpenBar(
            Document doc, Element host, RebarBarType barType,
            XYZ normal, IList<Curve> curves, string label)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc, RebarStyle.Standard, barType,
                null, null, host, normal, curves,
                RebarHookOrientation.Right, RebarHookOrientation.Right,
                true, true);

            if (rebar == null)
                throw new InvalidOperationException("Revit nao criou a barra.");

            Annotate(rebar, label);
            return rebar;
        }

        // -------------------------------------------------------------------
        // Estribos fechados
        // -------------------------------------------------------------------

        public static Rebar CreateClosedStirrup(
            Document doc, Element host, RebarBarType barType,
            XYZ normal, IList<Curve> closedCurves, RebarHookType? hookType, string label)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc, RebarStyle.StirrupTie, barType,
                hookType, null, host, normal, closedCurves,
                RebarHookOrientation.Right, RebarHookOrientation.Right,
                true, true);

            if (rebar == null)
                throw new InvalidOperationException("Revit nao criou o estribo.");

            Annotate(rebar, label);
            return rebar;
        }

        // -------------------------------------------------------------------
        // Geometria de barra reta com dobras geometricas nas pontas
        // -------------------------------------------------------------------

        public static IList<Curve> BuildBarWithBends(
            BlocoHostFrame frame,
            XYZ p0Local, XYZ p1Local,
            BlocoRebarBendConfig dobra)
        {
            double dobraI = dobra.HaDobraInicial ? ToCm(dobra.ComprimentoDobraInicialCm) : 0.0;
            double dobraF = dobra.HaDobraFinal ? ToCm(dobra.ComprimentoDobraFinalCm) : 0.0;
            double zSignI = dobra.DobraInicialParaCima ? 1.0 : -1.0;
            double zSignF = dobra.DobraFinalParaCima ? 1.0 : -1.0;

            List<XYZ> pts = new List<XYZ>();
            if (dobraI > MinSegFt)
                pts.Add(new XYZ(p0Local.X, p0Local.Y, p0Local.Z + dobraI * zSignI));
            pts.Add(p0Local);
            pts.Add(p1Local);
            if (dobraF > MinSegFt)
                pts.Add(new XYZ(p1Local.X, p1Local.Y, p1Local.Z + dobraF * zSignF));

            return BuildPolyline(frame, pts);
        }

        // -------------------------------------------------------------------
        // Geometria de retangulo fechado (estribo)
        // Passa 4 cantos em coordenadas locais — cria 4 segmentos + fechamento
        // -------------------------------------------------------------------

        public static IList<Curve> BuildRectangle(
            BlocoHostFrame frame,
            XYZ a, XYZ b, XYZ c, XYZ d)
        {
            return BuildClosedPolyline(frame, new[] { a, b, c, d });
        }

        // -------------------------------------------------------------------
        // Distribuicao de posicoes
        // -------------------------------------------------------------------

        public static List<double> CalcularPosicoes(
            double min, double max,
            BlocoModoQuantidade modo, int quantidade, double espacamentoCm)
        {
            if (max - min <= ToCm(5.0))
                return new List<double> { (min + max) / 2.0 };

            if (modo == BlocoModoQuantidade.PorQuantidade)
            {
                if (quantidade <= 0)
                    return new List<double>();
                if (quantidade == 1)
                    return new List<double> { (min + max) / 2.0 };
                double step = (max - min) / (quantidade - 1);
                return Enumerable.Range(0, quantidade).Select(i => min + i * step).ToList();
            }
            else
            {
                double spacing = ToCm(espacamentoCm);
                if (spacing <= ToCm(1.0))
                    return new List<double> { min, max };
                List<double> result = new List<double>();
                double pos = min;
                while (pos <= max + ToCm(1.0))
                {
                    result.Add(Math.Min(pos, max));
                    pos += spacing;
                }
                if (result.Count > 0 && max - result[result.Count - 1] > ToCm(5.0))
                    result.Add(max);
                return result.Distinct().ToList();
            }
        }

        // -------------------------------------------------------------------
        // Lookup de tipos
        // -------------------------------------------------------------------

        public static RebarBarType GetBarType(Document doc, string name)
        {
            RebarBarType? t = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase))
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .OrderBy(x => x.Name)
                    .FirstOrDefault();

            if (t == null)
                throw new InvalidOperationException("Nenhum tipo de vergalhao encontrado no projeto.");
            return t;
        }

        public static RebarHookType? GetHookTypeByAngle(Document doc, int angleDegrees)
        {
            // NBR 6118 secao 9.4.6.1 — v2.6.1 (hotfix P0 NBR-1):
            // filtrar por angulo E por multiplier do rabo NBR-compliant.
            // Antes (v2.6.0-): retornava hook mais proximo do angulo sem validar
            // multiplier — mesma classe de bug do v2.4.0 (hook 135 com rabo 6.O
            // em vez de 10.O). Regra extraida e testada em
            // PfStirrupHookRules.IsCompliantWithNbr.
            //
            // Comportamento de null: callers (HorizontalStirrupService,
            // VerticalStirrupService) ja tratam null como "sem hook" (RebarHookType?
            // nullable). Quando NENHUM hook compliant existe, o caller pode prosseguir
            // sem hook ou pode decidir criar (responsabilidade do caller; este
            // helper nao tem barType pra setar HookPermission).
            double rad = angleDegrees * Math.PI / 180.0;
            const double angleToleranceRad = 0.5 * Math.PI / 180.0;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .Where(h => Math.Abs(h.HookAngle - rad) <= angleToleranceRad)
                .Where(h => PfStirrupHookRules.IsCompliantWithNbr(angleDegrees, GetTailLengthMultiplier(h)))
                .OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>
        /// Le o multiplier do comprimento do rabo (tail length factor) de um
        /// RebarHookType existente. A API publica do Revit 2025 nao expoe
        /// MultiplierForTailLength como propriedade direta nem como BuiltInParameter
        /// estavel, entao usamos LookupParameter por nome (tenta EN e PT-BR
        /// — Revit localizado em portugues usa "Multiplicador do Comprimento da Cauda").
        /// Se o parametro nao for encontrado, retorna 0 — IsCompliantWithNbr
        /// vai rejeitar o hook (fail-safe; melhor recusar do que aceitar
        /// silenciosamente um multiplier desconhecido).
        /// </summary>
        private static double GetTailLengthMultiplier(RebarHookType hookType)
        {
            if (hookType == null)
                return 0.0;
            Parameter p = hookType.LookupParameter("Multiplier for Tail Length")
                ?? hookType.LookupParameter("Multiplicador do Comprimento da Cauda");
            if (p == null || p.StorageType != StorageType.Double)
                return 0.0;
            return p.AsDouble();
        }

        // -------------------------------------------------------------------
        // Privados
        // -------------------------------------------------------------------

        private static IList<Curve> BuildPolyline(BlocoHostFrame frame, IList<XYZ> localPts)
        {
            List<Curve> curves = new List<Curve>();
            for (int i = 0; i < localPts.Count - 1; i++)
            {
                XYZ p0 = frame.WorldPoint(localPts[i].X, localPts[i].Y, localPts[i].Z);
                XYZ p1 = frame.WorldPoint(localPts[i + 1].X, localPts[i + 1].Y, localPts[i + 1].Z);
                if (p0.DistanceTo(p1) > MinSegFt)
                    curves.Add(Line.CreateBound(p0, p1));
            }
            if (curves.Count == 0)
                throw new InvalidOperationException("Geometria da barra resultou em segmentos zerados.");
            return curves;
        }

        private static IList<Curve> BuildClosedPolyline(BlocoHostFrame frame, IList<XYZ> localPts)
        {
            List<XYZ> loop = localPts.ToList();
            loop.Add(loop[0]);
            return BuildPolyline(frame, loop);
        }

        private static void Annotate(Rebar rebar, string label)
        {
            Parameter? p = rebar?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                p.Set($"Bloco Fundacao - {label}");
        }

        internal static double ToCm(double cm)
            => UnitUtils.ConvertToInternalUnits(cm, UnitTypeId.Centimeters);
    }
}
