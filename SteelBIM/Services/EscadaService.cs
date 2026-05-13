using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System;
using System.Collections.Generic;

namespace FerramentaEMT.Services
{
    public class EscadaService
    {
        private const double StepAssemblyHeightReductionCm = 5.0;

        public void Executar(UIDocument uidoc, Document doc, EscadaConfig config)
        {
            XYZ pontoA;
            XYZ pontoB;

            try
            {
                pontoA = uidoc.Selection.PickPoint("Clique o ponto INFERIOR da escada (base)");
                pontoB = uidoc.Selection.PickPoint("Clique o ponto SUPERIOR da escada (topo)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            OrganizarPontosPorElevacao(ref pontoA, ref pontoB);

            StairValidationResult validacao = ValidarEntradas(pontoA, pontoB, config);
            if (!validacao.IsValid)
            {
                AppDialogService.ShowWarning("Escada", validacao.Message, "Não foi possível criar a escada");
                return;
            }

            if (!string.IsNullOrWhiteSpace(validacao.WarningMessage))
            {
                if (!AppDialogService.ShowConfirmation(
                    "Escada",
                    validacao.WarningMessage + "\n\nDeseja continuar mesmo assim?",
                    "Blondel fora da faixa recomendada",
                    "Continuar",
                    "Cancelar"))
                {
                    return;
                }
            }

            StairStepData stepData = CalcularDegraus(pontoA, pontoB, config);
            StairGeometryData geometry = CalcularGeometria(pontoA, pontoB, config, stepData);

            List<ElementId> elementosCriados = new List<ElementId>();
            int longarinasCriadas = 0;
            int degrausCriados = 0;

            using (Transaction t = new Transaction(doc, "Lançar Escada"))
            {
                t.Start();

                AtivarSymbolSeNecessario(config.SymbolLongarina, doc);

                if (config.CriarDegraus && config.TipoDegrau == EscadaTipoDegrau.PerfilLinear)
                    AtivarSymbolSeNecessario(config.SymbolDegrau, doc);

                FamilyInstance longarinaEsquerda = CriarViga(doc, geometry.LeftStringerStart, geometry.LeftStringerEnd, config.SymbolLongarina, config.NivelReferencia, config);
                FamilyInstance longarinaDireita = CriarViga(doc, geometry.RightStringerStart, geometry.RightStringerEnd, config.SymbolLongarina, config.NivelReferencia, config);

                ConfigurarOrientacaoLongarina(longarinaEsquerda, false);
                ConfigurarOrientacaoLongarina(longarinaDireita, true);

                if (longarinaEsquerda != null)
                {
                    elementosCriados.Add(longarinaEsquerda.Id);
                    longarinasCriadas++;
                }

                if (longarinaDireita != null)
                {
                    elementosCriados.Add(longarinaDireita.Id);
                    longarinasCriadas++;
                }

                if (config.CriarDegraus)
                {
                    for (int i = 0; i < stepData.StepCount; i++)
                    {
                        if (config.TipoDegrau == EscadaTipoDegrau.Chapa)
                        {
                            ElementId chapaId = CriarDegrauChapa(doc, geometry, stepData, config, i);
                            if (chapaId != ElementId.InvalidElementId)
                            {
                                elementosCriados.Add(chapaId);
                                degrausCriados++;
                            }
                        }
                        else
                        {
                            FamilyInstance degrau = CriarDegrauPerfilLinear(doc, geometry, stepData, config, i);
                            if (degrau != null)
                            {
                                elementosCriados.Add(degrau.Id);
                                degrausCriados++;
                            }
                        }
                    }
                }

                t.Commit();
            }

            uidoc.Selection.SetElementIds(elementosCriados);

            double espelhoCmReal = Math.Round(stepData.RiserHeightFt / RevitUtils.FT_PER_CM, 1);
            double pisoCmReal = Math.Round(stepData.ProfileTreadDepthFt / RevitUtils.FT_PER_CM, 1);
            double passoCmReal = Math.Round(stepData.RunPerStepFt / RevitUtils.FT_PER_CM, 1);
            double blondelCm = Math.Round((2.0 * stepData.RiserHeightFt + stepData.RunPerStepFt) / RevitUtils.FT_PER_CM, 1);

            AppDialogService.ShowInfo(
                "Escada",
                "Escada criada com sucesso.\n\n" +
                "Longarina: " + config.SymbolLongarina.FamilyName + " : " + config.SymbolLongarina.Name + "\n" +
                "Degrau: " + ObterDescricaoDegrau(config) + "\n\n" +
                "Lado de inserção: " + config.LadoInsercao + "\n" +
                "Largura: " + config.LarguraCm + " cm\n" +
                "Longarinas criadas: " + longarinasCriadas + "\n" +
                "Degraus criados: " + degrausCriados + "\n" +
                "Número de degraus: " + stepData.StepCount + "\n" +
                "Modo de quantificação: " + (config.QuantidadeDegraus > 0 ? "manual" : "automático") + "\n" +
                "Espelho real: " + espelhoCmReal + " cm\n" +
                "Pisada do perfil: " + pisoCmReal + " cm\n" +
                "Passo horizontal: " + passoCmReal + " cm\n" +
                "Blondel: " + blondelCm + " cm",
                "Escada criada");
        }

        private static void OrganizarPontosPorElevacao(ref XYZ pontoBase, ref XYZ pontoTopo)
        {
            if (pontoBase.Z > pontoTopo.Z)
            {
                XYZ tmp = pontoBase;
                pontoBase = pontoTopo;
                pontoTopo = tmp;
            }
        }

        private StairValidationResult ValidarEntradas(XYZ pontoBase, XYZ pontoTopo, EscadaConfig config)
        {
            if (pontoBase.DistanceTo(pontoTopo) < RevitUtils.EPS)
                return StairValidationResult.Invalid("Os pontos informados são coincidentes.");

            double totalRise = pontoTopo.Z - pontoBase.Z;
            if (totalRise < RevitUtils.EPS)
                return StairValidationResult.Invalid("O ponto superior deve estar acima do ponto inferior.");

            XYZ horizontalVec = new XYZ(pontoTopo.X - pontoBase.X, pontoTopo.Y - pontoBase.Y, 0.0);
            double totalRunH = horizontalVec.GetLength();
            if (totalRunH < RevitUtils.EPS)
                return StairValidationResult.Invalid("Os pontos possuem a mesma posição horizontal. A escada precisa de projeção horizontal.");

            if (config.SymbolLongarina == null)
                return StairValidationResult.Invalid("Selecione um perfil de longarina válido.");

            if (config.LarguraCm <= 0.0)
                return StairValidationResult.Invalid("A largura da escada deve ser maior que zero.");

            if (config.AlturaEspelhoCm <= 0.0)
                return StairValidationResult.Invalid("A altura do espelho deve ser maior que zero.");

            if (config.QuantidadeDegraus < 0)
                return StairValidationResult.Invalid("A quantidade de degraus não pode ser negativa.");

            if (config.CriarDegraus && config.TipoDegrau == EscadaTipoDegrau.PerfilLinear && config.SymbolDegrau == null)
                return StairValidationResult.Invalid("Selecione o perfil do degrau ou altere o tipo de degrau.");

            if (config.CriarDegraus && config.TipoDegrau == EscadaTipoDegrau.Chapa && config.EspessuraChapaDegrauCm <= 0.0)
                return StairValidationResult.Invalid("A espessura da chapa do degrau deve ser maior que zero.");

            double inclinacaoGraus = Math.Atan2(totalRise, totalRunH) * (180.0 / Math.PI);
            if (inclinacaoGraus > 60.0)
                return StairValidationResult.Invalid("A inclinação calculada excede 60 graus. Verifique os pontos informados.");

            StairStepData stepData = CalcularDegraus(pontoBase, pontoTopo, config);

            if (config.PisadaCm <= 0.0)
                return StairValidationResult.Invalid("A pisada deve ser maior que zero.");

            if (config.CriarDegraus && config.TipoDegrau == EscadaTipoDegrau.PerfilLinear)
            {
                if (stepData.RunPerStepFt + RevitUtils.EPS < stepData.ProfileTreadDepthFt)
                {
                    return StairValidationResult.Invalid(
                        "A pisada configurada é maior que o passo horizontal disponível.\n" +
                        "Ajuste a altura do espelho ou os pontos da escada.");
                }

                double blondelCm = (2.0 * stepData.RiserHeightFt + stepData.RunPerStepFt) / RevitUtils.FT_PER_CM;
                if (blondelCm < 63.0 || blondelCm > 65.0)
                {
                    return StairValidationResult.ValidWithWarning(
                        "A combinação entre espelho e pisada não atende Blondel.\n" +
                        "Valor calculado: " + Math.Round(blondelCm, 1) + " cm.\n" +
                        "Faixa recomendada: 63 a 65 cm.");
                }
            }

            return StairValidationResult.Valid();
        }

        private StairStepData CalcularDegraus(XYZ pontoBase, XYZ pontoTopo, EscadaConfig config)
        {
            double totalRise = pontoTopo.Z - pontoBase.Z;
            double totalRun = new XYZ(pontoTopo.X - pontoBase.X, pontoTopo.Y - pontoBase.Y, 0.0).GetLength();
            double espelhoDesejadoFt = config.AlturaEspelhoCm * RevitUtils.FT_PER_CM;
            double larguraFt = config.LarguraCm * RevitUtils.FT_PER_CM;
            double pisadaPerfilFt = config.PisadaCm * RevitUtils.FT_PER_CM;

            int nDegraus = config.QuantidadeDegraus > 0
                ? config.QuantidadeDegraus
                : Math.Max(1, (int)Math.Round(totalRise / espelhoDesejadoFt));
            double espelhoFt = totalRise / nDegraus;
            double pisoFt = totalRun / nDegraus;

            return new StairStepData(nDegraus, espelhoFt, pisoFt, totalRun, larguraFt, pisadaPerfilFt);
        }

        private StairGeometryData CalcularGeometria(XYZ pontoBase, XYZ pontoTopo, EscadaConfig config, StairStepData stepData)
        {
            XYZ horizontalVec = new XYZ(pontoTopo.X - pontoBase.X, pontoTopo.Y - pontoBase.Y, 0.0);
            XYZ dirH = RevitUtils.SafeNormalize(horizontalVec);
            XYZ leftDir = RevitUtils.SafeNormalize(XYZ.BasisZ.CrossProduct(dirH));

            double larguraFt = config.LarguraCm * RevitUtils.FT_PER_CM;
            double halfWidthFt = larguraFt / 2.0;

            double centerOffsetFt = 0.0;
            if (config.LadoInsercao == EscadaLadoInsercao.Esquerda)
                centerOffsetFt = -halfWidthFt;
            else if (config.LadoInsercao == EscadaLadoInsercao.Direita)
                centerOffsetFt = halfWidthFt;

            XYZ centerOffset = leftDir.Multiply(centerOffsetFt);
            XYZ baseCenter = pontoBase + centerOffset;
            XYZ topCenter = pontoTopo + centerOffset;
            double stepAssemblyReductionFt = StepAssemblyHeightReductionCm * RevitUtils.FT_PER_CM;
            double stringerVerticalOffsetFt = (stepData.RiserHeightFt / 2.0) - stepAssemblyReductionFt;
            XYZ stringerVerticalOffset = XYZ.BasisZ.Multiply(stringerVerticalOffsetFt);

            XYZ leftOffset = leftDir.Multiply(halfWidthFt);
            XYZ rightOffset = leftDir.Multiply(-halfWidthFt);

            return new StairGeometryData(
                baseCenter,
                topCenter,
                dirH,
                leftDir,
                baseCenter + leftOffset + stringerVerticalOffset,
                topCenter + leftOffset + stringerVerticalOffset,
                baseCenter + rightOffset + stringerVerticalOffset,
                topCenter + rightOffset + stringerVerticalOffset);
        }

        private void AtivarSymbolSeNecessario(FamilySymbol symbol, Document doc)
        {
            if (symbol == null)
                return;

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }
        }

        private FamilyInstance CriarDegrauPerfilLinear(Document doc, StairGeometryData geometry, StairStepData stepData, EscadaConfig config, int stepIndex)
        {
            XYZ leftPoint;
            XYZ rightPoint;
            CalcularLinhaCentroDegrauPerfil(stepIndex, geometry, stepData, out leftPoint, out rightPoint);
            FamilyInstance degrau = CriarViga(doc, leftPoint, rightPoint, config.SymbolDegrau, config.NivelReferencia, config);
            ConfigurarOrientacaoDegrau(degrau);
            return degrau;
        }

        private ElementId CriarDegrauChapa(Document doc, StairGeometryData geometry, StairStepData stepData, EscadaConfig config, int stepIndex)
        {
            XYZ backLeft;
            XYZ backRight;
            CalcularBordaPosteriorDegrau(stepIndex, geometry, stepData, out backLeft, out backRight);

            XYZ forward = geometry.StairAxisDirection.Multiply(stepData.ProfileTreadDepthFt);
            XYZ frontLeft = backLeft + forward;
            XYZ frontRight = backRight + forward;
            XYZ up = XYZ.BasisZ.Multiply(config.EspessuraChapaDegrauCm * RevitUtils.FT_PER_CM);

            IList<GeometryObject> shape = CriarParalelepipedo(backLeft, backRight, frontRight, frontLeft, up);
            if (shape.Count == 0)
                return ElementId.InvalidElementId;

            DirectShape directShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            directShape.ApplicationId = "FerramentaEMT";
            directShape.ApplicationDataId = "EscadaChapa";
            directShape.SetShape(shape);
            return directShape.Id;
        }

        private static IList<GeometryObject> CriarParalelepipedo(XYZ p0, XYZ p1, XYZ p2, XYZ p3, XYZ up)
        {
            TessellatedShapeBuilder builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(true);

            XYZ p4 = p0 + up;
            XYZ p5 = p1 + up;
            XYZ p6 = p2 + up;
            XYZ p7 = p3 + up;

            builder.AddFace(new TessellatedFace(new List<XYZ> { p0, p1, p2, p3 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new List<XYZ> { p4, p7, p6, p5 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new List<XYZ> { p0, p4, p5, p1 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new List<XYZ> { p1, p5, p6, p2 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new List<XYZ> { p2, p6, p7, p3 }, ElementId.InvalidElementId));
            builder.AddFace(new TessellatedFace(new List<XYZ> { p3, p7, p4, p0 }, ElementId.InvalidElementId));

            builder.CloseConnectedFaceSet();
            builder.Target = TessellatedShapeBuilderTarget.Solid;
            builder.Fallback = TessellatedShapeBuilderFallback.Abort;
            builder.Build();

            TessellatedShapeBuilderResult result = builder.GetBuildResult();
            return result != null ? result.GetGeometricalObjects() : new List<GeometryObject>();
        }

        private static void CalcularLinhaCentroDegrauPerfil(int stepIndex, StairGeometryData geometry, StairStepData stepData, out XYZ leftPoint, out XYZ rightPoint)
        {
            double stepAssemblyReductionFt = StepAssemblyHeightReductionCm * RevitUtils.FT_PER_CM;

            // O perfil linear deve começar no espelho do degrau, como a chapa.
            // Por isso usamos a metade da profundidade real do perfil, e não metade do passo.
            // Altura: topo do espelho i = (i + 1) × espelho
            // Com Z_JUSTIFICATION = Top, a linha de criação coincide com a superfície de pisada
            XYZ centerPoint = geometry.BaseCenter
                + geometry.StairAxisDirection.Multiply(stepIndex * stepData.RunPerStepFt + (stepData.ProfileTreadDepthFt / 2.0))
                + XYZ.BasisZ.Multiply(((stepIndex + 1) * stepData.RiserHeightFt) - stepAssemblyReductionFt);

            leftPoint = centerPoint + geometry.LeftDirection.Multiply(stepData.WidthFt / 2.0);
            rightPoint = centerPoint - geometry.LeftDirection.Multiply(stepData.WidthFt / 2.0);
        }

        private static void CalcularBordaPosteriorDegrau(int stepIndex, StairGeometryData geometry, StairStepData stepData, out XYZ leftPoint, out XYZ rightPoint)
        {
            double stepAssemblyReductionFt = StepAssemblyHeightReductionCm * RevitUtils.FT_PER_CM;

            // Borda posterior do degrau i = início do passo = i × piso
            // Altura: topo do espelho i = (i + 1) × espelho
            XYZ backCenterPoint = geometry.BaseCenter
                + geometry.StairAxisDirection.Multiply(stepIndex * stepData.RunPerStepFt)
                + XYZ.BasisZ.Multiply(((stepIndex + 1) * stepData.RiserHeightFt) - stepAssemblyReductionFt);

            leftPoint = backCenterPoint + geometry.LeftDirection.Multiply(stepData.WidthFt / 2.0);
            rightPoint = backCenterPoint - geometry.LeftDirection.Multiply(stepData.WidthFt / 2.0);
        }

        private FamilyInstance CriarViga(Document doc, XYZ inicio, XYZ fim, FamilySymbol symbol, Level nivel, EscadaConfig config)
        {
            if (symbol == null || inicio.DistanceTo(fim) < RevitUtils.EPS)
                return null;

            Line linha = Line.CreateBound(inicio, fim);
            FamilyInstance instancia = doc.Create.NewFamilyInstance(
                linha,
                symbol,
                nivel,
                StructuralType.Beam);

            RevitUtils.SetZJustification(instancia, config.ZJustificationValue);

            if (!config.UnirGeometrias)
                RevitUtils.DisallowJoins(instancia);

            return instancia;
        }

        private void ConfigurarOrientacaoLongarina(FamilyInstance instancia, bool flipProfile)
        {
            if (instancia == null)
                return;

            RevitUtils.SetYZOffsets(instancia, 0.0, 0.0);
            RevitUtils.SetSectionRotation(instancia, flipProfile ? Math.PI : 0.0);
        }

        private void ConfigurarOrientacaoDegrau(FamilyInstance instancia)
        {
            if (instancia == null)
                return;

            // ZJustification.Top = 2 no Revit API (Origin=0, Center=1, Top=2, Bottom=3)
            // A superfície de pisada (topo do perfil) deve coincidir com a linha de referência
            RevitUtils.SetZJustification(instancia, 2);
            RevitUtils.SetYZOffsets(instancia, 0.0, 0.0);
            RevitUtils.SetSectionRotation(instancia, -Math.PI / 2.0);
        }

        private static string ObterDescricaoDegrau(EscadaConfig config)
        {
            if (!config.CriarDegraus)
                return "não criado";

            if (config.TipoDegrau == EscadaTipoDegrau.Chapa)
                return "Chapa";

            return config.SymbolDegrau != null
                ? config.SymbolDegrau.FamilyName + " : " + config.SymbolDegrau.Name
                : "Perfil linear";
        }

        private sealed class StairValidationResult
        {
            private StairValidationResult(bool isValid, string message, string warningMessage)
            {
                IsValid = isValid;
                Message = message;
                WarningMessage = warningMessage;
            }

            public bool IsValid { get; }
            public string Message { get; }
            public string WarningMessage { get; }

            public static StairValidationResult Valid()
            {
                return new StairValidationResult(true, string.Empty, string.Empty);
            }

            public static StairValidationResult ValidWithWarning(string warningMessage)
            {
                return new StairValidationResult(true, string.Empty, warningMessage);
            }

            public static StairValidationResult Invalid(string message)
            {
                return new StairValidationResult(false, message, string.Empty);
            }
        }

        private sealed class StairStepData
        {
            public StairStepData(int stepCount, double riserHeightFt, double runPerStepFt, double totalRunFt, double widthFt, double profileTreadDepthFt)
            {
                StepCount = stepCount;
                RiserHeightFt = riserHeightFt;
                RunPerStepFt = runPerStepFt;
                TotalRunFt = totalRunFt;
                WidthFt = widthFt;
                ProfileTreadDepthFt = profileTreadDepthFt;
            }

            public int StepCount { get; }
            public double RiserHeightFt { get; }
            public double RunPerStepFt { get; }
            public double TotalRunFt { get; }
            public double WidthFt { get; }
            public double ProfileTreadDepthFt { get; }
        }

        private sealed class StairGeometryData
        {
            public StairGeometryData(
                XYZ baseCenter,
                XYZ topCenter,
                XYZ stairAxisDirection,
                XYZ leftDirection,
                XYZ leftStringerStart,
                XYZ leftStringerEnd,
                XYZ rightStringerStart,
                XYZ rightStringerEnd)
            {
                BaseCenter = baseCenter;
                TopCenter = topCenter;
                StairAxisDirection = stairAxisDirection;
                LeftDirection = leftDirection;
                LeftStringerStart = leftStringerStart;
                LeftStringerEnd = leftStringerEnd;
                RightStringerStart = rightStringerStart;
                RightStringerEnd = rightStringerEnd;
            }

            public XYZ BaseCenter { get; }
            public XYZ TopCenter { get; }
            public XYZ StairAxisDirection { get; }
            public XYZ LeftDirection { get; }
            public XYZ LeftStringerStart { get; }
            public XYZ LeftStringerEnd { get; }
            public XYZ RightStringerStart { get; }
            public XYZ RightStringerEnd { get; }
        }
    }
}
