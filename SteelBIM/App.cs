using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SteelBIM.Infrastructure;
using SteelBIM.Infrastructure.CrashReporting;
using SteelBIM.Infrastructure.Privacy;
using SteelBIM.Infrastructure.Telemetry;
using SteelBIM.Infrastructure.Update;
using SteelBIM.Licensing;
using SteelBIM.Models.Privacy;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM
{
    public class App : IExternalApplication
    {
        // PR-2 (auto-update): expoe o resultado da ultima verificacao em background
        // para a UI consumir quando usuario clicar num comando.
        internal static UpdateCheckResult LastUpdateCheckResult { get; set; }

        // PR-4: HttpClient compartilhado pelo TelemetryReporter. Lazy + singleton
        // pra evitar socket exhaustion (DefaultConnectionLimit gerenciado pelo CLR).
        private static readonly Lazy<HttpClient> _telemetryHttp = new Lazy<HttpClient>(
            () =>
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                return client;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);

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
            // PR-4: ao detectar Applied, agendamos a emissao do evento
            // 'update.applied' para depois que TelemetryReporter inicializar.
            string updateAppliedFromVersion = null;
            string updateAppliedToVersion = null;
            int updateAppliedAttempts = 0;
            try
            {
                // v2.7.10 (auditoria §5.3): Authenticode verify opt-in via setting.
                // Default false enquanto cert nao foi adquirido (ADR-009).
                IAuthenticodeVerifier verifier = null;
                try
                {
                    AppSettings settings = AppSettings.Load();
                    if (settings != null && settings.AuthenticodeVerifyEnabled)
                    {
                        verifier = new WinTrustAuthenticodeVerifier();
                    }
                }
                catch (Exception settingsEx)
                {
                    Logger.Warn(settingsEx, "[Update] falha ao carregar AppSettings — Authenticode pulado");
                }

                UpdateApplier applier = new UpdateApplier(verifier);
                ApplyResult applyResult = applier.ApplyPendingIfAny();
                if (applyResult == ApplyResult.Applied)
                {
                    Logger.Info("[Update] aplicado no boot — recarregando do disco");
                    updateAppliedFromVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
                    updateAppliedToVersion = applier.LastVersionAttempted ?? "unknown";
                    updateAppliedAttempts = applier.LastAttemptCount;
                }
                else if (applyResult == ApplyResult.SignatureInvalid)
                {
                    // v2.7.10 §5.3: Authenticode reprovou o DLL extraido —
                    // rollback ja foi feito, log error pra forensics e seguir boot
                    // com versao antiga (mais segura que abortar boot).
                    Logger.Error(
                        "[Update] assinatura Authenticode INVALIDA na versao {Version} — rollback aplicado, boot continua com versao antiga",
                        applier.LastVersionAttempted ?? "desconhecida");
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
                licenseStateResolver: ResolveLicenseStateForReporting,
                logInfo: msg => Logger.Info(msg),
                logInfoTemplate: (template, args) => Logger.Info(template, args),
                logWarn: (ex, msg) => Logger.Warn(ex, msg),
                releaseResolver: () => typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown");

            // 1.0.0: inicializar sistema de licenca (cria trial na primeira execucao)
            try
            { LicenseService.Initialize(); }
            catch (Exception licEx) { Logger.Error(licEx, "[App] LicenseService.Initialize falhou — continuar mesmo assim"); }

            // PR-4 (P0.6): telemetria de uso via PostHog (HTTP-direct).
            // DEPOIS de LicenseService.Initialize porque license.state_checked
            // emitido pelo proprio Init precisa do status real da licenca como
            // super property. Idempotente; silently no-op em DSN ausente,
            // consent denied, ou SDK falha.
            TelemetryStartupWiring.InitializeServices(
                privacyStore: new PrivacySettingsStore(),
                clientFactory: BuildPostHogTelemetryClient,
                licenseStateResolver: ResolveLicenseStateForReporting,
                logInfo: msg => Logger.Info(msg),
                logInfoTemplate: (template, args) => Logger.Info(template, args),
                logWarn: (ex, msg) => Logger.Warn(ex, msg),
                releaseResolver: () => typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown");

            // PR-4: emite update.applied se um update foi aplicado neste boot.
            // Telemetry ja inicializou (no-op silencioso se consent/api key
            // ausente).
            if (!string.IsNullOrEmpty(updateAppliedToVersion))
            {
                TelemetryReporter.Track(new TelemetryEvent(
                    SamplingDecider.EventUpdateApplied,
                    new Dictionary<string, object>
                    {
                        { "from_version", updateAppliedFromVersion ?? "unknown" },
                        { "to_version", updateAppliedToVersion },
                        { "attempts", updateAppliedAttempts },
                    }));
            }

            // PR-2: disparar verificacao de update em background. NAO bloqueia o boot.
            // Resultado fica em LastUpdateCheckResult; UI consome quando usuario
            // interage com o plugin pela primeira vez.
            StartUpdateCheckBackground();

            // PR-3 (P0.3): se a versao do consent persistido for menor que a
            // do codigo (PR-2 → 1, PR-3 → 2), reabrir PrivacyConsentWindow no
            // primeiro Idling — NAO bloqueia o boot do Revit (modal em
            // OnStartup teria esse risco). Self-detach atomic: o handler
            // se desinscreve PRIMEIRO, sem flag externa.
            try
            { application.Idling += OnFirstIdling; }
            catch (Exception idlEx) { Logger.Warn(idlEx, "[Privacy] falha ao registrar Idling handler"); }

            // v2.8.9: licenca agora usa assinatura assimetrica (ECDsa). O plugin embarca
            // apenas a chave PUBLICA de verificacao — nao ha mais segredo HMAC a resolver.
            Logger.Info("[Licensing] verificacao de licenca por chave publica embarcada (ECDsa P-256)");

            // v2.6.0: ribbon dividido em DUAS abas (decisao do Alef):
            //   "SteelBIM | Modelagem"    -> modelagem, conexoes, armaduras PF, visualizacao
            //   "SteelBIM | Detalhamento" -> vistas, cotagem, anotacao, CNC, sequenciamento, verificacao, licenca
            // Historico: v2.0.0 herdou 2 abas de v1.5.0; v2.1.0 unificou tudo numa
            // aba unica "SteelBIM"; v2.6.0 volta a separar — agora por FASE de
            // trabalho (modelar vs detalhar), mantendo a marca no prefixo da aba.
            // Apenas reorganizacao visual: nenhum command/service/AddInId mudou e
            // os internalName dos botoes foram preservados (atalhos do usuario OK).
            // v2.8.9: construcao do ribbon protegida por try/catch raiz. Uma excecao
            // aqui (ex.: internalName duplicado em reload, PNG corrompido, mudanca de
            // API do Revit) escaparia do OnStartup e o Revit DESABILITARIA o add-in
            // inteiro — o usuario pagante "fica sem a ferramenta". Melhor um ribbon
            // parcial (logado) que um plugin que nao carrega.
            try
            {
                RevitWindowThemeService.Initialize(application);

                string tabModelagem = "SteelBIM | Modelagem";
                string tabDetalhamento = "SteelBIM | Detalhamento";
                CreateRibbonTabSafe(application, tabModelagem);
                CreateRibbonTabSafe(application, tabDetalhamento);

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                BuildAbaModelagem(application, tabModelagem, assemblyPath);
                BuildAbaDetalhamento(application, tabDetalhamento, assemblyPath);
            }
            catch (Exception ribbonEx)
            {
                Logger.Error(ribbonEx,
                    "[App] Falha ao construir o ribbon — plugin continua carregado (ribbon pode estar parcial)");
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Logger.Info("App.OnShutdown");

            // PR-4: drena eventos pendentes da telemetria. PostHogHttpTelemetryClient
            // usa fire-and-forget sem batch buffer, entao Flush eh no-op imediato.
            try
            { TelemetryReporter.Flush(2000); }
            catch (Exception flushEx) { Logger.Warn(flushEx, "[Telemetry] Flush em OnShutdown falhou"); }

            // PR-3: drena eventos pendentes do Sentry antes de fechar (max 2s).
            // No-op silencioso se Sentry nao foi inicializado.
            try
            { SentryReporter.Flush(2000); }
            catch (Exception flushEx) { Logger.Warn(flushEx, "[Sentry] Flush em OnShutdown falhou"); }

            RevitWindowThemeService.Shutdown();
            Logger.Shutdown();
            return Result.Succeeded;
        }

        private static void CreateRibbonTabSafe(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception ex)
            {
                // Aba já existe — esperado quando o plugin recarrega
                Logger.Debug("CreateRibbonTab: aba ja existe ({Msg})", ex.Message);
            }
        }

        // v2.6.0: ABA "SteelBIM | Modelagem" — 7 paineis (modelagem geral,
        // estrutura metalica, operacoes em vigas, conexoes, PF construcao,
        // PF armaduras, visualizacao). Ordem dos paineis = ordem das chamadas
        // GetOrCreatePanel; ordem dos botoes = ordem das chamadas AddButton.
        private void BuildAbaModelagem(UIControlledApplication application, string tabName, string assemblyPath)
        {
            RibbonPanel panelModelagemGeral = GetOrCreatePanel(application, tabName, "Modelagem Geral");
            RibbonPanel panelEstruturaMetalica = GetOrCreatePanel(application, tabName, "Estrutura Metálica");
            RibbonPanel panelOperacoesVigas = GetOrCreatePanel(application, tabName, "Operações em Vigas");
            RibbonPanel panelConexoes = GetOrCreatePanel(application, tabName, "Conexões");
            RibbonPanel panelPfConstrucao = GetOrCreatePanel(application, tabName, "PF Construção");
            // v2.7.0: painel Importacao posicionado apos PF Construcao por afinidade
            // semantica (importacao de IFC -> compoe fluxo de modelagem + fundacao).
            // Nome ASCII deliberado: deixa margem pra futuros imports (Tekla XML,
            // AutoCAD DWG, etc) sem rebatizar painel.
            RibbonPanel panelImportacao = GetOrCreatePanel(application, tabName, "Importacao");
            RibbonPanel panelPfArmaduras = GetOrCreatePanel(application, tabName, "PF Armaduras");
            RibbonPanel panelVisualizacao = GetOrCreatePanel(application, tabName, "Visualização");

            // --- Modelagem Geral ---
            AddButton(
                panelModelagemGeral,
                "btnLancarPipeRack",
                "Pipe\nRack",
                assemblyPath,
                "SteelBIM.Commands.CmdLancarPipeRack",
                "Gera a Fase 1 do pipe rack com pilares, vigas, treliça superior, pentes e contraventamento básico.",
                "piperack_large.png",
                "piperack_small.png"
            );

            AddButton(
                panelModelagemGeral,
                "btnLancarEscada",
                "Escada",
                assemblyPath,
                "SteelBIM.Commands.CmdLancarEscada",
                "Gera longarinas inclinadas e degraus horizontais de uma escada convencional entre dois pontos.",
                "escada_large.png",
                "escada_small.png"
            );

            AddButton(
                panelModelagemGeral,
                "btnLancarGuardaCorpo",
                "Guarda-\nCorpo",
                assemblyPath,
                "SteelBIM.Commands.CmdLancarGuardaCorpo",
                "Lança guarda-corpo por dois pontos com altura configurável e postes automáticos.",
                "guardaropo_large.png",
                "guardaropo_small.png"
            );

            // --- Estrutura Metálica ---
            AddButton(
                panelEstruturaMetalica,
                "btnGerarTercasPlano",
                "Terças",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarTercasPlano",
                "Permite definir o plano pelo plano de trabalho atual da vista ou por face plana e gera as terças com opção de divisão nos banzos.",
                "tercas_large.png",
                "tercas_small.png"
            );

            AddButton(
                panelEstruturaMetalica,
                "btnGerarTravamentos",
                "Travamentos",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarTravamentos",
                "Gera tirantes e frechais a partir das terças selecionadas.",
                // v2.6.8: NAO revertido pra travamentos_large — esse icone ja esta em uso
                // por btnGerarContraventamentoPlano. Travamento != Contraventamento (secundario
                // transversal vs diagonal rigidez lateral). Aguardando Victor entregar icone proprio.
                "travamento_32_light.png",
                "travamento_32_light.png"
            );

            AddButton(
                panelEstruturaMetalica,
                "btnGerarTrelica",
                "Treliça",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarTrelica",
                "Gera uma treliça entre as terças selecionadas, com montantes em todos os vãos e diagonais opcionais.",
                "trelica_large.png",
                "trelica_small.png"
            );

            // v2.8.14: gerador de galpao completo em 1 clique (pilares + treliça/viga + terças +
            // contraventamentos + linha de corrente) a partir de uma janela.
            AddButton(
                panelEstruturaMetalica,
                "btnGerarPorticoCompleto",
                "Projeto\nCompleto",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarPorticoCompleto",
                "Gera um galpão completo (pilares, treliça/viga, terças, contraventamentos e linha de corrente) a partir de uma janela, com 1 clique.",
                "trelica_large.png",
                "trelica_small.png"
            );

            // Incorporacao Victor Final (Onda 5): Contraventamento e Placas de Base
            AddButton(
                panelEstruturaMetalica,
                "btnGerarContraventamentoPlano",
                "Contraven-\ntamento",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarContraventamentoPlano",
                "Cria um contraventamento em X no plano de trabalho ativo a partir de dois pontos opostos do painel.",
                "travamentos_large.png",
                "travamentos_small.png"
            );

            AddButton(
                panelEstruturaMetalica,
                "btnLancarPlacasBase",
                "Placas\nde Base",
                assemblyPath,
                "SteelBIM.Commands.CmdLancarPlacasBase",
                "Lanca automaticamente placas de base face-based sobre o topo do concreto encontrado abaixo dos pilares metalicos.",
                "column_line_large.png",
                "column_line_small.png"
            );

            // v2.8.1 (Victor): lancamento de conexoes estruturais em tercas selecionadas.
            AddButton(
                panelEstruturaMetalica,
                "btnInserirConexaoTercas",
                "Conexão\nTerça",
                assemblyPath,
                "SteelBIM.Commands.CmdInserirConexaoTercas",
                "Insere a conexao estrutural nas extremidades e/ou meio das tercas selecionadas, posicionada na face inferior da secao.",
                "link_large.png",
                "link_small.png"
            );

            // --- Operações em Vigas ---
            AddButton(
                panelOperacoesVigas,
                "btnCortarElementos",
                "Cortar\nElementos",
                assemblyPath,
                "SteelBIM.Commands.CmdCortarElementos",
                "Seleciona pisos, quadros estruturais e colunas/pilares; localiza interferencias e aplica corte automatico (JoinGeometry ou SolidSolidCut).",
                "viga_dividida_large.png",
                "viga_dividida_small.png"
            );

            AddButton(
                panelOperacoesVigas,
                "btnAjustarEncontroVigas",
                "Encontro",
                assemblyPath,
                "SteelBIM.Commands.CmdAjustarEncontroVigas",
                "Ajusta encontros entre viga principal e viga ou pilar a partir do ponto clicado, priorizando uniao, referencia de extremidade e coping.",
                "viga_encontro_large.png",
                "viga_encontro_small.png"
            );

            AddButton(
                panelOperacoesVigas,
                "btnCortarPerfilInterferencia",
                "Seccionar\nViga",
                assemblyPath,
                "SteelBIM.Commands.CmdCortarPerfilPorInterferencia",
                "Seleciona uma viga e varios elementos de referencia para gerar multiplos cortes de uma vez.",
                // v2.6.8: viga_dividida_large compartilhado com btnCortarElementos (semantico OK:
                // ambos sao operacoes de corte). Decisao OPCAO B do revert v2.6.3 -> Victor lucide_blue.
                "viga_dividida_large.png",
                "viga_dividida_small.png"
            );

            AddStackedButtons(
                panelOperacoesVigas,
                "btnDesabilitarUniaoVigasSelecao",
                "Sem União\nSeleção",
                assemblyPath,
                "SteelBIM.Commands.CmdDesabilitarUniaoVigasSelecao",
                "Desabilita a união nos dois extremos das vigas selecionadas.",
                "viga_sem_uniao_selecao_large.png",
                "viga_sem_uniao_selecao_small.png",
                "btnDesabilitarUniaoVigasVista",
                "Sem União\nVista",
                "SteelBIM.Commands.CmdDesabilitarUniaoVigasVista",
                "Desabilita a união nos dois extremos de todas as vigas da vista ativa.",
                "viga_sem_uniao_vista_large.png",
                "viga_sem_uniao_vista_small.png"
            );

            // --- Conexões ---
            AddButton(
                panelConexoes,
                "btnGerarConexao",
                "Gerar\nConexão",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarConexao",
                "Gera conexões metálicas (chapa de ponta, dupla cantoneira, chapa gusset) entre vigas e pilares ou entre vigas. Calcula bolt count para integração com lista de materiais.",
                "link_large.png",
                "link_small.png"
            );

            // --- PF Construção ---
            // Incorporacao Victor Final (Onda 5): Lancamento de fundacoes em massa
            AddButton(
                panelPfConstrucao,
                "btnPfLancarFundacoes",
                "Lancar\nFundacao",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfLancarFundacoes",
                "Localiza a base dos pilares e lanca a fundacao estrutural selecionada no centro dos pilares da selecao atual ou de todos os pilares visiveis na vista.",
                "foundation_piles_large.png",
                "foundation_piles_small.png"
            );

            // --- Importacao (v2.7.0) ---
            // Conversor IFC -> Perfis Nativos (co-autor 50/50 com Victor).
            // Converte DirectShape importado de IFC em FamilyInstance editavel.
            AddButton(
                panelImportacao,
                "btnConverterPerfilIfc",
                "Converter\nIFC -> Nativo",
                assemblyPath,
                "SteelBIM.Commands.CmdConverterPerfilIfc",
                "Converte elementos importados de IFC (DirectShape com parametros Ifc*) em " +
                "FamilyInstance nativos do Revit, agrupando por seccao + material e atribuindo " +
                "perfis correspondentes do projeto. Requer importacao previa do IFC via Insert > IFC.",
                "ifc_large.png",
                "ifc_small.png"
            );

            // --- PF Armaduras ---
            AddButton(
                panelPfArmaduras,
                "btnPfEstribosPilar",
                "Estribos\nPilar",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirEstribosPilar",
                "Lança estribos em pilares estruturais com cobrimento, espaçamento e quantidade definidos na interface.",
                "column_line_large.png",
                "column_line_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosPilar",
                "Acos\nPilar",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirAcosPilar",
                "Lança barras longitudinais em pilares estruturais com escolha do tipo de vergalhão e posições.",
                "pilar_concreto_large.png",
                "pilar_concreto_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfEstribosViga",
                "Estribos\nViga",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirEstribosViga",
                "Lança estribos em vigas com zonas de apoio e corpo central usando a Revit API.",
                "viga_w_large.png",
                "viga_w_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosViga",
                "Acos\nViga",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirAcosViga",
                "Lança barras superiores, inferiores e laterais em vigas estruturais sem usar Dynamo.",
                "viga_w_large.png",
                "viga_w_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfAcosConsolo",
                "Acos\nConsolo",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirAcosConsolo",
                "Lança a armadura base de consolos PF com tirantes, suspensões e estribos no fluxo C#.",
                "column_line_large.png",
                "column_line_small.png"
            );

            // Incorporacao Victor Final (Onda 5): editor unificado de armaduras de bloco
            // Substitui o antigo btnPfAcosBlocoDuasEstacas (que ficava limitado a 2 estacas).
            // O comando CmdPfInserirAcosBlocoDuasEstacas continua no codebase, apenas o botao saiu
            // do ribbon — atalhos personalizados do usuario continuam funcionando.
            AddButton(
                panelPfArmaduras,
                "btnBlocoFundacaoArmaduras",
                "Armaduras\nBloco",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdBlocoFundacaoArmaduras",
                "Editor manual completo de armaduras para blocos de fundacao: inferior, superior, lateral, estribos verticais/horizontais e faixa transversal.",
                "armadura_grid_large.png",
                "armadura_grid_small.png"
            );

            // v2.8.21 (Fase 1): comando dedicado que monta a GAIOLA FECHADA do bloco de coroamento
            // (malha de fundo acima das estacas + estribos perimetrais + malha de topo/pele opcionais),
            // resolvendo o detalhamento fragmentado ("U soltos").
            AddButton(
                panelPfArmaduras,
                "btnArmaduraCoroamento",
                "Gaiola\nCoroamento",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdArmaduraCoroamento",
                "Monta a gaiola fechada do bloco de coroamento: malha de fundo acima das estacas, estribos perimetrais que fecham fundo->topo, malha de topo e pele laterais opcionais.",
                "armadura_grid_large.png",
                "armadura_grid_small.png"
            );

            AddButton(
                panelPfArmaduras,
                "btnPfInserirAcosEstaca",
                "Armadura\nEstacas",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfInserirAcosEstaca",
                "Lanca barras longitudinais em estacas com distribuicao polar, cobrimento e quantidade configuraveis.",
                "armadura_grid_large.png",
                "armadura_grid_small.png"
            );

            // --- Visualização ---
            AddButton(
                panelVisualizacao,
                "btnIsolarVigasEstruturais",
                "Isolar\nVigas",
                assemblyPath,
                "SteelBIM.Commands.CmdIsolarVigasEstruturais",
                "Isola temporariamente apenas as vigas estruturais na vista ativa.",
                // v2.6.8: beam_isolar_large compartilhado com btnPfIsolarLajes (semantico OK:
                // ambos sao operacoes de "isolar"). Decisao OPCAO B do revert v2.6.3 -> Victor lucide_blue.
                "beam_isolar_large.png",
                "beam_isolar_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnIsolarPilaresEstruturais",
                "Isolar\nPilares",
                assemblyPath,
                "SteelBIM.Commands.CmdIsolarPilaresEstruturais",
                "Isola temporariamente apenas os pilares estruturais na vista ativa.",
                // v2.7.6: migrado de isolar_pilares_32_light (legado) pra columns canonico.
                // columns (plural) estava no set canonico aprovado e nenhum botao usava —
                // semanticamente perfeito pra "isolar pilares" (multiplas colunas).
                "columns_large.png",
                "columns_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnPfIsolarPilaresConsolos",
                "Isolar\nP+Cons.",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfIsolarPilaresConsolos",
                "Isola na vista ativa os pilares estruturais e as famílias PF com modelo Consolo.",
                "column_line_large.png",
                "column_line_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnPfIsolarLajes",
                "Isolar\nLajes",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfIsolarLajes",
                "Isola famílias PF cuja tipagem esteja marcada com Modelo = Laje.",
                "beam_isolar_large.png",
                "beam_isolar_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnAgruparPilaresPorTipo",
                "Agrupar\nPilares",
                assemblyPath,
                "SteelBIM.Commands.CmdAgruparPilaresPorTipo",
                "Agrupa pilares iguais por tipo com destaque visual por conjunto, evitando grupos nativos que possam conflitar com eixos.",
                "agruparpilares_large.png",
                "agruparpilares_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnAgruparVigasPorTipo",
                "Agrupar\nVigas",
                assemblyPath,
                "SteelBIM.Commands.CmdAgruparVigasPorTipo",
                "Agrupa vigas iguais por tipo, colore cada conjunto e cria grupos EMT.",
                // v2.7.6: dono semantico do icone canonico agruparvigas. Liberado depois que
                // btnDiagramaMontagem e btnSequenciamentoBim migraram pra blueprint/inspection
                // (saiu o placeholder de 3 botoes -> 1).
                "agruparvigas_large.png",
                "agruparvigas_small.png"
            );

            AddButton(
                panelVisualizacao,
                "btnLimparAgrupamentosVisuais",
                "Limpar",
                assemblyPath,
                "SteelBIM.Commands.CmdLimparAgrupamentosVisuais",
                "Remove as cores aplicadas na vista ativa e desfaz os grupos EMT criados para pilares e vigas.",
                // v2.6.8: broom_large compartilhado com btnVerificarModelo (semantico OK:
                // vassoura = limpeza/manutencao, "verificar" e parente proximo). Decisao OPCAO B
                // do revert v2.6.3 -> Victor lucide_blue.
                "broom_large.png",
                "broom_small.png"
            );

            // v2.8.11 (Onda 4 — P5): preparar arquivo para entrega (desfaz grupos temporarios
            // EMT_, preservando os membros). Reusa o icone de vassoura (limpeza).
            AddButton(
                panelVisualizacao,
                "btnLimparModelo",
                "Limpar\nModelo",
                assemblyPath,
                "SteelBIM.Commands.CmdLimparModelo",
                "Prepara o arquivo para entrega: desfaz os grupos temporarios criados pelo SteelBIM (prefixo EMT_), preservando os membros. Nao remove elementos modelados.",
                "broom_large.png",
                "broom_small.png"
            );
        }

        // v2.6.0: ABA "SteelBIM | Detalhamento" — 7 paineis (vistas, cotagem,
        // anotacao, fabricacao CNC, montagem e sequenciamento, verificacao,
        // licenca).
        private void BuildAbaDetalhamento(UIControlledApplication application, string tabName, string assemblyPath)
        {
            RibbonPanel panelVistas = GetOrCreatePanel(application, tabName, "Vistas");
            RibbonPanel panelCotagem = GetOrCreatePanel(application, tabName, "Cotagem");
            RibbonPanel panelAnotacao = GetOrCreatePanel(application, tabName, "Anotação");
            RibbonPanel panelFabricacaoCnc = GetOrCreatePanel(application, tabName, "Fabricação CNC");
            RibbonPanel panelMontagem = GetOrCreatePanel(application, tabName, "Montagem e Sequenciamento");
            RibbonPanel panelVerificacao = GetOrCreatePanel(application, tabName, "Verificação");
            RibbonPanel panelLicenca = GetOrCreatePanel(application, tabName, "Licença");

            // --- Vistas ---
            AddButton(
                panelVistas,
                "btnPfElevacaoPilares",
                "Elevação\nPilar",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfElevacaoFormaPilares",
                "Gera elevação e corte transversal para pilares estruturais, sem depender do Dynamo.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            AddButton(
                panelVistas,
                "btnPfElevacaoVigas",
                "Elevação\nVigas",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfElevacaoFormaVigas",
                "Gera elevação e corte transversal para vigas estruturais, sem depender do Dynamo.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            AddButton(
                panelVistas,
                "btnGerarVistaPeca",
                "Vista de\nPeça",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarVistaPeca",
                "Gera vistas de detalhe (longitudinal e transversal) para peças estruturais, voltadas para shop drawings de fabricação metálica.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            // v2.8.13 (Onda 2): alinhar/distribuir viewports na prancha (estilo PowerPoint).
            AddButton(
                panelVistas,
                "btnAlinharVistas",
                "Alinhar\nVistas",
                assemblyPath,
                "SteelBIM.Commands.CmdAlinharVistas",
                "Alinha e distribui as vistas (viewports) selecionadas na prancha — esquerda/direita/topo/base/centro e distribuir, estilo PowerPoint.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            // v2.8.13 (Onda 3): criar prancha + distribuir as vistas selecionadas em grade.
            AddButton(
                panelVistas,
                "btnPrancharVistas",
                "Pranchar\nVistas",
                assemblyPath,
                "SteelBIM.Commands.CmdPrancharVistas",
                "Cria uma prancha (carimbo escolhido) e distribui automaticamente em grade as vistas selecionadas no Navegador de Projeto.",
                "vista_peca_large.png",
                "vista_peca_small.png"
            );

            // --- Cotagem ---
            AddButton(
                panelCotagem,
                "btnGerarCotasAlinhamento",
                "Cotas\nAlinhamento",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarCotasPorAlinhamento",
                "Selecione os elementos e clique no lado onde a cota deve ficar. A ferramenta agrupa os alinhamentos automaticamente e gera as cotas na vista ativa.",
                // v2.6.8: NAO revertido pra cotas_eixo_large — mapeamento semantico duvidoso
                // (Alinhamento no Revit = linha de referencia arquitetonica; Eixo = grid
                // estrutural; nao sao a mesma coisa). Aguardando Victor confirmar mapeamento
                // correto OU entregar icone cotas_alinhamento proprio.
                "cotas_alinhamento_32_light.png",
                "cotas_alinhamento_32_light.png"
            );

            // Sprint 1 (Bug B5): registrar CmdGerarCotasPorEixo (estava orfao)
            // v2.7.6: migrado de ruler -> cotas_eixo (icone canonico semanticamente perfeito,
            // estava livre na pasta de referencia). ruler agora dedicado a CotarTrelica.
            AddButton(
                panelCotagem,
                "btnGerarCotasEixo",
                "Cotas\npor Eixo",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarCotasPorEixo",
                "Gera cotas automaticas perpendiculares aos eixos do projeto. Detecta interseccoes com vigas e pilares na vista ativa.",
                "cotas_eixo_large.png",
                "cotas_eixo_small.png"
            );

            AddButton(
                panelCotagem,
                "btnCotarTrelica",
                "Cotar\nTreliça",
                assemblyPath,
                "SteelBIM.Commands.CmdCotarTrelica",
                "Aplica cotagem EMT em 5 faixas (painéis superior/inferior, vão total, vãos parciais, alturas) sobre elevação/corte de treliça selecionada.",
                "ruler_large.png",
                "ruler_small.png"
            );

            AddButton(
                panelCotagem,
                "btnCotarPecaFabricacao",
                "Cotar\nFabricação",
                assemblyPath,
                "SteelBIM.Commands.CmdCotarPecaFabricacao",
                "Adiciona cotas automáticas de fabricação: comprimento total, altura do perfil, largura da mesa, furos e distâncias de borda.",
                "cotar_fabricacao_large.png",
                "cotar_fabricacao_small.png"
            );

            // --- Anotação ---
            AddButton(
                panelAnotacao,
                "btnNumerarItens",
                "Numerar",
                assemblyPath,
                "SteelBIM.Commands.CmdNumerarItens",
                "Numera elementos manualmente por ordem de clique com filtros, avanço/retrocesso e destaque visual dos itens já processados.",
                // v2.6.8: numeracao_large compartilhado com btnPfNomearElementos (semantico OK:
                // "numerar" e "nomear" sao primos diretos). Decisao OPCAO B do revert v2.6.3.
                "numeracao_large.png",
                "numeracao_small.png"
            );

            AddButton(
                panelAnotacao,
                "btnTagearTrelica",
                "Tagear\nTreliça",
                assemblyPath,
                "SteelBIM.Commands.CmdTagearTrelica",
                "Identifica perfis (banzos, montantes, diagonais) diretamente sobre a elevação da treliça com tags padronizadas EMT.",
                "tag_large.png",
                "tag_small.png"
            );

            AddButton(
                panelAnotacao,
                "btnIdentificarPerfil",
                "Identificar\nPerfil",
                assemblyPath,
                "SteelBIM.Commands.CmdIdentificarPerfil",
                "Identifica perfis estruturais selecionados com tag ou TextNote contendo nome do perfil, comprimento e quantidade agrupada.",
                "tag_large.png",
                "tag_small.png"
            );

            AddButton(
                panelAnotacao,
                "btnMarcarPecas",
                "Marcar\nPeças",
                assemblyPath,
                "SteelBIM.Commands.CmdMarcarPecas",
                "Agrupa peças idênticas por assinatura de fabricação (tipo + perfil + material + comprimento) e atribui marcas únicas automaticamente.",
                "marca_peca_large.png",
                "marca_peca_small.png"
            );

            AddButton(
                panelAnotacao,
                "btnPfNomearElementos",
                "Nomear\nPF",
                assemblyPath,
                "SteelBIM.Commands.PF.CmdPfNomearElementos",
                "Nomeia pilares, vigas e lajes PF com filtros por família, tipo e parâmetro, no mesmo padrão da rotina Numerar Itens.",
                "numeracao_large.png",
                "numeracao_small.png"
            );

            // --- Fabricação CNC ---
            AddButton(
                panelFabricacaoCnc,
                "btnExportarDstv",
                "Exportar\nDSTV/NC1",
                assemblyPath,
                "SteelBIM.Commands.CmdExportarDstv",
                "Gera arquivos .nc1 (formato DSTV) compatíveis com máquinas CNC de corte de aço estrutural. Suporta agrupamento por marca de peça.",
                "sheets_large.png",
                "sheets_small.png"
            );

            AddButton(
                panelFabricacaoCnc,
                "btnExportarListaMateriais",
                "Exportar",
                assemblyPath,
                "SteelBIM.Commands.CmdExportarListaMateriais",
                "Exporta uma lista de materiais estruturais para Excel com abas de perfis lineares, chapas/conexões e resumo consolidado.",
                "exportar_xls_large.png",
                "exportar_xls_small.png"
            );

            // --- Montagem e Sequenciamento ---
            // v2.7.6: agruparvigas era placeholder semantico. Diagrama de Montagem =
            // prancha de elevacao -> blueprint (icone canonico de prancha tecnica).
            AddButton(
                panelMontagem,
                "btnDiagramaMontagem",
                "Diagrama de\nMontagem",
                assemblyPath,
                "SteelBIM.Commands.CmdGerarDiagramaMontagem",
                "Diagrama de Montagem (padrao brasileiro) — Gera vista de elevacao " +
                "dos elementos estruturais selecionados com eixos do projeto visiveis, " +
                "cotas alinhadas entre eixos e tags com marca de fabricacao (Mark) " +
                "em cada peca. Util para entrega de pranchas de detalhamento " +
                "estrutural ao pessoal de obra. Escala padrao 1:75. " +
                "v2.3.0 = MVP (eixos+cotas+tags); cotas verticais e folha com " +
                "title block na v2.3.1+.",
                "blueprint_large.png",
                "blueprint_small.png"
            );

            // v2.7.6: agruparvigas era placeholder semantico. Sequenciamento BIM = 4D phasing
            // (inspecionar/acompanhar progresso por fase) -> inspection (icone canonico).
            AddButton(
                panelMontagem,
                "btnSequenciamentoBim",
                "Sequencia-\nmento BIM",
                assemblyPath,
                "SteelBIM.Commands.CmdPlanoMontagem",
                "Sequenciamento BIM (4D Phasing) — Atribui fases de montagem a " +
                "elementos estruturais para planejamento cronologico. Agrupa pecas " +
                "por etapa, aplica destaque visual com cores customizaveis por fase, " +
                "exporta relatorio para Excel. Util para planejamento 4D, " +
                "coordenacao de cronograma de obra, integracao com Synchro/Navisworks " +
                "Timeliner e simulacao de sequencia construtiva.",
                "inspection_large.png",
                "inspection_small.png"
            );

            // --- Verificação ---
            AddButton(
                panelVerificacao,
                "btnVerificarModelo",
                "Verificar\nModelo",
                assemblyPath,
                "SteelBIM.Commands.CmdVerificarModelo",
                "Roda múltiplas regras de validação no modelo (peças sem marca, sem material, perfis sobrepostos, etc.) e gera relatório consolidado.",
                "broom_large.png",
                "broom_small.png"
            );

            // v2.8.26: quantitativo de pintura calculado pela geometria (perimetro x comprimento),
            // gravado no parametro EMT_Area_Pintura + tabela. Funciona sem material aplicado.
            AddButton(
                panelVerificacao,
                "btnAreaPintura",
                "Área de\nPintura",
                assemblyPath,
                "SteelBIM.Commands.CmdAreaPintura",
                "Calcula a área de pintura dos perfis metálicos pela geometria (perímetro × comprimento), grava no parâmetro EMT_Area_Pintura e cria a tabela de quantitativo. Funciona mesmo sem material aplicado.",
                "table_large.png",
                "table_small.png"
            );

            // --- Licença ---
            AddStackedButtons(
                panelLicenca,
                "btnAtivarLicenca",
                "Ativar Licença",
                assemblyPath,
                "SteelBIM.Commands.CmdAtivarLicenca",
                "Cole sua chave de licença para ativar o plugin nesta máquina.",
                null,
                null,
                "btnSobre",
                "Sobre",
                "SteelBIM.Commands.CmdSobre",
                "Versão, estado da licença e identificador desta máquina.",
                null,
                null
            );
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

        // =============================================================
        // ICONES DO RIBBON — convencao oficial v2.6.3
        // =============================================================
        // Padrao novo (lucide_blue): produzido pelo Victor a partir do
        // set lucide.dev com paleta unificada outline #6BB7FF + fill #1E5BC6.
        //
        //   Nomenclatura: snake_case_<size>_<theme>.png
        //   - <size>: _32_light (large, 32x32) e _16_light (small, 16x16)
        //   - <theme>: por enquanto so "_light" (futuro: "_dark" pra dark mode Revit)
        //   - HiDPI: variantes opcionais _32_light_hidpi.png / _16_light_hidpi.png
        //            adjacentes — Revit detecta automaticamente via convencao SDK
        //
        // Quando NAO houver _16_light regular ainda (caso atual em ~16 botoes),
        // passar o mesmo _32_light em ambos argumentos do AddButton — Revit
        // faz downscale. Trade-off documentado: small fica levemente borrado
        // em algumas resolucoes, mas paleta visual consistente (vs misturar
        // 32_light novo + _small antigo).
        //
        // Padrao legado (em fade-out): <nome>_large.png / <nome>_small.png.
        // NAO criar arquivos novos no padrao antigo. Substituicoes acontecem
        // conforme Victor entrega refacoes ao set lucide_blue.
        //
        // Backups historicos de redesigns ficam em SteelBIM/Resources/_backup_*
        // — gitignored, workspace local apenas (.gitignore linha ~127).
        //
        // Ver tambem CHANGELOG v2.6.3 "Known follow-ups" para a lista de
        // botoes ainda no padrao antigo e variantes _16_light pendentes.
        // =============================================================
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

            string resourceName = $"SteelBIM.Resources.{imageName}";
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
        // PR-3 / PR-4 — Reporting helpers (compartilhados Sentry+Telemetry)
        // ===========================================================

        /// <summary>
        /// Resolve o license state para Sentry e Telemetria. Chamado lazy a
        /// CADA evento — reflete o estado corrente da licenca, nao o de boot.
        /// Try/catch raiz: nunca lanca.
        /// </summary>
        private static string ResolveLicenseStateForReporting()
        {
            try
            { return LicenseService.GetCurrentState().Status.ToString(); }
            catch { return "Unknown"; }
        }

        /// <summary>
        /// Constroi o cliente HTTP-direct do PostHog. Chamado uma vez por
        /// boot (TelemetryReporter eh idempotente). Resolve api key + host
        /// + session id no momento do build.
        /// </summary>
        private static ITelemetryClient BuildPostHogTelemetryClient()
        {
            string apiKey = PostHogApiKeyProvider.GetApiKey();
            string host = PostHogHostProvider.GetHost();
            string sessionId = SessionIdProvider.GetOrCreate();
            string release = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";

            return new PostHogHttpTelemetryClient(
                _telemetryHttp.Value,
                apiKey,
                host,
                sessionId,
                release,
                ResolveLicenseStateForReporting,
                logWarnException: (ex, msg) => Logger.Warn(ex, msg),
                logWarnTemplate: (template, args) => Logger.Warn(template, args));
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

            try
            { EnsureConsentIfNeeded(); }
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

                        // PR-4: emite update.detected quando ha versao nova.
                        // No-op silencioso se TelemetryReporter desabilitado.
                        if (result != null && result.Outcome == UpdateCheckOutcome.UpdateAvailable)
                        {
                            try
                            {
                                TelemetryReporter.Track(new TelemetryEvent(
                                    SamplingDecider.EventUpdateDetected,
                                    new Dictionary<string, object>
                                    {
                                        { "current_version", version },
                                        { "available_version", result.LatestVersion ?? "unknown" },
                                    }));
                            }
                            catch (Exception trackEx)
                            {
                                Logger.Warn(trackEx, "[Telemetry] falha ao emitir update.detected");
                            }
                        }
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
