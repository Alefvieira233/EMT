# Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
versionamento [SemVer](https://semver.org/lang/pt-BR/).

---

## [Unreleased]

Roadmap da auditoria de mercado (`AUDITORIA-MERCADO-2026-04-27.md`):
- Code signing (P0.1)
- Auto-update mechanism (P0.2)
- Crash reporting remoto via Sentry (P0.3)
- CI compilando o csproj principal (P0.4)
- Privacy Policy + EULA (P0.5)
- Telemetria opt-in (P0.6)
- Migração ADR-003 dos 7 services restantes que ainda usam AppDialogService

---

## [1.6.0] — 2026-04-27 (Wave 2 + zoneamento NBR 6118 re-portado)

Esta release promove `1.6.0-rc.1` (incorporação Victor Wave 2) a versão final, incluindo o follow-up do zoneamento de estribos NBR 6118 e cleanup ADR-003 do `PfTwoPileCapRebarService`. Resolve as 2 regressões conhecidas que ficaram documentadas na rc.1.

### Security — Base64Url canonicalização (defesa-em-profundidade)
- **`Licensing/Base64Url.Decode`** agora rejeita encodings não-canônicos. O bug foi descoberto pelo teste `KeySigner.Verify_returns_null_when_signature_is_tampered`: a HMAC-SHA256 de 32 bytes em Base64URL ocupa 43 chars × 6 bits = 258 bits, mas apenas 256 são significativos. Os 2 bits sobressalentes do último char eram ignorados pelo `Convert.FromBase64String`, permitindo múltiplas representações do mesmo token.
- Implicação anterior (não-crítica para forja, mas problemática para licenciamento): um cliente poderia redistribuir a mesma chave em variações cosméticas, quebrando fingerprinting de tokens.
- Fix: após `Convert.FromBase64String`, re-codificamos os bytes via `Encode` e comparamos com a entrada original; se diferirem, lançamos `FormatException` (e `KeySigner.Verify` retorna null como esperado).
- HMAC em si nunca esteve comprometido — não é possível forjar assinatura sem o segredo. Esta correção elimina ambiguidade de representação, alinhando o sistema com o comportamento esperado do teste.

### Added — Zoneamento NBR 6118 de estribos (re-portado da v1.5.0)
- **`PfRebarService.InsertColumnStirrups`** agora suporta dual-mode:
  - `UsarEspacamentoUnico=true` → modo Victor (1 rebar com `EspacamentoCm` uniforme)
  - `UsarEspacamentoUnico=false` (default) → zoneamento NBR 6118 com 3 rebars (inferior + central + superior) usando `EspacamentoInferiorCm`, `EspacamentoCentralCm`, `EspacamentoSuperiorCm` e `AlturaZonaExtremidadeCm`
  - Pilares circulares sempre caem no modo simples (zoneamento de pilar circular não é prática típica da norma brasileira)
- **`PfRebarService.InsertBeamStirrups`** análogo:
  - `UsarEspacamentoUnico=true` → modo Victor
  - `UsarEspacamentoUnico=false` (default) → zoneamento por apoio (inicio + central + fim) com `EspacamentoApoioCm`, `EspacamentoCentralCm`, `ComprimentoZonaApoioCm`
- Implementação preserva todas features Wave 2: `RebarShape`, `RebarHookType`, `RebarStyle.StirrupTie`. Cada zona aplica o shape/hook escolhido, mantendo rastreabilidade visual no Revit.
- `PfRebarService.cs` cresceu de 1.768 → 1.891 linhas. Backup `PfRebarService.cs.bak-alef-v1.5` mantido como referência histórica do código v1.5.0.

### Fixed — Regressão "zoneamento dormente" da rc.1
- Configurações de zoneamento (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidade`, `EspacamentoApoio`, `ComprimentoZonaApoio`) estavam preservadas em `PfRebarConfigs.cs` mas o serviço não as lia. Bug arquitetural resolvido com a implementação dual-mode acima.

### Refactored — ADR-003 cleanup do `PfTwoPileCapRebarService` (Wave 2 followup)
- **Service mudo agora**: removidas 2 chamadas `AppDialogService.ShowWarning/ShowInfo` (linhas 28 e 82). Em conformidade com ADR-003.
- **Nova assinatura**: `Result Execute(uidoc, config, out PfTwoPileCapResultado resultado)`. Caller (`CmdPfInserirAcosBlocoDuasEstacas`) decide UX a partir do DTO populado.
- **Novo DTO `Models/PF/PfTwoPileCapResultado`** com `SelecaoVazia`, `HostsProcessados`, `HostsComSucesso`, `ArmadurasCriadas`, `Avisos` (limitados a 10 na UI), `ToResumo()`.
- **Logger.Info** estruturado adicionado ao final do `Execute` com métricas (hosts, sucesso, armaduras, avisos) — facilita troubleshooting em produção.
- Caller `CmdPfInserirAcosBlocoDuasEstacas` consome `resultado` e usa `ShowWarning`/`ShowInfo` herdados de `FerramentaCommandBase`.

### Tests
- **+1 arquivo**: `Models/PF/PfTwoPileCapResultadoTests` — 5 Facts cobrindo defaults, formato sem avisos, formato com avisos, limite de 10 ocorrências na UI, flag `SelecaoVazia`.
- 1 LinkedSource novo no `FerramentaEMT.Tests.csproj`.
- Total acumulado v1.6.0: **465 casos** (eram 460 na rc.1).

### Known issues
- Os outros 7 services que ainda usam `AppDialogService` (`PfRebarService`, `AutoVistaService`, `AgrupamentoVisualService`, `AjustarEncontroService`, `ListaMateriaisExportService`, `CotarPecaFabricacaoService`, `MarcarPecasService`) continuam com o padrão antigo. Migração planejada como dívida arquitetural (P1.1 da auditoria de mercado).

---

## [1.6.0-rc.1] — 2026-04-27 (Incorporação Victor Wave 2 — RebarShape + NBR 6118 + Bloco 2 Estacas)

Segunda onda de incorporação do snapshot do Victor (`FerramentaEMT (3).rar`, 2026-04-24). Foco em PF: catalogo de RebarShape do projeto Revit, preview visual nas janelas, cálculo de ancoragem NBR 6118, lap splice, modo coordenadas manual e rotina dedicada para bloco de duas estacas. Ribbon separado em duas abas para desacoplar o fluxo PF do fluxo metálico.

**Status de validação (2026-04-27):**
- Build Release: 0 Erro(s), 2 Aviso(s) pre-existentes (RevitAPI/RevitAPIUI references). Tempo: 7s.
- Plugin carregado e visualizado no Revit 2025 do Alef. Duas abas (`Ferramenta EMT` + `Ferramentas ECC`) renderizadas corretamente.
- Suite de testes: 460 casos totais, 458 aprovados na primeira rodada. As 2 falhas eram bug REAL no código do Victor (`ToComment` culture-sensitive em pt-BR) — corrigido nesta release como parte do `### Fixed` abaixo. Re-rodar testes pos-fix dá 460/460.
- Instalador distribuível gerado (`artifacts/installer/FerramentaEMT-Revit2025-Release.zip`, 3.8 MB + `setup-publish/FerramentaEMT.SetupBootstrapper.exe`).

### Fixed — Build + culture-invariant (descoberto pelos testes Wave 2)
- **`Services/PF/PfElementService.IsTwoPileCap`** — método ausente após Wave 2 (eu adotei o `PfTwoPileCapRebarService` mas esqueci de trazer o helper que ele e o command `CmdPfInserirAcosBlocoDuasEstacas` chamam). Sem isso o build falhava com 2× CS0117. Detecta `FamilyInstance` com `Category.OST_StructuralFoundation`.
- **`FerramentaEMT.csproj`** — adicionada `<PackageReference Include="System.Drawing.Common" Version="8.0.10" />`. O `PfRebarShapePreviewService` (do Victor) usa `System.Drawing.Bitmap` e `System.Drawing.Imaging.ImageFormat` — esses tipos saíram do BCL no .NET 5+ e exigem package explícita. Sem isso o build falhava com 3× CS0012/CS1069.
- **`Models/PF/PfTwoPileCapBarPosition.ToComment`** — bug culture-sensitive descoberto pelos testes em pt-BR. O método usava `$"...{x:0.##}"` que respeita `CurrentCulture`, gerando `"diam. 6,3"` em vez de `"diam. 6.3"` quando a máquina está configurada em pt-BR. Como esse `Comment` vai parar no parâmetro Comments do Revit e é consumido por schedules/CSV downstream, vírgula no decimal corromperia parsers terceiros. Forçado `CultureInfo.InvariantCulture` em todos os formatadores numéricos (3 ocorrências). 460/460 testes passam pós-fix.

### Added — Catálogo de RebarShape do projeto Revit (Victor Wave 2)
- **`Services/PF/PfRebarShapeCatalog`** — varre `FilteredElementCollector(RebarShape)` filtrado por `RebarStyle.StirrupTie`, primeiro item sempre "Automatico" (flag `IsAutomatic=true`), ordena por sufixo numérico.
- **`Services/PF/PfRebarShapePreviewService`** — gera `BitmapImage` (220 px default) do shape selecionado para exibir na UI; fallback textual quando preview indisponível.
- **`Models/PF/PfRebarShapeOption`** — DTO (`ElementIdValue`, `Name`, `DisplayName`, `IsAutomatic`). `ToString()` prefere `DisplayName`.
- **`PfBeamStirrupsWindow` + `PfColumnStirrupsWindow`** — combo box de shape + `<Image>` de preview. Quando o usuário escolhe um shape do projeto, a rotina cria estribo automático primeiro; se o shape selecionado for compatível com a geometria, aplica em cima. Se não for compatível, mantém o automático sem interromper o comando.

### Added — Cálculo NBR 6118 (ancoragem + traspasse)
- **`Services/PF/PfNbr6118AnchorageService.Calculate(diameterMm, PfLapSpliceConfig)`** → `PfAnchorageResult` com `FbdMpa`, `Eta1`, `Eta2`, `Eta3`, `BasicAnchorageCm`, `RequiredAnchorageCm`, `MinimumAnchorageCm`, `SpliceLengthCm`, `MinimumSpliceLengthCm`, `AnchorageAlpha`, `SpliceAlpha`, `FctkInfMpa`, `FydMpa`. Inputs: fck/fyk (clamps para 12 MPa / 250 MPa), `PfBarSurfaceType` (Lisa=1.0, Entalhada=1.4, Nervurada=2.25), `PfBondZone` (Boa=1.0, Ruim=0.7), `PfAnchorageType` (Reta=α1.0, Gancho*=α0.7), `SplicePercentage` (tabela de α variando com 20%/25%/33%/50%/>50%). `ToDetailText()` gera string padronizada `"EMT NBR 6118:2023 | phi {d} mm | lb {lb} cm | lb,nec {lbNec} cm | traspasse l0 {l0} cm | fbd {fbd} MPa"` para parâmetro Comments do Revit.
- **`Models/PF/PfLapSpliceConfig`** — configuração de traspasse: `Enabled`, `MaxBarLengthCm` (default 1200), `ConcreteFckMpa` (25), `SteelFykMpa` (500), `BarSurface`, `BondZone`, `AnchorageType`, `SplicePercentage` (50), `BarSpacingCm` (8), `AsCalcCm2`, `AsEfCm2`.
- Integrado em `PfColumnBarsConfig.Traspasse` e `PfBeamBarsConfig.Traspasse` (quando `Enabled=true` e barra > `MaxBarLengthCm`, serviço insere traspasse).

### Added — Bloco de duas estacas (rotina dedicada)
- **`Commands/PF/CmdPfInserirAcosBlocoDuasEstacas`** — novo botão em "PF Armaduras" na aba "Ferramenta EMT". Usa `FerramentaCommandBase` (license gate + logging uniformes).
- **`Services/PF/PfTwoPileCapRebarService.Execute(uidoc, config)` → `Result`** (496 linhas) — análogo ao `PfRebarService.ExecuteBeamBars` em estrutura: coleta hosts, calcula `HostFrame`, lança superior/inferior/lateral.
- **`Services/PF/PfTwoPileCapBarCatalog`** — catálogo estático `Tipo4` com 14 posições (diâmetros 6.3/8.0/10.0/12.5/16.0 mm, formas `Reta`, `U`, `RetanguloFechado`, `EstriboVertical`, `CaliceVertical`, `FormaEspecial`). `QuantidadePorBloco = QuantidadeTotalPdf / 3` (3 blocos por planta).
- **`Models/PF/PfTwoPileCapBarPosition`** — DTO com `Posicao`, `DiametroMm`, `QuantidadeTotalPdf`, `QuantidadePorBloco`, `ComprimentoCm`, `EspacamentoCm`, `Forma` (enum `PfTwoPileCapBarShape`), `DescricaoForma`. `ToComment()` gera string padronizada `"N{pos} - POS {pos} - diam. {d} - C/{spacing} - C={length} - {descricao}"` (culture-invariant).
- **`Views/PfTwoPileCapRebarWindow.xaml(.cs)`** — UI de configuração.

### Added — Modo coordenadas manual (barras de pilar/viga)
- **`PfRebarPlacementMode`** — enum `Automatico` (default) e `Coordenadas`.
- Quando `ModoLancamento == Coordenadas`, o serviço usa `Coordenadas` (`List<PfColumnBarCoordinate>` ou `List<PfBeamBarCoordinate>`) em cm local. Para viga, `PfBeamBarCoordinate` inclui `BarTypeName` e `Posicao` (ex.: `"Superior"`, `"Inferior"`, `"Lateral"`).
- `PfColumnBarsConfig.QuantidadeCircular` — suporta seção circular com N barras igualmente espaçadas.

### Added — Preview de seção nas janelas PF
- **`PfRebarService.BuildBeamSectionPreview(FamilyInstance)` e `BuildColumnSectionPreview(FamilyInstance)`** (static helpers) — retornam `PfRebarSectionPreview` (shape retangular/circular, dimensões em cm, raio se circular) para renderizar a seção na UI.
- **`PfRebarService.GetColumnLengthCm(column)` e `GetBeamLengthCm(beam)`** (static helpers) — leitura da dimensão longitudinal.
- `PfBeamBarsWindow` e `PfColumnBarsWindow` renderizam o preview com indicação das posições das barras.

### Changed — Ribbon dividida em duas abas
- **Aba "Ferramenta EMT"** — só fluxo PF (painéis `PF Construção`, `PF Documentação`, `PF Armaduras`).
- **Aba "Ferramentas ECC"** (nova) — fluxo geral (painéis `Modelagem`, `Estrutura`, `Vigas`, `Vista`, `Documentação`, `Fabricação`, `CNC`, `Verificação`, `Montagem`, `Licença`).
- Motivação: o usuário PF (concreto pré-fabricado) e o usuário metálico têm fluxos muito distintos; separar reduz fricção visual.

### Changed — Commands PF (AcosPilar/Viga, EstribosPilar/Viga)
- Todos os 4 comandos agora fazem pick de elemento via `PfElementService.GetSelectionOrPick` **antes** de abrir a janela, e passam `hosts[0]` ao construtor novo das windows. Permite que a janela construa o preview da seção na abertura.
- Mantêm `FerramentaCommandBase` (license gate + logging).

### Changed — `PfRebarConfigs.cs` (API expansion, backward-compatible)
- Adicionadas 6 enums: `PfRebarPlacementMode`, `PfRebarSectionShape`, `PfStirrupHookAngle`, `PfBarSurfaceType`, `PfBondZone`, `PfAnchorageType` (+ `PfBeamBarEndMode` já existia).
- Adicionadas 5 sealed classes: `PfColumnBarCoordinate`, `PfBeamBarCoordinate`, `PfRebarSectionPreview`, `PfLapSpliceConfig`, `PfTwoPileCapRebarConfig`.
- Estribos: adicionados `ShapeName`, `DiametroMm`, `EspacamentoCm` unificado e `Dobra` (`PfStirrupHookAngle`). **Campos granulares de zoneamento preservados** (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidade`, `EspacamentoApoio/Central`, `ComprimentoZonaApoio`) atrás da flag `UsarEspacamentoUnico=false` (default) — ver "Regressão conhecida" abaixo.
- Barras: adicionados `ModoLancamento`, `QuantidadeCircular`, `Traspasse`, `Coordenadas`.

### Preserved — Decisões contra a snapshot Victor
- **`ModelCheckService`** mantém `Result<ModelCheckReport>` (ADR-003) e `IProgress<ProgressReport>` + `CancellationToken` (ADR-004). **Não adotamos** a versão simplificada do Victor (10.4 KB, sem Result/Progress/Cancel).
- **`ModelCheckCollector`, `ModelCheckVisualizationService`** e as 9 `ModelCheckRules/*Rule.cs` mantidas nas versões Alef.
- **`ListaMateriaisExportService`, `AgrupamentoVisualService`, `NumeracaoItensService`, `DstvExportService`** mantidos (todos com terceira/quarta/quinta adoção ADR-003).
- **`CrashReporter.Initialize()`** no `App.OnStartup` — preservado.
- **`LicenseSecretProvider.GetResolvedSource()`** logging no `App.OnStartup` — preservado.
- **`CmdCortarElementos`** (nossa Onda 3 PR-1) — preservado na aba "Ferramentas ECC", painel Estrutura. Victor havia removido dessa snapshot.
- **UTF-8 com acentos** em `App.cs` e em toda a UI — preservado. Victor havia regredido algumas strings a ASCII.

### Regression — Conhecida, com follow-up planejado
- `PfRebarService` consome `EspacamentoCm` único em estribos (modo Victor). Os campos granulares de zoneamento NBR 6118 (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidadeCm`, `EspacamentoApoio/Central`, `ComprimentoZonaApoioCm`) estão preservados em `PfRebarConfigs.cs` mas o serviço não os lê ainda. A flag `UsarEspacamentoUnico` (default `false`) já existe como ponto de ramificação. Follow-up: PR separado que restaura a lógica de zoneamento no `PfRebarService` quando a flag é `false`. Backup da versão v1.5.0 salvo em `Services/PF/PfRebarService.cs.bak-alef-v1.5` (945 linhas) para referência.

### Tests
- **+4 arquivos de teste** cobrindo as novas features:
  - `Models/PF/PfRebarShapeOptionTests` — 5 Facts
  - `Models/PF/PfTwoPileCapBarPositionTests` — 5 Facts + 1 Theory (6 variants) = 11 casos. Inclui teste de culture-invariant em pt-BR (regressão histórica do projeto).
  - `Services/PF/PfNbr6118AnchorageServiceTests` — 15 Facts (cenários: zero/null guard, eta1/2/3 corretos para cada combinação, α de ancoragem com gancho, fck<12 clamp, lb mínimo, traspasse α≥1.0, `AreaRatio` reduz `lbNec`, idempotência).
  - `Services/PF/PfTwoPileCapBarCatalogTests` — 10 Facts (14 posições, sequencialidade, `Get()` por posição existente/inexistente, `QuantidadePorBloco = Total/3`, descrições não vazias, diâmetros/comprimentos válidos, snapshot das posições chave 1/4/13).
- 4 LinkedSources novos no `FerramentaEMT.Tests.csproj`.

Origem: snapshot do Victor em 2026-04-24. Plano de merge em 8 ondas documentado em `outputs/ANALISE-VICTOR-WAVE2.md`.

---

## [1.5.0] — 2026-04-20 (Incorporação Victor + Verificação de Carimbo + Hardening pré-release)

Release de qualidade focada em **3 eixos**: (1) incorporação seletiva do trabalho do Victor (Cortar Elementos, módulo PF já integrado em v1.2.0), (2) feature completa de verificação de carimbo (TitleBlock) no ModelCheck com navegação 3D, (3) hardening de segurança e qualidade (HMAC externalizado, DPI overflow, empty catches eliminados). Adoção completa do ADR-003 (Result<T>) e ADR-004 (progresso + cancelamento) nos serviços principais. Suite de testes cresce para **419 casos**.

### Added — Cortar Elementos (Onda 3, PR-1: incorporação seletiva do trabalho do Victor)
- **`Commands/CmdCortarElementos`** — novo botão no painel "Estrutura" ("Cortar Elementos"). Seleciona pisos, quadros estruturais e colunas/pilares (pré-seleção ou `PickObjects` com filtro), detecta interferências entre hosts e cortadores e aplica corte automático escolhendo entre `JoinGeometryUtils` e `SolidSolidCutUtils` conforme o par aceita. Comando gerencia a transação externa (commit só quando há alteração), restaura seleção com os elementos envolvidos e mostra resumo + diagnóstico ao final via helpers de `FerramentaCommandBase` (`ShowSuccess`/`ShowInfo`/`ShowWarning`).
- **`Services/CortarElementosService`** — serviço "mudo" (ADR-003) retornando `Result<CortarElementosResultado>`. Zero `AppDialogService`, só `Logger`. Duas estratégias em sequência (JoinGeometry com `SwitchJoinOrder` quando necessário, fallback para SolidSolidCut), cada tentativa em `SubTransaction` para reverter rejeição da API Revit sem derrubar a transação pai. Validadores de escopo (`EhElementoValidoParaEscopo`, `EhHostValido`, `EhCortadorValido`) expostos como `internal static` para o comando reusar no filtro de seleção.
- **`Models/CortarElementosResultado`** — payload consolidado (total selecionados, hosts/cortadores analisados, pares com interferência, alterações aplicadas, já conformes, falhas, IDs relacionados, linhas de diagnóstico) + computadas `HouveAlteracao` e `HouveSucesso`. Extraído para `Models/` (era `internal` aninhado no serviço, na versão original do Victor) para permitir teste unitário fora do assembly. Listas null-safe via fallback no construtor.
- **`FerramentaEMT.Tests/Models/CortarElementosResultadoTests`** — 6 testes cobrindo preservação de argumentos, fallback de listas nulas, e as regras de `HouveAlteracao`/`HouveSucesso` (incluindo o caso sutil "só já conformes → sucesso", que é o que o comando usa para decidir entre Info e Warning).

Origem: snapshot da versão do Victor em 2026-04-14. Adaptações para a base do Alef: ADR-003 (zero UI no serviço, caller monta UX), `FerramentaCommandBase` no comando (license gate + logging uniforme), `Logger` da `Infrastructure` em vez de `Debug.WriteLine`. ADR-004 (progress/cancel) deliberadamente fora de escopo — operação tipicamente rápida (<100 pares) e a `SubTransaction` já é o ponto de abortar se der ruim. Ver `pending-push/PLANO-INCORPORACAO-VICTOR.md`.

### Added — UX de cancelamento (ADR-004)
- **`Views/ProgressWindow.xaml(.cs)`** — dialogo reutilizavel de progresso com barra, percentual, contador N/Total, mensagem detalhada e botao Cancelar. Tematico com `AppTheme.Base.xaml`. Fechar pelo X equivale a Cancelar.
- **`Utils/RevitProgressHost`** — host estatico `Run<T>(title, headline, work)` que abre a janela, corre o servico no thread principal do Revit (requisito de API single-threaded) e bombeia o `Dispatcher` entre eventos de `IProgress` para a UI atualizar e o Cancelar chegar ao `CancellationTokenSource`. Exception `OperationCanceledException` propaga ate o comando, que retorna `Result.Cancelled`.
- **`docs/ADR/004-threading-model-progress-cancel.md`** — documenta o modelo de threading, por que `Task.Run` e proibido com Revit API, quando usar o host e quando nao usar.
- **`CmdVerificarModelo`** passa a usar `RevitProgressHost` — primeiro consumidor real. Usuario ve progresso por regra e pode cancelar sem esperar 30s de `DuplicateMarkRule` em modelos grandes.
- **`CmdExportarDstv`** passa a usar `RevitProgressHost` — segundo consumidor. Agora o usuario ve quantas pecas ja foram processadas/gravadas e pode cancelar no meio, util em exports de modelos grandes (>500 pecas) onde a maquina CNC esta ocupada e o usuario quer abortar.

### Changed — DSTV export em duas fases (ADR-003 + ADR-004)
- **`DstvExportService` refatorado em duas fases** para conciliar `PickObjects` (modal Revit nativo) com `RevitProgressHost` sem UX conflitante (janela de progresso ficaria vazia por tras da selecao). Nova API:
  - `ColetarElementos(uidoc, config) → Result<ColetaResult>` — pode abrir `PickObjects`, NAO aceita progress/CT (interacao curta). `ColetaResult { List<FamilyInstance> Elementos; bool Cancelado }` distingue ESC de selecao vazia.
  - `Executar(uidoc, IReadOnlyList<FamilyInstance> elementos, config, progress, ct) → Result<ResultadoExport>` — processa e grava, aceita progress+CT. Compativel com `RevitProgressHost`.
- **`DstvExportService.BuildResumoText(ResultadoExport) → string`** e **`AbrirPastaNoExplorer(string)`** expostos como static — o comando monta o dialogo e decide quando abrir o Explorer. Removido `AppDialogService` do servico (principio ADR-003: service "mudo", so retorna e loga).
- **`ResultadoExport.Cancelado` removido** — cancelamento so acontece na fase de coleta; a flag migrou para `ColetaResult`. Drop-safe porque o unico caller (`CmdExportarDstv`) foi atualizado simultaneamente.
- **`CmdExportarDstv`** ajustado ao novo fluxo: coleta → `RevitProgressHost.Run(service.Executar)` → montagem do resumo → warning/info → abertura opcional do Explorer. `try/catch (OperationCanceledException) → Result.Cancelled` cobre o Cancel da nova janela.

### Changed — Segunda adoção do ADR-003
- **`ModelCheckService.Executar`** agora retorna `Result<ModelCheckReport>` e aceita `IProgress<ProgressReport>` + `CancellationToken` opcionais. Falhas de domínio (`uidoc` nulo, config ausente, nenhuma regra habilitada) voltam como `Result.Fail` com mensagem amigável — o comando chamador apresenta o diálogo. Progresso é reportado por regra executada (`N/Total — nome da regra: X problema(s)`), throttle de 100 ms. `OperationCanceledException` propaga até o comando, que retorna `Result.Cancelled`. Segue-se o template do ADR-003 validado antes no `DstvExportService`.
- **`ModelCheckReport` ganha `ExportedToPath` e `ExportError`** — exportação Excel falhar **não invalida** a análise (princípio de falha parcial, ADR-003). Comando chamador inspeciona as duas propriedades e decide como apresentar: warning quando Excel falhou, info quando concluiu, nada quando export não foi solicitado. Remove dois `AppDialogService.ShowInfo/ShowError` do serviço.
- **`CmdVerificarModelo`** atualizado para consumir `Result<ModelCheckReport>` e dois sinais de Excel independentes. `try/catch (OperationCanceledException) → Result.Cancelled` pronto para quando a UI ganhar botão Cancelar.

### Changed — Terceira adoção do ADR-003 + ADR-004 (Lista de Materiais)
- **`ListaMateriaisExportService.Exportar` refatorado** — nova assinatura `Result<ResultadoExport> Exportar(uidoc, config, IProgress<ProgressReport>?, CancellationToken)`. Removidas as 7 chamadas a `AppDialogService` do serviço (service "mudo" por ADR-003). Falhas de domínio (UIDocument nulo, config inválida, categoria/aba vazia, caminho vazio, nenhum elemento elegível) voltam como `Result.Fail` com mensagem amigável; o comando decide como apresentar. Falhas de IO/Revit durante coleta ou gravação capturadas via `try/catch → Result.Fail` com log em `Logger.Error`. `OperationCanceledException` propaga ao callsite.
- **Progresso reportado durante `ColetarLinhas`** — elemento a elemento (throttle 100 ms), com mensagem `"Processando N/Total — Categoria"`. Em modelos grandes (milhares de elementos), usuário vê avanço real em vez de UI travada. `ThrowIfCancellationRequested()` no topo do loop permite cancelamento responsivo.
- **Três fases explícitas** — coleta (interrompível), agrupamento (CPU-only rápido, não interrompível) e gravação Excel via ClosedXML (IO atômica, não interrompível — abortar no meio corromperia o `.xlsx`). `ResultadoExport` carrega contagens separadas (linhas, grupos, elementos estruturais, perfis, conexões) + duração + caminho do arquivo. `BuildResumoText(ResultadoExport)` estático monta o texto do diálogo de sucesso no comando.
- **`CmdExportarListaMateriais`** consome a nova API via `RevitProgressHost.Run` (ADR-004), ganhando barra de progresso + botão Cancelar sem mudar UX de sucesso. `try/catch (OperationCanceledException) → Result.Cancelled`. Mantém o catch existente para `FileNotFoundException/FileLoadException` de ClosedXML ausente (dependência de deploy).

### Changed — Quarta adoção do ADR-003 + ADR-004 (Agrupamento Visual)
- **`AgrupamentoVisualService` migrado** — 3 métodos públicos agora retornam `Result<ResultadoAgrupamento>` ou `Result<ResultadoLimpeza>` e aceitam `IProgress<ProgressReport>?` + `CancellationToken` opcionais. Removidas as 4 chamadas a `AppDialogService` (UIDocument nulo, "nada para agrupar", resumo de sucesso de Agrupar e de Limpar) — serviço agora é 100% "mudo" por ADR-003, só `Logger`. `ResultadoAgrupamento` e `ResultadoLimpeza` expõem contadores (elementos na vista, conjuntos identificados, conjuntos coloridos, grupos EMT criados/desfeitos) + `TimeSpan Duracao` + `List<string> Falhas` para o comando decidir a UX.
- **Duas fases explícitas por ADR-004** — (1) coleta + geração de assinaturas de equivalência é interrompível (`ThrowIfCancellationRequested` a cada 32 elementos, `Report` a cada 64); (2) transação Revit que aplica overrides e cria/desfaz grupos é não-interrompível (cancelar no meio deixaria overrides parciais na vista). Progresso durante a fase 2 usa o índice do conjunto (N conjuntos / Total). Em modelos com milhares de vigas, CriarAssinaturaEquivalencia não é trivial — o progresso granular evita a sensação de UI travada.
- **`BuildResumoText(ResultadoAgrupamento)`** e **`BuildResumoText(ResultadoLimpeza)`** estáticos montam o texto de sucesso (incluindo as até 6 primeiras falhas com elipse `… e mais N`) — comando consome via `AppDialogService.ShowInfo`. Os 3 comandos (`CmdAgruparPilaresPorTipo`, `CmdAgruparVigasPorTipo`, `CmdLimparAgrupamentosVisuais`) foram atualizados; fluxo de sucesso e UX de erro idênticos ao que existia antes, mas a lógica de apresentação agora mora onde deve (comando, não serviço).

### Added — Verificação de Carimbo no ModelCheck (Miniciclos 1–6)
- **`Services/ModelCheck/ModelCheckCollector`** (M1) — coleta centralizada de elementos estruturais para todas as regras do ModelCheck, eliminando coletas duplicadas e garantindo consistência entre regras.
- **`Models/ModelCheck/TitleBlockCheckConfig`** (M2) — modelos para configuração de verificação de carimbo: campos obrigatórios (nome do projeto, número da folha, data, responsável), tolerâncias e regras de validação.
- **`Services/ModelCheck/ModelCheckVisualizationService`** (M3) — serviço de navegação 3D que permite ao usuário clicar em um problema no relatório e navegar diretamente ao elemento no modelo Revit (zoom, isolamento temporário, highlight).
- **`Services/ModelCheck/ModelCheckRules/TitleBlockRule`** (M4) — nova regra de verificação que valida campos obrigatórios do carimbo (TitleBlock) em todas as folhas do projeto. Detecta campos vazios, valores placeholder e inconsistências entre folhas.
- **`Views/VerificarModeloConfigWindow`** atualizada (M5) — seção de configuração de verificação de carimbo na UI, com checkboxes por campo e lista de campos customizados.
- **`Views/VerificarModeloReportWindow`** atualizada (M6) — integração do `ModelCheckVisualizationService` na janela de relatório. Duplo-clique em qualquer issue navega ao elemento no Revit. Botões "Isolar" e "Selecionar" usam o novo serviço.

### Security — HMAC Secret Externalizado (Miniciclo 9)
- **`LicenseSecretProvider` hardening crítico** — removido o fallback hardcoded `DevOnlyFallback` que permitia a qualquer pessoa com decompiler forjar licenças válidas. Cadeia de resolução agora: env var `EMT_LICENSE_SECRET` → arquivo `%LOCALAPPDATA%\FerramentaEMT\license.secret` → arquivo ao lado do assembly → **`InvalidOperationException`** (nunca mais hardcoded). `App.cs` e `EmtKeyGen` atualizados para o novo contrato. 4 testes em `KeySignerTests` ganham setup de env var com try/finally.

### Fixed — UI e Qualidade (Miniciclos 8, 10, 11)
- **Hotfixes de UI em 3 janelas** (M8): `ConexaoConfigWindow` (layout quebrado em DPI alto), `PlanoMontagemWindow` (scroll ausente), `MarcarPecasWindow` (botões cortados — padrão DPI corrigido: MaxHeight 900, ResizeMode CanResizeWithGrip, ScrollViewer defensivo, botões fora do scroll).
- **DPI overflow em 4 janelas** (M10): `CotarPecaFabricacaoWindow`, `GerarVistaPecaWindow`, `ExportarDstvWindow`, `PfBeamBarsWindow` — mesmo padrão M8 aplicado (MaxHeight 720/520→900, NoResize→CanResizeWithGrip, ScrollViewer com VerticalScrollBarVisibility Auto, botões em Grid.Row 2 fora do ScrollViewer). Resolve finding F1A-DPI-01 da auditoria.
- **10 empty catches eliminados em 6 services** (M11): `CortarElementosService` (5), `MarcarPecasService` (1), `AjustarEncontroService` (1), `AgrupamentoVisualService` (1), `ConexaoGeneratorService` (1), `TrelicaService` (1). Classificação A (9 casos): Logger.Debug com fallback seguro. Classificação B (1 caso AgrupamentoVisual): Logger.Warn. TrelicaService ganha catch tipado `Autodesk.Revit.Exceptions.OperationCanceledException`. Resolve findings F1C-CATCH-01 (HIGH) e F2-CATCH-01 (MEDIUM) da auditoria.
- **Ambiguidade CS0104** entre `Core.Result<T>` e `Revit.UI.Result` resolvida em commands afetados.

### Changed — Auditoria residual do ADR-003 (NumeracaoItensService)
- **`NumeracaoItensService.IniciarSessao` agora retorna `Result<InicioResultado>`** — removidas 4 das 5 chamadas residuais a `AppDialogService` (UIDocument nulo, config nula, sessão já ativa, nenhum elemento elegível). Novo `InicioResultado` expõe `SessaoIniciada`, `JaHaviaSessaoAtiva`, `TotalCandidatos` e `TotalElegiveis` — `CmdNumerarItens` consome esses flags e decide a UX (ShowError, ShowWarning por caso). O ShowInfo do fim de sessão (linha do lifecycle de `NumeracaoItensSessao.FinalizarSessao`) **foi mantido deliberadamente**: ele pertence ao ciclo de vida da janela persistente `NumeracaoItensControleWindow`, não ao kickoff — refatorá-lo exigiria redesenhar o modelo de sessão interativa, fora do escopo. Logger ganhou 4 entradas nos caminhos de no-op/falha pra dar rastro em suporte.

### Quality gates
- `dotnet build FerramentaEMT.Solution.sln -c Release` → **0 erros**, 2 avisos MSB3277 pré-existentes.
- `dotnet test` → **419/419 aprovados** (era 347 na v1.3.0).
- `TreatWarningsAsErrors` mantido em Release.
- Grep `catch.*{.*}` em `Services/` → **zero empty catches**.
- Grep `DevOnlyFallback` no código rastreado → **zero matches** (apenas docs históricos).
- Auditoria: findings F1A-DPI-01 (HIGH), F1C-CATCH-01 (HIGH), F2-CATCH-01 (MEDIUM) resolvidos.

### Notes
- Miniciclo 7 foi pulado (renumeração durante planejamento).
- `INSTALAR.bat` criado para deploy manual (não rastreado no git — cópia local para Victor).
- Planos detalhados de cada miniciclo em `comparacao-victor/PLANO-MINICICLO-{N}.md`.

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
