using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Windows;
using System.Windows.Media.Imaging;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Infrastructure.CrashReporting;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Infrastructure.Update;
using FerramentaEMT.Licensing;
using FerramentaEMT.Models.Privacy;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT
{
    public class App : IExternalApplication
    {
        // PR-2 (auto-update): expoe o resultado da ultima verificacao em background
        // para a UI consumir quando usuario clicar num comando.
        internal static UpdateCheckResult LastUpdateCheckResult { get; set; }

        public Result OnStartup(UIControlledApplication application)
        {
            // Sprint 1: inicializar logging estruturado ANTES de qualquer coisa
            // (assim qualquer falha de boot fica registrada)
            Logger.Initialize();
            Logger.Info("App.OnStartup — registrando ribbon");

            // PR-2: wirar UpdateLog facade para o Logger real (subsistema de Update
            // foi escrito sem dep de Serilog para ser testavel em xUnit)
            WireUpdateLog();

            // PR-2 (auto-update): aplicar update pendente ANTES de carregar qualquer
            // componente do plugin (CLR ainda nao carregou Services, Commands, etc).
            // Falha aqui nao impede boot — apenas marca retry para o proximo startup.
            try
            {
                ApplyResult applyResult = new UpdateApplier().ApplyPendingIfAny();
                if (applyResult == ApplyResult.Applied)
                {
                    Logger.Info("[Update] aplicado no boot — recarregando do disco");
                }
            }
            catch (Exception updEx)
            {
                Logger.Warn(updEx, "[Update] falha ao aplicar pending — boot continua");
            }

            // 1.3.0: captura crashes nao-observados em arquivo local
            CrashReporter.Initialize();

            // PR-3 (P0.3): crash reporting REMOTO via Sentry. DEPOIS do
            // CrashReporter (que ja escreveu o handler de unhandled), e
            // ANTES de LicenseService — assim crashes do proprio License
            // tambem sao capturados. SentryReporter eh idempotente e
            // silently no-op em DSN ausente / consent denied / falha de Init.
            SentryStartupWiring.InitializeServices(
                privacyStore: new PrivacySettingsStore(),
                hubFactory: () => new SentryHubFacade(),
                licenseStateResolver: ResolveLicenseStateForSentry,
                logInfo: msg => Logger.Info(msg),
                logInfoTemplate: (template, args) => Logger.Info(template, args),
                logWarn: (ex, msg) => Logger.Warn(ex, msg),
                releaseResolver: () => typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown");

            // 1.0.0: inicializar sistema de licenca (cria trial na primeira execucao)
            try { LicenseService.Initialize(); }
            catch (Exception licEx) { Logger.Error(licEx, "[App] LicenseService.Initialize falhou — continuar mesmo assim"); }

            // PR-2: disparar verificacao de update em background. NAO bloqueia o boot.
            // Resultado fica em LastUpdateCheckResult; UI consome quando usuario
            // interage com o plugin pela primeira vez.
            StartUpdateCheckBackground();

            // PR-3 (P0.3): se a versao do consent persistido for menor que a
            // do codigo (PR-2 → 1, PR-3 → 2), reabrir PrivacyConsentWindow no
            // primeiro Idling — NAO bloqueia o boot do Revit (modal em
            // OnStartup teria esse risco). Self-detach atomic: o handler
            // se desinscreve PRIMEIRO, sem flag externa.
            try { application.Idling += OnFirstIdling; }
            catch (Exception idlEx) { Logger.Warn(idlEx, "[Privacy] falha ao registrar Idling handler"); }

            // 1.3.0: logar fonte do segredo HMAC
            try
            {
                LicenseSecretProvider.SecretSource source = LicenseSecretProvider.GetResolvedSource();
                Logger.Info("[Licensing] Segredo HMAC carregado de {Source}", source);
            }
            catch (Exception secEx) { Logger.Error(secEx, "[App] Falha ao consultar fonte do segredo HMAC"); }

            // Incorporacao Victor Wave 2: split em duas abas.
            //   tabName    = "Ferramenta EMT"  → SO os paineis PF (armadura de concreto pre-fabricado)
            //   eccTabName = "Ferramentas ECC" → paineis gerais (modelagem, estrutura, fabricacao, QA, montagem, licenca)
            string tabName = "Ferramenta EMT";
            string eccTabName = "Ferramentas ECC";
            RevitWindowThemeService.Initialize(application);

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception ex)
            {
                // Aba já existe — esperado quando o plugin recarrega
                Logger.Debug("CreateRibbonTab (EMT): aba ja existe ({Msg})", ex.Message);
            }

            try
            {
                application.CreateRibbonTab(eccTabName);
            }
            catch (Exception ex)
            {
                Logger.Debug("CreateRibbonTab (ECC): aba ja existe ({Msg})", ex.Message);
            }

            // --- Paineis na aba "Ferramentas ECC" (fluxo geral) ---
            RibbonPanel panelModelagem = GetOrCreatePanel(application, eccTabName, "Modelagem");
            RibbonPanel panelEstrutura = GetOrCreatePanel(application, eccTabName, "Estrutura");
            RibbonPanel panelVigas = GetOrCreatePanel(application, eccTabName, "Vigas");
            RibbonPanel panelVista = GetOrCreatePanel(application, eccTabName, "Vista");
            RibbonPanel panelDocumentacao = GetOrCreatePanel(application, eccTabName, "Documentação");

            // --- Paineis na aba "Ferramenta EMT" (so fluxo PF) ---
            RibbonPanel panelPfConstrucao = GetOrCreatePanel(application, tabName, "PF Construção");
            RibbonPanel panelPfDocumentacao = GetOrCreatePanel(application, tabName, "PF Documentação");
            RibbonPanel panelPfArmaduras = GetOrCreatePanel(application, tabName, "PF Armaduras");

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddButton(
                panelModelagem,
                "btnLancarPipeRack",
                "Pipe\nRack",
                assemblyPath,
                "FerramentaEMT.Commands.CmdLancarPipeRack",
                "Gera a Fase 1 do pipe rack com pilares, vigas, treliça superior, pentes e contraventamento básico.",
                "piperack_large.png",
                "piperack_small.png"
            );

            AddButton(
                panelModelagem,
                "btnLancarEscada",
                "Escada",
                assemblyPath,
                "FerramentaEMT.Commands.CmdLancarEscada",
                "Gera longarinas inclinadas e degraus horizontais de uma escada convencional entre dois pontos.",
                "escada_large.png",
                "escada_small.png"
            );

            AddButton(
                panelModelagem,
                "btnLancarGuardaCorpo",
                "Guarda-\nCorpo",
                assemblyPath,
                "FerramentaEMT.Commands.CmdLancarGuardaCorpo",
                "Lança guarda-corpo por dois pontos com altura configurável e postes automáticos.",
                "guardaropo_large.png",
                "guardaropo_small.png"
            );

            AddButton(
                panelEstrutura,
                "btnGerarTercasPlano",
                "Terças",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarTercasPlano",
                "Permite definir o plano pelo plano de trabalho atual da vista ou por face plana e gera as terças com opção de divisão nos banzos.",
                "tercas_large.png",
                "tercas_small.png"
            );

            AddButton(
                panelEstrutura,
                "btnGerarTravamentos",
                "Travamentos",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarTravamentos",
                "Gera tirantes e frechais a partir das terças selecionadas.",
                "travamentos_large.png",
                "travamentos_small.png"
            );

            AddButton(
                panelEstrutura,
                "btnGerarTrelica",
                "Treliça",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarTrelica",
                "Gera uma treliça entre as terças selecionadas, com montantes em todos os vãos e diagonais opcionais.",
                "trelica_large.png",
                "trelica_small.png"
            );

            AddButton(
                panelEstrutura,
                "btnCortarElementos",
                "Cortar\nElementos",
                assemblyPath,
                "FerramentaEMT.Commands.CmdCortarElementos",
                "Seleciona pisos, quadros estruturais e colunas/pilares; localiza interferencias e aplica corte automatico (JoinGeometry ou SolidSolidCut).",
                "viga_dividida_large.png",
                "viga_dividida_small.png"
            );

            AddButton(
                panelVigas,
                "btnAjustarEncontroVigas",
                "Encontro",
                assemblyPath,
                "FerramentaEMT.Commands.CmdAjustarEncontroVigas",
                "Ajusta encontros entre viga principal e viga ou pilar a partir do ponto clicado, priorizando uniao, referencia de extremidade e coping.",
                "viga_encontro_large.png",
                "viga_encontro_small.png"
            );

            AddButton(
                panelVigas,
                "btnCortarPerfilInterferencia",
                "Seccionar\nViga",
                assemblyPath,
                "FerramentaEMT.Commands.CmdCortarPerfilPorInterferencia",
                "Seleciona uma viga e varios elementos de referencia para gerar multiplos cortes de uma vez.",
                "viga_dividida_large.png",
                "viga_dividida_small.png"
            );

            AddStackedButtons(
                panelVigas,
                "btnDesabilitarUniaoVigasSelecao",
                "Sem União\nSeleção",
                assemblyPath,
                "FerramentaEMT.Commands.CmdDesabilitarUniaoVigasSelecao",
                "Desabilita a união nos dois extremos das vigas selecionadas.",
                "viga_sem_uniao_selecao_large.png",
                "viga_sem_uniao_selecao_small.png",
                "btnDesabilitarUniaoVigasVista",
                "Sem União\nVista",
                "FerramentaEMT.Commands.CmdDesabilitarUniaoVigasVista",
                "Desabilita a união nos dois extremos de todas as vigas da vista ativa.",
                "viga_sem_uniao_vista_large.png",
                "viga_sem_uniao_vista_small.png"
            );

            AddButton(
                panelVista,
                "btnIsolarVigasEstruturais",
                "Isolar\nVigas",
                assemblyPath,
                "FerramentaEMT.Commands.CmdIsolarVigasEstruturais",
                "Isola temporariamente apenas as vigas estruturais na vista ativa.",
                "beam_isolar_large.png",
                "beam_isolar_small.png"
            );

            AddButton(
                panelVista,
                "btnIsolarPilaresEstruturais",
                "Isolar\nPilares",
                assemblyPath,
                "FerramentaEMT.Commands.CmdIsolarPilaresEstruturais",
                "Isola temporariamente apenas os pilares estruturais na vista ativa.",
                "column_line_large.png",
                "column_line_small.png"
            );

            AddButton(
                panelVista,
                "btnAgruparPilaresPorTipo",
                "Agrupar\nPilares",
                assemblyPath,
                "FerramentaEMT.Commands.CmdAgruparPilaresPorTipo",
                "Agrupa pilares iguais por tipo com destaque visual por conjunto, evitando grupos nativos que possam conflitar com eixos.",
                "agruparpilares_large.png",
                "agruparpilares_small.png"
            );

            AddButton(
                panelVista,
                "btnAgruparVigasPorTipo",
                "Agrupar\nVigas",
                assemblyPath,
                "FerramentaEMT.Commands.CmdAgruparVigasPorTipo",
                "Agrupa vigas iguais por tipo, colore cada conjunto e cria grupos EMT.",
                "agruparvigas_large.png",
                "agruparvigas_small.png"
            );

            AddButton(
                panelVista,
                "btnLimparAgrupamentosVisuais",
                "Limpar",
                assemblyPath,
                "FerramentaEMT.Commands.CmdLimparAgrupamentosVisuais",
                "Remove as cores aplicadas na vista ativa e desfaz os grupos EMT criados para pilares e vigas.",
                "broom_large.png",
                "broom_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnNumerarItens",
                "Numerar",
                assemblyPath,
                "FerramentaEMT.Commands.CmdNumerarItens",
                "Numera elementos manualmente por ordem de clique com filtros, avanço/retrocesso e destaque visual dos itens já processados.",
                "tag_large.png",
                "tag_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnExportarListaMateriais",
                "Exportar",
                assemblyPath,
                "FerramentaEMT.Commands.CmdExportarListaMateriais",
                "Exporta uma lista de materiais estruturais para Excel com abas de perfis lineares, chapas/conexões e resumo consolidado.",
                "sheets_large.png",
                "sheets_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnGerarCotasAlinhamento",
                "Cotas\nAlinhamento",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarCotasPorAlinhamento",
                "Selecione os elementos e clique no lado onde a cota deve ficar. A ferramenta agrupa os alinhamentos automaticamente e gera as cotas na vista ativa.",
                "ruler_large.png",
                "ruler_small.png"
            );

            // Sprint 1 (Bug B5): registrar CmdGerarCotasPorEixo (estava orfao)
            AddButton(
                panelDocumentacao,
                "btnGerarCotasEixo",
                "Cotas\npor Eixo",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarCotasPorEixo",
                "Gera cotas automaticas perpendiculares aos eixos do projeto. Detecta interseccoes com vigas e pilares na vista ativa.",
                "ruler_large.png",
                "ruler_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnCotarTrelica",
                "Cotar\nTreliça",
                assemblyPath,
                "FerramentaEMT.Commands.CmdCotarTrelica",
                "Aplica cotagem EMT em 5 faixas (painéis superior/inferior, vão total, vãos parciais, alturas) sobre elevação/corte de treliça selecionada.",
                "ruler_large.png",
                "ruler_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnTagearTrelica",
                "Tagear\nTreliça",
                assemblyPath,
                "FerramentaEMT.Commands.CmdTagearTrelica",
                "Identifica perfis (banzos, montantes, diagonais) diretamente sobre a elevação da treliça com tags padronizadas EMT.",
                "tag_large.png",
                "tag_small.png"
            );

            AddButton(
                panelDocumentacao,
                "btnIdentificarPerfil",
                "Identificar\nPerfil",
                assemblyPath,
                "FerramentaEMT.Commands.CmdIdentificarPerfil",
                "Identifica perfis estruturais selecionados com tag ou TextNote contendo nome do perfil, comprimento e quantidade agrupada.",
                "tag_large.png",
                "tag_small.png"
            );

            // --- Painéis PF (Pré-Fabricado de Concreto) ---
            AddButton(
                panelPfConstrucao,
                "btnPfNomearElementos",
                "Nomear\nPF",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfNomearElementos",
                "Nomeia pilares, vigas e lajes PF com filtros por família, tipo e parâmetro, no mesmo padrão da rotina Numerar Itens.",
                "numeracao_large.png",
                "numeracao_small.png"
            );

            AddButton(
                panelPfConstrucao,
                "btnPfIsolarPilaresConsolos",
                "Isolar\nP+Cons.",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfIsolarPilaresConsolos",
                "Isola na vista ativa os pilares estruturais e as famílias PF com modelo Consolo.",
                "column_line_large.png",
                "column_line_small.png"
            );

            AddButton(
                panelPfConstrucao,
                "btnPfIsolarLajes",
                "Isolar\nLajes",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfIsolarLajes",
                "Isola famílias PF cuja tipagem esteja marcada com Modelo = Laje.",
                "beam_isolar_large.png",
                "beam_isolar_small.png"
            );

            AddButton(
                panelPfDocumentacao,
                "btnPfElevacaoPilares",
                "Elevação\nPilar",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfElevacaoFormaPilares",
                "Gera elevação e corte transversal para pilares estruturais, sem depender do Dynamo.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            AddButton(
                panelPfDocumentacao,
                "btnPfElevacaoVigas",
                "Elevação\nVigas",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfElevacaoFormaVigas",
                "Gera elevação e corte transversal para vigas estruturais, sem depender do Dynamo.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfEstribosPilar",
                "Estribos\nPilar",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirEstribosPilar",
                "Lança estribos em pilares estruturais com cobrimento, espaçamento e quantidade definidos na interface.",
                "column_line_large.png",
                "column_line_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosPilar",
                "Acos\nPilar",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirAcosPilar",
                "Lança barras longitudinais em pilares estruturais com escolha do tipo de vergalhão e posições.",
                "pilar_concreto_large.png",
                "pilar_concreto_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfEstribosViga",
                "Estribos\nViga",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirEstribosViga",
                "Lança estribos em vigas com zonas de apoio e corpo central usando a Revit API.",
                "viga_w_large.png",
                "viga_w_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosViga",
                "Acos\nViga",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirAcosViga",
                "Lança barras superiores, inferiores e laterais em vigas estruturais sem usar Dynamo.",
                "viga_w_large.png",
                "viga_w_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosConsolo",
                "Acos\nConsolo",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirAcosConsolo",
                "Lança a armadura base de consolos PF com tirantes, suspensões e estribos no fluxo C#.",
                "column_line_large.png",
                "column_line_small.png"
            );

            // Incorporacao Victor Wave 2: novo comando de bloco de duas estacas
            AddButton(
                panelPfArmaduras,
                "btnPfAcosBlocoDuasEstacas",
                "Aços Bloco\n2 Estacas",
                assemblyPath,
                "FerramentaEMT.Commands.PF.CmdPfInserirAcosBlocoDuasEstacas",
                "Lança barras superiores, inferiores e laterais em blocos de duas estacas com a mesma lógica base usada nas vigas.",
                "pilar_concreto_large.png",
                "pilar_concreto_small.png"
            );

            // --- Painel Fabricação (novos módulos) ---
            RibbonPanel panelFabricacao = GetOrCreatePanel(application, eccTabName, "Fabricação");

            AddButton(
                panelFabricacao,
                "btnGerarVistaPeca",
                "Vista de\nPeça",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarVistaPeca",
                "Gera vistas de detalhe (longitudinal e transversal) para peças estruturais, voltadas para shop drawings de fabricação metálica.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            AddButton(
                panelFabricacao,
                "btnCotarPecaFabricacao",
                "Cotar\nFabricação",
                assemblyPath,
                "FerramentaEMT.Commands.CmdCotarPecaFabricacao",
                "Adiciona cotas automáticas de fabricação: comprimento total, altura do perfil, largura da mesa, furos e distâncias de borda.",
                "cotar_fabricacao_large.png",
                "cotar_fabricacao_small.png"
            );

            AddButton(
                panelFabricacao,
                "btnMarcarPecas",
                "Marcar\nPeças",
                assemblyPath,
                "FerramentaEMT.Commands.CmdMarcarPecas",
                "Agrupa peças idênticas por assinatura de fabricação (tipo + perfil + material + comprimento) e atribui marcas únicas automaticamente.",
                "marca_peca_large.png",
                "marca_peca_small.png"
            );

            // --- Painel CNC (Sprint 5) ---
            RibbonPanel panelCnc = GetOrCreatePanel(application, eccTabName, "CNC");

            AddButton(
                panelCnc,
                "btnExportarDstv",
                "Exportar\nDSTV/NC1",
                assemblyPath,
                "FerramentaEMT.Commands.CmdExportarDstv",
                "Gera arquivos .nc1 (formato DSTV) compatíveis com máquinas CNC de corte de aço estrutural. Suporta agrupamento por marca de peça.",
                "sheets_large.png",
                "sheets_small.png"
            );

            // --- Painel QA (Sprint 6) ---
            RibbonPanel panelQa = GetOrCreatePanel(application, eccTabName, "Verificação");

            AddButton(
                panelQa,
                "btnVerificarModelo",
                "Verificar\nModelo",
                assemblyPath,
                "FerramentaEMT.Commands.CmdVerificarModelo",
                "Roda múltiplas regras de validação no modelo (peças sem marca, sem material, perfis sobrepostos, etc.) e gera relatório consolidado.",
                "broom_large.png",
                "broom_small.png"
            );

            // --- Painel Montagem (Sprint 7) ---
            RibbonPanel panelMontagem = GetOrCreatePanel(application, eccTabName, "Montagem");

            AddButton(
                panelMontagem,
                "btnPlanoMontagem",
                "Plano de\nMontagem",
                assemblyPath,
                "FerramentaEMT.Commands.CmdPlanoMontagem",
                "Atribui etapas de montagem a peças estruturais e aplica destaque visual por etapa para gerar planos de erection sequence.",
                "agruparvigas_large.png",
                "agruparvigas_small.png"
            );

            AddButton(
                panelMontagem,
                "btnGerarConexao",
                "Gerar\nConexão",
                assemblyPath,
                "FerramentaEMT.Commands.CmdGerarConexao",
                "Gera conexões metálicas (chapa de ponta, dupla cantoneira, chapa gusset) entre vigas e pilares ou entre vigas. Calcula bolt count para integração com lista de materiais.",
                "viga_encontro_large.png",
                "viga_encontro_small.png"
            );

            // --- Painel Licença (1.0.0) ---
            RibbonPanel panelLicenca = GetOrCreatePanel(application, eccTabName, "Licença");

            AddStackedButtons(
                panelLicenca,
                "btnAtivarLicenca",
                "Ativar Licença",
                assemblyPath,
                "FerramentaEMT.Commands.CmdAtivarLicenca",
                "Cole sua chave de licença para ativar o plugin nesta máquina.",
                null,
                null,
                "btnSobre",
                "Sobre",
                "FerramentaEMT.Commands.CmdSobre",
                "Versão, estado da licença e identificador desta máquina.",
                null,
                null
            );

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Logger.Info("App.OnShutdown");

            // PR-3: drena eventos pendentes do Sentry antes de fechar (max 2s).
            // No-op silencioso se Sentry nao foi inicializado.
            try { SentryReporter.Flush(2000); }
            catch (Exception flushEx) { Logger.Warn(flushEx, "[Sentry] Flush em OnShutdown falhou"); }

            RevitWindowThemeService.Shutdown();
            Logger.Shutdown();
            return Result.Succeeded;
        }

        private RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            foreach (RibbonPanel p in app.GetRibbonPanels(tabName))
            {
                if (p.Name == panelName)
                    return p;
            }

            return app.CreateRibbonPanel(tabName, panelName);
        }

        private void AddButton(
            RibbonPanel panel,
            string internalName,
            string buttonText,
            string assemblyPath,
            string className,
            string tooltip,
            string largeImageName,
            string smallImageName)
        {
            PushButtonData data = CreateButtonData(
                internalName,
                buttonText,
                assemblyPath,
                className,
                largeImageName,
                smallImageName);

            PushButton button = panel.AddItem(data) as PushButton;
            if (button != null)
                button.ToolTip = tooltip;
        }

        private void AddStackedButtons(
            RibbonPanel panel,
            string internalName1,
            string buttonText1,
            string assemblyPath,
            string className1,
            string tooltip1,
            string largeImageName1,
            string smallImageName1,
            string internalName2,
            string buttonText2,
            string className2,
            string tooltip2,
            string largeImageName2,
            string smallImageName2)
        {
            PushButtonData data1 = CreateButtonData(
                internalName1,
                buttonText1,
                assemblyPath,
                className1,
                largeImageName1,
                smallImageName1);

            PushButtonData data2 = CreateButtonData(
                internalName2,
                buttonText2,
                assemblyPath,
                className2,
                largeImageName2,
                smallImageName2);

            IList<RibbonItem> items = panel.AddStackedItems(data1, data2);
            if (items.Count > 0 && items[0] is PushButton button1)
                button1.ToolTip = tooltip1;
            if (items.Count > 1 && items[1] is PushButton button2)
                button2.ToolTip = tooltip2;
        }

        private PushButtonData CreateButtonData(
            string internalName,
            string buttonText,
            string assemblyPath,
            string className,
            string largeImageName,
            string smallImageName)
        {
            PushButtonData data = new PushButtonData(
                internalName,
                buttonText,
                assemblyPath,
                className);

            if (!string.IsNullOrEmpty(largeImageName))
                data.LargeImage = LoadImage(largeImageName);

            if (!string.IsNullOrEmpty(smallImageName))
                data.Image = LoadImage(smallImageName);

            return data;
        }

        private BitmapImage LoadImage(string imageName)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string diskPath = Path.Combine(assemblyDir, "Resources", imageName);

            if (File.Exists(diskPath))
            {
                using (var fileStream = File.OpenRead(diskPath))
                {
                    var diskImage = new BitmapImage();
                    diskImage.BeginInit();
                    diskImage.CacheOption = BitmapCacheOption.OnLoad;
                    diskImage.StreamSource = fileStream;
                    diskImage.EndInit();
                    diskImage.Freeze();
                    return diskImage;
                }
            }

            string resourceName = $"FerramentaEMT.Resources.{imageName}";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }

            return null;
        }

        // ===========================================================
        // PR-3 — Crash reporting helpers
        // ===========================================================

        /// <summary>
        /// Resolve o license state para o Sentry. Chamado lazy a CADA evento
        /// (em SentryOptionsBuilder.BeforeSend), entao reflete o estado
        /// corrente da licenca, nao o de boot. Try/catch raiz: nunca lanca.
        /// </summary>
        private static string ResolveLicenseStateForSentry()
        {
            try { return LicenseService.GetCurrentState().Status.ToString(); }
            catch { return "Unknown"; }
        }

        /// <summary>
        /// Handler do primeiro Idling event. Auto-desinscreve PRIMEIRO
        /// (idempotencia atomica — sem flag externa) e entao verifica se
        /// PrivacyConsentWindow precisa reabrir. Erros isolados em try/catch
        /// pra nao deixar Revit num estado ruim.
        /// </summary>
        private void OnFirstIdling(object sender, IdlingEventArgs e)
        {
            UIApplication uiApp = sender as UIApplication;
            if (uiApp != null)
            {
                uiApp.Idling -= OnFirstIdling;
            }

            try { EnsureConsentIfNeeded(); }
            catch (Exception ex) { Logger.Warn(ex, "[Privacy] consent dialog falhou"); }
        }

        /// <summary>
        /// Reabre a PrivacyConsentWindow se ConsentVersion persistido for
        /// menor que CurrentConsentVersion do codigo. Preserva fields
        /// transientes do PR-2 (LastUpdateCheckUtc, SkippedUpdateVersion).
        /// </summary>
        private static void EnsureConsentIfNeeded()
        {
            PrivacySettingsStore store = new PrivacySettingsStore();
            PrivacySettings current = store.Load();

            if (current.ConsentVersion >= PrivacyConsentWindow.CurrentConsentVersion)
                return;

            Logger.Info(
                "[Privacy] reabrindo consent via Idling event (consent version: {Persisted} -> {Current})",
                current.ConsentVersion, PrivacyConsentWindow.CurrentConsentVersion);

            PrivacyConsentWindow consent = new PrivacyConsentWindow(current);
            bool? result = consent.ShowDialog();
            if (result == true && consent.Result != null)
            {
                // Preserva campos do PR-2 que a janela nao toca.
                consent.Result.LastUpdateCheckUtc = current.LastUpdateCheckUtc;
                consent.Result.SkippedUpdateVersion = current.SkippedUpdateVersion ?? string.Empty;
                store.Save(consent.Result);
                Logger.Info(
                    "[Privacy] consent salvo (CrashReports={Crash}, AutoUpdate={Update})",
                    consent.Result.CrashReports, consent.Result.AutoUpdate);
                // Trade-off documentado no ADR-007: se usuario consentiu agora
                // e Sentry ja inicializou como IsEnabled=false, ele fica desligado
                // ate o proximo restart do Revit. SentryReporter eh idempotente
                // e re-init nao eh suportado pra evitar state inconsistente do SDK.
            }
        }

        // ===========================================================
        // PR-2 — Auto-update helpers
        // ===========================================================

        /// <summary>
        /// Conecta o UpdateLog facade (puro, no test csproj) ao Logger real
        /// (Serilog). Chamado uma vez no boot.
        /// </summary>
        private static void WireUpdateLog()
        {
            UpdateLog.Debug = (template, args) => Logger.Debug(template, args);
            UpdateLog.Info = (template, args) => Logger.Info(template, args);
            UpdateLog.Warn = (template, args) => Logger.Warn(template, args);
            UpdateLog.WarnException = (ex, template, args) => Logger.Warn(ex, template, args);
            UpdateLog.ErrorException = (ex, template, args) => Logger.Error(ex, template, args);
        }

        /// <summary>
        /// Dispara <see cref="UpdateCheckService.CheckAsync"/> em thread separada
        /// (Task.Run). NAO toca Revit API — checagem eh pura HTTP+JSON+filesystem.
        /// Falha NUNCA pode impedir boot do plugin: try/catch raiz na thread.
        /// </summary>
        private static void StartUpdateCheckBackground()
        {
            string version = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";

            // Fire-and-forget; resultado fica em App.LastUpdateCheckResult
            Task.Run(async () =>
            {
                try
                {
                    GitHubReleaseProvider provider = new GitHubReleaseProvider("Alefvieira233", "EMT");
                    PrivacySettingsStore store = new PrivacySettingsStore();
                    UpdateCheckService service = new UpdateCheckService(provider, store, version);

                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        UpdateCheckResult result = await service.CheckAsync(cts.Token).ConfigureAwait(false);
                        LastUpdateCheckResult = result;
                        Logger.Info("[Update] verificacao em background concluida: {Outcome}", result.Outcome);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[Update] excecao na thread de verificacao — boot nao afetado");
                }
            });
        }
    }
}
