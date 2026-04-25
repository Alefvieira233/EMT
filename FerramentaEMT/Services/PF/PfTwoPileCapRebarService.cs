using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.PF
{
    internal sealed class PfTwoPileCapRebarService
    {
        private const double MinSegmentMm = 50.0;

        public Result Execute(UIDocument uidoc, PfTwoPileCapRebarConfig config)
        {
            List<Element> selecionados = PfElementService.GetSelectionOrPick(
                uidoc,
                PfElementService.IsTwoPileCap,
                "Selecione os blocos de duas estacas para lancar as barras.");

            List<FamilyInstance> hosts = selecionados.OfType<FamilyInstance>().ToList();
            if (hosts.Count == 0)
            {
                AppDialogService.ShowWarning("PF - Acos Bloco 2 Estacas", "Nenhum bloco de fundacao elegivel foi selecionado.", "Selecao vazia");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            int hostsOk = 0;
            int rebarsCriados = 0;
            List<string> avisos = new List<string>();

            using (Transaction transaction = new Transaction(doc, "PF - Acos Bloco 2 Estacas"))
            {
                transaction.Start();

                foreach (FamilyInstance host in hosts)
                {
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            int created = InsertRebars(doc, host, config);
                            if (created <= 0)
                            {
                                avisos.Add($"Id {host.Id.Value}: nenhuma barra foi gerada.");
                                sub.RollBack();
                                continue;
                            }

                            hostsOk++;
                            rebarsCriados += created;
                            sub.Commit();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[PF - Acos Bloco 2 Estacas] falha no host {HostId}", host.Id.Value);
                            avisos.Add($"Id {host.Id.Value}: {CleanMessage(ex.Message)}");
                            sub.RollBack();
                        }
                    }
                }

                transaction.Commit();
            }

            uidoc.Selection.SetElementIds(hosts.Select(x => x.Id).ToList());

            string resumo =
                $"Hospedeiros processados: {hosts.Count}\n" +
                $"Hospedeiros com sucesso: {hostsOk}\n" +
                $"Armaduras criadas: {rebarsCriados}";

            if (avisos.Count > 0)
                resumo += "\n\nOcorrencias:\n- " + string.Join("\n- ", avisos.Take(10));

            AppDialogService.ShowInfo("PF - Acos Bloco 2 Estacas", resumo, "Processamento concluido");
            return hostsOk > 0 ? Result.Succeeded : Result.Failed;
        }

        private static int InsertRebars(Document doc, FamilyInstance host, PfTwoPileCapRebarConfig config)
        {
            if (!CanHostRebar(host))
                throw new InvalidOperationException("A familia selecionada nao esta habilitada para hospedar armaduras no Revit.");

            HostFrame frame = BuildFrame(host);
            double cover = ToFeetCm(Math.Max(1.0, config.CobrimentoCm));
            double x0 = frame.MinX + cover;
            double x1 = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double hookLength = config.ModoPonta == PfBeamBarEndMode.DobraInterna
                ? ToFeetCm(config.ComprimentoGanchoCm)
                : 0.0;

            if (x1 - x0 <= ToFeetMm(150.0) ||
                maxY - minY <= ToFeetMm(20.0) ||
                maxZ - minZ <= ToFeetMm(20.0))
            {
                return 0;
            }

            RebarBarType barTypeSuperior = GetBarType(doc, config.BarTypeSuperiorName);
            RebarBarType barTypeInferior = GetBarType(doc, config.BarTypeInferiorName);
            RebarBarType barTypeLateral = GetBarType(doc, config.BarTypeLateralName);

            List<double> topYs = config.QuantidadeSuperior > 0
                ? Distribute(config.QuantidadeSuperior, minY, maxY)
                : new List<double>();
            List<double> bottomYs = config.QuantidadeInferior > 0
                ? Distribute(config.QuantidadeInferior, minY, maxY)
                : new List<double>();

            ValidateMinimumBarSpacing("bloco superior", topYs.Select(y => new XYZ(0.0, y, maxZ)), barTypeSuperior);
            ValidateMinimumBarSpacing("bloco inferior", bottomYs.Select(y => new XYZ(0.0, y, minZ)), barTypeInferior);

            int created = 0;
            foreach (double y in topYs)
            {
                Rebar rebar = CreateOpenRebar(
                    doc,
                    host,
                    barTypeSuperior,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, y, maxZ, x0, x1, hookLength, config.ModoPonta == PfBeamBarEndMode.DobraInterna ? -1 : 0));
                Annotate(rebar, "Superior");
                created++;
            }

            foreach (double y in bottomYs)
            {
                Rebar rebar = CreateOpenRebar(
                    doc,
                    host,
                    barTypeInferior,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, y, minZ, x0, x1, hookLength, config.ModoPonta == PfBeamBarEndMode.DobraInterna ? 1 : 0));
                Annotate(rebar, "Inferior");
                created++;
            }

            if (config.QuantidadeLateral > 0)
            {
                List<double> levels = Distribute(config.QuantidadeLateral, minZ, maxZ);
                List<XYZ> lateralPositions = new List<XYZ>();
                foreach (double z in levels)
                {
                    lateralPositions.Add(new XYZ(0.0, minY, z));
                    lateralPositions.Add(new XYZ(0.0, maxY, z));
                }

                ValidateMinimumBarSpacing("bloco lateral", lateralPositions, barTypeLateral);

                foreach (double z in levels)
                {
                    Rebar left = CreateOpenRebar(
                        doc,
                        host,
                        barTypeLateral,
                        frame.YAxis,
                        CreateLongitudinalBar(frame, minY, z, x0, x1, 0.0, 0));
                    Annotate(left, "Lateral");
                    created++;

                    Rebar right = CreateOpenRebar(
                        doc,
                        host,
                        barTypeLateral,
                        frame.YAxis,
                        CreateLongitudinalBar(frame, maxY, z, x0, x1, 0.0, 0));
                    Annotate(right, "Lateral");
                    created++;
                }
            }

            return created;
        }

        private static HostFrame BuildFrame(FamilyInstance host)
        {
            BoundingBoxXYZ bbox = host.get_BoundingBox(null);
            if (bbox == null)
                throw new InvalidOperationException("Nao foi possivel ler a caixa envolvente do bloco.");

            double baseX = ReadLengthInternal(host, "DimensÃ£o horizontal da base", "Dimensao horizontal da base");
            double baseY = ReadLengthInternal(host, "DimensÃ£o vertical da base", "Dimensao vertical da base");
            double baseHeight = ReadLengthInternal(host, "Altura da base");
            bool hasBaseParameters = baseX > ToFeetMm(100.0) &&
                                     baseY > ToFeetMm(100.0) &&
                                     baseHeight > ToFeetMm(100.0);

            XYZ xAxis = NormalizeHorizontal(host.GetTransform().BasisX);
            if (xAxis.IsZeroLength())
                xAxis = (bbox.Max.X - bbox.Min.X) >= (bbox.Max.Y - bbox.Min.Y) ? XYZ.BasisX : XYZ.BasisY;

            XYZ yAxis = XYZ.BasisZ.CrossProduct(xAxis).Normalize();
            XYZ zAxis = XYZ.BasisZ;
            Transform localToWorld = Transform.Identity;
            localToWorld.Origin = ResolveBaseOrigin(host, bbox);
            localToWorld.BasisX = xAxis;
            localToWorld.BasisY = yAxis;
            localToWorld.BasisZ = zAxis;

            if (hasBaseParameters)
            {
                return new HostFrame(
                    localToWorld,
                    -baseX / 2.0,
                    baseX / 2.0,
                    -baseY / 2.0,
                    baseY / 2.0,
                    0.0,
                    baseHeight);
            }

            Transform inverse = localToWorld.Inverse;
            List<XYZ> corners = BoundingCorners(bbox).Select(inverse.OfPoint).ToList();
            return new HostFrame(
                localToWorld,
                corners.Min(p => p.X),
                corners.Max(p => p.X),
                corners.Min(p => p.Y),
                corners.Max(p => p.Y),
                corners.Min(p => p.Z),
                corners.Max(p => p.Z));
        }

        private static XYZ ResolveBaseOrigin(FamilyInstance host, BoundingBoxXYZ bbox)
        {
            XYZ xyOrigin = null;
            if (host.Location is LocationPoint locationPoint)
                xyOrigin = locationPoint.Point;

            if (xyOrigin == null)
            {
                xyOrigin = new XYZ(
                    (bbox.Min.X + bbox.Max.X) / 2.0,
                    (bbox.Min.Y + bbox.Max.Y) / 2.0,
                    bbox.Min.Z);
            }

            return new XYZ(xyOrigin.X, xyOrigin.Y, bbox.Min.Z);
        }

        private static IList<Curve> CreateLongitudinalBar(
            HostFrame frame,
            double y,
            double z,
            double x0,
            double x1,
            double hookLength,
            int hookDirection)
        {
            double actualHook = Math.Min(hookLength, Math.Max(0.0, (x1 - x0) / 4.0));
            List<XYZ> points = new List<XYZ>();

            if (actualHook > ToFeetMm(MinSegmentMm) && hookDirection != 0)
                points.Add(new XYZ(x0, y, z + (actualHook * hookDirection)));

            points.Add(new XYZ(x0, y, z));
            points.Add(new XYZ(x1, y, z));

            if (actualHook > ToFeetMm(MinSegmentMm) && hookDirection != 0)
                points.Add(new XYZ(x1, y, z + (actualHook * hookDirection)));

            return CreatePolyline(frame, points);
        }

        private static Rebar CreateOpenRebar(Document doc, Element host, RebarBarType barType, XYZ normal, IList<Curve> curves)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc,
                RebarStyle.Standard,
                barType,
                null,
                null,
                host,
                normal,
                curves,
                RebarHookOrientation.Right,
                RebarHookOrientation.Right,
                true,
                true);

            if (rebar == null)
                throw new InvalidOperationException("O Revit nao conseguiu gerar a barra para este bloco.");

            return rebar;
        }

        private static IList<Curve> CreatePolyline(HostFrame frame, IEnumerable<XYZ> localPoints)
        {
            List<XYZ> points = localPoints.ToList();
            List<Curve> curves = new List<Curve>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p0 = frame.LocalToWorld.OfPoint(points[i]);
                XYZ p1 = frame.LocalToWorld.OfPoint(points[i + 1]);
                if (p0.DistanceTo(p1) > ToFeetMm(1.0))
                    curves.Add(Line.CreateBound(p0, p1));
            }

            if (curves.Count == 0)
                throw new InvalidOperationException("Nao foi possivel montar a geometria da barra.");

            return curves;
        }

        private static List<double> Distribute(int count, double min, double max)
        {
            count = Math.Max(1, count);
            if (max - min <= ToFeetMm(10.0))
                return new List<double> { (min + max) / 2.0 };

            if (count == 1)
                return new List<double> { (min + max) / 2.0 };

            double step = (max - min) / (count - 1);
            return Enumerable.Range(0, count)
                .Select(i => min + (step * i))
                .ToList();
        }

        private static void ValidateMinimumBarSpacing(string context, IEnumerable<XYZ> localSectionPoints, RebarBarType barType)
        {
            List<XYZ> points = (localSectionPoints ?? Enumerable.Empty<XYZ>()).ToList();
            if (points.Count <= 1 || barType == null || barType.BarNominalDiameter <= ToFeetMm(1.0))
                return;

            double minCenterSpacing = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distance = points[i].DistanceTo(points[j]);
                    if (distance < minCenterSpacing)
                        minCenterSpacing = distance;
                }
            }

            if (double.IsInfinity(minCenterSpacing) || minCenterSpacing == double.MaxValue)
                return;

            double clearSpacing = minCenterSpacing - barType.BarNominalDiameter;
            double minimumClearSpacing = Math.Max(ToFeetMm(20.0), barType.BarNominalDiameter);
            if (clearSpacing + ToFeetMm(1.0) >= minimumClearSpacing)
                return;

            throw new InvalidOperationException(
                $"Espacamento insuficiente entre barras em {context}: livre {ToCentimeters(clearSpacing):0.#} cm; minimo adotado {ToCentimeters(minimumClearSpacing):0.#} cm.");
        }

        private static RebarBarType GetBarType(Document doc, string preferredName)
        {
            RebarBarType barType = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(x => string.Equals(x.Name, preferredName, StringComparison.CurrentCultureIgnoreCase))
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .FirstOrDefault();

            if (barType == null)
                throw new InvalidOperationException("Nenhum tipo de vergalhao foi encontrado no projeto.");

            return barType;
        }

        private static bool CanHostRebar(Element host)
        {
            return host != null && RebarHostData.GetRebarHostData(host) != null;
        }

        private static void Annotate(Rebar rebar, string label)
        {
            Parameter comments = rebar?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && !comments.IsReadOnly && comments.StorageType == StorageType.String)
                comments.Set($"PF Bloco 2 Estacas - {label}");
        }

        private static double ReadLengthInternal(FamilyInstance host, params string[] names)
        {
            Element type = host?.Document.GetElement(host.GetTypeId());
            foreach (string name in names)
            {
                double value = ReadLengthInternal(host?.LookupParameter(name));
                if (value > 0.0)
                    return value;

                value = ReadLengthInternal(type?.LookupParameter(name));
                if (value > 0.0)
                    return value;
            }

            return 0.0;
        }

        private static double ReadLengthInternal(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue)
                return 0.0;

            if (parameter.StorageType == StorageType.Double)
                return parameter.AsDouble();

            if (parameter.StorageType == StorageType.Integer)
                return ToFeetCm(parameter.AsInteger());

            string text = parameter.AsValueString() ?? parameter.AsString();
            if (string.IsNullOrWhiteSpace(text))
                return 0.0;

            text = text.Trim().Replace(',', '.');
            string numeric = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out double valueCm)
                ? ToFeetCm(valueCm)
                : 0.0;
        }

        private static XYZ NormalizeHorizontal(XYZ vector)
        {
            if (vector == null)
                return XYZ.Zero;

            XYZ horizontal = new XYZ(vector.X, vector.Y, 0.0);
            return horizontal.IsZeroLength() ? XYZ.Zero : horizontal.Normalize();
        }

        private static IEnumerable<XYZ> BoundingCorners(BoundingBoxXYZ bbox)
        {
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z);
        }

        private static double ToFeetMm(double value)
        {
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
        }

        private static double ToFeetCm(double value)
        {
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Centimeters);
        }

        private static double ToCentimeters(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Centimeters);
        }

        private static string CleanMessage(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "falha desconhecida."
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class HostFrame
        {
            public HostFrame(Transform localToWorld, double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
            {
                LocalToWorld = localToWorld;
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public Transform LocalToWorld { get; }
            public XYZ YAxis => LocalToWorld.BasisY;
            public double MinX { get; }
            public double MaxX { get; }
            public double MinY { get; }
            public double MaxY { get; }
            public double MinZ { get; }
            public double MaxZ { get; }
        }
    }
}
