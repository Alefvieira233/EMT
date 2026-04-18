using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System;
using System.Collections.Generic;

namespace FerramentaEMT.Services
{
    public class GuardaCorpoService
    {
        public void Executar(UIDocument uidoc, Document doc, GuardaCorpoConfig config)
        {
            XYZ pontoInicial;
            XYZ pontoFinal;

            try
            {
                pontoInicial = uidoc.Selection.PickPoint("Clique o ponto INICIAL do guarda-corpo");
                pontoFinal = uidoc.Selection.PickPoint("Clique o ponto FINAL do guarda-corpo");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            if (pontoInicial.DistanceTo(pontoFinal) < RevitUtils.EPS)
            {
                AppDialogService.ShowWarning("Guarda-Corpo", "Os pontos informados são coincidentes.", "Pontos inválidos");
                return;
            }

            XYZ direcao = RevitUtils.SafeNormalize(pontoFinal - pontoInicial);
            XYZ vertical = XYZ.BasisZ;
            XYZ direcaoHorizontal = new XYZ(direcao.X, direcao.Y, 0.0);
            direcaoHorizontal = RevitUtils.SafeNormalize(direcaoHorizontal);
            XYZ lateral = vertical.CrossProduct(direcaoHorizontal);
            lateral = RevitUtils.SafeNormalize(lateral);

            if (RevitUtils.IsZeroVector(lateral))
            {
                AppDialogService.ShowWarning("Guarda-Corpo", "Não foi possível determinar o offset lateral para o trecho selecionado.", "Direção inválida");
                return;
            }

            double alturaFt = config.AlturaCorrimaoCm * RevitUtils.FT_PER_CM;
            double offsetLateralFt = config.OffsetLateralCm * RevitUtils.FT_PER_CM;
            double espacamentoFt = config.EspacamentoMaximoPostesCm * RevitUtils.FT_PER_CM;

            XYZ deslocamento = lateral.Multiply(offsetLateralFt);
            XYZ elevacaoPerfil = vertical.Multiply(alturaFt);
            XYZ elevacaoPoste = vertical.Multiply(alturaFt);

            XYZ inicioBase = pontoInicial + deslocamento;
            XYZ fimBase = pontoFinal + deslocamento;
            XYZ inicioCorrimao = inicioBase + elevacaoPerfil;
            XYZ fimCorrimao = fimBase + elevacaoPerfil;

            int postesCriados = 0;
            List<FamilyInstance> instanciasCriadas = new List<FamilyInstance>();

            using (Transaction t = new Transaction(doc, "Lançar Guarda-Corpo"))
            {
                t.Start();

                if (!config.SymbolSelecionado.IsActive)
                {
                    config.SymbolSelecionado.Activate();
                    doc.Regenerate();
                }

                Line linhaCorrimao = Line.CreateBound(inicioCorrimao, fimCorrimao);
                FamilyInstance corrimao = doc.Create.NewFamilyInstance(
                    linhaCorrimao,
                    config.SymbolSelecionado,
                    config.NivelReferencia,
                    StructuralType.Beam);

                ConfigurarInstancia(corrimao, config);
                instanciasCriadas.Add(corrimao);

                List<XYZ> basesPostes = CalcularBasesDosPostes(inicioBase, fimBase, espacamentoFt);

                if (config.CriarTravessasIntermediarias)
                {
                    foreach (double alturaTravessaFt in CalcularAlturasTravessas(alturaFt, config.QuantidadeTravessasIntermediarias))
                    {
                        for (int i = 0; i < basesPostes.Count - 1; i++)
                        {
                            XYZ inicioTravessa = basesPostes[i] + vertical.Multiply(alturaTravessaFt);
                            XYZ fimTravessa = basesPostes[i + 1] + vertical.Multiply(alturaTravessaFt);

                            if (inicioTravessa.DistanceTo(fimTravessa) < RevitUtils.EPS)
                                continue;

                            FamilyInstance travessa = doc.Create.NewFamilyInstance(
                                Line.CreateBound(inicioTravessa, fimTravessa),
                                config.SymbolSelecionado,
                                config.NivelReferencia,
                                StructuralType.Beam);

                            ConfigurarInstancia(travessa, config);
                            instanciasCriadas.Add(travessa);
                        }
                    }
                }

                if (config.CriarPostes)
                {
                    foreach (XYZ basePoste in basesPostes)
                    {
                        XYZ topoPoste = basePoste + elevacaoPoste;
                        if (basePoste.DistanceTo(topoPoste) < RevitUtils.EPS)
                            continue;

                        FamilyInstance poste = doc.Create.NewFamilyInstance(
                            Line.CreateBound(basePoste, topoPoste),
                            config.SymbolSelecionado,
                            config.NivelReferencia,
                            StructuralType.Beam);

                        ConfigurarInstancia(poste, config);
                        instanciasCriadas.Add(poste);
                        postesCriados++;
                    }
                }

                t.Commit();
            }

            uidoc.Selection.SetElementIds(new List<ElementId>(instanciasCriadas.ConvertAll(i => i.Id)));

            AppDialogService.ShowInfo(
                "Guarda-Corpo",
                "Guarda-corpo criado com sucesso.\n\n" +
                "Tipo: " + config.SymbolSelecionado.FamilyName + " : " + config.SymbolSelecionado.Name + "\n" +
                "Altura do corrimão: " + config.AlturaCorrimaoCm + " cm\n" +
                "Travessas intermediárias: " + (config.CriarTravessasIntermediarias ? config.QuantidadeTravessasIntermediarias + " unidade(s)" : "não criar") + "\n" +
                "Offset lateral: " + config.OffsetLateralCm + " cm\n" +
                "Postes criados: " + postesCriados,
                "Guarda-corpo criado");
        }

        private void ConfigurarInstancia(FamilyInstance instancia, GuardaCorpoConfig config)
        {
            if (instancia == null)
                return;

            RevitUtils.SetZJustification(instancia, config.ZJustificationValue);

            if (!config.UnirGeometrias)
                RevitUtils.DisallowJoins(instancia);
        }

        private List<XYZ> CalcularBasesDosPostes(XYZ inicio, XYZ fim, double espacamentoMaximo)
        {
            List<XYZ> bases = new List<XYZ>();
            double comprimento = inicio.DistanceTo(fim);

            if (comprimento < RevitUtils.EPS)
            {
                bases.Add(inicio);
                return bases;
            }

            int segmentos = espacamentoMaximo > RevitUtils.EPS
                ? Math.Max(1, (int)Math.Ceiling(comprimento / espacamentoMaximo))
                : 1;
            XYZ direcao = RevitUtils.SafeNormalize(fim - inicio);

            for (int i = 0; i <= segmentos; i++)
            {
                double distancia = comprimento * i / segmentos;
                bases.Add(inicio + direcao.Multiply(distancia));
            }

            return bases;
        }

        private List<double> CalcularAlturasTravessas(double alturaTotal, int quantidadeTravessas)
        {
            List<double> alturas = new List<double>();
            if (alturaTotal <= RevitUtils.EPS || quantidadeTravessas <= 0)
                return alturas;

            double passo = alturaTotal / (quantidadeTravessas + 1);

            for (int i = 1; i <= quantidadeTravessas; i++)
                alturas.Add(passo * i);

            return alturas;
        }
    }
}
