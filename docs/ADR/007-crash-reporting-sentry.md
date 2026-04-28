# ADR 007: Crash reporting remoto via Sentry com opt-in explicito

- Status: aceita
- Data: 2026-04-28
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.3),
  ADR-006 (auto-update — mesmo padrao de consent + secret),
  ADR-005 (CI compila csproj com stubs Nice3point)

## Contexto

Em v1.6.0 o `CrashReporter.Initialize` registra handlers de
`AppDomain.UnhandledException` e `TaskScheduler.UnobservedTaskException`
e dumpa cada crash em arquivo texto em
`%LocalAppData%\FerramentaEMT\crashes\`. Util pra debug ad-hoc — o
usuario manda o arquivo por email — mas:

- Crashes silenciam o usuario (ele desinstala em vez de reportar).
- Nao ha visibilidade agregada: 1 cliente ou 100 clientes podem estar
  sofrendo o mesmo crash; impossivel saber sem cada um anexar arquivo.
- Bugs em campo ficam ativos por meses — sem priorizacao apoiada em
  dados.

A AUDITORIA-MERCADO-2026-04-27.md item P0.3 listou crash reporting
remoto como critico para escalar acima de poucos clientes.

## Decisao

Integrar **Sentry SDK** (NuGet `Sentry`, pin exato em **5.6.0**) como
**complemento** do CrashReporter local — nao substituto. Local file
continua sendo a fonte primaria de verdade; Sentry eh um forward
opcional, com opt-in explicito reusando a `PrivacyConsentWindow` da
PR-2.

### Por que NAO AppCenter / Bugsnag / custom backend

| Alternativa | Razao para descarte |
|---|---|
| Microsoft AppCenter | Anunciada deprecacao (retired 2025). Nao investir em pipeline morrendo. |
| Bugsnag | Plano gratis limitado (8 dev users, 7-day retention). Sentry plano free tem 5k events/mes + 30 dias retention — melhor pra escalar nos primeiros 12 meses. |
| Backend custom (HttpPost para api.ferramentaemt.com) | Mantemos: dashboard de issues + agrupamento por stack trace + filtros por release/tag + integracao Slack. Sentry resolve isso out-of-the-box; reinvencao gasta semanas. |
| Sentry (esta ADR) | NuGet maduro (>10 anos), API estavel em 5.x, 5k events/mes free, dashboard pronto. |

### Por que NAO Sentry.AspNetCore / Sentry.Serilog / Sentry.Profiling

| Sub-pacote | Razao para descarte |
|---|---|
| `Sentry.AspNetCore` | Plugin Revit nao eh app web. Nada a integrar. |
| `Sentry.Serilog` | Acopla logging com Sentry — `Logger.Info/Warn/Error` mandariam para o Sentry tambem. Queremos Logger desacoplado: file local sempre, Sentry so para crashes via `CaptureCrash`. |
| `Sentry.Profiling` | Profiling ativa overhead de coleta; nao agrega valor para troubleshooting de crash WPF. Disabled via `ProfilesSampleRate = 0.0`. |

### Layout das classes

```
FerramentaEMT/Infrastructure/CrashReporting/
├── SentryDsnProvider.cs         (puro, 4-source resolution + Lazy snapshot)
├── PiiScrubber.cs                (puro, regex email + Windows path)
├── SentryOptionsBuilder.cs       (puro, monta SentryOptions + ScrubAndTag)
├── ISentryHubFacade.cs           (interface mockavel)
├── SentryHubFacade.cs            (impl real, delega pro SentrySdk)
├── SentryReporter.cs             (facade estatico idempotente)
└── SentryStartupWiring.cs        (cola App.OnStartup ↔ SentryReporter)

FerramentaEMT.Tests/Infrastructure/CrashReporting/
├── SentryDsnProviderTests.cs     (10 testes)
├── PiiScrubberTests.cs           (14 testes)
├── SentryOptionsBuilderTests.cs  (13 testes)
├── SentryReporterTests.cs        (16 testes — Moq de ISentryHubFacade)
└── SentryEndToEndTests.cs        (3 smoke tests do pipeline completo)
```

Total: **56 testes novos**. Suite vai de 585 → 641.

### Por que opt-in explicito (e nao silently-on)

Mesma logica do ADR-006 §"Por que opt-in explicito": LGPD Art. 6º
(transparencia) + custo de UX baixo (~5s do usuario) + arquitetura
compartilhada de PrivacyConsentWindow (PR-2 ja deixou estrutura
generica com 2 toggles dormentes).

Esta PR descomenta `cbCrashReports` no XAML e bumpa
`CurrentConsentVersion` 1 → 2. Usuarios da PR-2 reabrem o dialog uma
vez no primeiro Idling do Revit pos-upgrade — o restante eh
silently no-op (default seguro: `CrashReports = Denied`).

### Initialization order (App.OnStartup)

```
Logger.Initialize()                  // 1. sem ele, nada loga
WireUpdateLog()
ApplyResult applyResult = ...        // 2. PR-2 update apply
CrashReporter.Initialize()           // 3. handlers locais de unhandled
SentryReporter.Initialize()          // 4. PR-3 — REMOTO. Apos CrashReporter.
LicenseService.Initialize()          // 5. (Sentry ja registrado — captura
                                     //     crashes do proprio License)
StartUpdateCheckBackground()
application.Idling += OnFirstIdling  // 6. consent dialog se ConsentVersion < 2
```

`SentryReporter.Initialize` eh idempotente e silently no-op nos casos:
DSN ausente, consent denied/unset, hub factory nao wirado, falha
de `SentrySdk.Init`. NUNCA lanca — try/catch raiz.

### DSN management — espelho de LicenseSecretProvider

Exatamente o mesmo padrao do `LicenseSecretProvider`:

1. Variavel de ambiente `EMT_SENTRY_DSN` (CI / dev)
2. Arquivo `%LocalAppData%\FerramentaEMT\sentry.dsn` (deploy production)
3. Arquivo `sentry.dsn` ao lado do `FerramentaEMT.dll` (portable)
4. **Fallback DEV**: retorna `string.Empty` + source `DevFallbackEmpty`

UNICA diferenca consciente vs LicenseSecretProvider: ausencia de DSN
**nao lanca**. Licenca ausente eh erro fatal; DSN ausente eh modo
silencioso valido (dev local, instalacao portable sem cliente final
configurado, opt-out manual deletando o arquivo).

`.gitignore` exclui `sentry.dsn` e `*.sentry.dsn` (paridade com
`license.secret`).

### PII scrubbing

`PiiScrubber` (puro) remove 2 padroes em qualquer string:

1. **Email** (`[\w.+\-]+@[\w.\-]+\.\w+`, case-insensitive): vira `<EMAIL>`.
2. **Path Windows com username** (`[A-Z]:\\Users\\<u>\\`): vira `<USER>\`,
   preservando o resto do path. Linux/Mac/UNC explicitamente fora de
   escopo.

`SentryOptionsBuilder.ScrubAndTag` (publico, testavel direto) aplica:
- Scrub em `evt.Message.Message` + `evt.Message.Formatted`.
- Scrub em `evt.SentryExceptions[].Value` (defesa em profundidade).
- Adiciona as 5 tags obrigatorias (ver §Sampling rationale).

`ScrubAndTag` eh exatamente o delegate registrado em
`options.SetBeforeSend(...)` — ou seja, o que roda em runtime eh o
mesmo que os testes exercitam.

### Sampling rationale

| Categoria | Rate | Justificativa |
|---|---|---|
| `SampleRate` (errors) | 1.0 | Capturamos 100% dos crashes — eh o ponto da feature. |
| `TracesSampleRate` (perf) | 0.0 | Desligado — nao usamos transactions/spans. |
| `ProfilesSampleRate` | 0.0 | Desligado — overhead sem valor para crashes WPF. |
| `MaxBreadcrumbs` | 30 | Reducao do default 100 (3.3x menos volume). Nao precisamos de breadcrumb log para diagnosticar crashes; cota free Sentry agradece. |
| `AutoSessionTracking` | false | Plugin Revit nao tem conceito de sessao — eh um plugin que vive enquanto o Revit vive. |
| `SendDefaultPii` | false | Por seguranca — scrubber roda mesmo assim, mas defense in depth. |
| `AttachStacktrace` | true | Sentry junta o stack trace mesmo em chamadas que nao tem exception nativo. |

Constantes publicas em `SentryOptionsBuilder` — ajustaveis sem novo PR.

### Tags padrao (5 obrigatorias)

| Tag | Origem | Exemplo |
|---|---|---|
| `version` | `Assembly.GetName().Version` | `1.7.0.0` |
| `revit_version` | constante `"2025"` | `2025` |
| `os` | `RuntimeInformation.OSDescription` | `Microsoft Windows 10.0.19045` |
| `culture` | `CultureInfo.CurrentCulture.Name` | `pt-BR` |
| `license_state` | `LicenseService.GetCurrentState().Status.ToString()` (lazy via Func) | `Trial` (NUNCA email) |

`license_state` eh resolvido lazy a CADA evento — reflete o estado
corrente da licenca, nao o de boot. `LicenseService` lanca? Cai pro
fallback `"Unknown"`.

### Consent flow — interacao com PrivacyConsentWindow

Modal em `App.OnStartup` foi descartado (D1 do plano) — bloquearia
o boot do Revit. Usamos `UIControlledApplication.Idling` event:

```csharp
private void OnFirstIdling(object sender, IdlingEventArgs e)
{
    UIApplication uiApp = sender as UIApplication;
    if (uiApp != null) uiApp.Idling -= OnFirstIdling;  // PRIMEIRO — atomico
    try { EnsureConsentIfNeeded(); }
    catch (Exception ex) { Logger.Warn(ex, "[Privacy] consent dialog falhou"); }
}
```

Self-detach **antes** de qualquer logica para garantir idempotencia
sem flag externa. Se `EnsureConsentIfNeeded` lancar, o handler ja
esta desinscrito — Revit nao pendura.

`EnsureConsentIfNeeded` reabre `PrivacyConsentWindow` se `ConsentVersion
persistido < CurrentConsentVersion = 2`. Preserva campos do PR-2
(`LastUpdateCheckUtc`, `SkippedUpdateVersion`). Loga:
`"[Privacy] reabrindo consent via Idling event (consent version: {Persisted} -> {Current})"`.

**Trade-off:** se usuario consentir agora, Sentry **fica desabilitado
ate o proximo restart do Revit**. `SentryReporter.Initialize` eh
idempotente e re-init nao eh suportado pra evitar state inconsistente
do SDK estatico. Usuario pode forcar uma re-init fechando e reabrindo
o Revit; documentado em §Operational Runbook.

## Operational Runbook

### Como Alef configura DSN em producao

1. Criar projeto no Sentry (https://sentry.io) e copiar o DSN do
   formato `https://<key>@oXXX.ingest.sentry.io/<projectId>`.

2. **Maquina dev (Alef):** colocar como variavel de ambiente do sistema:
   ```
   setx EMT_SENTRY_DSN "https://<key>@oXXX.ingest.sentry.io/<projectId>"
   ```
   Reabrir terminais para herdar. Alternativa: gravar em
   `%LocalAppData%\FerramentaEMT\sentry.dsn` localmente.

3. **Maquinas dos clientes (futuro):** o instalador (`Setup.exe` da
   PR-5+ com code signing) gravara o DSN em
   `%LocalAppData%\FerramentaEMT\sentry.dsn` durante a instalacao.
   Alternativa manual: o cliente pode criar o arquivo se quiser
   opt-in explicito ou deletar para opt-out.

4. **Validacao:** abrir Revit, ver no log
   `%LocalAppData%\FerramentaEMT\logs\` a linha
   `[Sentry] initialized (DSN source: LocalAppDataFile, sample rates: errors=100% breadcrumbs<=30)`.

### Como Alef ve os crashes

Sentry dashboard: https://sentry.io/organizations/<org>/issues/

Filtros uteis:
- `release:1.7.0` — crashes da release atual.
- `tag:revit_version:2025` — so Revit 2025.
- `tag:license_state:Trial` — usuarios em trial (vs Valid).
- `tag:culture:pt-BR` — usuarios brasileiros (vs en-US).
- `tag:kind:unobserved-task` — vs `unhandled` (origem do crash).

Cada issue traz: stack trace, OS, versao do plugin, numero de
ocorrencias, primeiro/ultimo evento.

### Threshold de upgrade

Plano free Sentry: **5.000 events/mes**. Acima disso eventos sao
droppados pelo proprio Sentry (mensagem no dashboard).

Sinal de alerta: ao atingir **80% (4.000 events)**, avaliar:
- Ha loop de crash em algum comando? (sample um issue, fix prioritario)
- Volume esta saudavel (>100 usuarios ativos)? Upgrade para Team plan
  (~U$ 26/mes) — cobre 50k events.

### Como desligar Sentry remotamente sem nova release

Caso de incidente (ex.: Sentry vaza PII por bug do scrubber em
producao):

1. Deletar o arquivo `%LocalAppData%\FerramentaEMT\sentry.dsn`
   (ou desativar o env var `EMT_SENTRY_DSN`).
2. Reiniciar Revit.
3. Plugin volta a no-op silencioso (DSN ausente → DevFallbackEmpty
   → `[Sentry] disabled (no DSN configured)`).
4. Crash dump local continua funcionando como sempre.

NAO ha kill switch remoto via API — limitacao consciente. Nao queremos
dependencia online pra desligar telemetria de seguranca (efeito
contrario ao desejado: se Sentry estivesse comprometido, o kill switch
poderia ser bloqueado pelo proprio incidente).

## Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| `SentrySdk.Init` lanca em DSN malformado | `SentryReporter.Initialize` tem try/catch raiz; `IsEnabled = false`; loga warn; plugin segue. Coberto por `Initialize_does_not_throw_when_hub_init_fails`. |
| Dialog em OnStartup bloquearia Revit | Resolvido por `Idling` event com self-detach atomico (D1). |
| DSN commitado por engano | (a) `.gitignore` exclui `sentry.dsn` + `*.sentry.dsn`. (b) Codigo NUNCA tem DSN literal — so resolve via `SentryDsnProvider`. (c) `SentryDsnProviderTests.HasMalformedDsnFile` detecta arquivos vazios em deploy. |
| Free tier 5k events/mes esgotado | `MaxBreadcrumbs = 30` reduz volume. Documentado threshold de 80% acima. Sentry SDK rate-limita internamente. |
| `BeforeSend` deixar passar PII | 14 testes do `PiiScrubberTests` cobrem email, paths, multilinha, null, combinacoes. `SentryOptionsBuilderTests` cobre integracao scrub+tag. `SentryEndToEndTests` cobre pipeline completo. |
| Init lento atrasa boot | Sentry SDK eh sync no Init mas async no envio. `Init` apenas configura — primeiro send eh que faz TLS. Caso medicao revele >100ms, mover Init para `Task.Run` (fire-and-forget) como UpdateCheck (PR-2 §StartUpdateCheckBackground). |
| Conflito transitive dep com Serilog 4.2 / System.Drawing | Validado no commit 1: build local + CI=true 0 erros. Versao 5.6.0 confirmada compativel. Fallback documentado na §"Versao do SDK pinada" abaixo. |
| Re-init nao suportado apos consent grant tardio | Trade-off documentado: usuario consentindo no Idling dialog precisa reiniciar Revit pra ativar Sentry nesta sessao. Alternativa (Reinitialize idempotente) considerada e descartada — risco de state inconsistente do SDK estatico. |
| Antivirus bloqueia HTTPS pra ingest.sentry.io | Try/catch em `Init` e `CaptureException`. Falha registrada via warn; plugin segue. CrashReporter local continua funcionando. |
| LicenseService nao inicializado quando primeira captura ocorre | `LicenseStateResolver` eh chamado lazy via Func<string>, com try/catch interno. Fallback `"Unknown"`. Coberto por design — `SentryReporter.Initialize` roda ANTES de `LicenseService.Initialize`, intencionalmente, pra capturar crashes do proprio License. |
| User consent salvo simultaneo entre Idling event e LicenseActivationWindow botao "Verificar atualizacoes" | `PrivacySettingsStore` abre/grava arquivo a cada chamada (atomico). Worst case: ultima escrita ganha. UX aceitavel — ambos os caminhos pedem o mesmo tipo de consent. |

## Versao do SDK pinada

`Sentry 5.6.0` (commit 1, validado no `dotnet build` local + CI=true,
0 erros). Pin **exato** — sem range — para reproducibilidade:
deploys identicos rodam o mesmo bytecode do Sentry SDK.

**Regra de fallback** (caso 5.6.0 falhe na infra de algum cliente):
- Tentar ultimo minor anterior (5.5.x → 5.4.x → ...) ate algum bater.
- Se nenhum 5.x bater, **pausar e reportar** — abre issue documentando
  antes de seguir.
- **NAO downgrade pra 4.x** sem analise previa: Sentry 4.x → 5.x teve
  breaking changes em `BeforeSend`, scrubbing API e telemetria; voltar
  exige replanejar.

Atualizacao para versao maior em PR futuro precisa: re-validar build
+ CI + manual smoke test de captura, e atualizar este ADR-007 com a
nova versao.

## Backward compatibility

PR-3 NAO quebra v1.6.0 ou versoes anteriores: instalacoes que nao tem
DSN configurado caem no `DevFallbackEmpty` e SentryReporter vira no-op
silencioso. CrashReporter local funciona identico ao v1.6.0.

Usuarios da PR-2 (auto-update + ConsentVersion=1) veem o dialog de
consent reabrir uma vez no primeiro Idling pos-upgrade pra v1.7.0. Eh
uma interrupcao de ~5s — aceitavel pra entrar uma feature nova de
privacidade.

## Validacao

- 56 testes novos (10 + 14 + 13 + 16 + 3) cobrindo subsistema completo:
  - `SentryDsnProvider`: 10 (env var, cache, atomicidade, malformed file, never-throw guarantee, etc).
  - `PiiScrubber`: 14 (email simples/subdominio/+tag/dash, path Windows/Linux/UNC, null-safe, combinado).
  - `SentryOptionsBuilder`: 13 (sample rates, tags, scrub integrado,
    license state fallback).
  - `SentryReporter`: 16 (idempotencia, no-op paths, capture/flush
    via mock IHub, swallow exceptions).
  - `SentryEndToEndTests`: 3 (pipeline completo: wiring → init →
    simulated crash → mock hub assertions).
- Suite total: **585 → 641** (excede a meta de ~620 do plano original).
- Build local Release + CI=true: 0 erros, 0 avisos.
- 3 runs consecutivos verdes (verifica nao-flakiness).

## Consequencias

### Positivas
- Crashes em producao deixam de ser invisiveis. Alef pode priorizar
  fixes baseado em dados de campo (1 cliente vs 100).
- Tag `license_state` permite distinguir crashes de Trial vs Valid —
  bugs de fluxo de ativacao saem do escuro.
- Estrutura compartilhada com PR-4 (PostHog telemetry) — descomenta
  `cbTelemetry` e bumpa `ConsentVersion` pra 3 sem refazer XAML
  nem layout de wiring.

### Neutras
- `App.OnStartup` ganha ~2 etapas (~10ms total: SentryReporter
  Init + Idling subscribe).
- Test csproj cresce em 56 testes (~6% mais runtime — 230ms vs 215ms).

### Negativas
- Dependencia de servico online (sentry.io). Se Sentry cair, captura
  remota falha — mas plugin continua funcionando (no-op silencioso),
  CrashReporter local ainda funciona.
- Plano free tem teto de 5k events/mes. Acima disso eventos sao
  droppados; aceitavel ate ~50-100 usuarios.

## Rollback

Se um problema critico aparecer pos-merge (ex.: regressao no scrubber
deixa email passar):

1. Reverter o commit do `App.cs` (apenas o trecho
   `SentryStartupWiring.InitializeServices(...)` + `OnFirstIdling`
   subscribe + `Flush` em OnShutdown — ~30 linhas).
2. Subsistema fica dormente (classes ficam mas nao sao chamadas).
3. PR de fix posterior corrige + religa.

Alternativa rapida sem revert: instruir usuarios afetados a deletar
`%LocalAppData%\FerramentaEMT\sentry.dsn` (ou unsetar `EMT_SENTRY_DSN`)
e reiniciar Revit. Plugin volta ao no-op silencioso.

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.3 (origem desta decisao)
- ADR-006 (auto-update — mesmo padrao de consent + secret resolution)
- ADR-005 (CI compila csproj — pre-requisito para que regressoes do
  subsistema de Sentry sejam detectadas no CI)
- ADR-004 (threading model — esclarece por que captura de
  `UnobservedTaskException` em background thread eh seguro)
- [Sentry .NET SDK changelog](https://github.com/getsentry/sentry-dotnet/blob/main/CHANGELOG.md)
- [Sentry data scrubbing best practices](https://docs.sentry.io/platforms/dotnet/data-management/sensitive-data/)
