#nullable enable
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Services.Trelica;
using SteelBIM.Utils;
using SteelBIM.Views.Helpers;

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.11 (Onda 2): geracao de treliça com padroes de treliçado (Pratt/Howe/Warren/X/...),
    /// espacamentos variaveis e dois modos:
    ///   - "preencher entre banzos" (selecionar 2+ curvas/terças) — IncluirBanzos=false;
    ///   - "treliça completa" (selecionar 1 linha-base + altura) — gera tambem os banzos.
    /// A topologia (quais membros) e decidida pelo helper PURO <see cref="TrelicaPatternBuilder"/>;
    /// as estacoes ao longo do vao por <see cref="TercasSpacingCalculator"/>.
    /// </summary>
    public class TrelicaService
    {
        public void Executar(UIDocument uidoc, Document doc, TrelicaConfig config)
        {
            if (config == null || config.Quantidade < 1)
            {
                AppDialogService.ShowError(
                    "Treliça",
                    "A quantidade de subdivisões precisa ser pelo menos 1.",
                    "Configuração inválida");
                return;
            }

            if (config.TrelicaCompleta)
            {
                ExecutarTrelicaCompleta(uidoc, doc, config);
                return;
            }

            ExecutarEntreBanzos(uidoc, doc, config);
        }

        // ---- Modo A: preencher o miolo entre 2+ banzos selecionados ----
        private void ExecutarEntreBanzos(UIDocument uidoc, Document doc, TrelicaConfig config)
        {
            double zOffsetFt = config.ZOffsetMm * RevitUtils.FT_PER_MM;
            IList<Reference>? refs;
            try
            {
                refs = uidoc.Selection.PickObjects(ObjectType.Element, "Selecione TODAS as TERÇAS/BANZOS em ordem");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Trelica] Erro inesperado na selecao de banzos");
                return;
            }

            if (refs == null || refs.Count < 2)
                return;

            using (Transaction t = new Transaction(doc, "Criar Treliça"))
            {
                t.Start();
                AtivarSimbolos(config);
                doc.Regenerate();

                Level nivel = RevitUtils.GetElementLevel(doc, doc.GetElement(refs[0]));
                int totalVaos = refs.Count - 1;

                for (int i = 0; i < totalVaos; i++)
                {
                    Curve cA = RevitUtils.GetElementCurve(doc.GetElement(refs[i]));
                    Curve cB = RevitUtils.GetElementCurve(doc.GetElement(refs[i + 1]));
                    if (cA == null || cB == null)
                        continue;

                    // Banzo superior = o de maior Z medio; inferior = o outro.
                    double zA = (cA.GetEndPoint(0).Z + cA.GetEndPoint(1).Z) / 2.0;
                    double zB = (cB.GetEndPoint(0).Z + cB.GetEndPoint(1).Z) / 2.0;
                    Curve cSup = zA >= zB ? cA : cB;
                    Curve cInf = zA >= zB ? cB : cA;

                    GerarVao(doc, nivel, cSup, cInf, incluirBanzos: false, config, zOffsetFt);
                }

                t.Commit();
            }

            AppDialogService.ShowInfo("Treliça", "Treliça criada com sucesso.", "Modelagem concluída");
        }

        // ---- Modo B: treliça completa a partir de 1 linha-base (banzo inferior) + altura ----
        private void ExecutarTrelicaCompleta(UIDocument uidoc, Document doc, TrelicaConfig config)
        {
            if (config.SymbolBanzo == null)
            {
                AppDialogService.ShowError("Treliça", "Selecione o perfil dos banzos para a treliça completa.", "Configuração inválida");
                return;
            }
            if (config.AlturaMm <= 0)
            {
                AppDialogService.ShowError("Treliça", "Informe uma altura de treliça maior que zero.", "Configuração inválida");
                return;
            }

            double zOffsetFt = config.ZOffsetMm * RevitUtils.FT_PER_MM;
            double alturaFt = config.AlturaMm * RevitUtils.FT_PER_MM;

            Reference rBase;
            try
            {
                rBase = uidoc.Selection.PickObject(ObjectType.Element, "Selecione a LINHA do banzo inferior (eixo da treliça)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Trelica] Erro inesperado na selecao da linha-base");
                return;
            }

            if (rBase == null)
                return;

            Element elBase = doc.GetElement(rBase);
            Curve cInf = RevitUtils.GetElementCurve(elBase);
            if (cInf == null)
            {
                AppDialogService.ShowError("Treliça", "Nao foi possivel ler a curva da linha-base selecionada.", "Geometria invalida");
                return;
            }

            Curve cSup = cInf.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, alturaFt)));

            using (Transaction t = new Transaction(doc, "Criar Treliça completa"))
            {
                t.Start();
                AtivarSimbolos(config);
                doc.Regenerate();

                Level nivel = RevitUtils.GetElementLevel(doc, elBase);
                GerarVao(doc, nivel, cSup, cInf, incluirBanzos: true, config, zOffsetFt);

                t.Commit();
            }

            AppDialogService.ShowInfo("Treliça", "Treliça completa criada com sucesso.", "Modelagem concluída");
        }

        // ---- Geracao de um vao (par de banzos), comum aos dois modos ----
        private void GerarVao(Document doc, Level nivel, Curve cSup, Curve cInf, bool incluirBanzos, TrelicaConfig config, double zOffsetFt)
        {
            double lenFt = cInf.Length;
            List<double> intermed = CalcularParametros(config, lenFt);

            List<double> full = new List<double> { 0.0 };
            full.AddRange(intermed);
            full.Add(1.0);
            int nEstacoes = full.Count;

            XYZ[] ptsSup = new XYZ[nEstacoes];
            XYZ[] ptsInf = new XYZ[nEstacoes];
            for (int k = 0; k < nEstacoes; k++)
            {
                ptsSup[k] = cSup.Evaluate(full[k], true);
                ptsInf[k] = cInf.Evaluate(full[k], true);
            }

            TrussBuildOptions opcoes = new TrussBuildOptions
            {
                Padrao = config.Padrao,
                IncluirBanzos = incluirBanzos,
                MontantesIntermediarios = config.MontantesIntermediarios,
                MontantesExtremidade = config.MontantesExtremidade,
                DiagonaisExtremidade = config.DiagonaisExtremidade,
                Espelhar = config.InverterSentido
            };

            List<TrussSegment> segmentos = TrelicaPatternBuilder.Construir(nEstacoes, opcoes);

            foreach (TrussSegment seg in segmentos)
            {
                if (seg.Tipo == TrussMemberKind.Montante && !config.LancarMontante)
                    continue;
                if (seg.Tipo == TrussMemberKind.Diagonal && !config.LancarDiagonal)
                    continue;

                FamilySymbol? sym = seg.Tipo switch
                {
                    TrussMemberKind.Banzo => config.SymbolBanzo,
                    TrussMemberKind.Montante => config.SymbolMontante,
                    TrussMemberKind.Diagonal => config.SymbolDiagonal,
                    _ => null
                };
                if (sym == null)
                    continue;

                XYZ p1 = PontoDe(seg.De, ptsSup, ptsInf);
                XYZ p2 = PontoDe(seg.Para, ptsSup, ptsInf);
                CriarMembro(doc, nivel, sym, p1, p2, config.ZJustificationValue, zOffsetFt);
            }
        }

        private static XYZ PontoDe(TrussNode no, XYZ[] sup, XYZ[] inf)
        {
            XYZ[] arr = no.Chord == TrussChord.Superior ? sup : inf;
            int idx = Math.Max(0, Math.Min(arr.Length - 1, no.Estacao));
            return arr[idx];
        }

        // Estacoes intermediarias (parametros 0..1) conforme o modo de espacamento.
        private List<double> CalcularParametros(TrelicaConfig config, double lenFt)
        {
            if (config.ModoEspacamento == TrussSpacingMode.ListaEspacamentos)
            {
                int qtd = Math.Max(1, config.EspacamentosCm.Count - 1);
                return TercasSpacingCalculator.CalcularParametrosPosicao(qtd, true, config.EspacamentosCm, lenFt);
            }

            if (config.ModoEspacamento == TrussSpacingMode.Posicoes)
            {
                List<double> ps = new List<double>();
                if (lenFt <= 0)
                    return ps;
                foreach (double posCm in config.EspacamentosCm)
                {
                    double par = (posCm * TercasSpacingCalculator.FtPerCm) / lenFt;
                    if (par > 0.0 && par < 1.0)
                        ps.Add(Math.Clamp(par, 0.0, 1.0));
                }
                // Posicoes sao absolutas e podem vir fora de ordem; ordenar para garantir
                // estacoes monotonicas (senao diagonais/montantes cruzam). Coincidentes sao
                // descartadas depois pelo guard de distancia em CriarMembro.
                ps.Sort();
                return ps;
            }

            // Uniforme (default).
            return TercasSpacingCalculator.CalcularParametrosPosicao(config.Quantidade, false, null, lenFt);
        }

        private static void AtivarSimbolos(TrelicaConfig config)
        {
            if (config.SymbolMontante != null && !config.SymbolMontante.IsActive)
                config.SymbolMontante.Activate();
            if (config.SymbolDiagonal != null && !config.SymbolDiagonal.IsActive)
                config.SymbolDiagonal.Activate();
            if (config.SymbolBanzo != null && !config.SymbolBanzo.IsActive)
                config.SymbolBanzo.Activate();
        }

        private void CriarMembro(
            Document doc,
            Level nivel,
            FamilySymbol symbol,
            XYZ inicio,
            XYZ fim,
            int zJustificationValue,
            double zOffsetFt)
        {
            if (inicio == null || fim == null || inicio.DistanceTo(fim) < RevitUtils.EPS)
                return;

            Line line = Line.CreateBound(inicio, fim);
            FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
            if (fi == null)
                return;

            RevitUtils.SetZJustification(fi, zJustificationValue);
            RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
            RevitUtils.DisallowJoins(fi);
        }
    }
}
