using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Licensing;
using FerramentaEMT.Utils;

namespace FerramentaEMT
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Sprint 1: inicializar logging estruturado ANTES de qualquer coisa
            // (assim qualquer falha de boot fica registrada)
            Logger.Initialize();
            Logger.Info("App.OnStartup — registrando ribbon");

            // 1.3.0: captura crashes nao-observados em arquivo local (futuro: Sentry)
            CrashReporter.Initialize();

            // 1.0.0: inicializar sistema de licenca (cria trial na primeira execucao)
            try { LicenseService.Initialize(); }
            catch (Exception licEx) { Logger.Error(licEx, "[App] LicenseService.Initialize falhou — continuar mesmo assim"); }

            // 1.3.0: logar fonte do segredo HMAC (alertar se ainda usa fallback DEV_ONLY)
            try
            {
                LicenseSecretProvider.SecretSource source = LicenseSecretProvider.GetResolvedSource();
                if (source == LicenseSecretProvider.SecretSource.DevOnlyFallback)
                {
                    Logger.Warn("[Licensing] Segredo HMAC nao externalizado — usando fallback DEV_ONLY. " +
                                "Em producao defina {EnvVar} ou crie {FileName} em %LOCALAPPDATA%\\FerramentaEMT\\.",
                                LicenseSecretProvider.EnvVarName, LicenseSecretProvider.SecretFileName);
                }
                else
                {
                    Logger.Info("[Licensing] Segredo HMAC carregado de {Source}", source);
                }
            }
            catch (Exception secEx) { Logger.Error(secEx, "[App] Falha ao consultar fonte do segredo HMAC"); }

            string tabName = "Ferramenta EMT";
            RevitWindowThemeService.Initialize(application);

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception ex)
            {
                // Aba já existe — esperado quando o plugin recarrega
                Logger.Debug("CreateRibbonTab: aba ja existe ({Msg})", ex.Message);
            }

            RibbonPanel panelModelagem = GetOrCreatePanel(application, tabName, "Modelagem");
            RibbonPanel panelEstrutura = GetOrCreatePanel(application, tabName, "Estrutura");
            RibbonPanel panelVigas = GetOrCreatePanel(application, tabName, "Vigas");
            RibbonPanel panelVista = GetOrCreatePanel(application, tabName, "Vista");
            RibbonPanel panelDocumentacao = GetOrCreatePanel(application, tabName, "Documentação");
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

            // --- Painel Fabricação (novos módulos) ---
            RibbonPanel panelFabricacao = GetOrCreatePanel(application, tabName, "Fabricação");

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
            RibbonPanel panelCnc = GetOrCreatePanel(application, tabName, "CNC");

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
            RibbonPanel panelQa = GetOrCreatePanel(application, tabName, "Verificação");

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
            RibbonPanel panelMontagem = GetOrCreatePanel(application, tabName, "Montagem");

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
            RibbonPanel panelLicenca = GetOrCreatePanel(application, tabName, "Licença");

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
    }
}
