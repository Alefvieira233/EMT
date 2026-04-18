using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace FerramentaEMT.Services
{
    internal sealed class AjustarEncontroService
    {
        internal sealed class AjustarEncontroResultado
        {
            internal AjustarEncontroResultado(bool houveAlteracao, List<string> diagnostico)
            {
                HouveAlteracao = houveAlteracao;
                Diagnostico = diagnostico ?? new List<string>();
            }

            internal bool HouveAlteracao { get; }

            internal List<string> Diagnostico { get; }
        }

        internal AjustarEncontroResultado Executar(
            Document doc,
            FamilyInstance vigaPrincipal,
            Element cortador,
            Reference referenciaCortador)
        {
            List<string> diagnostico = new List<string>();
            bool houveAlteracao = false;

            int extremidadePrincipal = ObterExtremidadeMaisProxima(vigaPrincipal, cortador, referenciaCortador);
            houveAlteracao |= GarantirJoinPermitido(vigaPrincipal, extremidadePrincipal, "viga principal", diagnostico);

            if (cortador is FamilyInstance fiCortador && EhVigaEstrutural(fiCortador))
            {
                int extremidadeCortador = ObterExtremidadeMaisProxima(fiCortador, vigaPrincipal, null);
                houveAlteracao |= GarantirJoinPermitido(fiCortador, extremidadeCortador, "viga de encontro", diagnostico);
            }

            doc.Regenerate();

            if (TryJoinGeometry(doc, vigaPrincipal, cortador, diagnostico))
            {
                houveAlteracao = true;
                doc.Regenerate();
            }

            if (TryPriorizarElementoDeEncontro(doc, vigaPrincipal, cortador, diagnostico))
            {
                houveAlteracao = true;
                doc.Regenerate();
            }

            if (TryDefinirReferenciaDeExtremidade(vigaPrincipal, extremidadePrincipal, referenciaCortador, diagnostico))
            {
                houveAlteracao = true;
                doc.Regenerate();
            }

            if (TryAplicarCoping(vigaPrincipal, cortador, diagnostico))
                houveAlteracao = true;

            return new AjustarEncontroResultado(houveAlteracao, diagnostico);
        }

        internal static bool EhCortadorValido(Element elemento)
        {
            if (elemento is not FamilyInstance fi || fi.Category == null)
                return false;

            long categoryId = fi.Category.Id.Value;
            return categoryId == (long)BuiltInCategory.OST_StructuralFraming ||
                   categoryId == (long)BuiltInCategory.OST_StructuralColumns;
        }

        private static bool GarantirJoinPermitido(
            FamilyInstance instancia,
            int extremidade,
            string descricao,
            List<string> diagnostico)
        {
            if (instancia == null)
                return false;

            if (StructuralFramingUtils.IsJoinAllowedAtEnd(instancia, extremidade))
            {
                diagnostico.Add($"Extremidade {extremidade} da {descricao} ja permitia uniao.");
                return false;
            }

            StructuralFramingUtils.AllowJoinAtEnd(instancia, extremidade);
            diagnostico.Add($"Extremidade {extremidade} da {descricao} liberada para uniao.");
            return true;
        }

        private static bool TryJoinGeometry(Document doc, Element vigaPrincipal, Element cortador, List<string> diagnostico)
        {
            try
            {
                if (!JoinGeometryUtils.AreElementsJoined(doc, vigaPrincipal, cortador))
                {
                    JoinGeometryUtils.JoinGeometry(doc, vigaPrincipal, cortador);
                    diagnostico.Add("Uniao geometrica criada.");
                    return true;
                }

                diagnostico.Add("Os elementos ja estavam unidos geometricamente.");
                return false;
            }
            catch (Exception ex)
            {
                diagnostico.Add($"JoinGeometry falhou: {ex.Message}");
                return false;
            }
        }

        private static bool TryPriorizarElementoDeEncontro(
            Document doc,
            Element vigaPrincipal,
            Element cortador,
            List<string> diagnostico)
        {
            if (!JoinGeometryUtils.AreElementsJoined(doc, vigaPrincipal, cortador))
            {
                diagnostico.Add("A ordem de corte nao foi ajustada porque os elementos nao estao unidos.");
                return false;
            }

            try
            {
                if (JoinGeometryUtils.IsCuttingElementInJoin(doc, cortador, vigaPrincipal))
                {
                    diagnostico.Add("O elemento de encontro ja estava priorizado no corte.");
                    return false;
                }

                JoinGeometryUtils.SwitchJoinOrder(doc, vigaPrincipal, cortador);
                diagnostico.Add("Ordem da uniao geometrica invertida para priorizar o elemento de encontro.");
                return true;
            }
            catch (Exception ex)
            {
                diagnostico.Add($"Nao foi possivel inverter a ordem da uniao: {ex.Message}");
                return false;
            }
        }

        private static bool TryDefinirReferenciaDeExtremidade(
            FamilyInstance vigaPrincipal,
            int extremidade,
            Reference referenciaCortador,
            List<string> diagnostico)
        {
            if (referenciaCortador == null)
                return false;

            try
            {
                if (!StructuralFramingUtils.CanSetEndReference(vigaPrincipal, extremidade))
                {
                    diagnostico.Add($"A extremidade {extremidade} da viga principal nao aceita referencia de extremidade.");
                    return false;
                }

                if (!StructuralFramingUtils.IsEndReferenceValid(vigaPrincipal, extremidade, referenciaCortador))
                {
                    diagnostico.Add($"A referencia selecionada nao e valida para a extremidade {extremidade}.");
                    return false;
                }

                StructuralFramingUtils.SetEndReference(vigaPrincipal, extremidade, referenciaCortador);
                diagnostico.Add($"Referencia de extremidade {extremidade} aplicada na viga principal.");
                return true;
            }
            catch (Exception ex)
            {
                diagnostico.Add($"SetEndReference falhou: {ex.Message}");
                return false;
            }
        }

        private static bool TryAplicarCoping(FamilyInstance vigaPrincipal, Element cortador, List<string> diagnostico)
        {
            if (cortador is not FamilyInstance fiCortador)
            {
                diagnostico.Add("Coping nao aplicado porque o elemento de encontro nao e uma familia estrutural compativel.");
                return false;
            }

            try
            {
                if (vigaPrincipal.AddCoping(fiCortador))
                {
                    diagnostico.Add("Coping aplicado na viga principal.");
                    return true;
                }

                diagnostico.Add("Coping nao retornou alteracao.");
                return false;
            }
            catch (Exception ex)
            {
                diagnostico.Add($"AddCoping falhou: {ex.Message}");
                return false;
            }
        }

        private static int ObterExtremidadeMaisProxima(FamilyInstance viga, Element outroElemento, Reference referencia)
        {
            Curve curva = (viga.Location as LocationCurve)?.Curve;
            if (curva == null)
                return 1;

            XYZ alvo = ObterPontoDeComparacao(outroElemento, referencia);
            if (alvo == null)
                return 1;

            XYZ p0 = curva.GetEndPoint(0);
            XYZ p1 = curva.GetEndPoint(1);

            return p0.DistanceTo(alvo) <= p1.DistanceTo(alvo) ? 0 : 1;
        }

        private static XYZ ObterPontoDeComparacao(Element elemento, Reference referencia)
        {
            if (referencia?.GlobalPoint != null)
                return referencia.GlobalPoint;

            if (elemento == null)
                return null;

            if (elemento.Location is LocationCurve lc && lc.Curve != null)
            {
                try
                {
                    return lc.Curve.Evaluate(0.5, true);
                }
                catch
                {
                }
            }

            if (elemento.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ bbox = elemento.get_BoundingBox(null);
            return bbox == null ? null : (bbox.Min + bbox.Max) * 0.5;
        }

        private static bool EhVigaEstrutural(FamilyInstance instancia) =>
            instancia != null &&
            instancia.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming &&
            instancia.StructuralType == StructuralType.Beam;
    }
}
