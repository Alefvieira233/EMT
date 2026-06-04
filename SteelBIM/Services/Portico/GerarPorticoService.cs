#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Models.Bloco;
using SteelBIM.Models.Conexoes;
using SteelBIM.Models.PF;
using SteelBIM.Services.Bloco;
using SteelBIM.Services.Conexoes;
using SteelBIM.Services.PF;
using SteelBIM.Utils;

namespace SteelBIM.Services.Portico
{
    /// <summary>
    /// v2.8.14: "Gerar Projeto Completo (Portico)" — servico HEADLESS que materializa um galpao
    /// inteiro numa unica transacao a partir do GerarPorticoConfig. A geometria vem do nucleo PURO
    /// <see cref="PorticoGeometriaCalculator"/>; a treliça de cada portico reusa
    /// <c>TrelicaService.GerarTrelicaCompletaNoEixo</c>. Sem nenhum pick interativo.
    /// </summary>
    public sealed class GerarPorticoService
    {
        private static readonly double FtPerMm = RevitUtils.FT_PER_MM;

        // Deslocamento (origem) escolhido pelo usuario via clique; somado em X/Y a cada ponto.
        private XYZ _origem = XYZ.Zero;

        public void Executar(UIDocument uidoc, GerarPorticoConfig config, XYZ? origem = null)
        {
            _origem = origem ?? XYZ.Zero;
            Document doc = uidoc.Document;

            Level? nivel = ResolverNivel(doc);
            if (nivel == null)
            {
                AppDialogService.ShowError("Gerar Pórtico", "Nenhum nível encontrado no projeto.", "Nível ausente");
                return;
            }
            if (config.SymbolPilar == null)
            {
                AppDialogService.ShowError("Gerar Pórtico", "Selecione o perfil do pilar.", "Configuração incompleta");
                return;
            }
            FamilySymbol pilarSymbol = config.SymbolPilar;

            // Em modo treliça, sem nenhum perfil de banzo a cobertura sairia "vazia" (0 membros)
            // silenciosamente — avisa cedo, como ja' se faz com o pilar.
            if (config.UsarTrelica && config.SymbolBanzoSuperior == null && config.SymbolBanzoInferior == null)
            {
                AppDialogService.ShowError("Gerar Pórtico",
                    "Selecione ao menos um perfil de banzo (superior ou inferior) para a treliça.",
                    "Configuração incompleta");
                return;
            }
            if (!config.UsarTrelica && config.SymbolViga == null)
            {
                AppDialogService.ShowError("Gerar Pórtico",
                    "Selecione o perfil da viga de cobertura.", "Configuração incompleta");
                return;
            }

            GerarPorticoEntrada entrada = MapearEntrada(config);
            PorticoLayout layout = PorticoGeometriaCalculator.Calcular(entrada);
            if (layout.XPorticosMm.Count == 0)
            {
                AppDialogService.ShowWarning(
                    "Gerar Pórtico",
                    "Geometria inválida. Verifique nº de pórticos (≥2), espaçamento e vão.",
                    "Nada gerado");
                return;
            }

            int pilares = 0;
            int trelicas = 0;
            int membrosTrelica = 0;
            int vigas = 0;
            int tercas = 0;
            int contravCob = 0;
            int contravPil = 0;
            int linhas = 0;
            int placas = 0;
            var pilarIds = new List<ElementId>();
            var tercaIds = new List<ElementId>();
            var banzoSupIds = new List<ElementId>();

            using (Transaction t = new Transaction(doc, "Gerar Pórtico Completo"))
            {
                t.Start();
                AtivarSimbolos(doc, config);

                // ===== PILARES =====
                foreach (Segmento s in layout.Pilares)
                {
                    FamilyInstance? fp = CriarPilar(doc, pilarSymbol, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B));
                    if (fp != null)
                    {
                        pilares++;
                        pilarIds.Add(fp.Id);
                    }
                }

                // ===== COBERTURA: treliça OU viga =====
                if (config.UsarTrelica)
                {
                    TrelicaConfig tc = MapearTrelicaConfig(config);
                    TrelicaService trelicaService = new TrelicaService();
                    foreach (Segmento s in layout.EixosInferioresTrelica)
                    {
                        Line eixo = Line.CreateBound(ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B));
                        int m = trelicaService.GerarTrelicaCompletaNoEixo(doc, nivel, eixo, tc, banzoSupIds);
                        membrosTrelica += m;
                        if (m > 0)
                            trelicas++;
                    }
                }
                else
                {
                    foreach (Segmento s in layout.Vigas)
                    {
                        if (CriarBarra(doc, config.SymbolViga, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)) != null)
                            vigas++;
                    }
                }

                // ===== TERÇAS (layout ja' vazio quando desligado) =====
                // Cada terça assenta na inclinacao da agua (automatico, sem dado do usuario):
                // a rotacao da secao vem da geometria via InclinacaoTercaRad.
                foreach (Segmento s in layout.Tercas)
                {
                    double rotTerca = PorticoGeometriaCalculator.InclinacaoTercaRad(entrada, s.A.YMm);
                    FamilyInstance? fiT = CriarBarra(doc, config.SymbolTerca, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B), rotTerca);
                    if (fiT != null)
                    {
                        tercas++;
                        tercaIds.Add(fiT.Id);
                    }
                }

                // ===== CONTRAVENTAMENTOS (cobertura + pilares) =====
                foreach (Segmento s in layout.ContravCobertura)
                {
                    if (CriarBarra(doc, config.SymbolContravCobertura, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)) != null)
                        contravCob++;
                }
                foreach (Segmento s in layout.ContravPilares)
                {
                    if (CriarBarra(doc, config.SymbolContravPilares, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)) != null)
                        contravPil++;
                }

                // ===== LINHA DE CORRENTE =====
                foreach (Segmento s in layout.LinhasCorrente)
                {
                    if (CriarBarra(doc, config.SymbolLinhaCorrente, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)) != null)
                        linhas++;
                }

                // ===== EIXOS (grid A-G x 1-2) =====
                if (config.CriarEixos)
                    CriarEixos(doc, layout);

                t.Commit();
            }

            // Fundações (opcional) — sapata sob cada pilar; reusa PfFoundationPlacementService.
            // Lançadas ANTES das placas de base: a placa só assenta onde há concreto/apoio abaixo.
            int fundacoes = 0;
            List<ElementId> fundacoesCriadas = new List<ElementId>();
            if (config.LancarFundacoes && config.SymbolFundacao != null && pilarIds.Count > 0)
            {
                fundacoesCriadas = LancarFundacoes(uidoc, doc, config.SymbolFundacao, pilarIds);
                fundacoes = fundacoesCriadas.Count;
            }

            // Armadura de fundação (opt-in, best-effort) — depende de a familia aceitar armadura.
            int fundArmadas = 0;
            int fundPuladas = 0;
            string fundMotivo = string.Empty;
            if (config.LancarArmaduraFundacao && fundacoesCriadas.Count > 0)
            {
                (fundArmadas, fundPuladas, fundMotivo) = ArmarFundacoes(uidoc, doc, fundacoesCriadas);
            }

            // Placas de base (opcional) — fora da transacao acima; o servico abre a sua propria.
            // Restringe aos pilares recem-criados; so' assenta onde ha concreto/fundacao abaixo
            // (por isso vem DEPOIS das fundacoes).
            if (config.LancarPlacasBase)
                placas = LancarPlacasBase(doc, pilarIds);

            // Ligação de terça (opcional) — fora da transacao; o ConexaoTercasService abre a sua.
            // So' faz sentido em cobertura por treliça (precisa do banzo superior); em modo viga
            // nao ha banzo, entao o passo e' pulado e o motivo e' reportado/logado.
            int ligacoes = 0;
            string ligacaoMotivo = string.Empty;
            if (config.InserirLigacaoTerca)
            {
                if (config.SymbolLigacaoTerca == null)
                    ligacaoMotivo = "selecione a família de ligação";
                else if (!config.UsarTrelica)
                    ligacaoMotivo = "requer cobertura em treliça (banzo superior)";
                else if (tercaIds.Count == 0)
                    ligacaoMotivo = "nenhuma terça lançada";
                else if (banzoSupIds.Count == 0)
                    ligacaoMotivo = "banzo superior não identificado";
                else
                    ligacoes = InserirLigacoesTerca(uidoc, doc, config, tercaIds, banzoSupIds);

                if (!string.IsNullOrEmpty(ligacaoMotivo))
                    Logger.Info("[GerarPortico] ligacao de terca pulada: {Motivo}", ligacaoMotivo);
            }

            string resumo = $"Pórtico gerado.\nPilares: {pilares}";
            if (config.UsarTrelica)
                resumo += $"\nTreliças: {trelicas} ({membrosTrelica} membros)";
            else
                resumo += $"\nVigas: {vigas}";
            resumo += $"\nTerças: {tercas}";
            if (contravCob > 0 || contravPil > 0)
                resumo += $"\nContraventamentos: {contravCob + contravPil}";
            if (config.LancarLinhaCorrente)
                resumo += $"\nLinha de corrente: {linhas} barra(s) em {config.NumeroLinhasCorrente} fileira(s)";
            if (config.LancarPlacasBase)
            {
                resumo += $"\nPlacas de base: {placas}";
                if (placas == 0)
                    resumo += " — nenhum pilar tem apoio de concreto abaixo (ligue \"Lançar fundações\")";
            }
            if (config.InserirLigacaoTerca)
            {
                resumo += $"\nLigações de terça: {ligacoes} terça(s) processada(s)";
                if (!string.IsNullOrEmpty(ligacaoMotivo))
                    resumo += $" — pulada ({ligacaoMotivo})";
            }
            if (config.LancarFundacoes)
                resumo += $"\nFundações: {fundacoes}";
            if (config.LancarArmaduraFundacao)
            {
                resumo += $"\nArmadura de fundação: {fundArmadas} armada(s), {fundPuladas} pulada(s)";
                if (!string.IsNullOrEmpty(fundMotivo))
                    resumo += $" — {fundMotivo}";
            }

            Logger.Info(
                "[GerarPortico] pilares={P} trelicas={T} membros={M} vigas={V} tercas={Te} contrav={C} linhas={L} placas={Pl} ligacoes={Lg} fund={F} fundArm={FA}",
                pilares, trelicas, membrosTrelica, vigas, tercas, contravCob + contravPil, linhas, placas, ligacoes, fundacoes, fundArmadas);
            AppDialogService.ShowInfo("Gerar Pórtico", resumo, "Concluído");
        }

        // ===== Nivel =====
        private static Level? ResolverNivel(Document doc)
        {
            Level? ativo = doc.ActiveView?.GenLevel;
            if (ativo != null)
                return ativo;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        // ===== Mapeamentos config -> entrada pura / TrelicaConfig =====
        private static GerarPorticoEntrada MapearEntrada(GerarPorticoConfig c) => new GerarPorticoEntrada
        {
            NumeroPorticos = c.NumeroPorticos,
            EspacamentoPorticosMm = c.EspacamentoPorticosMm,
            VaoGalpaoMm = c.VaoGalpaoMm,
            AlturaPilarMm = c.AlturaPilarMm,
            PilarCentral = c.PilarCentral,
            UsarTrelica = c.UsarTrelica,
            AlturaExtremidadeMm = c.AlturaExtremidadeMm,
            AlturaCentralMm = c.AlturaCentralMm,
            AlturaCumeeiraMm = c.AlturaCumeeiraMm,
            LancarTercas = c.LancarTercas,
            EspacamentoTercasMm = c.EspacamentoTercasMm,
            ElevacaoTercasMm = c.ElevacaoTercasMm,
            InverterAberturaTerca = c.InverterAberturaTerca,
            ContravCobertura = c.ContravCobertura,
            TercasPorXCobertura = c.TercasPorXCobertura,
            DistribuicaoContravCobertura = c.DistribuicaoContravCobertura,
            ContravPilares = c.ContravPilares,
            DistribuicaoContravPilares = c.DistribuicaoContravPilares,
            LancarLinhaCorrente = c.LancarLinhaCorrente,
            NumeroLinhasCorrente = c.NumeroLinhasCorrente
        };

        private static TrelicaConfig MapearTrelicaConfig(GerarPorticoConfig c) => new TrelicaConfig
        {
            TrelicaCompleta = true,
            Padrao = c.PadraoTrelica,
            AlturaExtremidadeMm = c.AlturaExtremidadeMm,
            AlturaCentralMm = c.AlturaCentralMm,
            // Com terças ligadas, os montantes seguem o espaçamento das terças (P painéis par =>
            // terça sobre cada montante, nó na cumeeira). Sem terças, usa as divisões da janela.
            Quantidade = c.LancarTercas
                ? Math.Max(1, PorticoGeometriaCalculator.PaineisTrelica(c.VaoGalpaoMm, c.EspacamentoTercasMm) - 1)
                : Math.Max(1, c.DivisoesTrelica),
            ModoEspacamento = TrussSpacingMode.Uniforme,
            LancarMontante = true,
            LancarDiagonal = true,
            MontantesExtremidade = true,
            DiagonaisExtremidade = true,
            SymbolBanzoSuperior = c.SymbolBanzoSuperior,
            SymbolBanzoInferior = c.SymbolBanzoInferior,
            SymbolBanzo = c.SymbolBanzoInferior ?? c.SymbolBanzoSuperior, // fallback p/ guards internos
            SymbolDiagonal = c.SymbolDiagonal,
            SymbolMontante = c.SymbolMontante,
            RotacaoBanzoSuperiorGraus = c.RotacaoBanzoSuperiorGraus,
            RotacaoBanzoInferiorGraus = c.RotacaoBanzoInferiorGraus,
            RotacaoDiagonalGraus = c.RotacaoDiagonalGraus,
            RotacaoMontanteGraus = c.RotacaoMontanteGraus
        };

        private static void AtivarSimbolos(Document doc, GerarPorticoConfig c)
        {
            FamilySymbol?[] simbolos =
            {
                c.SymbolPilar, c.SymbolViga, c.SymbolTerca,
                c.SymbolContravCobertura, c.SymbolContravPilares, c.SymbolLinhaCorrente,
                c.SymbolBanzoSuperior, c.SymbolBanzoInferior, c.SymbolDiagonal, c.SymbolMontante
            };
            foreach (FamilySymbol? s in simbolos)
            {
                if (s != null && !s.IsActive)
                    s.Activate();
            }
            doc.Regenerate();
        }

        // ===== Criacao de membros =====
        private static FamilyInstance? CriarPilar(Document doc, FamilySymbol symbol, Level nivel, XYZ baseP, XYZ topo)
        {
            if (baseP.DistanceTo(topo) < RevitUtils.EPS)
                return null;

            bool ehColuna = symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
            if (!ehColuna)
                return CriarBarra(doc, symbol, nivel, baseP, topo); // perfil nao-coluna: vira viga reta

            try
            {
                FamilyInstance fi = doc.Create.NewFamilyInstance(baseP, symbol, nivel, StructuralType.Column);
                if (fi == null)
                    return null;

                SetParamId(fi, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, nivel.Id);
                SetParamId(fi, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, nivel.Id);
                SetParamDouble(fi, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, baseP.Z - nivel.Elevation);
                SetParamDouble(fi, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, topo.Z - nivel.Elevation);
                return fi;
            }
            catch (Exception ex)
            {
                // familia incompativel para coluna: isola o erro sem abortar a geracao inteira.
                Logger.Warn(ex, "[GerarPortico] falha ao criar pilar com {Familia}", symbol.FamilyName);
                return null;
            }
        }

        private static FamilyInstance? CriarBarra(Document doc, FamilySymbol? symbol, Level nivel, XYZ a, XYZ b, double rotacaoRad = 0.0)
        {
            if (symbol == null || a.DistanceTo(b) < RevitUtils.EPS)
                return null;

            try
            {
                Line line = Line.CreateBound(a, b);
                FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
                if (fi == null)
                    return null;

                RevitUtils.DisallowJoins(fi);
                if (Math.Abs(rotacaoRad) > RevitUtils.EPS)
                    RevitUtils.SetSectionRotation(fi, rotacaoRad);
                return fi;
            }
            catch (Exception ex)
            {
                // perfil incompativel (ex.: familia point-based escolhida para um membro reto):
                // pula este membro sem abortar a transacao inteira.
                Logger.Warn(ex, "[GerarPortico] falha ao criar barra com {Familia}", symbol.FamilyName);
                return null;
            }
        }

        // ===== Helpers =====
        private XYZ ParaXYZ(Level nivel, Ponto3D p) =>
            new XYZ(_origem.X + p.XMm * FtPerMm, _origem.Y + p.YMm * FtPerMm, nivel.Elevation + p.ZMm * FtPerMm);

        private static void SetParamId(FamilyInstance fi, BuiltInParameter bip, ElementId valor)
        {
            try
            {
                Parameter p = fi.get_Parameter(bip);
                if (p != null && !p.IsReadOnly)
                    p.Set(valor);
            }
            catch
            {
                // familia sem o parametro: ignora.
            }
        }

        private static void SetParamDouble(FamilyInstance fi, BuiltInParameter bip, double valor)
        {
            try
            {
                Parameter p = fi.get_Parameter(bip);
                if (p != null && !p.IsReadOnly)
                    p.Set(valor);
            }
            catch
            {
                // familia sem o parametro: ignora.
            }
        }

        // ===== Eixos (grid) =====
        private void CriarEixos(Document doc, PorticoLayout layout)
        {
            if (layout.XPorticosMm.Count == 0 || layout.YEixosMm.Count < 2)
                return;

            // CriarEixos monta a grid direto (nao passa por ParaXYZ) — soma o offset da origem aqui.
            double ox = _origem.X;
            double oy = _origem.Y;
            double comprimentoFt = layout.XPorticosMm[layout.XPorticosMm.Count - 1] * FtPerMm;
            double larguraFt = layout.YEixosMm[layout.YEixosMm.Count - 1] * FtPerMm;

            // Letras (A, B, ...) — uma por portico, na direcao do vao (Y).
            for (int i = 0; i < layout.XPorticosMm.Count; i++)
            {
                double xFt = layout.XPorticosMm[i] * FtPerMm;
                CriarGrid(doc, new XYZ(ox + xFt, oy, 0.0), new XYZ(ox + xFt, oy + larguraFt, 0.0), LetraEixo(i));
            }

            // Numeros (1, 2) — nas duas linhas de apoio (y=0 e y=vao), na direcao do comprimento (X).
            for (int j = 0; j < layout.YEixosMm.Count; j++)
            {
                double yFt = layout.YEixosMm[j] * FtPerMm;
                CriarGrid(doc, new XYZ(ox, oy + yFt, 0.0), new XYZ(ox + comprimentoFt, oy + yFt, 0.0), (j + 1).ToString());
            }
        }

        private static void CriarGrid(Document doc, XYZ a, XYZ b, string nome)
        {
            if (a.DistanceTo(b) < RevitUtils.EPS)
                return;

            try
            {
                Grid grid = Grid.Create(doc, Line.CreateBound(a, b));
                try
                {
                    grid.Name = nome;
                }
                catch
                {
                    // nome ja' em uso: mantem o gerado pelo Revit.
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[GerarPortico] falha ao criar eixo {Nome}", nome);
            }
        }

        private static string LetraEixo(int indice)
        {
            if (indice < 26)
                return ((char)('A' + indice)).ToString();
            return "E" + (indice + 1);
        }

        // ===== Placas de base (opcional) =====
        private static int LancarPlacasBase(Document doc, ICollection<ElementId> pilarIds)
        {
            try
            {
                IList<FamilySymbol> compativeis = PlacaBaseLancamentoService.CollectCompatibleSymbols(doc);
                if (compativeis.Count == 0)
                    return 0;

                FamilySymbol pb = compativeis[0];
                PlacaBaseConfig pbConfig = new PlacaBaseConfig
                {
                    FamilyName = pb.FamilyName,
                    TypeName = pb.Name,
                    FamilySymbolId = pb.Id
                };
                PlacaBaseLancamentoResultado res =
                    new PlacaBaseLancamentoService().Lancar(doc, pbConfig, pilarIds);
                return res.PlacasInseridas;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[GerarPortico] falha ao lançar placas de base");
                return 0;
            }
        }

        // ===== Ligação de terça (opcional) — reusa ConexaoTercasService headless =====
        private static int InserirLigacoesTerca(UIDocument uidoc, Document doc, GerarPorticoConfig config,
            List<ElementId> tercaIds, List<ElementId> banzoSupIds)
        {
            FamilySymbol? symbol = config.SymbolLigacaoTerca;
            if (symbol == null)
                return 0;

            try
            {
                if (!symbol.IsActive)
                {
                    using (Transaction t = new Transaction(doc, "Ativar conexão de terça"))
                    {
                        t.Start();
                        symbol.Activate();
                        doc.Regenerate();
                        t.Commit();
                    }
                }

                List<Reference> tercasRefs = tercaIds.Select(doc.GetElement).OfType<Element>().Select(el => new Reference(el)).ToList();
                List<Reference> vigasRefs = banzoSupIds.Select(doc.GetElement).OfType<Element>().Select(el => new Reference(el)).ToList();
                ConexaoTercasConfig cfg = new ConexaoTercasConfig
                {
                    SymbolSelecionado = symbol,
                    ColocarExtremidades = true,
                    ColocarMeio = false,
                    Referencia = ReferenciaChapa.Cruzamento,
                    VigasRefs = vigasRefs,
                    OffsetVerticalAdicionalMm = config.LigacaoOffsetZmm,
                    OffsetLateralMm = config.LigacaoOffsetXmm,
                    InverterFace = config.LigacaoInverterFace
                };
                new ConexaoTercasService().Executar(uidoc, doc, cfg, tercasRefs);
                return tercasRefs.Count;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[GerarPortico] falha ao inserir ligações de terça");
                return 0;
            }
        }

        // ===== Fundações (opcional) — reusa PfFoundationPlacementService headless =====
        private static List<ElementId> LancarFundacoes(UIDocument uidoc, Document doc, FamilySymbol symbol, List<ElementId> pilarIds)
        {
            ICollection<ElementId> selAntes = uidoc.Selection.GetElementIds();
            try
            {
                var antes = new HashSet<ElementId>(ColetarFundacoes(doc));
                uidoc.Selection.SetElementIds(pilarIds);
                PfFoundationPlacementConfig fcfg = new PfFoundationPlacementConfig
                {
                    SymbolId = symbol.Id,
                    Escopo = PfFoundationPlacementScope.SelecaoAtual,
                    OrientarPeloPilar = true,
                    IgnorarSeJaExisteFundacao = true
                };
                new PfFoundationPlacementService().Execute(uidoc, fcfg);
                return ColetarFundacoes(doc).Where(id => !antes.Contains(id)).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[GerarPortico] falha ao lançar fundações");
                return new List<ElementId>();
            }
            finally
            {
                // nao deixa a selecao do usuario bagunçada com os pilares.
                uidoc.Selection.SetElementIds(selAntes);
            }
        }

        private static ICollection<ElementId> ColetarFundacoes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .ToElementIds();
        }

        // ===== Armadura de fundação (opt-in, best-effort) — reusa BlocoFundacaoRebarOrchestrator =====
        private static (int armadas, int puladas, string motivo) ArmarFundacoes(UIDocument uidoc, Document doc, List<ElementId> fundacaoIds)
        {
            try
            {
                // precisa de pelo menos um tipo de vergalhao (RebarBarType) no projeto.
                RebarBarType? barType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .OrderBy(b => b.Name)
                    .FirstOrDefault();
                if (barType == null)
                    return (0, fundacaoIds.Count, "nenhum tipo de vergalhão (RebarBarType) carregado no projeto");

                List<Element> hosts = fundacaoIds.Select(doc.GetElement).OfType<Element>().ToList();
                List<Element> armaveis = hosts.Where(BlockGeometryService.CanHostRebar).ToList();
                int puladas = hosts.Count - armaveis.Count;
                if (armaveis.Count == 0)
                    return (0, puladas, "a família da fundação não aceita armadura (use sapata de concreto estrutural)");

                BlocoFundacaoRebarConfig cfg = MontarArmaduraSapata(barType.Name);
                var run = new BlocoFundacaoRebarOrchestrator().Execute(uidoc, armaveis, cfg, mostrarResumo: false);
                int armadas = run.hostsOk; // conta so as que realmente geraram barra
                int totalPuladas = fundacaoIds.Count - armadas;

                // duas causas distintas para uma fundacao ficar sem armadura — reportar separadas:
                int semSuporte = puladas;                       // familia nao aceita armadura
                int semBarra = armaveis.Count - armadas;        // aceita, mas nao gerou barra
                var motivos = new List<string>();
                if (semSuporte > 0)
                    motivos.Add($"{semSuporte} sem suporte a armadura (use sapata de concreto estrutural)");
                if (semBarra > 0)
                    motivos.Add($"{semBarra} sem barras geradas (verifique dimensões/cobrimento da sapata)");
                string motivo = string.Join("; ", motivos);
                return (armadas, totalPuladas, motivo);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[GerarPortico] falha ao armar fundações");
                return (0, fundacaoIds.Count, "erro ao gerar armadura");
            }
        }

        /// <summary>Padrao de armadura de sapata isolada: malha inferior X+Y por espacamento,
        /// cobrimento 5 cm, com gancho de 10 cm para cima nas pontas das barras.</summary>
        private static BlocoFundacaoRebarConfig MontarArmaduraSapata(string barTypeName)
        {
            BlocoFundacaoRebarConfig cfg = new BlocoFundacaoRebarConfig();
            cfg.LancarArmaduraInferior = true;
            foreach (BlocoBarraDirecaoConfig dir in new[] { cfg.ArmaduraInferior.BarsX, cfg.ArmaduraInferior.BarsY })
            {
                dir.Ativo = true;
                dir.BarTypeName = barTypeName;
                dir.CobrimentoCm = 5.0;
                dir.ModoQuantidade = BlocoModoQuantidade.PorEspacamento;
                dir.EspacamentoCm = 15.0;
                dir.Dobra.HaDobraInicial = true;
                dir.Dobra.HaDobraFinal = true;
                dir.Dobra.ComprimentoDobraInicialCm = 10.0;
                dir.Dobra.ComprimentoDobraFinalCm = 10.0;
                dir.Dobra.DobraInicialParaCima = true;
                dir.Dobra.DobraFinalParaCima = true;
            }
            return cfg;
        }
    }
}
