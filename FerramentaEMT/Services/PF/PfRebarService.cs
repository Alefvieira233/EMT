using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services.PF
{
    internal sealed class PfRebarService
    {
        private const double DefaultCoverMm = 30.0;
        private const double MinSegmentMm = 50.0;
        private const double MaxSupportZoneMm = 1000.0;

        public Result ExecuteColumnStirrups(UIDocument uidoc, PfColumnStirrupsConfig config)
        {
            return ExecuteForHosts(
                uidoc,
                "PF - Estribos Pilar",
                PfElementService.IsStructuralColumn,
                "Selecione os pilares estruturais para lançar os estribos.",
                host => InsertColumnStirrups(uidoc.Document, host, config));
        }

        public Result ExecuteColumnBars(UIDocument uidoc, PfColumnBarsConfig config)
        {
            return ExecuteForHosts(
                uidoc,
                "PF - Acos Pilar",
                PfElementService.IsStructuralColumn,
                "Selecione os pilares estruturais para lançar as barras longitudinais.",
                host => InsertColumnBars(uidoc.Document, host, config));
        }

        public Result ExecuteBeamStirrups(UIDocument uidoc, PfBeamStirrupsConfig config)
        {
            return ExecuteForHosts(
                uidoc,
                "PF - Estribos Viga",
                PfElementService.IsStructuralBeam,
                "Selecione as vigas estruturais para lançar os estribos.",
                host => InsertBeamStirrups(uidoc.Document, host, config));
        }

        public Result ExecuteBeamBars(UIDocument uidoc, PfBeamBarsConfig config)
        {
            return ExecuteForHosts(
                uidoc,
                "PF - Acos Viga",
                PfElementService.IsStructuralBeam,
                "Selecione as vigas estruturais para lançar as barras.",
                host => InsertBeamBars(uidoc.Document, host, config));
        }

        public Result ExecuteConsoloBars(UIDocument uidoc, PfConsoloRebarConfig config)
        {
            return ExecuteForHosts(
                uidoc,
                "PF - Acos Consolo",
                PfElementService.IsPfConsolo,
                "Selecione os consolos PF para lançar a armadura base.",
                host => InsertConsoloBars(uidoc.Document, host, config));
        }

        private Result ExecuteForHosts(
            UIDocument uidoc,
            string commandName,
            Func<Element, bool> predicate,
            string prompt,
            Func<FamilyInstance, int> processor)
        {
            List<Element> selecionados = PfElementService.GetSelectionOrPick(uidoc, predicate, prompt);
            List<FamilyInstance> hosts = selecionados.OfType<FamilyInstance>().ToList();
            if (hosts.Count == 0)
            {
                AppDialogService.ShowWarning(commandName, "Nenhum elemento elegível foi selecionado.", "Seleção vazia");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            int hostsOk = 0;
            int rebarsCriados = 0;
            List<string> avisos = new List<string>();

            using (Transaction transaction = new Transaction(doc, commandName))
            {
                transaction.Start();

                foreach (FamilyInstance host in hosts)
                {
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            if (!CanHostRebar(host))
                            {
                                avisos.Add($"Id {host.Id.Value}: hospedeiro não aceita armadura.");
                                sub.RollBack();
                                continue;
                            }

                            int criadosNoHost = processor(host);
                            if (criadosNoHost <= 0)
                            {
                                avisos.Add($"Id {host.Id.Value}: nenhuma barra foi gerada.");
                                sub.RollBack();
                                continue;
                            }

                            hostsOk++;
                            rebarsCriados += criadosNoHost;
                            sub.Commit();
                        }
                        catch (Exception ex)
                        {
                            avisos.Add($"Id {host.Id.Value}: {LimparMensagem(ex.Message)}");
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
                resumo += "\n\nOcorrências:\n• " + string.Join("\n• ", avisos.Take(10));

            AppDialogService.ShowInfo(commandName, resumo, "Processamento concluído");
            return hostsOk > 0 ? Result.Succeeded : Result.Failed;
        }

        private int InsertColumnStirrups(Document doc, FamilyInstance column, PfColumnStirrupsConfig config)
        {
            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            HostFrame frame = BuildColumnFrame(column);
            double cover = GetCover(config.CobrimentoCm);
            double minX = frame.MinX + cover;
            double maxX = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double clearHeight = maxZ - minZ;
            if (maxX - minX <= ToFeetMm(20) || maxY - minY <= ToFeetMm(20) || clearHeight <= ToFeetMm(100))
                return 0;

            double zoneHeight = Math.Min(ToFeetCm(config.AlturaZonaExtremidadeCm), clearHeight / 2.0);
            double middleHeight = Math.Max(ToFeetMm(50), clearHeight - (zoneHeight * 2.0));
            int created = 0;

            if (config.EspacamentoInferiorCm > 0 && zoneHeight > ToFeetMm(10))
            {
                Rebar lower = CreateClosedRebar(
                    doc,
                    column,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.ZAxis,
                    CreateRectangleLoopHorizontal(frame, minZ, minX, maxX, minY, maxY));
                ApplyMaximumSpacingLayout(lower, ToFeetCm(config.EspacamentoInferiorCm), zoneHeight);
                created++;
            }

            if (config.EspacamentoCentralCm > 0 && middleHeight > ToFeetMm(10))
            {
                Rebar middle = CreateClosedRebar(
                    doc,
                    column,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.ZAxis,
                    CreateRectangleLoopHorizontal(frame, minZ + zoneHeight, minX, maxX, minY, maxY));
                ApplyMaximumSpacingLayout(middle, ToFeetCm(config.EspacamentoCentralCm), middleHeight);
                created++;
            }

            if (config.EspacamentoSuperiorCm > 0 && zoneHeight > ToFeetMm(10))
            {
                Rebar upper = CreateClosedRebar(
                    doc,
                    column,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.ZAxis,
                    CreateRectangleLoopHorizontal(frame, maxZ - zoneHeight, minX, maxX, minY, maxY));
                ApplyMaximumSpacingLayout(upper, ToFeetCm(config.EspacamentoSuperiorCm), zoneHeight);
                created++;
            }

            return created;
        }

        private int InsertColumnBars(Document doc, FamilyInstance column, PfColumnBarsConfig config)
        {
            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            HostFrame frame = BuildColumnFrame(column);
            double cover = GetCover(config.CobrimentoCm);
            double minX = frame.MinX + cover;
            double maxX = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double z0 = frame.MinZ + cover;
            double z1 = frame.MaxZ - cover;
            if (z1 - z0 <= ToFeetMm(100) || maxX - minX <= ToFeetMm(20) || maxY - minY <= ToFeetMm(20))
                return 0;

            List<XYZ> offsets = BuildPerimeterPositions(
                DistributePositions(config.QuantidadeLargura, minX, maxX),
                DistributePositions(config.QuantidadeProfundidade, minY, maxY));
            int created = 0;
            foreach (XYZ offset in offsets)
            {
                IList<Curve> curves = CreateVerticalBar(frame, offset.Y, offset.X, z0, z1);
                CreateOpenRebar(doc, column, RebarStyle.Standard, barType, frame.XAxis, curves);
                created++;
            }

            return created;
        }

        private int InsertBeamStirrups(Document doc, FamilyInstance beam, PfBeamStirrupsConfig config)
        {
            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            HostFrame frame = BuildBeamFrame(beam);
            double cover = GetCover(config.CobrimentoCm);
            double minX = frame.MinX + cover;
            double maxX = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double clearLength = maxX - minX;
            if (maxY - minY <= ToFeetMm(20) || maxZ - minZ <= ToFeetMm(20) || clearLength <= ToFeetMm(150))
                return 0;

            double supportLength = Math.Min(clearLength / 2.0, ToFeetCm(config.ComprimentoZonaApoioCm));
            double middleLength = Math.Max(ToFeetMm(50), clearLength - (supportLength * 2.0));
            int created = 0;

            if (config.EspacamentoApoioCm > 0 && supportLength > ToFeetMm(10))
            {
                Rebar startSet = CreateClosedRebar(
                    doc,
                    beam,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.XAxis,
                    CreateRectangleLoopVertical(frame, minX, minY, maxY, minZ, maxZ));
                ApplyMaximumSpacingLayout(startSet, ToFeetCm(config.EspacamentoApoioCm), supportLength);
                created++;
            }

            if (config.EspacamentoCentralCm > 0 && middleLength > ToFeetMm(10))
            {
                Rebar middleSet = CreateClosedRebar(
                    doc,
                    beam,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.XAxis,
                    CreateRectangleLoopVertical(frame, minX + supportLength, minY, maxY, minZ, maxZ));
                ApplyMaximumSpacingLayout(middleSet, ToFeetCm(config.EspacamentoCentralCm), middleLength);
                created++;
            }

            if (config.EspacamentoApoioCm > 0 && supportLength > ToFeetMm(10))
            {
                Rebar endSet = CreateClosedRebar(
                    doc,
                    beam,
                    RebarStyle.StirrupTie,
                    barType,
                    frame.XAxis,
                    CreateRectangleLoopVertical(frame, maxX - supportLength, minY, maxY, minZ, maxZ));
                ApplyMaximumSpacingLayout(endSet, ToFeetCm(config.EspacamentoApoioCm), supportLength);
                created++;
            }

            return created;
        }

        private int InsertBeamBars(Document doc, FamilyInstance beam, PfBeamBarsConfig config)
        {
            HostFrame frame = BuildBeamFrame(beam);
            RebarBarType barTypeSuperior = GetBarType(doc, config.BarTypeSuperiorName);
            RebarBarType barTypeInferior = GetBarType(doc, config.BarTypeInferiorName);
            RebarBarType barTypeLateral = GetBarType(doc, config.BarTypeLateralName);

            double cover = GetCover(config.CobrimentoCm);
            double x0 = frame.MinX + cover;
            double x1 = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double clearLength = x1 - x0;
            double hookLength = config.ModoPonta == PfBeamBarEndMode.DobraInterna
                ? ToFeetCm(config.ComprimentoGanchoCm)
                : 0.0;
            if (clearLength <= ToFeetMm(150) || maxY - minY <= ToFeetMm(20) || maxZ - minZ <= ToFeetMm(20))
                return 0;

            int created = 0;

            foreach (double y in DistributePositions(config.QuantidadeSuperior, minY, maxY))
            {
                CreateOpenRebar(
                    doc,
                    beam,
                    RebarStyle.Standard,
                    barTypeSuperior,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, y, maxZ, x0, x1, hookLength, config.ModoPonta == PfBeamBarEndMode.DobraInterna ? -1 : 0));
                created++;
            }

            foreach (double y in DistributePositions(config.QuantidadeInferior, minY, maxY))
            {
                CreateOpenRebar(
                    doc,
                    beam,
                    RebarStyle.Standard,
                    barTypeInferior,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, y, minZ, x0, x1, hookLength, config.ModoPonta == PfBeamBarEndMode.DobraInterna ? 1 : 0));
                created++;
            }

            if (config.QuantidadeLateral > 0)
            {
                List<double> levels = DistributePositions(config.QuantidadeLateral, minZ, maxZ);

                foreach (double z in levels)
                {
                    CreateOpenRebar(
                        doc,
                        beam,
                        RebarStyle.Standard,
                        barTypeLateral,
                        frame.YAxis,
                        CreateLongitudinalBar(frame, minY, z, x0, x1, 0.0, 0));
                    CreateOpenRebar(
                        doc,
                        beam,
                        RebarStyle.Standard,
                        barTypeLateral,
                        frame.YAxis,
                        CreateLongitudinalBar(frame, maxY, z, x0, x1, 0.0, 0));
                    created += 2;
                }
            }

            return created;
        }

        private int InsertConsoloBars(Document doc, FamilyInstance consolo, PfConsoloRebarConfig config)
        {
            HostFrame frame = BuildConsoloFrame(consolo);
            RebarBarType tiranteType = GetBarType(doc, config.BarTypeTiranteName);
            RebarBarType suspensaoType = GetBarType(doc, config.BarTypeSuspensaoName);
            RebarBarType estriboVerticalType = GetBarType(doc, config.BarTypeEstriboVerticalName);
            RebarBarType estriboHorizontalType = GetBarType(doc, config.BarTypeEstriboHorizontalName);

            double cover = GetCover(DefaultCoverMm / 10.0);
            double x0 = frame.MinX + cover;
            double x1 = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double zTop = maxZ;
            int created = 0;

            if (config.NumeroTirantes > 0)
            {
                Rebar tirantes = CreateOpenRebar(
                    doc,
                    consolo,
                    RebarStyle.Standard,
                    tiranteType,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, (minY + maxY) / 2.0, zTop, x0, Math.Min(x1, x0 + ToFeetCm(config.ComprimentoTiranteCm)), 0.0, 0));
                ApplyLinearLayout(tirantes, config.NumeroTirantes, maxY - minY);
                created++;
            }

            if (config.NumeroSuspensoes > 0)
            {
                double xFront = Math.Min(x1, frame.MinX + ToFeetCm(config.ComprimentoSuspensaoCm));
                Rebar suspensoes = CreateOpenRebar(
                    doc,
                    consolo,
                    RebarStyle.Standard,
                    suspensaoType,
                    frame.YAxis,
                    CreateVerticalBar(frame, (minY + maxY) / 2.0, xFront, minZ, maxZ));
                ApplyLinearLayout(suspensoes, config.NumeroSuspensoes, maxY - minY);
                created++;
            }

            if (config.QuantidadeEstribosVerticais > 0)
            {
                Rebar verticalSet = CreateClosedRebar(
                    doc,
                    consolo,
                    RebarStyle.StirrupTie,
                    estriboVerticalType,
                    frame.XAxis,
                    CreateRectangleLoopVertical(frame, frame.MinX + cover, minY, maxY, minZ, maxZ));
                ApplyLinearLayout(verticalSet, config.QuantidadeEstribosVerticais, x1 - x0);
                created++;
            }

            if (config.QuantidadeEstribosHorizontais > 0)
            {
                Rebar horizontalSet = CreateClosedRebar(
                    doc,
                    consolo,
                    RebarStyle.StirrupTie,
                    estriboHorizontalType,
                    frame.ZAxis,
                    CreateRectangleLoopHorizontal(frame, minZ, x0, x1, minY, maxY));
                ApplyLinearLayout(horizontalSet, config.QuantidadeEstribosHorizontais, maxZ - minZ);
                created++;
            }

            return created;
        }

        private static bool CanHostRebar(Element host)
        {
            return host != null && RebarHostData.GetRebarHostData(host) != null;
        }

        private static RebarBarType GetBarType(Document doc, string name)
        {
            RebarBarType barType = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase))
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .FirstOrDefault();

            if (barType == null)
                throw new InvalidOperationException("Nenhum tipo de vergalhão foi encontrado no projeto.");

            return barType;
        }

        private static double GetCover(double coverCm)
        {
            return ToFeetCm(Math.Max(1.0, coverCm));
        }

        private static Rebar CreateClosedRebar(
            Document doc,
            Element host,
            RebarStyle style,
            RebarBarType barType,
            XYZ normal,
            IList<Curve> curves)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc,
                style,
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
                throw new InvalidOperationException("O Revit não conseguiu gerar a armadura fechada para este hospedeiro.");

            return rebar;
        }

        private static Rebar CreateOpenRebar(
            Document doc,
            Element host,
            RebarStyle style,
            RebarBarType barType,
            XYZ normal,
            IList<Curve> curves)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc,
                style,
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
                throw new InvalidOperationException("O Revit não conseguiu gerar a armadura aberta para este hospedeiro.");

            return rebar;
        }

        private static void ApplyLinearLayout(Rebar rebar, int count, double pathLength)
        {
            if (rebar == null || count <= 1 || pathLength <= ToFeetMm(5))
                return;

            double spacing = pathLength / Math.Max(1, count - 1);
            if (spacing <= 0)
                return;

            rebar.GetShapeDrivenAccessor()
                .SetLayoutAsNumberWithSpacing(count, spacing, true, true, true);
        }

        private static void ApplyMaximumSpacingLayout(Rebar rebar, double spacing, double pathLength)
        {
            if (rebar == null || spacing <= ToFeetMm(1) || pathLength <= ToFeetMm(5))
                return;

            try
            {
                rebar.GetShapeDrivenAccessor()
                    .SetLayoutAsMaximumSpacing(spacing, pathLength, true, true, true);
            }
            catch
            {
                int count = Math.Max(2, (int)Math.Floor(pathLength / spacing) + 1);
                double realSpacing = pathLength / Math.Max(1, count - 1);
                rebar.GetShapeDrivenAccessor()
                    .SetLayoutAsNumberWithSpacing(count, realSpacing, true, true, true);
            }
        }

        private static HostFrame BuildColumnFrame(FamilyInstance column)
        {
            XYZ zAxis = XYZ.BasisZ;
            XYZ xAxis = NormalizeHorizontal(column.HandOrientation);
            if (xAxis.IsZeroLength())
                xAxis = NormalizeHorizontal(column.GetTransform().BasisX);
            if (xAxis.IsZeroLength())
                xAxis = XYZ.BasisX;

            XYZ yAxis = NormalizeHorizontal(column.FacingOrientation);
            if (yAxis.IsZeroLength() || Math.Abs(yAxis.DotProduct(xAxis)) > 0.95)
                yAxis = zAxis.CrossProduct(xAxis);
            if (yAxis.IsZeroLength())
                yAxis = XYZ.BasisY;

            XYZ origin = GetColumnOrigin(column);
            Transform localToWorld = CreateTransform(origin, xAxis, yAxis, zAxis);
            LocalBounds bounds = ComputeBounds(column, localToWorld.Inverse);
            return new HostFrame(localToWorld, bounds);
        }

        private static HostFrame BuildBeamFrame(FamilyInstance beam)
        {
            if (!(beam.Location is LocationCurve lc) || lc.Curve == null)
                throw new InvalidOperationException("A viga precisa ter uma LocationCurve válida.");

            XYZ start = lc.Curve.GetEndPoint(0);
            XYZ end = lc.Curve.GetEndPoint(1);
            XYZ xAxis = (end - start).Normalize();
            XYZ zAxis = XYZ.BasisZ;
            if (Math.Abs(xAxis.DotProduct(zAxis)) > 0.95)
                zAxis = NormalizeHorizontal(beam.HandOrientation).IsZeroLength() ? XYZ.BasisY : NormalizeHorizontal(beam.HandOrientation);

            XYZ yAxis = zAxis.CrossProduct(xAxis);
            if (yAxis.IsZeroLength())
                yAxis = NormalizeHorizontal(beam.HandOrientation);
            if (yAxis.IsZeroLength())
                yAxis = XYZ.BasisY;

            zAxis = xAxis.CrossProduct(yAxis).Normalize();
            if (zAxis.IsZeroLength())
                zAxis = XYZ.BasisZ;

            Transform localToWorld = CreateTransform(start, xAxis, yAxis, zAxis);
            LocalBounds bounds = ComputeBounds(beam, localToWorld.Inverse);
            return new HostFrame(localToWorld, bounds);
        }

        private static HostFrame BuildConsoloFrame(FamilyInstance consolo)
        {
            Transform baseTransform = consolo.GetTransform();
            XYZ xAxis = Normalize(baseTransform.BasisX, XYZ.BasisX);
            XYZ yAxis = Normalize(baseTransform.BasisY, XYZ.BasisY);
            XYZ zAxis = Normalize(baseTransform.BasisZ, XYZ.BasisZ);
            Transform localToWorld = CreateTransform(baseTransform.Origin, xAxis, yAxis, zAxis);
            LocalBounds bounds = ComputeBounds(consolo, localToWorld.Inverse);
            return new HostFrame(localToWorld, bounds);
        }

        private static XYZ GetColumnOrigin(FamilyInstance column)
        {
            if (column.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            return bbox == null ? XYZ.Zero : (bbox.Min + bbox.Max) / 2.0;
        }

        private static Transform CreateTransform(XYZ origin, XYZ xAxis, XYZ yAxis, XYZ zAxis)
        {
            Transform transform = Transform.Identity;
            transform.Origin = origin;
            transform.BasisX = Normalize(xAxis, XYZ.BasisX);
            transform.BasisY = Normalize(yAxis, XYZ.BasisY);
            transform.BasisZ = Normalize(zAxis, XYZ.BasisZ);
            return transform;
        }

        private static LocalBounds ComputeBounds(Element element, Transform worldToLocal)
        {
            List<XYZ> points = new List<XYZ>();
            Options options = new Options
            {
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            CollectPoints(element.get_Geometry(options), Transform.Identity, worldToLocal, points);

            if (points.Count == 0)
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    foreach (XYZ corner in GetBoundingBoxCorners(bbox))
                        points.Add(worldToLocal.OfPoint(corner));
                }
            }

            if (points.Count == 0)
                throw new InvalidOperationException("Não foi possível determinar a geometria do hospedeiro.");

            return new LocalBounds(
                points.Min(x => x.X),
                points.Max(x => x.X),
                points.Min(x => x.Y),
                points.Max(x => x.Y),
                points.Min(x => x.Z),
                points.Max(x => x.Z));
        }

        private static void CollectPoints(
            GeometryElement geometry,
            Transform current,
            Transform worldToLocal,
            List<XYZ> points)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject obj in geometry)
            {
                if (obj is GeometryInstance instance)
                {
                    // GetSymbolGeometry() retorna geometria no espaço local do símbolo;
                    // current.Multiply(instance.Transform) a converte para espaço mundo.
                    // Usar GetInstanceGeometry() aqui seria double-transform (já está em mundo).
                    CollectPoints(instance.GetSymbolGeometry(), current.Multiply(instance.Transform), worldToLocal, points);
                    continue;
                }

                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        foreach (XYZ pt in edge.Tessellate())
                            points.Add(worldToLocal.OfPoint(current.OfPoint(pt)));
                    }
                    continue;
                }

                if (obj is Curve curve)
                {
                    foreach (XYZ pt in curve.Tessellate())
                        points.Add(worldToLocal.OfPoint(current.OfPoint(pt)));
                }
            }
        }

        private static IEnumerable<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ bbox)
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

        private static IList<Curve> CreateRectangleLoopHorizontal(HostFrame frame, double z, double minX, double maxX, double minY, double maxY)
        {
            return CreateRectangleLoop(
                frame,
                new XYZ(minX, minY, z),
                new XYZ(maxX, minY, z),
                new XYZ(maxX, maxY, z),
                new XYZ(minX, maxY, z));
        }

        private static IList<Curve> CreateRectangleLoopVertical(HostFrame frame, double x, double minY, double maxY, double minZ, double maxZ)
        {
            return CreateRectangleLoop(
                frame,
                new XYZ(x, minY, minZ),
                new XYZ(x, maxY, minZ),
                new XYZ(x, maxY, maxZ),
                new XYZ(x, minY, maxZ));
        }

        private static IList<Curve> CreateRectangleLoop(HostFrame frame, XYZ p1, XYZ p2, XYZ p3, XYZ p4)
        {
            XYZ w1 = frame.LocalToWorld.OfPoint(p1);
            XYZ w2 = frame.LocalToWorld.OfPoint(p2);
            XYZ w3 = frame.LocalToWorld.OfPoint(p3);
            XYZ w4 = frame.LocalToWorld.OfPoint(p4);

            return new List<Curve>
            {
                Line.CreateBound(w1, w2),
                Line.CreateBound(w2, w3),
                Line.CreateBound(w3, w4),
                Line.CreateBound(w4, w1)
            };
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

        private static IList<Curve> CreateVerticalBar(
            HostFrame frame,
            double y,
            double x,
            double z0,
            double z1)
        {
            return CreatePolyline(
                frame,
                new[]
                {
                    new XYZ(x, y, z0),
                    new XYZ(x, y, z1)
                });
        }

        private static IList<Curve> CreatePolyline(HostFrame frame, IEnumerable<XYZ> localPoints)
        {
            List<XYZ> points = localPoints.ToList();
            List<Curve> curves = new List<Curve>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p0 = frame.LocalToWorld.OfPoint(points[i]);
                XYZ p1 = frame.LocalToWorld.OfPoint(points[i + 1]);
                if (p0.DistanceTo(p1) > ToFeetMm(1))
                    curves.Add(Line.CreateBound(p0, p1));
            }

            if (curves.Count == 0)
                throw new InvalidOperationException("Não foi possível montar a geometria da barra.");

            return curves;
        }

        private static List<XYZ> BuildPerimeterPositions(List<double> xs, List<double> ys)
        {
            Dictionary<string, XYZ> unique = new Dictionary<string, XYZ>(StringComparer.OrdinalIgnoreCase);
            if (xs.Count == 0 || ys.Count == 0)
                return new List<XYZ>();

            double bottom = ys.First();
            double top = ys.Last();
            double left = xs.First();
            double right = xs.Last();

            foreach (double x in xs)
            {
                AddUnique(unique, new XYZ(x, bottom, 0.0));
                AddUnique(unique, new XYZ(x, top, 0.0));
            }

            foreach (double y in ys.Skip(1).Take(Math.Max(0, ys.Count - 2)))
            {
                AddUnique(unique, new XYZ(left, y, 0.0));
                AddUnique(unique, new XYZ(right, y, 0.0));
            }

            return unique.Values
                .OrderByDescending(x => x.Y)
                .ThenBy(x => x.X)
                .ToList();
        }

        private static void AddUnique(IDictionary<string, XYZ> unique, XYZ point)
        {
            string key = $"{Math.Round(point.X, 6)}|{Math.Round(point.Y, 6)}";
            if (!unique.ContainsKey(key))
                unique[key] = point;
        }

        private static List<double> DistributePositions(int count, double min, double max)
        {
            count = Math.Max(1, count);
            if (max - min <= ToFeetMm(10))
                return new List<double> { (min + max) / 2.0 };

            if (count == 1)
                return new List<double> { (min + max) / 2.0 };

            List<double> values = new List<double>();
            double step = (max - min) / (count - 1);
            for (int i = 0; i < count; i++)
                values.Add(min + (step * i));

            return values;
        }

        private static XYZ NormalizeHorizontal(XYZ vector)
        {
            if (vector == null)
                return XYZ.Zero;

            XYZ horizontal = new XYZ(vector.X, vector.Y, 0.0);
            return horizontal.IsZeroLength() ? XYZ.Zero : horizontal.Normalize();
        }

        private static XYZ Normalize(XYZ vector, XYZ fallback)
        {
            return vector == null || vector.IsZeroLength() ? fallback : vector.Normalize();
        }

        private static double ToFeetMm(double value)
        {
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
        }

        private static double ToFeetCm(double value)
        {
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Centimeters);
        }

        private static string LimparMensagem(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "falha desconhecida."
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class LocalBounds
        {
            public LocalBounds(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public double MinX { get; }
            public double MaxX { get; }
            public double MinY { get; }
            public double MaxY { get; }
            public double MinZ { get; }
            public double MaxZ { get; }
        }

        private sealed class HostFrame
        {
            public HostFrame(Transform localToWorld, LocalBounds bounds)
            {
                LocalToWorld = localToWorld;
                MinX = bounds.MinX;
                MaxX = bounds.MaxX;
                MinY = bounds.MinY;
                MaxY = bounds.MaxY;
                MinZ = bounds.MinZ;
                MaxZ = bounds.MaxZ;
            }

            public Transform LocalToWorld { get; }
            public XYZ XAxis => LocalToWorld.BasisX;
            public XYZ YAxis => LocalToWorld.BasisY;
            public XYZ ZAxis => LocalToWorld.BasisZ;
            public double MinX { get; }
            public double MaxX { get; }
            public double MinY { get; }
            public double MaxY { get; }
            public double MinZ { get; }
            public double MaxZ { get; }
            public double HalfLength => (MaxX - MinX) / 2.0;
            public double HalfWidth => (MaxY - MinY) / 2.0;
            public double HalfDepth => (MaxZ - MinZ) / 2.0;
        }
    }
}
