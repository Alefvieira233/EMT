using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    public class TrelicaService
    {
        public void Executar(UIDocument uidoc, Document doc, TrelicaConfig config)
        {
            // Guard: config.Quantidade >= 0 (se -1, 1.0/(Quantidade+1) = div/0; se 0, sem subdivisao — sem sentido)
            if (config == null || config.Quantidade < 1)
            {
                AppDialogService.ShowError(
                    "Treliça",
                    "A quantidade de subdivisões precisa ser pelo menos 1.",
                    "Configuração inválida");
                return;
            }
            double zOffsetFt = config.ZOffsetMm * RevitUtils.FT_PER_MM;
            IList<Reference> refs = null;
            try
            {
                refs = uidoc.Selection.PickObjects(ObjectType.Element, "Selecione TODAS as TERÇAS em ordem");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Trelica] Erro inesperado na selecao de tercas");
                return;
            }

            if (refs == null || refs.Count < 2)
                return;

            using (Transaction t = new Transaction(doc, "Criar Treliça"))
            {
                t.Start();

                if (config.LancarMontante && config.SymbolMontante != null && !config.SymbolMontante.IsActive)
                    config.SymbolMontante.Activate();
                if (config.LancarDiagonal && config.SymbolDiagonal != null && !config.SymbolDiagonal.IsActive)
                    config.SymbolDiagonal.Activate();

                doc.Regenerate();

                Level nivel = RevitUtils.GetElementLevel(doc, doc.GetElement(refs[0]));
                int totalVaos = refs.Count - 1;
                double step = 1.0 / (config.Quantidade + 1);

                for (int i = 0; i < totalVaos; i++)
                {
                    Element elA = doc.GetElement(refs[i]);
                    Element elB = doc.GetElement(refs[i + 1]);

                    Curve cA = RevitUtils.GetElementCurve(elA);
                    Curve cB = RevitUtils.GetElementCurve(elB);

                    if (cA == null || cB == null)
                        continue;

                    List<XYZ> ptsA = new List<XYZ> { cA.GetEndPoint(0) };
                    List<XYZ> ptsB = new List<XYZ> { cB.GetEndPoint(0) };

                    for (int k = 1; k <= config.Quantidade; k++)
                    {
                        double p = step * k;
                        XYZ ptA = cA.Evaluate(p, true);
                        XYZ ptB = cB.Evaluate(p, true);

                        ptsA.Add(ptA);
                        ptsB.Add(ptB);

                        if (config.LancarMontante && config.SymbolMontante != null)
                            CriarMembro(doc, nivel, config.SymbolMontante, ptA, ptB, config.ZJustificationValue, zOffsetFt, config.InverterSentido);
                    }

                    ptsA.Add(cA.GetEndPoint(1));
                    ptsB.Add(cB.GetEndPoint(1));

                    if (!config.LancarDiagonal || config.SymbolDiagonal == null)
                        continue;

                    for (int k = 0; k < ptsA.Count - 1; k++)
                    {
                        bool diagonalPadrao = (k % 2 == 0);
                        if (config.InverterSentido)
                            diagonalPadrao = !diagonalPadrao;

                        Line line = diagonalPadrao
                            ? CriarLinhaComSentido(ptsA[k], ptsB[k + 1], config.InverterSentido)
                            : CriarLinhaComSentido(ptsB[k], ptsA[k + 1], config.InverterSentido);

                        if (line == null)
                            continue;

                        FamilyInstance fi = doc.Create.NewFamilyInstance(line, config.SymbolDiagonal, nivel, StructuralType.Beam);
                        if (fi == null)
                            continue;

                        RevitUtils.SetZJustification(fi, config.ZJustificationValue);
                        RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
                        RevitUtils.DisallowJoins(fi);
                    }
                }

                t.Commit();
            }

            AppDialogService.ShowInfo("Treliça", "Treliça criada com sucesso.", "Modelagem concluída");
        }

        private void CriarMembro(
            Document doc,
            Level nivel,
            FamilySymbol symbol,
            XYZ inicio,
            XYZ fim,
            int zJustificationValue,
            double zOffsetFt,
            bool inverterSentido)
        {
            Line line = CriarLinhaComSentido(inicio, fim, inverterSentido);
            if (line == null)
                return;

            FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
            if (fi == null)
                return;

            RevitUtils.SetZJustification(fi, zJustificationValue);
            RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
            RevitUtils.DisallowJoins(fi);
        }

        private Line CriarLinhaComSentido(XYZ inicio, XYZ fim, bool inverterSentido)
        {
            if (inicio == null || fim == null || inicio.DistanceTo(fim) < RevitUtils.EPS)
                return null;

            return inverterSentido
                ? Line.CreateBound(fim, inicio)
                : Line.CreateBound(inicio, fim);
        }
    }
}
