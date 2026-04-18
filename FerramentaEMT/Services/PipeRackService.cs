using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FerramentaEMT.Services
{
    public class PipeRackService
    {
        public void Executar(UIDocument uidoc, Document doc, PipeRackConfig config)
        {
            XYZ origem;
            XYZ pontoDirecao;

            try
            {
                origem = uidoc.Selection.PickPoint("Clique o ponto INICIAL do pipe rack");
                pontoDirecao = uidoc.Selection.PickPoint("Clique o ponto que define a DIREÇÃO longitudinal do pipe rack");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            XYZ dirLong = RevitUtils.SafeNormalize(new XYZ(
                pontoDirecao.X - origem.X,
                pontoDirecao.Y - origem.Y,
                0.0));

            if (RevitUtils.IsZeroVector(dirLong))
            {
                AppDialogService.ShowWarning("Pipe Rack", "Não foi possível determinar a direção longitudinal.", "Direção inválida");
                return;
            }

            origem = new XYZ(origem.X, origem.Y, config.NivelBase.Elevation);

            XYZ dirTrans = XYZ.BasisZ.CrossProduct(dirLong);
            dirTrans = RevitUtils.SafeNormalize(dirTrans);

            double alturaTotalMm = (config.NivelTopoPilares.Elevation - config.NivelBase.Elevation) / RevitUtils.FT_PER_MM;
            if (alturaTotalMm <= 0)
            {
                AppDialogService.ShowWarning("Pipe Rack", "O nível de topo precisa estar acima do nível de base.", "Níveis incompatíveis");
                return;
            }

            if (config.QuantidadeModulos <= 0)
            {
                AppDialogService.ShowWarning("Pipe Rack", "A quantidade de módulos precisa ser maior que zero.", "Quantidade inválida");
                return;
            }

            double alturaTrelicaMm = config.AlturaModuloMm * config.QuantidadeModulos;
            if (config.AlturaModuloMm <= 0 || alturaTrelicaMm >= alturaTotalMm)
            {
                AppDialogService.ShowWarning("Pipe Rack", "A altura total da treliça precisa ser positiva e menor que a altura total dos pilares.", "Altura inválida");
                return;
            }

            List<double> xPos = ConstruirPosicoesAcumuladas(config.VaosMm);
            List<double> yPos = ConstruirPosicoesTransversais(config.LarguraEstruturaMm, config.NumeroModulosLargura);
            double zBaseTrelicaMm = alturaTotalMm - alturaTrelicaMm;
            double zTopoTrelicaMm = alturaTotalMm;

            using (Transaction t = new Transaction(doc, "Lançar Pipe Rack"))
            {
                t.Start();

                Ativar(config.SymbolPilar, doc);
                Ativar(config.SymbolViga, doc);
                Ativar(config.SymbolMontante, doc);
                Ativar(config.SymbolDiagonal, doc);

                CriarPilares(doc, config, origem, dirLong, dirTrans, xPos, yPos, alturaTotalMm);
                CriarCaixaTrelicada(doc, config, origem, dirLong, dirTrans, xPos, yPos, zBaseTrelicaMm, zTopoTrelicaMm);

                t.Commit();
            }

            AppDialogService.ShowInfo(
                "Pipe Rack",
                "Pipe rack gerado com sucesso.\n\n" +
                $"Pórticos: {xPos.Count}\n" +
                $"Linhas transversais: {yPos.Count}\n" +
                $"Altura total: {Math.Round(alturaTotalMm)} mm\n" +
                $"Módulos empilhados: {config.QuantidadeModulos}",
                "Pipe rack gerado");
        }

        private void CriarPilares(Document doc, PipeRackConfig config, XYZ origem, XYZ dirLong, XYZ dirTrans, List<double> xPos, List<double> yPos, double alturaTotalMm)
        {
            List<double> apoiosTransversais = new List<double> { yPos.First(), yPos.Last() }.Distinct().ToList();
            foreach (double x in xPos)
            {
                foreach (double y in apoiosTransversais)
                {
                    XYZ basePt = Ponto(origem, dirLong, dirTrans, x, y, 0);
                    XYZ topPt = Ponto(origem, dirLong, dirTrans, x, y, alturaTotalMm);
                    CriarMembro(doc, config.SymbolPilar, config.NivelBase, basePt, topPt, true, config.DesabilitarUniaoMembros, config.NivelTopoPilares);
                }
            }
        }

        private void CriarCaixaTrelicada(
            Document doc,
            PipeRackConfig config,
            XYZ origem,
            XYZ dirLong,
            XYZ dirTrans,
            List<double> xPos,
            List<double> yPos,
            double zBaseTrelicaMm,
            double zTopoTrelicaMm)
        {
            List<double> ladosExternos = new List<double> { yPos.First(), yPos.Last() }.Distinct().ToList();
            List<double> niveisModulo = Enumerable.Range(0, config.QuantidadeModulos + 1)
                .Select(i => zBaseTrelicaMm + (config.AlturaModuloMm * i))
                .ToList();

            foreach (double z in niveisModulo)
            {
                for (int i = 0; i < xPos.Count - 1; i++)
                {
                    foreach (double y in ladosExternos)
                    {
                        XYZ p0 = Ponto(origem, dirLong, dirTrans, xPos[i], y, z);
                        XYZ p1 = Ponto(origem, dirLong, dirTrans, xPos[i + 1], y, z);
                        CriarMembro(doc, config.SymbolViga, config.NivelBase, p0, p1, false, config.DesabilitarUniaoMembros);
                    }
                }
            }

            CriarFechamentosTransversais(doc, config, origem, dirLong, dirTrans, xPos, yPos, niveisModulo);

            for (int i = 0; i < xPos.Count - 1; i++)
            {
                double span = xPos[i + 1] - xPos[i];
                int paineis = CalcularNumeroPaineis(span, config.AlturaModuloMm);
                double passo = span / paineis;

                List<double> xGrades = new List<double>();
                for (int p = 0; p <= paineis; p++)
                    xGrades.Add(xPos[i] + passo * p);

                foreach (double y in ladosExternos)
                {
                    for (int nivel = 0; nivel < niveisModulo.Count - 1; nivel++)
                    {
                        double zInferior = niveisModulo[nivel];
                        double zSuperior = niveisModulo[nivel + 1];

                        for (int p = 1; p < xGrades.Count - 1; p++)
                        {
                            double xp = xGrades[p];
                            XYZ m0 = Ponto(origem, dirLong, dirTrans, xp, y, zInferior);
                            XYZ m1 = Ponto(origem, dirLong, dirTrans, xp, y, zSuperior);
                            CriarMembro(doc, config.SymbolMontante, config.NivelBase, m0, m1, false, config.DesabilitarUniaoMembros);
                        }

                        for (int p = 0; p < xGrades.Count - 1; p++)
                        {
                            double xa = xGrades[p];
                            double xb = xGrades[p + 1];
                            XYZ b0 = Ponto(origem, dirLong, dirTrans, xa, y, zInferior);
                            XYZ b1 = Ponto(origem, dirLong, dirTrans, xb, y, zInferior);
                            XYZ t0 = Ponto(origem, dirLong, dirTrans, xa, y, zSuperior);
                            XYZ t1 = Ponto(origem, dirLong, dirTrans, xb, y, zSuperior);

                            foreach (Line diag in ConstruirDiagonais(config.TipoTrelica, config.PadraoDiagonais, p, b0, b1, t0, t1))
                                CriarMembro(doc, config.SymbolDiagonal, config.NivelBase, diag.GetEndPoint(0), diag.GetEndPoint(1), false, config.DesabilitarUniaoMembros);
                        }
                    }
                }

                foreach (double x in xGrades.Skip(1).Take(xGrades.Count - 2))
                {
                    CriarFechamentosTransversais(doc, config, origem, dirLong, dirTrans, new List<double> { x }, yPos, niveisModulo);
                }
            }
        }

        private void CriarFechamentosTransversais(
            Document doc,
            PipeRackConfig config,
            XYZ origem,
            XYZ dirLong,
            XYZ dirTrans,
            IEnumerable<double> posicoesX,
            List<double> yPos,
            IEnumerable<double> niveisZ)
        {
            foreach (double x in posicoesX)
            {
                foreach (double z in niveisZ)
                {
                    for (int j = 0; j < yPos.Count - 1; j++)
                    {
                        XYZ p0 = Ponto(origem, dirLong, dirTrans, x, yPos[j], z);
                        XYZ p1 = Ponto(origem, dirLong, dirTrans, x, yPos[j + 1], z);
                        CriarMembro(doc, config.SymbolViga, config.NivelBase, p0, p1, false, config.DesabilitarUniaoMembros);
                    }
                }
            }
        }

        private IEnumerable<Line> ConstruirDiagonais(string tipoTrelica, string padraoDiagonais, int indicePainel, XYZ b0, XYZ b1, XYZ t0, XYZ t1)
        {
            bool sobe = DeterminarSentidoDiagonal(padraoDiagonais, indicePainel);

            switch (tipoTrelica)
            {
                case "Howe":
                    yield return sobe ? Line.CreateBound(t0, b1) : Line.CreateBound(b0, t1);
                    break;
                case "Warren":
                    if (sobe)
                        yield return Line.CreateBound(b0, t1);
                    else
                        yield return Line.CreateBound(t0, b1);
                    break;
                case "X":
                    yield return Line.CreateBound(b0, t1);
                    yield return Line.CreateBound(t0, b1);
                    break;
                case "Diagonal simples":
                    yield return sobe ? Line.CreateBound(b0, t1) : Line.CreateBound(t0, b1);
                    break;
                default:
                    yield return sobe ? Line.CreateBound(b0, t1) : Line.CreateBound(t0, b1);
                    break;
            }
        }

        private bool DeterminarSentidoDiagonal(string padraoDiagonais, int indicePainel)
        {
            return padraoDiagonais switch
            {
                "Sempre descendo" => false,
                "Sempre subindo" => true,
                _ => indicePainel % 2 == 0
            };
        }

        private int CalcularNumeroPaineis(double spanMm, double alturaModuloMm)
        {
            int melhor = 1;
            double melhorDelta = double.MaxValue;

            for (int n = 1; n <= 12; n++)
            {
                double larguraPainel = spanMm / n;
                double angulo = Math.Atan2(alturaModuloMm, larguraPainel) * 180.0 / Math.PI;
                if (angulo < 30.0 || angulo > 60.0)
                    continue;

                double delta = Math.Abs(angulo - 45.0);
                if (delta < melhorDelta)
                {
                    melhor = n;
                    melhorDelta = delta;
                }
            }

            return melhor;
        }

        private FamilyInstance CriarMembro(Document doc, FamilySymbol symbol, Level level, XYZ p0, XYZ p1, bool coluna, bool desabilitarUniao, Level topLevel = null)
        {
            if (symbol is null || level is null || p0.DistanceTo(p1) < RevitUtils.EPS)
                return null;

            if (coluna && symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
            {
                try
                {
                    FamilyInstance colunaInstancia = doc.Create.NewFamilyInstance(
                        Line.CreateBound(p0, p1),
                        symbol,
                        level,
                        StructuralType.Column);
                    ConfigurarExtremosColuna(colunaInstancia, level, topLevel);
                    PosProcessarInstancia(colunaInstancia, desabilitarUniao);
                    return colunaInstancia;
                }
                catch
                {
                    try
                    {
                        FamilyInstance colunaInstancia = doc.Create.NewFamilyInstance(p0, symbol, level, StructuralType.Column);
                        if (colunaInstancia.Location is LocationPoint locPt)
                            locPt.Point = new XYZ(p0.X, p0.Y, level.Elevation);

                        if (colunaInstancia.Location is LocationCurve locCurve)
                            locCurve.Curve = Line.CreateBound(p0, p1);

                        ConfigurarExtremosColuna(colunaInstancia, level, topLevel);
                        PosProcessarInstancia(colunaInstancia, desabilitarUniao);
                        return colunaInstancia;
                    }
                    catch
                    {
                        // segue para o fluxo genérico
                    }
                }
            }

            try
            {
                FamilyInstance instancia = doc.Create.NewFamilyInstance(
                    Line.CreateBound(p0, p1),
                    symbol,
                    level,
                    coluna ? StructuralType.Column : StructuralType.Beam);
                if (coluna)
                    ConfigurarExtremosColuna(instancia, level, topLevel);
                PosProcessarInstancia(instancia, desabilitarUniao);
                return instancia;
            }
            catch
            {
                FamilyInstance instancia = doc.Create.NewFamilyInstance(Line.CreateBound(p0, p1), symbol, level, StructuralType.Beam);
                PosProcessarInstancia(instancia, desabilitarUniao);
                return instancia;
            }
        }

        private void ConfigurarExtremosColuna(FamilyInstance instancia, Level baseLevel, Level topLevel)
        {
            if (instancia is null || baseLevel is null)
                return;

            DefinirParametro(instancia.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM), baseLevel.Id);
            DefinirParametro(instancia.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM), 0.0);

            if (topLevel is not null)
            {
                DefinirParametro(instancia.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM), topLevel.Id);
                DefinirParametro(instancia.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM), 0.0);
            }

            DefinirParametro(instancia.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM), topLevel is null ? 0.0 : topLevel.Elevation - baseLevel.Elevation);
        }

        private void DefinirParametro(Parameter parameter, ElementId value)
        {
            if (parameter is null || parameter.IsReadOnly)
                return;

            parameter.Set(value);
        }

        private void DefinirParametro(Parameter parameter, double value)
        {
            if (parameter is null || parameter.IsReadOnly)
                return;

            parameter.Set(value);
        }

        private void PosProcessarInstancia(FamilyInstance instancia, bool desabilitarUniao)
        {
            if (instancia is null || !desabilitarUniao)
                return;

            RevitUtils.DisallowJoins(instancia);
        }

        private void Ativar(FamilySymbol symbol, Document doc)
        {
            if (symbol is null || symbol.IsActive)
                return;

            symbol.Activate();
            doc.Regenerate();
        }

        private List<double> ConstruirPosicoesAcumuladas(List<double> vaosMm)
        {
            List<double> posicoes = new List<double> { 0.0 };
            double acumulado = 0.0;
            foreach (double vao in vaosMm)
            {
                acumulado += vao;
                posicoes.Add(acumulado);
            }
            return posicoes;
        }

        private List<double> ConstruirPosicoesTransversais(double larguraMm, int modulos)
        {
            modulos = Math.Max(2, modulos);
            if (modulos == 2)
                return new List<double> { 0.0, larguraMm };

            return new List<double> { 0.0, larguraMm * 0.5, larguraMm };
        }

        private XYZ Ponto(XYZ origem, XYZ dirLong, XYZ dirTrans, double xMm, double yMm, double zMm)
        {
            return origem
                + dirLong.Multiply(xMm * RevitUtils.FT_PER_MM)
                + dirTrans.Multiply(yMm * RevitUtils.FT_PER_MM)
                + XYZ.BasisZ.Multiply(zMm * RevitUtils.FT_PER_MM);
        }
    }
}
