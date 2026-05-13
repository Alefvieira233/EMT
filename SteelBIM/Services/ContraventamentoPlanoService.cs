using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services
{
    /// <summary>
    /// Service de geracao de contraventamento plano. ADR-003: mudo.
    /// Comunica falhas e sucesso via ContraventamentoPlanoResultado.
    /// Unico ponto interativo (confirmacao de pre-visualizacao) eh
    /// delegado pelo callback opcional confirmarPreview.
    /// </summary>
    public class ContraventamentoPlanoService
    {
        private const int TransparenciaTemporariaQuadros = 40;

        public Result Executar(
            UIDocument uidoc,
            Document doc,
            ContraventamentoPlanoConfig config,
            Plane plane,
            out ContraventamentoPlanoResultado resultado,
            Func<bool> confirmarPreview = null)
        {
            resultado = new ContraventamentoPlanoResultado();

            if (uidoc == null || doc == null)
            {
                resultado.DocumentoIndisponivel = true;
                Logger.Warn("[Contraventamento] Documento indisponivel");
                return Result.Failed;
            }

            if (plane == null)
            {
                resultado.PlanoInvalido = true;
                Logger.Warn("[Contraventamento] Plano nao informado ou invalido");
                return Result.Failed;
            }

            if (config == null || config.SymbolSelecionado == null)
            {
                resultado.ConfiguracaoInvalida = true;
                Logger.Warn("[Contraventamento] Configuracao invalida (config null ou sem perfil)");
                return Result.Cancelled;
            }

            TransparencyScope transparencyScope = TransparencyScope.TryApply(
                doc,
                doc.ActiveView,
                BuiltInCategory.OST_StructuralFraming,
                TransparenciaTemporariaQuadros);

            try
            {
                XYZ pontoC1;
                XYZ pontoC2;

                try
                {
                    pontoC1 = uidoc.Selection.PickPoint("Clique o ponto C1 do contraventamento");
                    pontoC2 = uidoc.Selection.PickPoint("Clique o ponto C2 do contraventamento");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    resultado.PickPointCancelado = true;
                    return Result.Cancelled;
                }

                XYZ xVec = RevitUtils.SafeNormalize(plane.XVec);
                XYZ yVec = RevitUtils.SafeNormalize(plane.YVec);
                if (RevitUtils.IsZeroVector(xVec) || RevitUtils.IsZeroVector(yVec))
                {
                    resultado.EixosInvalidos = true;
                    Logger.Warn("[Contraventamento] Plano sem eixos validos");
                    return Result.Failed;
                }

                XYZ c1Projetado = RevitUtils.ProjectPointOntoPlane(pontoC1, plane);
                XYZ c2Projetado = RevitUtils.ProjectPointOntoPlane(pontoC2, plane);

                double u1 = (c1Projetado - plane.Origin).DotProduct(xVec);
                double v1 = (c1Projetado - plane.Origin).DotProduct(yVec);
                double u2 = (c2Projetado - plane.Origin).DotProduct(xVec);
                double v2 = (c2Projetado - plane.Origin).DotProduct(yVec);

                if (Math.Abs(u2 - u1) < RevitUtils.EPS || Math.Abs(v2 - v1) < RevitUtils.EPS)
                {
                    resultado.PontosInvalidos = true;
                    Logger.Warn("[Contraventamento] Pontos C1/C2 nao formam painel valido");
                    return Result.Cancelled;
                }

                XYZ canto1 = CriarPontoNoPlano(plane, xVec, yVec, u1, v1);
                XYZ canto2 = CriarPontoNoPlano(plane, xVec, yVec, u2, v2);
                XYZ canto3 = CriarPontoNoPlano(plane, xVec, yVec, u2, v1);
                XYZ canto4 = CriarPontoNoPlano(plane, xVec, yVec, u1, v2);

                XYZ normalPlano = RevitUtils.SafeNormalize(plane.Normal);
                double offsetSegundaDiagonalFt = config.OffsetSegundaDiagonalMm * RevitUtils.FT_PER_MM;

                XYZ segundaDiagonalInicio = canto4 + normalPlano.Multiply(offsetSegundaDiagonalFt);
                XYZ segundaDiagonalFim = canto3 + normalPlano.Multiply(offsetSegundaDiagonalFt);

                Level nivel = ObterNivelReferencia(doc, new[] { canto1, canto2, canto3, canto4, segundaDiagonalInicio, segundaDiagonalFim });
                if (nivel == null)
                {
                    resultado.NivelNaoEncontrado = true;
                    Logger.Warn("[Contraventamento] Nenhum nivel encontrado nas proximidades");
                    return Result.Failed;
                }

                List<ElementId> idsCriados = new List<ElementId>();

                using (TransactionGroup tg = new TransactionGroup(doc, "Criar Contraventamento no Plano"))
                {
                    tg.Start();

                    List<ElementId> idsPreview = null;
                    bool previewDisponivel = false;

                    try
                    {
                        idsPreview = CriarPreview(doc, plane, canto1, canto2, segundaDiagonalInicio, segundaDiagonalFim);
                        previewDisponivel = idsPreview.Count > 0;
                    }
                    catch
                    {
                        previewDisponivel = false;
                    }

                    if (previewDisponivel)
                    {
                        // ADR-003: ShowConfirmation foi substituido por callback opcional.
                        // Command fornece a funcao; service so pergunta resultado bool.
                        // Se callback null (test cases), confirma automatico.
                        bool confirmou = confirmarPreview?.Invoke() ?? true;

                        if (!confirmou)
                        {
                            tg.RollBack();
                            resultado.PreviewCancelado = true;
                            return Result.Cancelled;
                        }
                    }

                    using (Transaction t = new Transaction(doc, "Gerar Contraventamento no Plano"))
                    {
                        t.Start();

                        if (idsPreview != null && idsPreview.Count > 0)
                            doc.Delete(idsPreview);

                        if (!config.SymbolSelecionado.IsActive)
                        {
                            config.SymbolSelecionado.Activate();
                            doc.Regenerate();
                        }

                        FamilyInstance barraA = CriarBarra(doc, config, nivel, canto1, canto2);
                        FamilyInstance barraB = CriarBarra(doc, config, nivel, segundaDiagonalInicio, segundaDiagonalFim);

                        if (barraA != null)
                            idsCriados.Add(barraA.Id);
                        if (barraB != null)
                            idsCriados.Add(barraB.Id);

                        if (idsCriados.Count == 0)
                        {
                            t.RollBack();
                            tg.RollBack();
                            resultado.NenhumaBarraCriada = true;
                            Logger.Warn("[Contraventamento] Nenhuma barra criada apos commit attempt");
                            return Result.Cancelled;
                        }

                        t.Commit();
                    }

                    tg.Assimilate();
                }

                uidoc.Selection.SetElementIds(idsCriados);

                double larguraMm = Math.Abs(u2 - u1) / RevitUtils.FT_PER_MM;
                double alturaMm = Math.Abs(v2 - v1) / RevitUtils.FT_PER_MM;

                resultado.Sucesso = true;
                resultado.BarrasCriadas = idsCriados.Count;
                resultado.PerfilNome = config.SymbolSelecionado.FamilyName + " : " + config.SymbolSelecionado.Name;
                resultado.LarguraMm = larguraMm;
                resultado.AlturaMm = alturaMm;
                resultado.OffsetSegundaDiagonalMm = config.OffsetSegundaDiagonalMm;
                resultado.IdsBarrasCriadas.AddRange(idsCriados);

                Logger.Info(
                    "[Contraventamento] Sucesso barras={Barras} perfil={Perfil}",
                    resultado.BarrasCriadas, resultado.PerfilNome);

                return Result.Succeeded;
            }
            finally
            {
                transparencyScope?.Restore();
            }
        }

        private FamilyInstance CriarBarra(
            Document doc,
            ContraventamentoPlanoConfig config,
            Level nivel,
            XYZ inicio,
            XYZ fim)
        {
            if (inicio == null || fim == null || inicio.DistanceTo(fim) < RevitUtils.EPS)
                return null;

            Line linha = Line.CreateBound(inicio, fim);
            FamilyInstance fi = doc.Create.NewFamilyInstance(linha, config.SymbolSelecionado, nivel, StructuralType.Beam);
            if (fi == null)
                return null;

            RevitUtils.SetZJustification(fi, config.ZJustificationValue);
            RevitUtils.SetYZOffsets(fi, 0.0, 0.0);

            if (config.DesabilitarUniao)
                RevitUtils.DisallowJoins(fi);

            return fi;
        }

        private List<ElementId> CriarPreview(
            Document doc,
            Plane planeBase,
            XYZ inicioDiagonal1,
            XYZ fimDiagonal1,
            XYZ inicioDiagonal2,
            XYZ fimDiagonal2)
        {
            List<ElementId> ids = new List<ElementId>();

            using (Transaction t = new Transaction(doc, "Preview Contraventamento"))
            {
                t.Start();

                SketchPlane sketch1 = SketchPlane.Create(doc, planeBase);
                ids.Add(sketch1.Id);
                ModelCurve curva1 = doc.Create.NewModelCurve(Line.CreateBound(inicioDiagonal1, fimDiagonal1), sketch1);
                if (curva1 != null)
                    ids.Add(curva1.Id);

                Plane planeSegundaDiagonal = Plane.CreateByOriginAndBasis(
                    inicioDiagonal2,
                    RevitUtils.SafeNormalize(planeBase.XVec),
                    RevitUtils.SafeNormalize(planeBase.YVec));

                SketchPlane sketch2 = SketchPlane.Create(doc, planeSegundaDiagonal);
                ids.Add(sketch2.Id);
                ModelCurve curva2 = doc.Create.NewModelCurve(Line.CreateBound(inicioDiagonal2, fimDiagonal2), sketch2);
                if (curva2 != null)
                    ids.Add(curva2.Id);

                t.Commit();
            }

            return ids;
        }

        private XYZ CriarPontoNoPlano(Plane plane, XYZ xVec, XYZ yVec, double u, double v)
        {
            return plane.Origin + xVec.Multiply(u) + yVec.Multiply(v);
        }

        private Level ObterNivelReferencia(Document doc, IEnumerable<XYZ> pontos)
        {
            if (doc?.ActiveView?.GenLevel != null)
                return doc.ActiveView.GenLevel;

            double menorZ = pontos == null || !pontos.Any()
                ? 0.0
                : pontos.Min(p => p?.Z ?? 0.0);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - menorZ))
                .FirstOrDefault();
        }

        private sealed class TransparencyScope
        {
            private readonly Document _doc;
            private readonly View _view;
            private readonly Dictionary<long, OverrideGraphicSettings> _originalOverrides;

            private TransparencyScope(Document doc, View view, Dictionary<long, OverrideGraphicSettings> originalOverrides)
            {
                _doc = doc;
                _view = view;
                _originalOverrides = originalOverrides;
            }

            public static TransparencyScope TryApply(Document doc, View view, BuiltInCategory category, int transparency)
            {
                if (doc == null || view == null)
                    return null;

                List<ElementId> ids = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .Distinct()
                    .ToList();

                if (ids.Count == 0)
                    return null;

                Dictionary<long, OverrideGraphicSettings> originals = ids.ToDictionary(
                    id => id.Value,
                    id => new OverrideGraphicSettings(view.GetElementOverrides(id)));

                using (Transaction t = new Transaction(doc, "Aplicar transparencia temporaria"))
                {
                    t.Start();

                    foreach (ElementId id in ids)
                    {
                        OverrideGraphicSettings overrideAtual = new OverrideGraphicSettings(view.GetElementOverrides(id));
                        overrideAtual.SetSurfaceTransparency(transparency);
                        view.SetElementOverrides(id, overrideAtual);
                    }

                    t.Commit();
                }

                return new TransparencyScope(doc, view, originals);
            }

            public void Restore()
            {
                if (_doc == null || _view == null || _originalOverrides == null || _originalOverrides.Count == 0)
                    return;

                using (Transaction t = new Transaction(_doc, "Restaurar transparencia"))
                {
                    t.Start();

                    foreach (KeyValuePair<long, OverrideGraphicSettings> pair in _originalOverrides)
                    {
                        _view.SetElementOverrides(new ElementId(pair.Key), pair.Value);
                    }

                    t.Commit();
                }
            }
        }
    }
}
