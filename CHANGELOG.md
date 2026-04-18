# Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
versionamento [SemVer](https://semver.org/lang/pt-BR/).

---

## [Unreleased]

Trabalho em direção ao produto comercial 10/10 (ver `docs/PLANO-100-100.md`).

### Added — UX de cancelamento (ADR-004)
- **`Views/ProgressWindow.xaml(.cs)`** — dialogo reutilizavel de progresso com barra, percentual, contador N/Total, mensagem detalhada e botao Cancelar. Tematico com `AppTheme.Base.xaml`. Fechar pelo X equivale a Cancelar.
- **`Utils/RevitProgressHost`** — host estatico `Run<T>(title, headline, work)` que abre a janela, corre o servico no thread principal do Revit (requisito de API single-threaded) e bombeia o `Dispatcher` entre eventos de `IProgress` para a UI atualizar e o Cancelar chegar ao `CancellationTokenSource`. Exception `OperationCanceledException` propaga ate o comando, que retorna `Result.Cancelled`.
- **`docs/ADR/004-threading-model-progress-cancel.md`** — documenta o modelo de threading, por que `Task.Run` e proibido com Revit API, quando usar o host e quando nao usar.
- **`CmdVerificarModelo`** passa a usar `RevitProgressHost` — primeiro consumidor real. Usuario ve progresso por regra e pode cancelar sem esperar 30s de `DuplicateMarkRule` em modelos grandes.

### Changed — Segunda adoção do ADR-003
- **`ModelCheckService.Executar`** agora retorna `Result<ModelCheckReport>` e aceita `IProgress<ProgressReport>` + `CancellationToken` opcionais. Falhas de domínio (`uidoc` nulo, config ausente, nenhuma regra habilitada) voltam como `Result.Fail` com mensagem amigável — o comando chamador apresenta o diálogo. Progresso é reportado por regra executada (`N/Total — nome da regra: X problema(s)`), throttle de 100 ms. `OperationCanceledException` propaga até o comando, que retorna `Result.Cancelled`. Segue-se o template do ADR-003 validado antes no `DstvExportService`.
- **`ModelCheckReport` ganha `ExportedToPath` e `ExportError`** — exportação Excel falhar **não invalida** a análise (princípio de falha parcial, ADR-003). Comando chamador inspeciona as duas propriedades e decide como apresentar: warning quando Excel falhou, info quando concluiu, nada quando export não foi solicitado. Remove dois `AppDialogService.ShowInfo/ShowError` do serviço.
- **`CmdVerificarModelo`** atualizado para consumir `Result<ModelCheckReport>` e dois sinais de Excel independentes. `try/catch (OperationCanceledException) → Result.Cancelled` pronto para quando a UI ganhar botão Cancelar.

---

## [1.3.0] — 2026-04-18 (Fundação arquitetural + Primeira adoção ADR-003)

Primeira release focada em **qualidade interna**: fundação arquitetural (Result<T>, IRevitContext, ProgressReporter com CancellationToken), hardening a partir de auditoria independente, primeiro serviço (DSTV Export) migrado para o novo pattern como template, além de documentação operacional (RUNBOOK) e ADR-003 formalizando a política de adoção incremental.

### Security
- **Segredo HMAC de licenciamento externalizado.** `LicenseSecretProvider` resolve em cascata: `EMT_LICENSE_SECRET` (env var) → `%LOCALAPPDATA%\FerramentaEMT\license.secret` → arquivo ao lado do assembly → fallback DEV_ONLY hardcoded. Fallback mantém compatibilidade 100% com licenças já emitidas. `App.OnStartup` logga a fonte resolvida e emite warning quando cai no DEV_ONLY. `EmtKeyGen` alerta em amarelo no console.
- **`LicenseSecretProvider` cache agora é atômico** via `Lazy<ResolvedSecret>` com `ExecutionAndPublication` — elimina janela em que leitor concorrente via `secret` resolvido mas `source` ainda `NotResolved`. Teste de concorrência com 128 threads valida a invariante.
- **`LicenseSecretProvider.HasMalformedSecretFile`** detecta arquivo de segredo vazio/whitespace-only para distinguir "não configurado" de "mal configurado" em logs de startup.

### Added
- **`FerramentaEMT/Core/Result<T>`** — struct imutável para fluxos previsíveis de domínio (input inválido, regra de negócio, seleção vazia), deixando exceções para bugs e falhas de infra. Cobertura: 11 testes (inclui regressão do `default` struct). Documentado em `docs/ADR/001-result-pattern.md`.
- **`FerramentaEMT/Core/IRevitContext`** — wrapper skeleton v1 sobre `UIDocument`/`Document` para desacoplar serviços da construção de `ExternalCommandData`. Abre caminho para abstrações de nível mais alto (`IElementQuery`, `ITransactionScope`) conforme necessidade. Documentado em `docs/ADR/002-irevit-context.md`.
- **`FerramentaEMT/Core/ProgressReporter`** agora aceita `CancellationToken` opcional, expõe `IsCancellationRequested` e `ThrowIfCancellationRequested()` para loops longos cancelarem graciosamente.
- **`FerramentaEMT/Infrastructure/CrashReporter`** — captura `AppDomain.UnhandledException` e `TaskScheduler.UnobservedTaskException`, dump em `%LOCALAPPDATA%\FerramentaEMT\crashes\`.
- **`docs/ADR/`** — diretório de Architecture Decision Records inaugurado com 2 ADRs.
- **`docs/PLANO-100-100.md`** — roadmap em 7 fases para levar o plugin de 7/10 interno para 10/10 comercial (26 semanas, ~$485-1085/ano de custo externo).
- **`.editorconfig`** — regras de formatação/estilo C# consumíveis por Visual Studio, Rider, VS Code e `dotnet format`.
- **`.github/PULL_REQUEST_TEMPLATE.md`** e **issue templates** (bug, feature, docs) — polish de processo.
- **`CONTRIBUTING.md`** — workflow de PR, convenção de commits, regras de commits e testes.

### Changed — Primeira adocao do ADR-003
- **`DstvExportService.Executar`** agora retorna `Result<ResultadoExport>` e aceita `IProgress<ProgressReport>` + `CancellationToken` opcionais. Falhas de dominio (pasta nao informada, selecao vazia, filtro vazio, pasta com erro de IO) voltam como `Result.Fail` com mensagem amigavel — o comando chamador decide se exibe dialog. Progresso e reportado por peca processada e por arquivo gravado, throttlado em 100 ms. Loops respeitam `ThrowIfCancellationRequested()`. Breaking change: callers do metodo publico (apenas `CmdExportarDstv` hoje) foram atualizados.

### Fixed (audit 2026-04)
- **`Core/Result<T>` default-struct trap.** Antes, `default(Result<T>)` produzia `IsSuccess=false + Error=null`, causando NRE em qualquer `if (r.IsFailure) log(r.Error)`. Agora o flag interno é `_isFailure` (nasce `false`), então `default` é tratado como `Ok(default(T))`. Regressão coberta por teste.
- **`Infrastructure/CrashReporter` dupla subscrição.** Se `Logger.Info` falhasse no primeiro `Initialize()`, `_initialized` continuava `false` e o próximo `Initialize()` registraria os handlers **de novo**, produzindo dois dumps por crash. Agora `_initialized=true` é definido **antes** da subscrição, e o logger final fica em try/catch isolado.

### Changed
- `.gitignore` adicionado para `license.secret`, `*.license.secret` e `sentry.dsn` — prevenir commit acidental.

### Fixed
- Parse de `double` em 13 janelas WPF (WPF inteiro + PF + PipeRack) padronizado via `NumberParsing.TryParseDouble`, que tenta `InvariantCulture` e cai em `pt-BR` — elimina bug de usuário pt-BR digitando `"3,5"` e recebendo `35` em locales mistos.
- `ModelCheck` rules: log agregado em vez de catch-block vazio para elementos pulados por erro de leitura — agora gera `Logger.Warn("[{Rule}] {Count} elemento(s) pulado(s)...")` em todas as 9 regras.

---

## [1.2.0] — 2026-04-17 (Módulo PF — Pré-Fabricado de Concreto)

Integração do fork do Victor (concreto pré-fabricado) sobre o tronco oficial Alef. A versão Alef ganha 10 novos comandos e 3 painéis de ribbon cobrindo documentação de pilares/vigas PF, inserção de armaduras (estribos, barras longitudinais, armadura de consolo) e organização de modelos com elementos PF, sem depender do Dynamo.

### Added — Módulo PF
- **`Commands/PF/`** (10 comandos): `CmdPfNomearElementos`, `CmdPfIsolarPilaresConsolos`, `CmdPfIsolarLajes`, `CmdPfElevacaoFormaPilares`, `CmdPfElevacaoFormaVigas`, `CmdPfInserirEstribosPilar`, `CmdPfInserirAcosPilar`, `CmdPfInserirEstribosViga`, `CmdPfInserirAcosViga`, `CmdPfInserirAcosConsolo`. Todos herdam `FerramentaCommandBase` (licenciamento + logging + tratamento de erro centralizados).
- **`Services/PF/`** (6 arquivos): `PfElementService` (predicados e coleta de elementos PF, ordenação por eixo), `PfIsolationService` (isolar temporário na vista ativa), `PfNamingCatalog` + `PfNamingService` (nomeação padronizada de pilares/vigas/lajes), `PfRebarService` (946 linhas — pipeline completo de inserção de armaduras), `PfRebarTypeCatalog` (lookup de `RebarBarType`).
- **`Services/PF/PfNamingFormatter`** (novo, puro, culture-invariant) — helper extraído de `PfNamingConfig.MontarValor` para viabilizar testes unitários. Garante que `"P" + 1000` nunca vire `"P1.000"` em máquinas pt-BR/de-DE.
- **`Models/PF/`** (2 arquivos, 9 configs): `PfNamingConfig` (+ `PfNamingTarget`), `PfRebarConfigs` agrupando `PfColumnStirrupsConfig`, `PfColumnBarsConfig`, `PfBeamStirrupsConfig`, `PfBeamBarsConfig`, `PfConsoloRebarConfig` e o enum `PfBeamBarEndMode`.
- **`Views/Pf*Window.xaml(.cs)`** — 7 janelas WPF com tema Revit: `PfBeamBarsWindow`, `PfBeamStirrupsWindow`, `PfColumnBarsWindow`, `PfColumnStirrupsWindow`, `PfConsoloRebarWindow`, `PfNamingWindow` + code-behind.
- **Ribbon — 3 painéis novos** em `App.cs`: `PF Construção` (Nomear, Isolar P+Cons., Isolar Lajes), `PF Documentação` (Elevação Pilar, Elevação Vigas), `PF Armaduras` (Estribos Pilar, Aços Pilar, Estribos Viga, Aços Viga, Aços Consolo).

### Added — Refinamentos do núcleo metálico (vindos do fork Victor)
- **`Models/GerarVistaPecaConfig`** — novo enum `VistaPecaCategoriaFiltro { Todos, Pilares, Vigas }` + propriedade `FiltroCategoria`, usado para reutilizar o serviço de geração de vistas tanto em metálica quanto em PF.
- **`Services/AutoVistaService`** — suporta `FiltroCategoria` para coletar apenas `OST_StructuralColumns` ou `OST_StructuralFraming` dentro do escopo selecionado.
- **`Services/Trelica/TagearTrelicaService`** — implementação completa dos rótulos de banzo via `TextNote` (`BANZO SUPERIOR: <perfis>` e `BANZO INFERIOR: <perfis>`), substituindo o `TODO` que existia no v1.1.0.
- **`Utils/AppSettings`** — 9 propriedades `LastPfNaming*` persistem a última configuração da janela de nomeação PF entre sessões.

### Added — Cobertura de testes
- Suite unitária cresce de **223 → 279 testes** (todos verdes em 52 ms):
  - `PfNamingFormatterTests` (9 testes: casos básicos, nulos, culture pt-BR/de-DE/fr-FR/en-US, números grandes)
  - `PfRebarConfigsTests` (8 testes: defaults de 5 configs + enum + mutabilidade)
  - `FerramentaEMT.Tests.csproj` agora linka também os helpers puros de `Services/Trelica/` (Classificador, Geometria, PerfilFormatter, Topologia, CotaFaixaBuilder, CotarTrelicaReport) + novos helpers PF, fechando um gap silencioso em que tests de Trelica existiam mas não compilavam.

### Fixed
- **`Tests/Services/Trelica/CotarTrelicaReportTests.cs`** — adicionado `using FluentAssertions;` que faltava (impedia compilação ao linkar os sources Trelica).

### Added — Auditoria AIOX (score 7.25/10 → melhorado)
- **32 catch blocks silenciosos substituídos por `Logger.Warn`** com contexto do elemento/operação — paradigma "falhas devem deixar rastro".
- **`MaxHeight` adicionado em 21 janelas WPF** que faltavam (garante cabimento em telas 1366×768 junto com `MinHeight`/`MinWidth` já existentes).
- **`CultureInfo.InvariantCulture`** aplicado em `Services/CncExport/DstvExportService` (output de relatório) e `Services/ModelCheck/ModelCheckRules/ZeroLengthRule` (formatação de descrição de issue) — garante que marcadores e relatórios saiam idênticos em pt-BR, en-US, de-DE.
- **`Commands/CmdCortarPerfilPorInterferencia`** migrado para `FerramentaCommandBase` — licenciamento + logging + tratamento de erro centralizados, padronizando com os outros 36 commands.
- **9 classes de testes de Config criadas** (`*ConfigTests.cs` em `Models/`): `ExportarDstvConfig`, `ConexaoConfig`, `CotarTrelicaConfig`, `ExportarListaMateriaisConfig`, `GerarVistaPecaConfig`, `IdentificarPerfilConfig`, `MarcarPecasConfig`, `ModelCheckConfig`, `PlanoMontagemConfig`, `TagearTrelicaConfig` — **68 novos testes** cobrindo defaults, mutabilidade e roundtrips.

### Quality gates
- `dotnet build FerramentaEMT.Solution.sln -c Release` → plugin principal: **0 erros, 2 avisos MSB3277** (cruzamento de referências Revit API, não-impeditivos).
- `dotnet test` → **347/347 aprovados** (era 279 antes da auditoria AIOX).
- `TreatWarningsAsErrors` mantido em Release.

### Notes
- Pasta do fork original (`FerramentaEMT-Victor/`) preservada em `backup-victor-pre-merge.zip` (301 MB) e removida após validação.
- Três test files de comportamento dependente de Revit runtime (`TagearTrelicaReportTests`, `TrelicaRevitHelperTests`, `IdentificarPerfilReportTests`) estão explicitamente excluídos do build pois testam membros de services Revit-bound; seu comportamento é validado por smoke test manual no Revit.
- HMAC secret em `Licensing/KeySigner.cs` mantido hardcoded por decisão explícita do mantenedor. O comentário no arquivo alerta: "TROCAR ANTES DA PRIMEIRA VENDA". Antes de distribuir a clientes externos, o repositório deve estar privado ou o secret deve ser movido para env var / DPAPI / arquivo externo.

---

## [1.1.0] — 2026-04-15 (Cotar Treliça + Identificação de Perfis)

### Added — Módulo Cotar Treliça (5 faixas EMT)
- **`Commands/CmdCotarTrelica`** — Comando principal: usuario seleciona barras da trelica em vista Elevation/Section, abre janela de configuracao, executa cotagem automatica em 5 faixas (paineis banzo superior, paineis banzo inferior, vao total, vaos parciais entre apoios, altura de cada montante) + identificacao de perfis. Segue padrao EMT extraido de 4 projetos de referencia.
- **`Services/Trelica/CotarTrelicaService`** (638 linhas) — Orquestrador com pipeline de 10 etapas: classificacao de barras → separacao banzos → extracao de topologia → calculo geometrico → construcao de faixas → criacao de cotas/tags/textos Revit. Contem 5 TODOs explicitos para integracao final de References Revit (Wave A.1.5).
- **5 helpers puros** (testaveis sem Revit API):
  - `TrelicaClassificador` — classifica barras por inclinacao (Banzo/Montante/Diagonal) e altura (Superior/Inferior/BanzoIndefinido)
  - `TrelicaGeometria` — calcula largura dos paineis, vao total, alturas por estacao, extremos de apoio
  - `TrelicaPerfilFormatter` — formata nome de perfil com multiplicador ("2x L 76x76x6.3"), detecta cantoneira
  - `TrelicaTopologia` — detecta tipo da trelica (Plana/DuasAguas/Shed/Desconhecida)
  - `CotaFaixaBuilder` — constroi especificacoes das 5 faixas de cota como records imutaveis
- **`Services/Trelica/CotarTrelicaReport`** — Record imutavel com metricas (CotasCriadas, TagsCriadas, TextosCriados, WarningsCount, TempoMs, Resumo)
- **`Models/CotarTrelicaConfig`** — DTO com 9 propriedades (CotarPaineisSuperior/Inferior, VaoTotal, VaosParciais, Alturas, IdentificarPerfis, CantoneiraDupla, OffsetFaixaMm)
- **`Views/CotarTrelicaWindow`** — WPF com 7 checkboxes, OK/Cancel, tema Revit

### Added — Módulo Tagear Treliça
- **`Commands/CmdTagearTrelica`** + **`Services/Trelica/TagearTrelicaService`** — Identifica perfis diretamente sobre a elevacao da trelica com tags padrao EMT. Classifica barras e aplica tags por grupo (banzos, montantes, diagonais).
- **`Models/TagearTrelicaConfig`** + **`Views/TagearTrelicaWindow`** — Configuracao e janela WPF

### Added — Módulo Identificar Perfil (genérico)
- **`Commands/CmdIdentificarPerfil`** + **`Services/IdentificacaoPerfil/IdentificarPerfilService`** — Identifica perfis estruturais selecionados com tag ou TextNote contendo nome do perfil, comprimento e quantidade agrupada. Funciona em qualquer vista, nao so trelica.
- **`Models/IdentificarPerfilConfig`** + **`Views/IdentificarPerfilWindow`** — Configuracao e janela WPF

### Added — Botões no Ribbon
- 3 novos botoes no painel **Documentação**: `Cotar Treliça`, `Tagear Treliça`, `Identificar Perfil`

### Added — Wave A.1.5: Implementação real das APIs Revit
- **`Services/Trelica/TrelicaRevitHelper.cs`** (novo, ~310 linhas) — Helper estatico que centraliza todas as chamadas de API Revit: extracao de References de endpoints de barras (`ObterReferenciaExtremo`), criacao de running dimensions (`CriarRunningDimension` via `doc.Create.NewDimension`), criacao de tags (`IndependentTag.Create`), criacao de TextNotes (`TextNote.Create`), projecao/desprojecao de coordenadas 2D↔3D.
- **`CotarTrelicaService.cs` reescrito** — 5 TODOs substituidos por implementacao real:
  - `CriarDimensionsNoRevit`: extrai References dos endpoints de cada barra, monta ReferenceArray por faixa, cria running dimensions reais. Para faixa AlturasMontantes, cria TextNotes verticais com valor em mm.
  - `TentarCriarTag`: cria IndependentTag real com offset 150mm, leader em barras curtas (<400mm).
  - `CriarTextosRotuloBanzos`: detecta perfil do banzo superior/inferior, cria TextNotes "BANZO SUPERIOR W200x26.6" acima e "BANZO INFERIOR 2x L 76x76x6.3" abaixo.
  - Novo helper `DetectarPerfilBanzo` para leitura de perfil representativo do banzo.

### Refactored — UX (Wave E)
- **`Utils/RevitWindowThemeService.AttachEscapeHandler()`** — handler centralizado de ESC para todas as janelas WPF (~23 janelas beneficiadas). Removido handler duplicado de `WindowExtensions.cs`.
- **Migração `IntegerValue` → `ElementId`** em `CmdCotarTrelica.cs` (best practice Revit 2024+)

### Tests — 27+ novos testes unitários
- `TrelicaClassificadorTests` (7 testes: inclinacao, altura, tolerancia, indefinido)
- `TrelicaGeometriaTests` (9 testes: paineis, vao total, alturas por estacao, extremos, nunca negativa)
- `TrelicaPerfilFormatterTests` (8 testes: formatacao, cantoneira, placeholder, multiplicador zero)
- `TrelicaTopologiaTests` (7 testes: plana, duas aguas, shed, ruido no pico, nos identicos)
- `CotaFaixaBuilderTests` (8 testes: 5 faixas, 3 apoios, segmentos consecutivos)
- `CotarTrelicaReportTests` (5 testes: record, resumo, warnings)

### Docs
- **`docs/PLANO-LAPIDACAO-V2.md`** — Plano consolidado com revisao de 2 engenheiros seniores, APIs Revit oficiais, checklist de 20 itens market-ready, 5 ondas recalibradas (22 dias)
- **`docs/reference-projects/cobertura-samsung/`** — Padrao de cotagem de trelica em 5 faixas
- **`docs/reference-projects/galpao-padrao-emt/`** — Template minimo de entrega EMT em 3 pranchas

---

## [Unreleased]

### Fixed — Onda 5 bugs reais descobertos via testes automatizados (v1.0.5)
- **ConexaoCalculator gerava marcadores com virgula decimal em pt-BR**: `$"{x:F1}"` usava `CurrentCulture`, entao em maquinas brasileiras marcadores como "CP-12,7-150x250-4xM19" eram gerados em vez de "CP-12.7-...". Bug **afetava producao** porque marcadores viajam em nomes de arquivo, CNC, DSTV e shop drawings — precisam ser culture-invariant globalmente. Fix em `Services/Conexoes/ConexaoCalculator.cs` trocando interpolacao por `string.Format(CultureInfo.InvariantCulture, ...)` em todos os 3 formatters (ChapaDePonta, DuplaCantoneira, ChapaGusset). Regression test parametrizado rodando em pt-BR, de-DE, fr-FR, en-US em `ConexaoCalculatorCultureTests`.
- **DstvProfileMapper classificava strings livres como perfis**: `MapByDesignation` usava `StartsWith(letra) + HasDigit(string)`, entao "CUSTOM-001" virava U-channel (comeca com 'C' e tem digito em algum lugar). Qualquer nome de tipo nao padrao com uma letra + digito em qualquer posicao era classificado erroneamente — produzia DSTV com codigo de perfil errado para familias custom. Fix: novo helper `StartsDigit(s, prefix)` que exige digito **imediatamente apos** o prefixo (tolerando '-' ou espaco opcional). Regression tests em `DstvProfileMapperStrictnessTests` cobrindo "CUSTOM-001", "UNKNOWN-1", "LABEL-5", "TEST-7" cairem em `SO`, e W12X26/C310X45/L4X4X1/2 continuarem sendo reconhecidos.

### Refactored — Extracao de helpers puros para testes unitarios (v1.0.5)
- **`Services/Montagem/EtapaMontagemParser.cs`** (novo): extraido do `PlanoMontagemService.LerEtapaDoElemento` o parsing puro de "Etapa:N" do parametro Comments. Servico agora delega ao parser. 9 testes em `EtapaMontagemParserTests`.
- **`Services/CncExport/DstvFileNameSanitizer.cs`** (novo): extraido do `DstvExportService.SanitizarNomeArquivo` a logica de substituicao de chars invalidos. Servico agora delega. 8 testes em `DstvFileNameSanitizerTests`.
- **`Services/Conexoes/ConexaoFamilyNames.cs`** (novo): extraido do `ConexaoGeneratorService.NomeFamiliaEsperado` o mapa `TipoConexao -> nome de familia Revit`. 4 testes em `ConexaoFamilyNamesTests`.
- **`DstvFileWriter.FormatNumber`**: trocado `Logger.Warn` por `System.Diagnostics.Debug.WriteLine` para manter arquivo puro (linkavel em testes sem Serilog).

### Fixed — ModelCheckReportTests.Report_ExecutionTime_IsSet (flaky)
- Ordem do Arrange estava errada: `timeBefore = DateTime.Now` era capturado DEPOIS do `new ModelCheckReport()`, entao `ExecutionTime < timeBefore` em maquinas rapidas. Corrigido + adicionadas mensagens de diagnostico nos asserts.

### Added — Cobertura de testes
- Suite `FerramentaEMT.Tests` cresceu para **170 casos** (todos verdes em 49ms), cobrindo parsing de etapa, sanitizacao de nome DSTV, mapeamento de familias de conexao, formatacao culture-invariant de marcadores, classificacao estrita de perfis DSTV.

### Fixed — Onda 1 pos-feedback Victor (v1.0.1)
- **Janelas WPF muito grandes**: reduzidos Width/Height de 8 janelas (PipeRack, NumeracaoItens, Escada, Tercas, ExportarListaMateriais, VerificarModeloReport, ConexaoConfig, GuardaCorpo), adicionados MinWidth/MinHeight/MaxHeight para garantir que caibam em telas pequenas (1366x768).
- **Cotar Fabricacao pegando pontos em vez de faces (cotas inclinadas)**: caminho principal agora usa `FamilyInstance.GetReferences(FamilyInstanceReferenceType.Left/Right/Top/Bottom/Front/Back)` que retorna refs apontando para FACES da peca. Edge picker virou fallback.
- **Cotas por Eixo/Alinhamento so gerando horizontais**: `ExecutarCotagemAutomatica` agora roda em AMBOS os eixos da vista (horizontal E vertical) ao inves de so no eixo principal da selecao.
- **Verificar Modelo - botao "Selecionar" nao seleciona o item clicado**: novo metodo `ResolverElementIdsParaAcao` respeita o item escolhido no TreeView (se for issue individual, seleciona so aquele; senao, todos). Botao tambem minimiza a janela WPF pra usuario ver a selecao no Revit. Duplo-clique em item da arvore seleciona + foca com `ShowElements`.
- **Verificar Modelo - Isolar sem transaction**: `IsolateElementsTemporary` agora roda dentro de Transaction dedicada.
- **Vista da Peca nao isola nem cota**: apos criar a ViewSection (longitudinal e transversal), o servico agora chama `doc.Regenerate()` + `IsolateElementsTemporary` + `ConvertTemporaryHideIsolateToPermanent` + cria cotas automaticas usando `FamilyInstanceReferenceType` (Left/Right na longitudinal, Top/Bottom e Front/Back na transversal).

### Fixed — Onda 4 hardening defensivo (v1.0.4) — pre-ativo, sem bug reportado
- **Stale ElementIds em Verificar Modelo**: se o usuario apagasse um elemento no Revit entre gerar o relatorio e clicar "Isolar na Vista"/"Selecionar", o `IsolateElementsTemporary`/`SetElementIds` lancava `ArgumentException`. Agora `ResolverElementIdsParaAcao` filtra via `doc.GetElement(id) != null` antes de retornar.
Sweep sistematico do codebase identificou e corrigiu 5 crashes latentes:
- **Divisao por zero em Trelica/Tercas/Travamento**: `step = 1.0 / (config.Quantidade + 1)` — se `Quantidade == -1`, div/0. Agora com guard `Quantidade < 1` + mensagem clara em `Services/TrelicaService.cs`, `Services/TercasService.cs`, `Services/TravamentoService.cs`.
- **NumeracaoItensCatalog.ColetarCandidatos**: `doc.ActiveView.Id` estourava NRE quando nao havia vista ativa no escopo VistaAtiva. Agora fallback pra `Enumerable.Empty<Element>()`.
- **ListaMateriaisExportService**: mesmo padrao, agora cai em modelo inteiro se `ActiveView == null`.
- **NumeracaoItensSessao**: `_view = _doc.ActiveView` sem guard causava NRE em 3 pontos distantes (linhas 330/345/539 — Get/SetElementOverrides). Agora fail-fast no construtor com mensagem clara ao usuario.

### Fixed — Onda 3 pos-feedback Victor (v1.0.3) — CNC/DSTV
- **CNC "nao consigo avaliar"**: investigacao proativa identificou 3 raizes que produziam NC1 silenciosamente invalidos:
  - **(a) Cancelamento mascarado**: ESC no PickObjects retornava lista vazia e o caller mostrava "Nenhuma peca estrutural encontrada para exportar" (mensagem errada). Agora `ResultadoExport.Cancelado` distingue cancelamento legitimo de selecao vazia — o caller retorna sem alarmar o usuario.
  - **(b) Dimensoes zeradas silenciosas**: se a familia de viga nao expoe `STRUCTURAL_SECTION_COMMON_HEIGHT` ou se `STRUCTURAL_FRAME_CUT_LENGTH` esta ausente, o NC1 saia com altura/comprimento = 0 (arquivo invalido pra CNC). Novo `ArquivosComDimensaoZerada` conta esses casos; o resumo agora sai como WARNING (nao Info) listando elemento + parametro faltante. Victor agora ve EXATAMENTE qual elemento causou o problema.
  - **(c) NaN/Infinity silencioso em FormatNumber**: continua retornando "0" pra nao quebrar estrutura do arquivo, mas agora loga warning explicito (antes era totalmente mudo).

### Fixed — Onda 2 pos-feedback Victor (v1.0.2)
- **Plano de Montagem nao conseguia selecionar perfis**: `PickObjects` dentro de WPF modal bloqueava a UI do Revit. Agora: (1) usa pre-selecao do Revit se houver; (2) senao `Hide()` a janela, chama `PickObjects`, depois `Show()/Activate()` — mantem o `ShowDialog()` vivo.
- **Plano de Montagem "nao criava o plano" apesar de atribuicao bem-sucedida**: combo mortal: `AtribuirEtapa` caia em fallback `Comments` (string "Etapa:N") quando o parametro Integer nao existia, mas `GerarRelatorio` so lia Integer — dados sumiam silenciosamente. Novo `LerEtapaDoElemento` le Integer OU parseia "Etapa:N" de Comments. `AtribuirEtapa` agora limpa "Etapa:N" antiga via regex antes de escrever (sem acumular).
- **Gerar Conexao "aparece as opcoes mas nao cria"**: duas raizes. (1) `doc.ActiveView.SketchPlane.Normal` lancava `NullReferenceException` — a maioria das vistas nao tem SketchPlane — caia no catch generico e virava "pendente" sem explicacao. Agora usa o overload 3-arg `NewFamilyInstance(ponto, simbolo, StructuralType)`. (2) Quando a familia `EMT_Chapa_Ponta` / `EMT_Dupla_Cantoneira` / `EMT_Chapa_Gusset` nao esta carregada no modelo, a msg antes era generica; agora o dialogo informa EXATAMENTE qual arquivo `.rfa` carregar e os passos (Insert > Load Family).

### Added
- Nada ainda.

### Changed
- Nada ainda.

### Fixed
- Nada ainda.

---

## [1.0.0+licenca] — 2026-04-13 (post-audit + licenciamento self-hosted)

### Added (Sistema de Licença — comercializacao)
- **Modulo `FerramentaEMT/Licensing/` completo** (offline, sem custo de SaaS):
  - `LicenseStatus` (enum: Valid/Trial/Expired/TrialExpired/NotActivated/Tampered/WrongMachine)
  - `LicensePayload` (Email/IssuedAt/ExpiresAt/Version + helpers IsExpired/DiasRestantes)
  - `MachineFingerprint` — SHA-256(MachineGuid + UserName), 16 chars hex
  - `KeySigner` — HMAC-SHA256 com secret hardcoded, encode Base64URL
  - `Base64Url` — encoder/decoder URL-safe
  - `SimpleJson` — serializador minimo (deterministico para HMAC)
  - `LicenseStore` — persiste em `%LocalAppData%\FerramentaEMT\license\` com DPAPI (CurrentUser)
  - `LicenseService` — orquestrador: Initialize/Activate/GetCurrentState com cache em memoria
- **Janelas WPF** (tema Revit, ESC fecha):
  - `LicenseActivationWindow` — colar chave, mostrar fingerprint, copiar para clipboard
  - `AboutWindow` — versao, estado da licenca, dados de suporte
- **Comandos** (IExternalCommand direto, fora do gate de licenca):
  - `CmdAtivarLicenca`, `CmdSobre`
- **Painel "Licença"** no ribbon com botoes empilhados Ativar/Sobre
- **Gate de licenca** em `FerramentaCommandBase.Execute`: bloqueia comando se
  estado nao for `Valid` ou `Trial`. Abre janela de ativacao automaticamente
- **Trial automatico de 14 dias** na primeira execucao
- **Projeto `tools/EmtKeyGen/`** — console standalone para gerar chaves
  (`<Compile Link>` para reusar Secret/HMAC do projeto principal)
- **Documentacao**: `docs/SISTEMA-LICENCA.md` com workflow Hotmart → email → ativacao
- **Testes**: `KeySignerTests`, `LicensePayloadTests`, `Base64UrlTests` (~15 novos casos)

### Changed (Auditoria pos-Sprint 8)
- `FerramentaCommandBase` ganhou propriedade virtual `RequiresLicense` (default true)

### Fixed (Auditoria pos-Sprint 8)
- **`PlanoMontagemService.GerarRelatorio`** — chamava `FilteredElementCollector.FromViewport(view)`
  que NAO existe na Revit API. Corrigido para criar collector view-scoped via construtor:
  `new FilteredElementCollector(doc, doc.ActiveView.Id)`.
- **`CmdGerarConexao`** — chamava `Logger.Error(null, "...")` que jogaria NRE no Serilog.
  Corrigido para usar overload string-only.
- **`CmdGerarConexao`** — try/catch externo redundante (a base ja captura). Removido,
  trocadas chamadas de dialog para helpers `ShowSuccess`/`ShowWarning` da base.
- **`AppTheme.Base.xaml`** — adicionados `AccentButtonStyle` (alias de `PrimaryActionButton`)
  e `LabelText` (TextBlock SemiBold) referenciados em janelas Sprint 6/7.
- **`AppTheme.Light.xaml` / `AppTheme.Dark.xaml`** — adicionados aliases
  `ButtonBackgroundBrush`, `ButtonForegroundBrush`, `PanelBackgroundBrush`,
  `TextBoxBackgroundBrush`, `TextBoxForegroundBrush` (XAML referenciava brushes
  inexistentes — `DynamicResource` resolveria como Transparent em runtime).

---

## [1.0.0] — 2026-04-13 (Release oficial)

Marco oficial do FerramentaEMT — pronto para uso em produção.
Engloba o trabalho dos Sprints 2 a 8 desde 0.9.1, todos entregues no mesmo ciclo.

### Added (Sprint 5 — Export DSTV/NC1)
- **Modulo CNC completo** com geração de arquivos `.nc1` no formato DSTV
  - `Models/CncExport/`: `DstvProfileType` (I/U/L/B/RO/M/T/SO + extensão `ToDstvCode`), `DstvHole` (faces v/h/o/u/s), `DstvFile`, `ExportarDstvConfig`
  - `Services/CncExport/DstvFileWriter` — escrita pura ASCII com CRLF, `InvariantCulture` (sempre `.` decimal), blocos `ST → SC → BO → SI → EN`
  - `Services/CncExport/DstvProfileMapper` — mapeia famílias Revit (W*, HEA*, IPE*, HSS*, L*, etc.) para códigos DSTV
  - `Services/CncExport/DstvHeaderBuilder` — popula header lendo `STRUCTURAL_SECTION_COMMON_*`
  - `Services/CncExport/DstvHoleExtractor` — lê furos paramétricos via convenção `Hole {i} Diameter/X/Y/Face` (e `Furo {i} ...`)
  - `Services/CncExport/DstvExportService` — orquestrador com 3 escopos (seleção/vista/modelo) e agrupamento por marca ou instância
  - `Commands/CmdExportarDstv` + `Views/ExportarDstvWindow`
- **Botão `Exportar DSTV/NC1`** no painel "CNC" do ribbon

### Added (Sprint 6 — Model Checker / Verificação)
- **10 regras de validação automatizada** do modelo estrutural:
  - `MissingMaterialRule`, `MissingMarkRule`, `DuplicateMarkRule` (vê tipos diferentes na mesma marca)
  - `OverlappingElementsRule` (BBox + `BooleanOperationsUtils` com volume > 0,0001 m³)
  - `MissingProfileRule`, `ZeroLengthRule` (<1 mm), `MissingLevelRule`
  - `StructuralWithoutTypeRule`, `MissingCommentRule` (Info), `OrphanGroupRule`
- Modelos puros C# em `Models/ModelCheck/` (Severity, Issue, RuleResult, Report, Config)
- `Services/ModelCheck/ModelCheckService` orquestra com export Excel via ClosedXML
- `Views/VerificarModeloReportWindow` — TreeView agrupado por Severidade/Regra com isolar/selecionar elementos
- **Botão `Verificar Modelo`** no painel "Verificação"

### Added (Sprint 7 — Plano de Montagem + Conexões)
- **Plano de montagem (erection sequence)**:
  - `Models/Montagem/EtapaMontagem`, `PlanoMontagemConfig`, `PlanoMontagemReport`
  - `Services/Montagem/PlanoMontagemService` com paleta cíclica de 5 cores e relatório Excel
  - `Commands/CmdPlanoMontagem` + `Views/PlanoMontagemWindow` (TabControl 3 abas)
- **Geração de conexões metálicas** (3 tipos):
  - `ChapaDePonta`, `DuplaCantoneira`, `ChapaGusset`
  - `Services/Conexoes/ConexaoCalculator` (puro: contagem de parafusos + marcador `CP-12-150x250-4xM19`)
  - `Services/Conexoes/ConexaoGeneratorService` — tolerante a ausência de famílias (escreve em `EMT_Conexao_Tipo`)
  - `Commands/CmdGerarConexao` + `Views/ConexaoConfigWindow`
- **Painel "Montagem"** no ribbon com botões `Plano de Montagem` e `Gerar Conexão`

### Added (Sprint 4 — UX Consistency)
- **`Utils/WindowExtensions.InitializeFerramentaWindow()`** — helper único que aplica tema do Revit, posicionamento padrão e atalho ESC para fechar (substitui chamada explícita de `RevitWindowThemeService.Attach`; ambas convivem)
- **`AppSettings.Update(Action<AppSettings>)`** — load+mutar+save em uma chamada, com tratamento de erro embutido
- **`FerramentaCommandBase`** ganhou helpers padronizados de feedback:
  - `ShowSuccess(message, headline)`, `ShowWarning`, `ShowInfo`
  - `Confirm(message, ...)` para diálogos de confirmação
  - `NothingToDo(reason)` — caminho padrão para "nada a fazer", retorna `Result.Cancelled` e loga

### Changed (Sprint 2 — Performance)
- **Fix N+1 em `ListaMateriaisExportService.ColetarLinhas`**: adicionado `Dictionary<ElementId, Material>` cache; chamadas de `doc.GetElement` por material reduzidas de O(elementos) para O(materiais distintos), tipicamente <50

### Fixed (Sprint 1 — extras descobertos)
- **Logger overload faltando**: `AppSettings.Save/Load` chamava `Logger.Warn(ex, "...{Path}", path)` mas só existia `Warn(Exception, string)`. Corrigido com overloads `Warn/Error/Fatal(Exception, string template, params object[] args)` — sem essa correção, **o projeto não compilava**.

### Tests
- 30+ testes adicionados no `FerramentaEMT.Tests` cobrindo a lógica pura dos novos módulos:
  - `DstvFileTests`, `DstvFileWriterTests`, `DstvProfileMapperTests`
  - `ModelCheckIssueTests`, `ModelCheckReportTests`
  - `EtapaMontagemTests`, `PlanoMontagemReportTests`
  - `ConexaoConfigTests`, `ConexaoCalculatorTests`
- Padrão estabelecido: `<Compile Include>` com `Link=` para testar lógica pura sem referenciar `RevitAPI.dll`

### Notes
- Sprint 3 (refator de `CotasService`) foi avaliado como **não necessário no escopo da 1.0**: as chamadas a `doc.GetElement` no service operam em seleções pequenas do usuário (não há hotspot N+1). O serviço continua estável.
- Sprint 8 entrega documentação de release e handoff para Victor (compilação + instalação) — ver `docs/HANDOFF-VICTOR.md`.

---

## [0.9.1] — 2026-04-13 (Sprint 1 — completion)

### Changed
- **21 commands migrados para `FerramentaCommandBase`** (de 22 — `CmdCortarPerfilPorInterferencia` postergado para Sprint 2 por complexidade)
  - 9 commands TRIVIAL: `CmdAgruparPilaresPorTipo`, `CmdAgruparVigasPorTipo`, `CmdDesabilitarUniaoVigasSelecao`, `CmdDesabilitarUniaoVigasVista`, `CmdGerarCotasPorAlinhamento`, `CmdGerarCotasPorEixo`, `CmdIsolarPilaresEstruturais`, `CmdIsolarVigasEstruturais`, `CmdLimparAgrupamentosVisuais`
  - 12 commands MEDIA: `CmdAjustarEncontroVigas`, `CmdCotarPecaFabricacao`, `CmdExportarListaMateriais`, `CmdGerarTercasPlano`, `CmdGerarTravamentos`, `CmdGerarTrelica`, `CmdGerarVistaPeca`, `CmdLancarEscada`, `CmdLancarGuardaCorpo`, `CmdLancarPipeRack`, `CmdMarcarPecas`, `CmdNumerarItens`
  - **Resultado**: ~600 linhas de boilerplate eliminadas, logging automático em todos os commands
- **`AppSettings` agora thread-safe** (`ReaderWriterLockSlim` + escrita atômica via `.tmp` rename)
- **`AppSettings` exceções específicas**: trata `IOException`, `JsonException`, `UnauthorizedAccessException` separadamente, todos com log

### Fixed
- **Bug B5** — `CmdGerarCotasPorEixo` registrado no ribbon (estava órfão no `App.cs`)
- **Bug B2** — 6 `catch {}` silenciosos em `CotasService.cs` substituídos por catch com `Logger.Warn` + contexto:
  - `CriarCotaAlinhada` (linha 256)
  - `TentarObterLinhaDeCota` (linha 337)
  - `TentarObterPontoDeLado` (linha 354)
  - `TentarCriarCotaAlinhada` (linha 444)
  - `TentarCriarDimensao` (linha 871)
  - `TentarCriarDimensaoPorPlanosAuxiliares` (linha 948)

### Pending (Sprint 2)
- `CmdCortarPerfilPorInterferencia` (775 linhas, COMPLEXA) — migrar com cuidado + testes
- Refator de `ListaMateriaisExportService` (2.081 linhas)

---

## [0.9.0] — 2026-04-13

### Added
- **Sprint 0** — Repo hygiene profissional: `.gitignore`, `README.md`, `CHANGELOG.md`
- **Sprint 0** — GitHub Actions CI workflow (`build.yml`)
- **Sprint 0** — Scripts auxiliares: `Compilar-Debug.bat`, `Limpar-Tudo.bat`
- **Sprint 1** — Sistema de logging estruturado com Serilog
  - Logs salvos em `%LocalAppData%\FerramentaEMT\logs\emt-YYYYMMDD.log`
  - Rotação diária, retenção de 30 dias
- **Sprint 1** — `FerramentaCommandBase` — classe base abstrata para todos os commands
  - Try/catch padronizado
  - Logging automático de início/fim/duração
  - Tratamento separado de `OperationCanceledException`
  - Diálogo de erro padronizado
- **Sprint 1** — Projeto de testes `FerramentaEMT.Tests` (xUnit + Moq + FluentAssertions)
- **Sprint 1** — `Constants.cs` — magic numbers extraídos (offsets de cota, tolerâncias)

### Changed
- **Sprint 1** — `FerramentaEMT.csproj` agora compila com `TreatWarningsAsErrors=true` em Release
- **Sprint 1** — Pacote NuGet `Serilog` adicionado
- **Sprint 1** — Os 22 commands existentes migrados para herdar de `FerramentaCommandBase`

### Fixed
- **Bug B1** — `Visibility.Visible` ambíguo em `GerarVistaPecaWindow.xaml.cs` (já corrigido em sessão anterior, registrado aqui para histórico)
- **Bug B2** — `catch {}` silenciosos substituídos por catches específicos com log em `CotasService`
- **Bug B4** — `AppSettings` agora usa `ReaderWriterLockSlim` (thread-safe)
- **Bug B5** — `CmdGerarCotasPorEixo` (órfão) registrado no ribbon

### Security
- Nenhuma mudança de segurança nesta versão.

---

## Versionamento Planejado

| Versão | Tema | Previsão |
|---|---|---|
| `0.9.0` | Sprint 0/1 — Fundação + qualidade | abr/2026 |
| `0.10.0` | Sprint 2 — Refator ListaMateriaisExportService | abr/2026 |
| `0.11.0` | Sprint 3 — Refator CotasService + CotarPecaFabricacaoService | mai/2026 |
| `0.12.0` | Sprint 4 — UX consistency + ribbon reorg | mai/2026 |
| `0.13.0` | Sprint 5 — Export DSTV/NC1 (CNC) | jun/2026 |
| `0.14.0` | Sprint 6 — Verificação de Modelo (Clash + QA) | jun/2026 |
| `0.15.0` | Sprint 7 — Plano de Montagem + Conexões | jul/2026 |
| `1.0.0` | Sprint 8 — Polish + Release oficial | jul/2026 |

---

## Convenções

### Tipos de mudança
- **Added** — novas features
- **Changed** — mudanças em features existentes
- **Deprecated** — features marcadas para remoção
- **Removed** — features removidas
- **Fixed** — correções de bug
- **Security** — correções de segurança

### Versão
- **Major** (`X.0.0`) — quebra compatibilidade ou API pública
- **Minor** (`0.X.0`) — nova funcionalidade compatível
- **Patch** (`0.0.X`) — correção de bug compatível
