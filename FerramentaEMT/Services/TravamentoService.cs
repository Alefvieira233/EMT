using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System.Collections.Generic;

namespace FerramentaEMT.Services
{
    public class TravamentoService
    {
        public void Executar(UIDocument uidoc, Document doc, TravamentoConfig config)
        {
            // Guard: config.Quantidade >= 1 (evita div/0 em step = 1.0/(Quantidade+1))
            if (config == null || config.Quantidade < 1)
            {
                AppDialogService.ShowError(
                    "Travamento",
                    "A quantidade de travamentos precisa ser pelo menos 1.",
                    "Configuração inválida");
                return;
            }
            double zOffsetFt = config.ZOffsetMm * RevitUtils.FT_PER_MM;
            IList<Reference> refs = null;
            try
            {
                refs = uidoc.Selection.PickObjects(ObjectType.Element, "Selecione TODAS as TERÇAS em ordem");
            }
            catch
            {
                return;
            }

            if (refs == null || refs.Count < 2)
                return;

            using (Transaction t = new Transaction(doc, "Criar Travamentos"))
            {
                t.Start();

                if (config.LancarTirante && config.SymbolTirante != null && !config.SymbolTirante.IsActive)
                    config.SymbolTirante.Activate();
                if (config.LancarFrechal && config.SymbolFrechal != null && !config.SymbolFrechal.IsActive)
                    config.SymbolFrechal.Activate();

                doc.Regenerate();

                Level nivel = RevitUtils.GetElementLevel(doc, doc.GetElement(refs[0]));
                int totalVaos = refs.Count - 1;
                bool isOdd = (config.Quantidade % 2 != 0);
                int midIndex = (config.Quantidade + 1) / 2;

                for (int i = 0; i < totalVaos; i++)
                {
                    Element elA = doc.GetElement(refs[i]);
                    Element elB = doc.GetElement(refs[i + 1]);

                    Curve cA = RevitUtils.GetElementCurve(elA);
                    Curve cB = RevitUtils.GetElementCurve(elB);

                    if (cA == null || cB == null) continue;

                    double step = 1.0 / (config.Quantidade + 1);
                    bool isPonta = (i == 0 || i == totalVaos - 1);

                    List<XYZ> ptsA = new List<XYZ>();
                    List<XYZ> ptsB = new List<XYZ>();

                    ptsA.Add(cA.GetEndPoint(0));
                    ptsB.Add(cB.GetEndPoint(0));

                    for (int k = 1; k <= config.Quantidade; k++)
                    {
                        double p = step * k;
                        XYZ ptA = cA.Evaluate(p, true);
                        XYZ ptB = cB.Evaluate(p, true);

                        ptsA.Add(ptA);
                        ptsB.Add(ptB);

                        bool skipTirante = !config.LancarTirante || (config.Quantidade >= 3 && isOdd && k == midIndex);

                        if (!skipTirante && config.SymbolTirante != null)
                        {
                            Line line = CriarLinhaComSentido(ptA, ptB, config.InverterSentido);
                            FamilyInstance fi = doc.Create.NewFamilyInstance(line, config.SymbolTirante, nivel, StructuralType.Beam);
                            if (fi != null)
                            {
                                RevitUtils.SetZJustification(fi, config.ZJustificationValue);
                                RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
                                RevitUtils.DisallowJoins(fi);
                            }
                        }
                    }

                    ptsA.Add(cA.GetEndPoint(1));
                    ptsB.Add(cB.GetEndPoint(1));

                    if (isPonta && config.LancarFrechal && config.SymbolFrechal != null)
                    {
                        for (int k = 0; k < ptsA.Count - 1; k++)
                        {
                            Line line = null;
                            bool diagonalPadrao = (k % 2 == 0);
                            if (config.InverterSentido)
                                diagonalPadrao = !diagonalPadrao;

                            if (diagonalPadrao)
                                line = CriarLinhaComSentido(ptsA[k], ptsB[k + 1], config.InverterSentido);
                            else
                                line = CriarLinhaComSentido(ptsB[k], ptsA[k + 1], config.InverterSentido);

                            if (line != null)
                            {
                                FamilyInstance fi = doc.Create.NewFamilyInstance(line, config.SymbolFrechal, nivel, StructuralType.Beam);
                                if (fi != null)
                                {
                                    RevitUtils.SetZJustification(fi, config.ZJustificationValue);
                                    RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
                                    RevitUtils.DisallowJoins(fi);
                                }
                            }
                        }
                    }
                }

                t.Commit();
            }

            AppDialogService.ShowInfo("Travamentos", "Travamentos criados com sucesso.", "Modelagem concluída");
        }

        private Line CriarLinhaComSentido(XYZ inicio, XYZ fim, bool inverterSentido)
        {
            return inverterSentido
                ? Line.CreateBound(fim, inicio)
                : Line.CreateBound(inicio, fim);
        }
    }
}
