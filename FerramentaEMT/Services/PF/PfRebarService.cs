// =========================================================================
// PfRebarService - versao reconciliada (Onda Wave 2, 2026-04-24)
//
// ORIGEM: snapshot do Victor em FerramentaEMT (3).rar (2026-04-24).
// Adotada por completo por conter as seguintes funcionalidades novas:
//   - RebarShape catalog (ShapeName das configs aplica shape do projeto Revit)
//   - Preview de secao (BuildBeamSectionPreview / BuildColumnSectionPreview)
//   - Modo Coordenadas (PfRebarPlacementMode.Coordenadas)
//   - BarRange para lancamento em zonas
//   - Integracao com PfLapSpliceConfig (NBR 6118)
//
// REGRESSAO CONHECIDA (a reverter em follow-up):
//   Este arquivo usa EspacamentoCm unico em estribos (modo Victor).
//   Os campos EspacamentoInferior/Central/Superior + AlturaZonaExtremidadeCm
//   e EspacamentoApoio + ComprimentoZonaApoio + Central ainda existem em
//   PfRebarConfigs.cs (atras da flag UsarEspacamentoUnico=false), mas o
//   service nao le esses campos ainda - a logica de zoneamento da nossa
//   v1.5.0 vai voltar num PR separado (PR-Wave2-FOLLOWUP-zoneamento).
//
// Backup da versao anterior (945 linhas, com zoneamento NBR 6118):
//   PfRebarService.cs.bak-alef-v1.5
// =========================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

        public static PfRebarSectionPreview BuildBeamSectionPreview(FamilyInstance beam)
        {
            HostFrame frame = BuildBeamFrame(beam);
            double centerX = (frame.MinY + frame.MaxY) / 2.0;
            double centerY = (frame.MinZ + frame.MaxZ) / 2.0;

            return new PfRebarSectionPreview
            {
                MinXCm = ToCentimeters(frame.MinY - centerX),
                MaxXCm = ToCentimeters(frame.MaxY - centerX),
                MinYCm = ToCentimeters(frame.MinZ - centerY),
                MaxYCm = ToCentimeters(frame.MaxZ - centerY)
            };
        }

        public static PfRebarSectionPreview BuildColumnSectionPreview(FamilyInstance column)
        {
            HostFrame frame = BuildColumnFrame(column);
            double centerX = (frame.MinX + frame.MaxX) / 2.0;
            double centerY = (frame.MinY + frame.MaxY) / 2.0;
            bool isCircular = IsCircularColumn(column, frame);
            double radius = Math.Min(frame.MaxX - frame.MinX, frame.MaxY - frame.MinY) / 2.0;

            return new PfRebarSectionPreview
            {
                Shape = isCircular ? PfRebarSectionShape.Circular : PfRebarSectionShape.Retangular,
                MinXCm = ToCentimeters(frame.MinX - centerX),
                MaxXCm = ToCentimeters(frame.MaxX - centerX),
                MinYCm = ToCentimeters(frame.MinY - centerY),
                MaxYCm = ToCentimeters(frame.MaxY - centerY),
                RadiusCm = ToCentimeters(radius)
            };
        }

        public static double GetColumnLengthCm(FamilyInstance column)
        {
            if (column == null)
                return 0.0;

            HostFrame frame = BuildColumnFrame(column);
            return ToCentimeters(frame.MaxZ - frame.MinZ);
        }

        public static double GetBeamLengthCm(FamilyInstance beam)
        {
            if (beam == null)
                return 0.0;

            HostFrame frame = BuildBeamFrame(beam);
            return ToCentimeters(frame.MaxX - frame.MinX);
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
            RebarBarType barType = GetBarTypeByDiameter(doc, config.DiametroMm, config.BarTypeName);
            RebarHookType hookType = GetHookTypeByAngle(doc, barType, (int)config.Dobra);
            RebarShape shape = GetRebarShape(doc, config.ShapeName, RebarStyle.StirrupTie);
            HostFrame frame = BuildColumnFrame(column);
            double cover = GetCover(config.CobrimentoCm);
            double minX = frame.MinX + cover;
            double maxX = frame.MaxX - cover;
            double minY = frame.MinY + cover;
            double maxY = frame.MaxY - cover;
            double minZ = frame.MinZ + cover;
            double maxZ = frame.MaxZ - cover;
            double clearHeight = maxZ - minZ;
            bool isCircular = IsCircularColumn(column, frame);
            if (clearHeight <= ToFeetMm(100))
                return 0;

            IList<Curve> loop;
            if (isCircular)
            {
                double centerX = (frame.MinX + frame.MaxX) / 2.0;
                double centerY = (frame.MinY + frame.MaxY) / 2.0;
                double radius = (Math.Min(frame.MaxX - frame.MinX, frame.MaxY - frame.MinY) / 2.0) - cover;
                if (radius <= ToFeetMm(20))
                    return 0;

                loop = CreateCircleLoopHorizontal(frame, minZ, centerX, centerY, radius);
            }
            else
            {
                if (maxX - minX <= ToFeetMm(20) || maxY - minY <= ToFeetMm(20))
                    return 0;

                loop = CreateRectangleLoopHorizontal(frame, minZ, minX, maxX, minY, maxY);
            }

            Rebar stirrup = CreateClosedRebar(
                doc,
                column,
                RebarStyle.StirrupTie,
                barType,
                frame.ZAxis,
                loop,
                hookType,
                shape);

            ApplyMaximumSpacingLayout(stirrup, ToFeetCm(config.EspacamentoCm), clearHeight);
            return 1;
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

            bool isCircular = IsCircularColumn(column, frame);
            if (isCircular)
            {
                double radius = (Math.Min(frame.MaxX - frame.MinX, frame.MaxY - frame.MinY) / 2.0) - cover;
                if (radius <= ToFeetMm(20))
                    return 0;

                if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas)
                    return InsertColumnBarsByCoordinates(doc, column, config, frame, minX, maxX, minY, maxY, z0, z1, true, radius);

                return InsertCircularColumnBars(doc, column, config, frame, z0, z1, radius);
            }

            if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas)
                return InsertColumnBarsByCoordinates(doc, column, config, frame, minX, maxX, minY, maxY, z0, z1, false, 0.0);

            List<XYZ> offsets = BuildPerimeterPositions(
                DistributePositions(config.QuantidadeLargura, minX, maxX),
                DistributePositions(config.QuantidadeProfundidade, minY, maxY));
            ValidateMinimumBarSpacing("pilar", offsets, barType);
            int created = 0;
            foreach (XYZ offset in offsets)
            {
                created += CreateVerticalBarsWithLap(
                    doc,
                    column,
                    barType,
                    frame,
                    offset.Y,
                    offset.X,
                    z0,
                    z1,
                    config.Traspasse,
                    created);
            }

            return created;
        }

        private int InsertCircularColumnBars(
            Document doc,
            FamilyInstance column,
            PfColumnBarsConfig config,
            HostFrame frame,
            double z0,
            double z1,
            double radius)
        {
            int count = Math.Max(1, config.QuantidadeCircular);
            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            double centerX = (frame.MinX + frame.MaxX) / 2.0;
            double centerY = (frame.MinY + frame.MaxY) / 2.0;
            List<XYZ> positions = new List<XYZ>();

            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2.0 * i) / count;
                positions.Add(new XYZ(
                    centerX + (Math.Cos(angle) * radius),
                    centerY + (Math.Sin(angle) * radius),
                    0.0));
            }

            ValidateMinimumBarSpacing("pilar circular", positions, barType);
            int created = 0;

            foreach (XYZ position in positions)
            {
                created += CreateVerticalBarsWithLap(
                    doc,
                    column,
                    barType,
                    frame,
                    position.Y,
                    position.X,
                    z0,
                    z1,
                    config.Traspasse,
                    created);
            }

            return created;
        }

        private int InsertColumnBarsByCoordinates(
            Document doc,
            FamilyInstance column,
            PfColumnBarsConfig config,
            HostFrame frame,
            double minX,
            double maxX,
            double minY,
            double maxY,
            double z0,
            double z1,
            bool isCircular,
            double circularRadius)
        {
            if (config.Coordenadas.Count == 0)
                return 0;

            RebarBarType barType = GetBarType(doc, config.BarTypeName);
            double centerX = (frame.MinX + frame.MaxX) / 2.0;
            double centerY = (frame.MinY + frame.MaxY) / 2.0;
            double tolerance = ToFeetMm(1.0);
            List<XYZ> positions = new List<XYZ>();

            foreach (PfColumnBarCoordinate coordinate in config.Coordenadas)
                positions.Add(ResolveColumnCoordinatePosition(coordinate, frame, centerX, centerY, isCircular));

            ValidateMinimumBarSpacing("pilar", positions, barType);
            int created = 0;

            for (int i = 0; i < config.Coordenadas.Count; i++)
            {
                PfColumnBarCoordinate coordinate = config.Coordenadas[i];
                XYZ position = positions[i];
                double x = position.X;
                double y = position.Y;

                bool outside = isCircular
                    ? Math.Sqrt(Math.Pow(x - centerX, 2.0) + Math.Pow(y - centerY, 2.0)) > circularRadius + tolerance
                    : x < minX - tolerance || x > maxX + tolerance || y < minY - tolerance || y > maxY + tolerance;

                if (outside)
                {
                    throw new InvalidOperationException(
                        $"Coordenada X={coordinate.XCm:0.###} cm, Y={coordinate.YCm:0.###} cm fora da secao util do pilar.");
                }

                created += CreateVerticalBarsWithLap(
                    doc,
                    column,
                    barType,
                    frame,
                    y,
                    x,
                    z0,
                    z1,
                    config.Traspasse,
                    created);
            }

            return created;
        }

        private int InsertBeamStirrups(Document doc, FamilyInstance beam, PfBeamStirrupsConfig config)
        {
            RebarBarType barType = GetBarTypeByDiameter(doc, config.DiametroMm, config.BarTypeName);
            RebarHookType hookType = GetHookTypeByAngle(doc, barType, (int)config.Dobra);
            RebarShape shape = GetRebarShape(doc, config.ShapeName, RebarStyle.StirrupTie);
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

            Rebar stirrup = CreateClosedRebar(
                doc,
                beam,
                RebarStyle.StirrupTie,
                barType,
                frame.XAxis,
                CreateRectangleLoopVertical(frame, minX, minY, maxY, minZ, maxZ),
                hookType,
                shape);

            ApplyMaximumSpacingLayout(stirrup, ToFeetCm(config.EspacamentoCm), clearLength);
            return 1;
        }

        private int InsertBeamBars(Document doc, FamilyInstance beam, PfBeamBarsConfig config)
        {
            HostFrame frame = BuildBeamFrame(beam);
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

            if (config.ModoLancamento == PfRebarPlacementMode.Coordenadas)
            {
                return InsertBeamBarsByCoordinates(
                    doc,
                    beam,
                    config,
                    frame,
                    x0,
                    x1,
                    minY,
                    maxY,
                    minZ,
                    maxZ,
                    hookLength);
            }

            RebarBarType barTypeSuperior = GetBarType(doc, config.BarTypeSuperiorName);
            RebarBarType barTypeInferior = GetBarType(doc, config.BarTypeInferiorName);
            RebarBarType barTypeLateral = GetBarType(doc, config.BarTypeLateralName);
            List<double> topYs = config.QuantidadeSuperior > 0
                ? DistributePositions(config.QuantidadeSuperior, minY, maxY)
                : new List<double>();
            List<double> bottomYs = config.QuantidadeInferior > 0
                ? DistributePositions(config.QuantidadeInferior, minY, maxY)
                : new List<double>();
            ValidateMinimumBarSpacing("viga superior", topYs.Select(y => new XYZ(0.0, y, maxZ)), barTypeSuperior);
            ValidateMinimumBarSpacing("viga inferior", bottomYs.Select(y => new XYZ(0.0, y, minZ)), barTypeInferior);
            int created = 0;

            foreach (double y in topYs)
            {
                created += CreateLongitudinalBarsWithLap(
                    doc,
                    beam,
                    barTypeSuperior,
                    frame,
                    y,
                    maxZ,
                    x0,
                    x1,
                    hookLength,
                    config.ModoPonta == PfBeamBarEndMode.DobraInterna ? -1 : 0,
                    config.Traspasse,
                    created);
            }

            foreach (double y in bottomYs)
            {
                created += CreateLongitudinalBarsWithLap(
                    doc,
                    beam,
                    barTypeInferior,
                    frame,
                    y,
                    minZ,
                    x0,
                    x1,
                    hookLength,
                    config.ModoPonta == PfBeamBarEndMode.DobraInterna ? 1 : 0,
                    config.Traspasse,
                    created);
            }

            if (config.QuantidadeLateral > 0)
            {
                List<double> levels = config.QuantidadeLateral > 0
                    ? DistributePositions(config.QuantidadeLateral, minZ, maxZ)
                    : new List<double>();
                List<XYZ> lateralPositions = new List<XYZ>();
                foreach (double z in levels)
                {
                    lateralPositions.Add(new XYZ(0.0, minY, z));
                    lateralPositions.Add(new XYZ(0.0, maxY, z));
                }

                ValidateMinimumBarSpacing("viga lateral", lateralPositions, barTypeLateral);

                foreach (double z in levels)
                {
                    created += CreateLongitudinalBarsWithLap(
                        doc,
                        beam,
                        barTypeLateral,
                        frame,
                        minY,
                        z,
                        x0,
                        x1,
                        0.0,
                        0,
                        config.Traspasse,
                        created);
                    created += CreateLongitudinalBarsWithLap(
                        doc,
                        beam,
                        barTypeLateral,
                        frame,
                        maxY,
                        z,
                        x0,
                        x1,
                        0.0,
                        0,
                        config.Traspasse,
                        created);
                }
            }

            return created;
        }

        private int InsertBeamBarsByCoordinates(
            Document doc,
            FamilyInstance beam,
            PfBeamBarsConfig config,
            HostFrame frame,
            double x0,
            double x1,
            double minY,
            double maxY,
            double minZ,
            double maxZ,
            double hookLength)
        {
            if (config.Coordenadas.Count == 0)
                return 0;

            double centerY = (frame.MinY + frame.MaxY) / 2.0;
            double centerZ = (frame.MinZ + frame.MaxZ) / 2.0;
            double tolerance = ToFeetMm(1.0);
            List<XYZ> positions = config.Coordenadas
                .Select(x => new XYZ(0.0, frame.MinY + ToFeetCm(x.XCm), frame.MinZ + ToFeetCm(x.YCm)))
                .ToList();
            ValidateMinimumBarSpacing("viga", positions, GetMaxBarDiameterFeet(doc, config.Coordenadas.Select(x => x.BarTypeName)));
            int created = 0;

            foreach (PfBeamBarCoordinate coordinate in config.Coordenadas)
            {
                double y = frame.MinY + ToFeetCm(coordinate.XCm);
                double z = frame.MinZ + ToFeetCm(coordinate.YCm);

                if (y < minY - tolerance || y > maxY + tolerance || z < minZ - tolerance || z > maxZ + tolerance)
                {
                    throw new InvalidOperationException(
                        $"Coordenada X={coordinate.XCm:0.###} cm, Y={coordinate.YCm:0.###} cm fora da secao util da viga.");
                }

                RebarBarType barType = GetBarType(doc, coordinate.BarTypeName);
                int hookDirection = config.ModoPonta == PfBeamBarEndMode.DobraInterna
                    ? (z >= centerZ ? -1 : 1)
                    : 0;

                created += CreateLongitudinalBarsWithLap(
                    doc,
                    beam,
                    barType,
                    frame,
                    y,
                    z,
                    x0,
                    x1,
                    hookLength,
                    hookDirection,
                    config.Traspasse,
                    created);
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

        private static RebarBarType GetBarTypeByDiameter(Document doc, double diameterMm, string fallbackName)
        {
            List<RebarBarType> barTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (barTypes.Count == 0)
                throw new InvalidOperationException("Nenhum tipo de vergalhao foi encontrado no projeto.");

            if (diameterMm > 0)
            {
                double target = ToFeetMm(diameterMm);
                RebarBarType byNominalDiameter = barTypes
                    .Select(x => new { Type = x, Difference = Math.Abs(x.BarNominalDiameter - target) })
                    .Where(x => x.Difference <= ToFeetMm(0.25))
                    .OrderBy(x => x.Difference)
                    .Select(x => x.Type)
                    .FirstOrDefault();

                if (byNominalDiameter != null)
                    return byNominalDiameter;

                string diameterToken = FormatDiameterToken(diameterMm);
                RebarBarType byName = barTypes.FirstOrDefault(x =>
                    NormalizeText(x.Name).Contains(diameterToken));

                if (byName != null)
                    return byName;
            }

            return string.IsNullOrWhiteSpace(fallbackName)
                ? barTypes.First()
                : GetBarType(doc, fallbackName);
        }

        private static double GetMaxBarDiameterFeet(Document doc, IEnumerable<string> barTypeNames)
        {
            double maxDiameter = 0.0;
            foreach (string name in barTypeNames ?? Enumerable.Empty<string>())
            {
                RebarBarType barType = GetBarType(doc, name);
                maxDiameter = Math.Max(maxDiameter, barType.BarNominalDiameter);
            }

            return maxDiameter;
        }

        private static RebarHookType GetHookTypeByAngle(Document doc, RebarBarType barType, int angleDegrees)
        {
            double target = angleDegrees;
            RebarHookType hookType = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .Where(x => Math.Abs(RadiansToDegrees(x.HookAngle) - target) <= 0.5)
                .Where(x => barType == null || barType.GetHookPermission(x.Id))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();

            if (hookType != null)
                return hookType;

            double multiplier = angleDegrees == 90 ? 12.0 : 6.0;
            RebarHookType created = RebarHookType.Create(doc, DegreesToRadians(angleDegrees), multiplier);
            if (created == null)
                throw new InvalidOperationException($"Nenhum tipo de gancho {angleDegrees} graus foi encontrado ou criado no projeto.");

            if (barType != null)
                barType.SetHookPermission(created.Id, true);

            try
            {
                created.Name = $"EMT Gancho {angleDegrees} graus";
            }
            catch
            {
                // Revit may keep the default generated name when a duplicate name exists.
            }

            return created;
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
            IList<Curve> curves,
            RebarHookType hookType = null,
            RebarShape shape = null)
        {
            Rebar rebar = Rebar.CreateFromCurves(
                doc,
                style,
                barType,
                hookType,
                hookType,
                host,
                normal,
                curves,
                RebarHookOrientation.Right,
                RebarHookOrientation.Right,
                true,
                true);

            if (rebar == null)
                throw new InvalidOperationException("O Revit não conseguiu gerar a armadura fechada para este hospedeiro.");

            TryApplyShapeToRebar(rebar, shape);
            return rebar;
        }

        private static void TryApplyShapeToRebar(Rebar rebar, RebarShape shape)
        {
            if (rebar == null || shape == null)
                return;

            try
            {
                RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor();
                accessor?.SetRebarShapeId(shape.Id);
            }
            catch
            {
                // Se o formato escolhido nao representar bem a geometria criada,
                // mantemos o estribo automatico em vez de falhar o comando.
            }
        }

        private static RebarShape GetRebarShape(Document doc, string name, RebarStyle expectedStyle)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            RebarShape shape = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .FirstOrDefault(x =>
                    string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));

            if (shape == null)
                throw new InvalidOperationException($"O formato de vergalhao \"{name}\" nao foi encontrado no projeto.");

            if (shape.RebarStyle != expectedStyle)
                throw new InvalidOperationException($"O formato \"{name}\" nao e compativel com armadura do tipo estribo.");

            return shape;
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

        private static int CreateVerticalBarsWithLap(
            Document doc,
            Element host,
            RebarBarType barType,
            HostFrame frame,
            double y,
            double x,
            double z0,
            double z1,
            PfLapSpliceConfig lapConfig,
            int staggerIndex)
        {
            PfAnchorageResult anchorage = CalculateAnchorageIfEnabled(barType, lapConfig);
            if (anchorage == null)
            {
                CreateOpenRebar(doc, host, RebarStyle.Standard, barType, frame.XAxis, CreateVerticalBar(frame, y, x, z0, z1));
                return 1;
            }

            List<BarRange> ranges = BuildLapRanges(z0, z1, ToFeetCm(lapConfig.MaxBarLengthCm), ToFeetCm(anchorage.SpliceLengthCm), staggerIndex);
            for (int i = 0; i < ranges.Count; i++)
            {
                BarRange range = ranges[i];
                Rebar rebar = CreateOpenRebar(
                    doc,
                    host,
                    RebarStyle.Standard,
                    barType,
                    frame.XAxis,
                    CreateVerticalBar(frame, y, x, range.Start, range.End));
                AnnotateAnchorage(rebar, anchorage, ranges.Count > 1, i + 1, ranges.Count);
            }

            return ranges.Count;
        }

        private static int CreateLongitudinalBarsWithLap(
            Document doc,
            Element host,
            RebarBarType barType,
            HostFrame frame,
            double y,
            double z,
            double x0,
            double x1,
            double hookLength,
            int hookDirection,
            PfLapSpliceConfig lapConfig,
            int staggerIndex)
        {
            PfAnchorageResult anchorage = CalculateAnchorageIfEnabled(barType, lapConfig);
            if (anchorage == null)
            {
                CreateOpenRebar(
                    doc,
                    host,
                    RebarStyle.Standard,
                    barType,
                    frame.YAxis,
                    CreateLongitudinalBar(frame, y, z, x0, x1, hookLength, hookDirection));
                return 1;
            }

            List<BarRange> ranges = BuildLapRanges(x0, x1, ToFeetCm(lapConfig.MaxBarLengthCm), ToFeetCm(anchorage.SpliceLengthCm), staggerIndex);
            for (int i = 0; i < ranges.Count; i++)
            {
                BarRange range = ranges[i];
                bool startHook = i == 0;
                bool endHook = i == ranges.Count - 1;
                Rebar rebar = CreateOpenRebar(
                    doc,
                    host,
                    RebarStyle.Standard,
                    barType,
                    frame.YAxis,
                    CreateLongitudinalBarSegment(frame, y, z, range.Start, range.End, hookLength, hookDirection, startHook, endHook));
                AnnotateAnchorage(rebar, anchorage, ranges.Count > 1, i + 1, ranges.Count);
            }

            return ranges.Count;
        }

        private static PfAnchorageResult CalculateAnchorageIfEnabled(RebarBarType barType, PfLapSpliceConfig lapConfig)
        {
            if (lapConfig == null || !lapConfig.Enabled)
                return null;

            double diameterMm = barType == null
                ? 0.0
                : UnitUtils.ConvertFromInternalUnits(barType.BarNominalDiameter, UnitTypeId.Millimeters);

            return PfNbr6118AnchorageService.Calculate(diameterMm, lapConfig);
        }

        private static List<BarRange> BuildLapRanges(double start, double end, double maxPieceLength, double lapLength, int staggerIndex)
        {
            double totalLength = end - start;
            if (totalLength <= ToFeetMm(MinSegmentMm))
                return new List<BarRange>();

            if (maxPieceLength <= ToFeetMm(MinSegmentMm) || totalLength <= maxPieceLength + ToFeetMm(1.0))
                return new List<BarRange> { new BarRange(start, end) };

            double minPiece = ToFeetMm(300.0);
            if (maxPieceLength <= lapLength + minPiece)
                throw new InvalidOperationException("O comprimento maximo da barra precisa ser maior que o traspasse calculado.");

            List<BarRange> ranges = new List<BarRange>();
            double usefulStep = maxPieceLength - lapLength;
            double staggerStep = Math.Min(lapLength / 2.0, usefulStep / 3.0);
            double firstReduction = (Math.Abs(staggerIndex) % 3) * staggerStep;
            double currentStart = start;
            double currentEnd = Math.Min(end, start + maxPieceLength - firstReduction);

            while (currentEnd < end - ToFeetMm(1.0))
            {
                if (currentEnd - currentStart < minPiece)
                    currentEnd = Math.Min(end, currentStart + minPiece);

                ranges.Add(new BarRange(currentStart, currentEnd));
                currentStart = currentEnd - lapLength;
                currentEnd = Math.Min(end, currentStart + maxPieceLength);
            }

            if (end - currentStart >= ToFeetMm(MinSegmentMm))
                ranges.Add(new BarRange(currentStart, end));

            return ranges.Count == 0
                ? new List<BarRange> { new BarRange(start, end) }
                : ranges;
        }

        private static void AnnotateAnchorage(Rebar rebar, PfAnchorageResult anchorage, bool hasLap, int pieceIndex, int pieceCount)
        {
            if (rebar == null || anchorage == null)
                return;

            string detail = anchorage.ToDetailText();
            if (hasLap)
                detail += $" | emenda {pieceIndex}/{pieceCount}";

            SetStringParameter(rebar, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, detail);
            SetLookupParameter(rebar, "EMT_CriterioNBR", "NBR 6118:2023");
            SetLookupParameter(rebar, "EMT_TraspasseCm", anchorage.SpliceLengthCm.ToString("0.##", CultureInfo.InvariantCulture));
            SetLookupParameter(rebar, "EMT_AncoragemCm", anchorage.RequiredAnchorageCm.ToString("0.##", CultureInfo.InvariantCulture));
            SetLookupParameter(rebar, "EMT_Emenda", hasLap ? $"Peca {pieceIndex}/{pieceCount}" : "Sem divisao");
        }

        private static void SetStringParameter(Element element, BuiltInParameter builtInParameter, string value)
        {
            Parameter parameter = element?.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
                return;

            string existing = parameter.AsString();
            if (string.IsNullOrWhiteSpace(existing))
            {
                parameter.Set(value);
                return;
            }

            if (!existing.Contains("EMT NBR 6118", StringComparison.OrdinalIgnoreCase))
                parameter.Set(existing + " | " + value);
        }

        private static void SetLookupParameter(Element element, string name, string value)
        {
            Parameter parameter = element?.LookupParameter(name);
            if (parameter == null || parameter.IsReadOnly)
                return;

            if (parameter.StorageType == StorageType.String)
                parameter.Set(value);
            else if (parameter.StorageType == StorageType.Double && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                parameter.Set(number);
            else if (parameter.StorageType == StorageType.Integer && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                parameter.Set(integer);
        }

        private static void ValidateMinimumBarSpacing(string context, IEnumerable<XYZ> localSectionPoints, RebarBarType barType)
        {
            ValidateMinimumBarSpacing(context, localSectionPoints, barType?.BarNominalDiameter ?? 0.0);
        }

        private static void ValidateMinimumBarSpacing(string context, IEnumerable<XYZ> localSectionPoints, double barDiameter)
        {
            List<XYZ> points = (localSectionPoints ?? Enumerable.Empty<XYZ>()).ToList();
            if (points.Count <= 1 || barDiameter <= ToFeetMm(1.0))
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

            double clearSpacing = minCenterSpacing - barDiameter;
            double minimumClearSpacing = Math.Max(ToFeetMm(20.0), barDiameter);
            if (clearSpacing + ToFeetMm(1.0) >= minimumClearSpacing)
                return;

            throw new InvalidOperationException(
                $"Espacamento insuficiente entre barras em {context}: livre {ToCentimeters(clearSpacing):0.#} cm; minimo adotado {ToCentimeters(minimumClearSpacing):0.#} cm.");
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

        private static bool IsCircularColumn(FamilyInstance column, HostFrame frame)
        {
            if (column == null || frame == null)
                return false;

            double width = frame.MaxX - frame.MinX;
            double depth = frame.MaxY - frame.MinY;
            double max = Math.Max(width, depth);
            if (max <= ToFeetMm(10))
                return false;

            bool nearlySquareBounds = Math.Abs(width - depth) / max <= 0.08;
            if (!nearlySquareBounds)
                return false;

            return HasCircularGeometry(column) || HasCircularColumnMetadata(column);
        }

        private static bool HasCircularGeometry(Element element)
        {
            Options options = new Options
            {
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            return CountCircularCurves(element.get_Geometry(options)) >= 2;
        }

        private static int CountCircularCurves(GeometryElement geometry)
        {
            if (geometry == null)
                return 0;

            int count = 0;
            foreach (GeometryObject obj in geometry)
            {
                if (obj is GeometryInstance instance)
                {
                    count += CountCircularCurves(instance.GetSymbolGeometry());
                    continue;
                }

                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve edgeCurve = edge.AsCurve();
                        if (IsCircularCurve(edgeCurve))
                            count++;
                    }

                    continue;
                }

                if (obj is Curve curve && IsCircularCurve(curve))
                    count++;
            }

            return count;
        }

        private static bool IsCircularCurve(Curve curve)
        {
            return curve is Arc || curve is Ellipse;
        }

        private static bool HasCircularColumnMetadata(FamilyInstance column)
        {
            string text = NormalizeText(
                $"{column.Name} {column.Symbol?.Name} {column.Symbol?.FamilyName}");

            if (text.Contains("circular") ||
                text.Contains("redondo") ||
                text.Contains("diametro") ||
                text.Contains("diameter") ||
                text.Contains("ø"))
            {
                return true;
            }

            foreach (string parameterName in new[]
            {
                "Diametro",
                "Diâmetro",
                "Diameter",
                "Ø"
            })
            {
                if (HasPositiveParameter(column, parameterName) ||
                    HasPositiveParameter(column.Symbol, parameterName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPositiveParameter(Element element, string parameterName)
        {
            Parameter parameter = element?.LookupParameter(parameterName);
            return parameter != null &&
                   parameter.HasValue &&
                   parameter.StorageType == StorageType.Double &&
                   parameter.AsDouble() > ToFeetMm(10);
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            normalized = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
            return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
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

        private static IList<Curve> CreateCircleLoopHorizontal(HostFrame frame, double z, double centerX, double centerY, double radius)
        {
            List<Curve> curves = new List<Curve>();
            for (int i = 0; i < 4; i++)
            {
                double start = i * Math.PI / 2.0;
                double end = (i + 1) * Math.PI / 2.0;
                double mid = (start + end) / 2.0;

                XYZ p0 = frame.LocalToWorld.OfPoint(new XYZ(
                    centerX + Math.Cos(start) * radius,
                    centerY + Math.Sin(start) * radius,
                    z));
                XYZ p1 = frame.LocalToWorld.OfPoint(new XYZ(
                    centerX + Math.Cos(end) * radius,
                    centerY + Math.Sin(end) * radius,
                    z));
                XYZ pm = frame.LocalToWorld.OfPoint(new XYZ(
                    centerX + Math.Cos(mid) * radius,
                    centerY + Math.Sin(mid) * radius,
                    z));

                curves.Add(Arc.Create(p0, p1, pm));
            }

            return curves;
        }

        private static XYZ ResolveColumnCoordinatePosition(
            PfColumnBarCoordinate coordinate,
            HostFrame frame,
            double centerX,
            double centerY,
            bool isCircular)
        {
            if (isCircular)
            {
                double fullRadius = Math.Min(frame.MaxX - frame.MinX, frame.MaxY - frame.MinY) / 2.0;
                return new XYZ(
                    centerX - fullRadius + ToFeetCm(coordinate.XCm),
                    centerY - fullRadius + ToFeetCm(coordinate.YCm),
                    0.0);
            }

            return new XYZ(
                frame.MinX + ToFeetCm(coordinate.XCm),
                frame.MinY + ToFeetCm(coordinate.YCm),
                0.0);
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

        private static IList<Curve> CreateLongitudinalBarSegment(
            HostFrame frame,
            double y,
            double z,
            double x0,
            double x1,
            double hookLength,
            int hookDirection,
            bool startHook,
            bool endHook)
        {
            double actualHook = Math.Min(hookLength, Math.Max(0.0, (x1 - x0) / 4.0));
            List<XYZ> points = new List<XYZ>();

            if (startHook && actualHook > ToFeetMm(MinSegmentMm) && hookDirection != 0)
                points.Add(new XYZ(x0, y, z + (actualHook * hookDirection)));

            points.Add(new XYZ(x0, y, z));
            points.Add(new XYZ(x1, y, z));

            if (endHook && actualHook > ToFeetMm(MinSegmentMm) && hookDirection != 0)
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

        private static double ToCentimeters(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Centimeters);
        }

        private static double RadiansToDegrees(double value)
        {
            return value * 180.0 / Math.PI;
        }

        private static double DegreesToRadians(double value)
        {
            return value * Math.PI / 180.0;
        }

        private static string FormatDiameterToken(double diameterMm)
        {
            return diameterMm.ToString("0.###", CultureInfo.InvariantCulture)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty);
        }

        private static string LimparMensagem(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "falha desconhecida."
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class BarRange
        {
            public BarRange(double start, double end)
            {
                Start = start;
                End = end;
            }

            public double Start { get; }
            public double End { get; }
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
