using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdCortarPerfilPorInterferencia : FerramentaCommandBase
    {
        protected override string CommandName => "Seccionar Viga";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
                FamilyInstance viga = SelecionarViga(uidoc, doc);
                if (viga == null)
                    return Result.Cancelled;

                if (!viga.CanSplit)
                {
                    AppDialogService.ShowWarning(
                        "Seccionar Viga",
                        "A viga selecionada nao pode ser seccionada pela API. Use uma viga reta ou em arco que permita split.");
                    return Result.Cancelled;
                }

                Curve curvaViga = (viga.Location as LocationCurve)?.Curve;
                if (curvaViga == null)
                {
                    AppDialogService.ShowWarning("Seccionar Viga", "Nao foi possivel obter a curva da viga selecionada.", "Curva indisponível");
                    return Result.Cancelled;
                }

                List<Element> referencias = SelecionarReferencias(uidoc, doc, viga.Id);
                if (referencias.Count == 0)
                {
                    AppDialogService.ShowWarning("Seccionar Viga", "Nenhum elemento de referencia valido foi selecionado.", "Nenhuma referência encontrada");
                    return Result.Cancelled;
                }

                Plane planoVista = ObterPlanoDaVista(doc.ActiveView);
                List<string> diagnostico = new List<string>();
                List<PontoDeCorte> cortes = ObterPontosDeCorte(curvaViga, referencias, planoVista, diagnostico);
                if (cortes.Count == 0)
                {
                    AppDialogService.ShowWarning(
                        "Seccionar Viga",
                        "Nenhum ponto de corte interno foi encontrado a partir dos elementos de referencia selecionados." +
                        (diagnostico.Count > 0 ? "\n\nDiagnostico:\n" + string.Join("\n", diagnostico.Take(12)) : string.Empty),
                        "Nenhum ponto de corte encontrado");
                    return Result.Cancelled;
                }

                List<ElementId> idsResultado = new List<ElementId> { viga.Id };
                int cortesAplicados = 0;
                int referenciasAproveitadas = cortes.Select(c => c.ElementoId.Value).Distinct().Count();

                using (Transaction t = new Transaction(doc, "Seccionar Viga"))
                {
                    t.Start();

                    foreach (PontoDeCorte corte in cortes.OrderByDescending(c => c.Parametro))
                    {
                        if (!viga.CanSplit)
                            continue;

                        Curve curvaAtual = (viga.Location as LocationCurve)?.Curve;
                        if (curvaAtual == null)
                            continue;

                        double parametroAtual = ObterParametroNormalizado(curvaAtual, corte.Ponto);
                        if (parametroAtual <= 1e-6 || parametroAtual >= 1.0 - 1e-6)
                            continue;

                        ElementId novaVigaId = viga.Split(parametroAtual);
                        if (novaVigaId == null || novaVigaId == ElementId.InvalidElementId)
                            continue;

                        FamilyInstance novaViga = doc.GetElement(novaVigaId) as FamilyInstance;
                        if (novaViga == null)
                            continue;

                        StructuralFramingUtils.DisallowJoinAtEnd(viga, 1);
                        StructuralFramingUtils.DisallowJoinAtEnd(novaViga, 0);

                        idsResultado.Add(novaVigaId);
                        cortesAplicados++;
                    }

                    t.Commit();
                }

                if (cortesAplicados == 0)
                {
                    AppDialogService.ShowWarning("Seccionar Viga", "Nenhum corte foi aplicado na viga selecionada.", "Nenhum corte aplicado");
                    return Result.Cancelled;
                }

                uidoc.Selection.SetElementIds(idsResultado);

                AppDialogService.ShowInfo(
                    "Seccionar Viga",
                    $"Viga seccionada com sucesso." +
                    $"\n\nViga original: {ObterDescricaoElemento(viga)}" +
                    $"\nReferencias selecionadas: {referencias.Count}" +
                    $"\nReferencias aproveitadas: {referenciasAproveitadas}" +
                    $"\nPontos de corte encontrados: {cortes.Count}" +
                    $"\nCortes aplicados: {cortesAplicados}" +
                    $"\nSegmentos selecionados ao final: {idsResultado.Count}" +
                    (diagnostico.Count > 0 ? $"\n\nDiagnostico:\n{string.Join("\n", diagnostico.Take(12))}" : string.Empty),
                    "Seccionamento concluído");

                return Result.Succeeded;
        }

        private FamilyInstance SelecionarViga(UIDocument uidoc, Document doc)
        {
            Reference referencia = uidoc.Selection.PickObject(
                ObjectType.Element,
                new FiltroVigasSeccionaveis(),
                "Selecione a viga que sera seccionada");

            FamilyInstance viga = doc.GetElement(referencia.ElementId) as FamilyInstance;
            if (!EhVigaValida(viga))
            {
                AppDialogService.ShowWarning("Seccionar Viga", "O elemento selecionado precisa ser uma viga estrutural.", "Seleção inválida");
                return null;
            }

            return viga;
        }

        private List<Element> SelecionarReferencias(UIDocument uidoc, Document doc, ElementId vigaId)
        {
            IList<Reference> referencias = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new FiltroElementosReferencia(vigaId),
                "Selecione os elementos de referencia para gerar varios cortes na viga");

            return referencias
                .Select(r => doc.GetElement(r))
                .Where(e => e != null)
                .GroupBy(e => e.Id.Value)
                .Select(g => g.First())
                .ToList();
        }

        private List<PontoDeCorte> ObterPontosDeCorte(Curve curvaViga, List<Element> referencias, Plane planoVista, List<string> diagnostico)
        {
            List<PontoDeCorte> cortes = new List<PontoDeCorte>();

            foreach (Element referencia in referencias)
            {
                Curve curvaReferencia = ObterCurvaDeReferencia(referencia);
                if (curvaReferencia == null)
                {
                    diagnostico?.Add($"{ObterDescricaoElemento(referencia)} -> sem curva de referencia.");
                    continue;
                }

                List<ResultadoPontoDeCorte> resultados = ObterPontosDeCorteDaReferencia(curvaViga, curvaReferencia, referencia, planoVista);
                if (resultados.Count == 0)
                {
                    diagnostico?.Add($"{ObterDescricaoElemento(referencia)} -> nenhum ponto encontrado.");
                }

                foreach (ResultadoPontoDeCorte resultado in resultados)
                {
                    XYZ ponto = resultado.Ponto;
                    double parametro = ObterParametroNormalizado(curvaViga, ponto);
                    if (parametro <= 1e-6 || parametro >= 1.0 - 1e-6)
                    {
                        diagnostico?.Add($"{ObterDescricaoElemento(referencia)} -> {resultado.Origem} descartado (param {parametro:F4} fora do trecho).");
                        continue;
                    }

                    if (cortes.Any(c => Math.Abs(c.Parametro - parametro) < 1e-6))
                    {
                        diagnostico?.Add($"{ObterDescricaoElemento(referencia)} -> {resultado.Origem} descartado (duplicado em {parametro:F4}).");
                        continue;
                    }

                    cortes.Add(new PontoDeCorte(parametro, ponto, referencia.Id));
                    string sufixo = resultado.Distancia >= 0.0 ? $" dist={resultado.Distancia:F4}" : string.Empty;
                    diagnostico?.Add($"{ObterDescricaoElemento(referencia)} -> {resultado.Origem} aceito em param={parametro:F4}{sufixo}.");
                }
            }

            return cortes.OrderBy(c => c.Parametro).ToList();
        }

        private Curve ObterCurvaDeReferencia(Element referencia)
        {
            if (referencia is Grid grid)
                return grid.Curve;

            if (referencia is FamilyInstance instancia)
            {
                Curve curvaInstancia = ObterCurvaCentralDaInstancia(instancia);
                if (curvaInstancia != null)
                    return curvaInstancia;
            }

            Curve curva = RevitUtils.GetElementCurve(referencia);
            if (curva != null)
                return curva;

            return CriarCurvaCentralPorBoundingBox(referencia);
        }

        private List<ResultadoPontoDeCorte> ObterPontosDeCorteDaReferencia(Curve curvaViga, Curve curvaReferencia, Element referencia, Plane planoVista)
        {
            List<ResultadoPontoDeCorte> pontos = new List<ResultadoPontoDeCorte>();

            if (curvaViga == null || curvaReferencia == null)
                return pontos;

            IntersectionResultArray resultados;
            SetComparisonResult comparacao = curvaViga.Intersect(curvaReferencia, out resultados);

            if ((comparacao == SetComparisonResult.Overlap ||
                 comparacao == SetComparisonResult.Subset ||
                 comparacao == SetComparisonResult.Superset) &&
                resultados != null)
            {
                for (int i = 0; i < resultados.Size; i++)
                {
                    IntersectionResult resultado = resultados.get_Item(i);
                    if (resultado?.XYZPoint != null)
                        pontos.Add(new ResultadoPontoDeCorte(resultado.XYZPoint, "intersecao-3d"));
                }
            }

            if (pontos.Count > 0)
                return pontos;

            ResultadoPontoDeCorte pontoMaisProximo = TentarPontoPorMenorDistancia(curvaViga, curvaReferencia, referencia);
            if (pontoMaisProximo != null)
            {
                pontos.Add(pontoMaisProximo);
            }

            if (pontos.Count > 0)
                return pontos;

            ResultadoPontoDeCorte pontoPorCaixa = TentarPontoPorSobreposicaoNaCaixa(curvaViga, referencia);
            if (pontoPorCaixa != null)
            {
                pontos.Add(pontoPorCaixa);
            }

            if (pontos.Count > 0)
                return pontos;

            XYZ pontoVista = TentarIntersecaoNoPlanoDaVista(curvaViga, curvaReferencia, planoVista);
            if (pontoVista != null)
            {
                pontos.Add(new ResultadoPontoDeCorte(pontoVista, "intersecao-vista"));
            }

            if (pontos.Count > 0)
                return pontos;

            XYZ pontoCentral = TentarPontoCentralDaReferenciaNaVista(curvaViga, curvaReferencia, referencia, planoVista);
            if (pontoCentral != null)
            {
                pontos.Add(new ResultadoPontoDeCorte(pontoCentral, "centro-vista"));
            }

            return pontos;
        }

        private Curve ObterCurvaCentralDaInstancia(FamilyInstance instancia)
        {
            Curve curvaBase = RevitUtils.GetElementCurve(instancia);
            if (curvaBase == null)
                return null;

            XYZ deslocamento = ObterDeslocamentoDaCurva(instancia);
            if (RevitUtils.IsZeroVector(deslocamento))
                return curvaBase;

            try
            {
                return curvaBase.CreateTransformed(Transform.CreateTranslation(deslocamento));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao transformar curva central da instancia");
                return curvaBase;
            }
        }

        private XYZ ObterDeslocamentoDaCurva(FamilyInstance instancia)
        {
            if (instancia == null)
                return XYZ.Zero;

            Transform transform = instancia.GetTransform();
            if (transform == null)
                return XYZ.Zero;

            double offsetY = ObterValorDouble(instancia, BuiltInParameter.Y_OFFSET_VALUE);
            double offsetZ = ObterValorDouble(instancia, BuiltInParameter.Z_OFFSET_VALUE);

            XYZ eixoY = RevitUtils.SafeNormalize(transform.BasisY);
            XYZ eixoZ = RevitUtils.SafeNormalize(transform.BasisZ);

            XYZ deslocamento = XYZ.Zero;

            if (!RevitUtils.IsZeroVector(eixoY) && Math.Abs(offsetY) > 1e-9)
                deslocamento += eixoY.Multiply(offsetY);

            if (!RevitUtils.IsZeroVector(eixoZ) && Math.Abs(offsetZ) > 1e-9)
                deslocamento += eixoZ.Multiply(offsetZ);

            return deslocamento;
        }

        private double ObterValorDouble(Element elemento, BuiltInParameter parametro)
        {
            Parameter p = elemento?.get_Parameter(parametro);
            return p != null && p.HasValue ? p.AsDouble() : 0.0;
        }

        private Curve CriarCurvaCentralPorBoundingBox(Element referencia)
        {
            BoundingBoxXYZ bbox = referencia?.get_BoundingBox(null);
            if (bbox == null)
                return null;

            XYZ centro = (bbox.Min + bbox.Max) * 0.5;
            double altura = bbox.Max.Z - bbox.Min.Z;
            double larguraX = bbox.Max.X - bbox.Min.X;
            double larguraY = bbox.Max.Y - bbox.Min.Y;

            if (altura >= larguraX && altura >= larguraY && altura > 1e-6)
            {
                XYZ basePt = new XYZ(centro.X, centro.Y, bbox.Min.Z);
                XYZ topoPt = new XYZ(centro.X, centro.Y, bbox.Max.Z);
                if (basePt.DistanceTo(topoPt) > 1e-6)
                    return Line.CreateBound(basePt, topoPt);
            }

            XYZ p0 = new XYZ(bbox.Min.X, centro.Y, centro.Z);
            XYZ p1 = new XYZ(bbox.Max.X, centro.Y, centro.Z);
            if (larguraY > larguraX)
            {
                p0 = new XYZ(centro.X, bbox.Min.Y, centro.Z);
                p1 = new XYZ(centro.X, bbox.Max.Y, centro.Z);
            }

            return p0.DistanceTo(p1) > 1e-6 ? Line.CreateBound(p0, p1) : null;
        }

        private XYZ ObterPontoProjetadoNaViga(Curve curvaViga, XYZ ponto)
        {
            IntersectionResult projecao = curvaViga.Project(ponto);
            return projecao?.XYZPoint;
        }

        private XYZ TentarIntersecaoNoPlanoDaVista(Curve curvaViga, Curve curvaReferencia, Plane planoVista)
        {
            if (planoVista == null)
                return null;

            Line linhaViga = ConverterParaLinha(curvaViga);
            Line linhaReferencia = ConverterParaLinha(curvaReferencia);
            if (linhaViga == null || linhaReferencia == null)
                return null;

            Line vigaProjetada = RevitUtils.ProjectLineOntoPlane(linhaViga, planoVista);
            Line referenciaProjetada = RevitUtils.ProjectLineOntoPlane(linhaReferencia, planoVista);
            if (vigaProjetada == null || referenciaProjetada == null)
                return null;

            XYZ intersecao;
            if (!RevitUtils.TryIntersectLines2DInPlane(vigaProjetada, referenciaProjetada, planoVista, out intersecao))
                return null;

            return ObterPontoProjetadoNaViga(curvaViga, intersecao);
        }

        private XYZ TentarPontoCentralDaReferenciaNaVista(Curve curvaViga, Curve curvaReferencia, Element referencia, Plane planoVista)
        {
            if (curvaViga == null || curvaReferencia == null || referencia == null || planoVista == null)
                return null;

            Line linhaViga = ConverterParaLinha(curvaViga);
            if (linhaViga == null)
                return null;

            Line vigaProjetada = RevitUtils.ProjectLineOntoPlane(linhaViga, planoVista);
            if (vigaProjetada == null)
                return null;

            XYZ centroReferencia = ObterPontoCentralDaReferencia(curvaReferencia, referencia);
            if (centroReferencia == null)
                return null;

            XYZ centroProjetado = RevitUtils.ProjectPointOntoPlane(centroReferencia, planoVista);
            IntersectionResult projecaoNaViga = vigaProjetada.Project(centroProjetado);
            if (projecaoNaViga?.XYZPoint == null)
                return null;

            XYZ pontoNaVigaProjetada = projecaoNaViga.XYZPoint;
            if (!PontoPertenceAoSegmento(vigaProjetada, pontoNaVigaProjetada))
                return null;

            double distanciaPlano = centroProjetado.DistanceTo(pontoNaVigaProjetada);
            double tolerancia = ObterToleranciaDaReferenciaNaVista(referencia, centroProjetado, planoVista);
            if (distanciaPlano > tolerancia)
                return null;

            return ObterPontoProjetadoNaViga(curvaViga, pontoNaVigaProjetada);
        }

        private ResultadoPontoDeCorte TentarPontoPorMenorDistancia(Curve curvaViga, Curve curvaReferencia, Element referencia)
        {
            if (curvaViga == null || curvaReferencia == null || referencia == null)
                return null;

            try
            {
                IList<ClosestPointsPairBetweenTwoCurves> pares;
                curvaViga.ComputeClosestPoints(curvaReferencia, true, true, false, out pares);
                if (pares == null || pares.Count == 0)
                    return null;

                ClosestPointsPairBetweenTwoCurves melhorPar = pares
                    .OrderBy(p => p.Distance)
                    .FirstOrDefault();

                if (melhorPar == null || melhorPar.XYZPointOnFirstCurve == null)
                    return null;

                double tolerancia = ObterToleranciaEspacialDaReferencia(referencia, melhorPar.XYZPointOnSecondCurve);
                if (melhorPar.Distance > tolerancia)
                    return null;

                return new ResultadoPontoDeCorte(melhorPar.XYZPointOnFirstCurve, "menor-distancia", melhorPar.Distance);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao calcular menor distancia entre curvas");
                return null;
            }
        }

        private ResultadoPontoDeCorte TentarPontoPorSobreposicaoNaCaixa(Curve curvaViga, Element referencia)
        {
            if (curvaViga == null || referencia == null)
                return null;

            Line linhaViga = ConverterParaLinha(curvaViga);
            BoundingBoxXYZ bbox = referencia.get_BoundingBox(null);
            if (linhaViga == null || bbox == null)
                return null;

            XYZ p0 = linhaViga.GetEndPoint(0);
            XYZ p1 = linhaViga.GetEndPoint(1);
            XYZ dir = p1 - p0;

            double tEnter = 0.0;
            double tExit = 1.0;
            const double eps = 1e-9;
            const double folga = 0.01;

            if (!AtualizarIntervaloPorEixo(p0.X, dir.X, bbox.Min.X - folga, bbox.Max.X + folga, ref tEnter, ref tExit, eps))
                return null;

            if (!AtualizarIntervaloPorEixo(p0.Y, dir.Y, bbox.Min.Y - folga, bbox.Max.Y + folga, ref tEnter, ref tExit, eps))
                return null;

            if (!AtualizarIntervaloPorEixo(p0.Z, dir.Z, bbox.Min.Z - folga, bbox.Max.Z + folga, ref tEnter, ref tExit, eps))
                return null;

            if (tExit < 0.0 || tEnter > 1.0)
                return null;

            double tInicio = Math.Max(0.0, tEnter);
            double tFim = Math.Min(1.0, tExit);
            if (tFim - tInicio < 1e-6)
                return null;

            double tMedio = (tInicio + tFim) * 0.5;
            XYZ ponto = p0 + dir.Multiply(tMedio);
            XYZ pontoProjetado = ObterPontoProjetadoNaViga(curvaViga, ponto);
            if (pontoProjetado == null)
                return null;

            return new ResultadoPontoDeCorte(pontoProjetado, "sobreposicao-caixa");
        }

        private bool AtualizarIntervaloPorEixo(
            double origem,
            double direcao,
            double minimo,
            double maximo,
            ref double tEnter,
            ref double tExit,
            double eps)
        {
            if (Math.Abs(direcao) < eps)
                return origem >= minimo && origem <= maximo;

            double t0 = (minimo - origem) / direcao;
            double t1 = (maximo - origem) / direcao;

            if (t0 > t1)
            {
                double temp = t0;
                t0 = t1;
                t1 = temp;
            }

            tEnter = Math.Max(tEnter, t0);
            tExit = Math.Min(tExit, t1);
            return tEnter <= tExit;
        }

        private XYZ ObterPontoCentralDaReferencia(Curve curvaReferencia, Element referencia)
        {
            if (curvaReferencia != null && curvaReferencia.IsBound)
            {
                try
                {
                    return curvaReferencia.Evaluate(0.5, true);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Falha ao avaliar ponto central da curva de referencia");
                }
            }

            BoundingBoxXYZ bbox = referencia?.get_BoundingBox(null);
            if (bbox == null)
                return null;

            return (bbox.Min + bbox.Max) * 0.5;
        }

        private double ObterToleranciaDaReferenciaNaVista(Element referencia, XYZ centroProjetado, Plane planoVista)
        {
            const double toleranciaMinima = 0.05;
            const double folga = 0.01;

            BoundingBoxXYZ bbox = referencia?.get_BoundingBox(null);
            if (bbox == null)
                return toleranciaMinima;

            List<XYZ> cantos = ObterCantosDaCaixa(bbox);
            if (cantos.Count == 0)
                return toleranciaMinima;

            double raioMaximo = 0.0;
            foreach (XYZ canto in cantos)
            {
                XYZ cantoProjetado = RevitUtils.ProjectPointOntoPlane(canto, planoVista);
                raioMaximo = Math.Max(raioMaximo, cantoProjetado.DistanceTo(centroProjetado));
            }

            return Math.Max(toleranciaMinima, raioMaximo + folga);
        }

        private double ObterToleranciaEspacialDaReferencia(Element referencia, XYZ centro)
        {
            const double toleranciaMinima = 0.05;
            const double folga = 0.01;

            BoundingBoxXYZ bbox = referencia?.get_BoundingBox(null);
            if (bbox == null || centro == null)
                return toleranciaMinima;

            List<XYZ> cantos = ObterCantosDaCaixa(bbox);
            if (cantos.Count == 0)
                return toleranciaMinima;

            double raioMaximo = 0.0;
            foreach (XYZ canto in cantos)
            {
                raioMaximo = Math.Max(raioMaximo, canto.DistanceTo(centro));
            }

            return Math.Max(toleranciaMinima, raioMaximo + folga);
        }

        private List<XYZ> ObterCantosDaCaixa(BoundingBoxXYZ bbox)
        {
            List<XYZ> cantos = new List<XYZ>();
            if (bbox == null)
                return cantos;

            XYZ min = bbox.Min;
            XYZ max = bbox.Max;

            cantos.Add(new XYZ(min.X, min.Y, min.Z));
            cantos.Add(new XYZ(min.X, min.Y, max.Z));
            cantos.Add(new XYZ(min.X, max.Y, min.Z));
            cantos.Add(new XYZ(min.X, max.Y, max.Z));
            cantos.Add(new XYZ(max.X, min.Y, min.Z));
            cantos.Add(new XYZ(max.X, min.Y, max.Z));
            cantos.Add(new XYZ(max.X, max.Y, min.Z));
            cantos.Add(new XYZ(max.X, max.Y, max.Z));
            return cantos;
        }

        private bool PontoPertenceAoSegmento(Line linha, XYZ ponto)
        {
            if (linha == null || ponto == null)
                return false;

            double parametro = RevitUtils.ParameterAlongLine(linha, ponto);
            return parametro >= -1e-6 && parametro <= 1.0 + 1e-6;
        }

        private Line ConverterParaLinha(Curve curva)
        {
            if (curva == null)
                return null;

            if (curva is Line linha)
                return linha;

            XYZ p0 = curva.GetEndPoint(0);
            XYZ p1 = curva.GetEndPoint(1);
            if (p0 == null || p1 == null || p0.DistanceTo(p1) < 1e-9)
                return null;

            return Line.CreateBound(p0, p1);
        }

        private Plane ObterPlanoDaVista(View view)
        {
            if (view == null)
                return null;

            try
            {
                return Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Falha ao obter plano da vista");
                return null;
            }
        }

        private double ObterParametroNormalizado(Curve curva, XYZ ponto)
        {
            if (curva == null || ponto == null)
                return -1.0;

            IntersectionResult projecao = curva.Project(ponto);
            if (projecao == null)
                return -1.0;

            return curva.ComputeNormalizedParameter(projecao.Parameter);
        }

        private bool EhVigaValida(FamilyInstance instancia) =>
            instancia != null &&
            instancia.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming &&
            instancia.StructuralType == StructuralType.Beam;

        private string ObterDescricaoElemento(Element elemento)
        {
            if (elemento == null)
                return "<nulo>";

            string categoria = elemento.Category?.Name ?? "Sem categoria";
            string nome = elemento.Name ?? elemento.GetType().Name;
            return $"{categoria} | {nome} (Id {elemento.Id.Value})";
        }

        private string ObterDescricaoElemento(FamilyInstance instancia)
        {
            if (instancia == null)
                return "<nulo>";

            return $"{instancia.Symbol?.FamilyName} : {instancia.Name} (Id {instancia.Id.Value})";
        }

        private sealed class FiltroVigasSeccionaveis : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem is not FamilyInstance fi || fi.Category == null)
                    return false;

                return fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming &&
                       fi.StructuralType == StructuralType.Beam;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private sealed class FiltroElementosReferencia : ISelectionFilter
        {
            private readonly ElementId _vigaId;

            public FiltroElementosReferencia(ElementId vigaId)
            {
                _vigaId = vigaId;
            }

            public bool AllowElement(Element elem)
            {
                if (elem == null || elem.Id == _vigaId)
                    return false;

                if (elem is Grid)
                    return true;

                if (RevitUtils.GetElementCurve(elem) != null)
                    return true;

                return elem.Location is LocationPoint || elem.get_BoundingBox(null) != null;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private sealed class PontoDeCorte
        {
            public PontoDeCorte(double parametro, XYZ ponto, ElementId elementoId)
            {
                Parametro = parametro;
                Ponto = ponto;
                ElementoId = elementoId;
            }

            public double Parametro { get; }
            public XYZ Ponto { get; }
            public ElementId ElementoId { get; }
        }

        private sealed class ResultadoPontoDeCorte
        {
            public ResultadoPontoDeCorte(XYZ ponto, string origem, double distancia = -1.0)
            {
                Ponto = ponto;
                Origem = origem;
                Distancia = distancia;
            }

            public XYZ Ponto { get; }
            public string Origem { get; }
            public double Distancia { get; }
        }
    }
}
