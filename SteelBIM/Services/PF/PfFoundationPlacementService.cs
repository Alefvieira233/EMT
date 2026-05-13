#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models.PF;
using SteelBIM.Utils;

namespace SteelBIM.Services.PF
{
    internal sealed class PfFoundationPlacementService
    {
        internal sealed class ResultSummary
        {
            public int PilaresProcessados { get; set; }
            public int FundacoesCriadas { get; set; }
            public int PilaresSemNivel { get; set; }
            public int PilaresIgnoradosPorFundacaoExistente { get; set; }
            public int Falhas { get; set; }

            public string BuildMessage()
            {
                return
                    $"Pilares processados: {PilaresProcessados}\n" +
                    $"Fundações criadas: {FundacoesCriadas}\n" +
                    $"Ignorados por fundação existente: {PilaresIgnoradosPorFundacaoExistente}\n" +
                    $"Sem nível de inserção: {PilaresSemNivel}\n" +
                    $"Falhas: {Falhas}";
            }
        }

        public static IList<FamilySymbol> CollectFoundationSymbols(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(IsSupportedFoundationSymbol)
                .OrderBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public ResultSummary Execute(UIDocument uidoc, PfFoundationPlacementConfig config)
        {
            Document doc = uidoc.Document;
            FamilySymbol symbol = doc.GetElement(config.SymbolId) as FamilySymbol
                ?? throw new InvalidOperationException("Tipo de fundação não encontrado no documento.");

            List<FamilyInstance> pilares = CollectTargetColumns(uidoc, config);
            List<FamilyInstance> fundacoesExistentes = CollectExistingFoundations(doc);

            ResultSummary summary = new ResultSummary
            {
                PilaresProcessados = pilares.Count
            };

            using (Transaction tx = new Transaction(doc, "ECC - Lançar Fundações"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                foreach (FamilyInstance pilar in pilares)
                {
                    XYZ basePoint = GetColumnBasePoint(pilar);

                    if (config.IgnorarSeJaExisteFundacao &&
                        HasNearbyFoundation(basePoint, fundacoesExistentes, ToFeet(config.ToleranciaCentroMm)))
                    {
                        summary.PilaresIgnoradosPorFundacaoExistente++;
                        continue;
                    }

                    Level? level = ResolvePlacementLevel(doc, pilar, basePoint);
                    if (level == null)
                    {
                        summary.PilaresSemNivel++;
                        continue;
                    }

                    try
                    {
                        XYZ insertionPoint = new XYZ(basePoint.X, basePoint.Y, level.Elevation);
                        FamilyInstance instancia = doc.Create.NewFamilyInstance(
                            insertionPoint,
                            symbol,
                            level,
                            StructuralType.Footing);

                        if (config.OrientarPeloPilar)
                            TryRotateToColumn(doc, instancia, pilar, insertionPoint);

                        TrySetTraceability(instancia, pilar);
                        summary.FundacoesCriadas++;
                        fundacoesExistentes.Add(instancia);
                    }
                    catch (Exception ex)
                    {
                        summary.Falhas++;
                        Logger.Error(ex, "[PF Foundation] falha ao inserir fundação para pilar {PilarId}", pilar.Id.Value);
                    }
                }

                tx.Commit();
            }

            return summary;
        }

        private static List<FamilyInstance> CollectTargetColumns(UIDocument uidoc, PfFoundationPlacementConfig config)
        {
            Document doc = uidoc.Document;
            IEnumerable<Element> elementos;

            if (config.Escopo == PfFoundationPlacementScope.SelecaoAtual)
            {
                elementos = uidoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .Where(x => x != null);
            }
            else
            {
                View view = doc.ActiveView ?? throw new InvalidOperationException("Não há vista ativa.");
                elementos = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }

            return elementos
                .OfType<FamilyInstance>()
                .Where(PfElementService.IsStructuralColumn)
                .GroupBy(x => x.Id.Value)
                .Select(g => g.First())
                .ToList();
        }

        private static List<FamilyInstance> CollectExistingFoundations(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .ToList();
        }

        private static bool IsSupportedFoundationSymbol(FamilySymbol symbol)
        {
            if (symbol?.Category?.BuiltInCategory != BuiltInCategory.OST_StructuralFoundation)
                return false;

            FamilyPlacementType placementType = symbol.Family?.FamilyPlacementType ?? FamilyPlacementType.Invalid;
            return placementType == FamilyPlacementType.OneLevelBased ||
                   placementType == FamilyPlacementType.OneLevelBasedHosted;
        }

        private static XYZ GetColumnBasePoint(FamilyInstance column)
        {
            if (column.Location is LocationPoint lp)
                return lp.Point;

            if (column.Location is LocationCurve lc && lc.Curve != null)
            {
                XYZ p0 = lc.Curve.GetEndPoint(0);
                XYZ p1 = lc.Curve.GetEndPoint(1);
                return p0.Z <= p1.Z ? p0 : p1;
            }

            BoundingBoxXYZ? bb = column.get_BoundingBox(null);
            if (bb != null)
            {
                return new XYZ(
                    (bb.Min.X + bb.Max.X) * 0.5,
                    (bb.Min.Y + bb.Max.Y) * 0.5,
                    bb.Min.Z);
            }

            return XYZ.Zero;
        }

        private static Level? ResolvePlacementLevel(Document doc, FamilyInstance column, XYZ basePoint)
        {
            Level? direct = RevitUtils.GetElementLevel(doc, column);
            if (direct != null)
                return direct;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - basePoint.Z))
                .FirstOrDefault();
        }

        private static bool HasNearbyFoundation(XYZ basePoint, IEnumerable<FamilyInstance> foundations, double toleranceFt)
        {
            foreach (FamilyInstance foundation in foundations)
            {
                XYZ center = GetFoundationCenter(foundation);
                double dx = center.X - basePoint.X;
                double dy = center.Y - basePoint.Y;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));
                if (dist <= toleranceFt)
                    return true;
            }

            return false;
        }

        private static XYZ GetFoundationCenter(FamilyInstance foundation)
        {
            if (foundation.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ? bb = foundation.get_BoundingBox(null);
            if (bb != null)
                return (bb.Min + bb.Max) * 0.5;

            return XYZ.Zero;
        }

        private static void TryRotateToColumn(Document doc, FamilyInstance foundation, FamilyInstance column, XYZ insertionPoint)
        {
            double? angle = TryGetColumnRotationAngle(column);
            if (!angle.HasValue || Math.Abs(angle.Value) < 1e-9)
                return;

            Line axis = Line.CreateBound(insertionPoint, insertionPoint + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(doc, foundation.Id, axis, angle.Value);
        }

        private static double? TryGetColumnRotationAngle(FamilyInstance column)
        {
            XYZ basis = column.GetTransform().BasisX;
            XYZ horizontal = new XYZ(basis.X, basis.Y, 0.0);
            if (horizontal.IsZeroLength())
                return null;

            XYZ dir = horizontal.Normalize();
            return Math.Atan2(dir.Y, dir.X);
        }

        private static void TrySetTraceability(FamilyInstance foundation, FamilyInstance column)
        {
            Parameter? comments = foundation.LookupParameter("Comments");
            if (comments != null && !comments.IsReadOnly && comments.StorageType == StorageType.String)
                comments.Set($"EMT_FUNDACAO_PILAR|Pilar:{column.Id.Value}");

            Parameter? hostParam = foundation.LookupParameter("EMT_Pilar_Base");
            if (hostParam != null && !hostParam.IsReadOnly && hostParam.StorageType == StorageType.ElementId)
                hostParam.Set(column.Id);
        }

        private static double ToFeet(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }
    }
}
