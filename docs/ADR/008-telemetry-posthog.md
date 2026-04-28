# ADR 008: Telemetria de uso via PostHog (HTTP-direct) com opt-in explicito

- Status: aceita
- Data: 2026-04-28
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.6),
  ADR-007 (crash reporting via Sentry — mesmo padrao de consent +
  secret resolution), ADR-006 (auto-update — mesmo padrao de
  PrivacyConsentWindow generica)

## Contexto

A AUDITORIA-MERCADO-2026-04-27.md item P0.6 listou telemetria de uso
como critico para tomar decisoes de produto baseadas em dados:

- Quantos usuarios ativos por mes/semana
- Quais comandos sao mais usados
- Quais comandos falham com mais frequencia
- Tempo medio de execucao por comando

Sem isso, decisoes de produto viram chute. Refator de feature pouco
usada eh esforco perdido; gastar 2 semanas otimizando algo que 3% dos
usuarios tocam eh anti-pattern de v1.7.0.

## Decisao

Integrar PostHog como subsistema de telemetria com **3 escolhas
arquiteturais materiais**:

1. **HTTP-direct via HttpClient** em vez do NuGet oficial PostHog 2.5.0
   (que esta em pre-release com warning explicito de breaking changes).
2. **5 eventos** apenas (escopo PR-4): `command.executed`,
   `command.failed`, `license.state_checked`, `update.detected`,
   `update.applied`. Mais que isso eh escopo creep — defere pra v1.7.x.
3. **Endpoint default `eu.posthog.com`** (LGPD-friendly: dados na UE,
   jurisdicao mais alinhada com clientes brasileiros que us.posthog.com).

## Decisao: HTTP-direct vs SDK oficial

Alternativas consideradas:

1. **PostHog (NuGet oficial)** — descartado.
   Versao atual 2.5.0 (abril 2026) eh pre-release com warning explicito
   do mantenedor: "many breaking changes until we reach a stable release"
   + "we are far short of [non-AspNetCore support]". Pinar SDK pre-release
   a release comercial v1.7.0 expoe a re-emissoes obrigatorias quando
   o SDK quebrar API.

2. **PostHog.NET (community fork by Gamefound)** — descartado.
   Fork community estavel (1.x → 2.x lancado), mas 33k downloads vs 620k
   do oficial. Para produto comercial, dependencia de fork nao-oficial
   eh risco de manutencao (Gamefound pode abandonar a qualquer momento).

3. **HTTP-direct via HttpClient** — adotado.

Razoes para HTTP-direct:

- API `/capture` do PostHog eh REST estavel ha anos (versionamento
  implicito via additive changes). Eh a mesma API consumida por TODAS
  as SDKs PostHog.
- Ja temos infraestrutura HttpClient no projeto (UpdateCheckService
  PR-2) — simetria arquitetural.
- Sem dependencia transitiva instavel; futuro-proof contra SDK breaking.
- Auditavel: ~120 linhas em PostHogHttpTelemetryClient vs caixa preta
  de SDK.
- Migracao para self-hosted PostHog eh trivial (mudanca de URL via
  EMT_POSTHOG_HOST env var).

Trade-offs aceitos:

- Sem retry automatico em falha (telemetry-loss eh tolerado;
  CrashReporter via Sentry cobre o critico).
- Sem offline queue (eventos durante offline sao perdidos).
- Sem feature flags / A/B testing (escopo PR-4 — 5 eventos, baixo
  volume — nao exige).
- Manutencao de ~120 linhas de cliente HTTP.

Re-avaliacao: se PostHog SDK 3.x estabilizar antes de v2.0.0 do plugin,
considerar migracao do HTTP-direct para SDK oficial.

## Layout das classes

```
FerramentaEMT/Infrastructure/Telemetry/
├── PostHogApiKeyProvider.cs         (puro, 4-source resolution + Lazy snapshot)
├── PostHogHostProvider.cs           (puro, env override + default eu.posthog.com)
├── SessionIdProvider.cs             (UUID v4 anonimo + JSON persist)
├── TelemetryEvent.cs                (DTO {Name, Properties})
├── ITelemetryClient.cs              (interface mockavel)
├── PostHogHttpTelemetryClient.cs    (impl HTTP-direct, fire-and-forget)
├── SamplingDecider.cs               (puro, sample rate por evento)
├── TelemetryOptionsBuilder.cs       (puro, super properties + scrubbing)
├── TelemetryReporter.cs             (facade estatico idempotente)
└── TelemetryStartupWiring.cs        (cola App.OnStartup ↔ TelemetryReporter)

FerramentaEMT/Infrastructure/
└── PiiScrubber.cs                   (movido em PR-4 de CrashReporting/)
```

Total: 54 testes novos. Suite de 641 → 716.

## Initialization order (App.OnStartup)

```
Logger.Initialize()                       // 1
WireUpdateLog()
ApplyPendingUpdate()                      // 2 PR-2 update apply
CrashReporter.Initialize()                // 3 PR-3 local crashes
SentryReporter.Initialize()               // 4 PR-3 remote crashes
LicenseService.Initialize()               // 5
TelemetryReporter.Initialize()            // 6 PR-4 — DEPOIS de License (precisa do status)
TrackUpdateAppliedIfAny()                 // 7 PR-4 — emite update.applied retroativo
StartUpdateCheckBackground()              // 8
application.Idling += OnFirstIdling       // 9 consent dialog se ConsentVersion < 3
```

`TelemetryReporter.Initialize` eh idempotente e silently no-op em:
api key ausente, consent denied/unset, client factory nao wirado,
factory lanca, factory retorna null. NUNCA propaga.

## API key management — espelho de SentryDsnProvider

Mesmo padrao do `SentryDsnProvider` (PR-3) e `LicenseSecretProvider`:

1. Variavel de ambiente `EMT_POSTHOG_API_KEY` (CI / dev local).
2. Arquivo `%LocalAppData%\FerramentaEMT\posthog.apikey` (deploy).
3. Arquivo `posthog.apikey` ao lado do `FerramentaEMT.dll` (portable).
4. **Fallback DEV**: retorna `string.Empty` + source `DevFallbackEmpty`.

Diferenca vs LicenseSecretProvider (igual ao SentryDsnProvider):
ausencia de api key **nao lanca**. Modo silencioso valido.

`.gitignore` exclui `posthog.apikey` + `*.posthog.apikey` +
`session-id.json`.

## Session ID anonimo

`SessionIdProvider.GetOrCreate()` retorna UUID v4 (gerado via
`Guid.NewGuid()` — RNG do CLR). Persistido em
`%LocalAppData%\FerramentaEMT\session-id.json`:

```json
{
  "session_id": "550e8400-e29b-41d4-a716-446655440000",
  "created_at_utc": "2026-04-28T13:00:00Z"
}
```

**Garantias auditadas via teste**:

1. NUNCA derivado de `Environment.MachineName`, MAC address, BIOS,
   `Environment.UserName`, fingerprint, ou qualquer fonte que
   identifique usuario ou maquina.
2. Mesmo `session_id` por todo lifetime da instalacao naquela maquina.
3. Reset manual = deletar o arquivo. Usuario nao tem motivo realista
   pra fazer isso.
4. NAO usado como PostHog distinct_id de identidade — eh apenas um
   handle anonimo de instalacao.

Privacy Policy (PR-6) precisa documentar isso explicitamente. Esta ADR
ja carrega a versao curta. **Alef sabe quantos usuarios ativos tem;
Alef nao sabe QUEM**. Eh anonimo por design.

## Eventos — escopo minimo (5)

| Evento | Properties | Sample rate | Disparo |
|---|---|---|---|
| `command.executed` | `command_name`, `duration_ms`, `success` | 10% se success, 100% se falha | Final de `IExternalCommand.Execute` em `FerramentaCommandBase` (sucesso, falha, ou OperationCancelled) |
| `command.failed` | `command_name`, `exception_type`, `duration_ms` | 100% | Catch raiz em `FerramentaCommandBase` (Revit InvalidOperationException + Exception generica) |
| `license.state_checked` | `status` | 100% | `TelemetryReporter.Initialize` apos sucesso (briefing §4.9 Opcao B — sem queue interno) |
| `update.detected` | `current_version`, `available_version` | 100% | `App.StartUpdateCheckBackground` callback quando `Outcome == UpdateAvailable` |
| `update.applied` | `from_version`, `to_version`, `attempts` | 100% | `App.OnStartup` apos `UpdateApplier.ApplyPendingIfAny` retornar `Applied` |

**NUNCA enviado em properties:** email, machine ID, MAC, paths absolutos,
`ElementId.Value`, file names, username Windows ou Revit.

## Sampling rationale

| Categoria | Rate | Justificativa |
|---|---|---|
| `command.executed` (success=true) | **10%** | Volume alto: cada clique no plugin. 10% da amostra estatistica + economiza cota free Sentry/PostHog. |
| `command.executed` (success=false) | **100%** | Falhas raras + valiosas. |
| `command.failed` | **100%** | Crashes sao critical para priorizacao. |
| `license.state_checked` | **100%** | Volume baixo (1 por boot). |
| `update.detected` / `update.applied` | **100%** | Volume baixissimo (dias entre eventos). |

Constantes publicas em `SamplingDecider` — ajustaveis sem novo PR.
Implementacao usa `Random.Shared.NextDouble()` (RNG global do CLR);
testes injetam `Random` com seed fixa para determinismo.

## Super properties (6 obrigatorias)

Anexadas em CADA evento, via `TelemetryOptionsBuilder.ScrubAndTag`:

| Tag | Origem | Exemplo |
|---|---|---|
| `version` | `Assembly.GetName().Version` | `1.7.0.0` |
| `revit_version` | constante `"2025"` | `2025` |
| `os` | `RuntimeInformation.OSDescription` | `Microsoft Windows 10.0.19045` |
| `culture` | `CultureInfo.CurrentCulture.Name` | `pt-BR` |
| `license_state` | `LicenseService.GetCurrentState().Status.ToString()` lazy | `Trial` |
| `session_id` | `SessionIdProvider.GetOrCreate()` | UUID v4 |

`license_state` eh resolvido lazy a cada Track — reflete o estado
corrente da licenca. `LicenseService` lanca? Cai pro fallback `"Unknown"`.

## Reuso do PiiScrubber

`PiiScrubber` (movido em PR-4 commit 0 de
`Infrastructure/CrashReporting/PiiScrubber.cs` para
`Infrastructure/PiiScrubber.cs`) eh aplicado a properties string ANTES
da serializacao JSON, via
`TelemetryOptionsBuilder.ScrubProperties` →
`TelemetryOptionsBuilder.ScrubAndTag`. Cobre os mesmos 2 padroes:

- Email: `[\w.+\-]+@[\w.\-]+\.\w+` → `<EMAIL>`.
- Path Windows com username: `(?i)[A-Z]:\\Users\\[^\\]+\\` → `<USER>\`.

Tipos nao-string (`int`, `bool`, `double`, etc) sao preservados intactos.

## Endpoint privacy — eu.posthog.com

Default hardcoded em `PostHogHostProvider`:

```csharp
public const string DefaultHost = "https://eu.posthog.com";
public const string EnvVarName = "EMT_POSTHOG_HOST";
```

Override via env var (CI/dev override; clientes self-hosted PostHog).
LGPD-friendly: dados na Uniao Europeia, jurisdicao mais alinhada com
clientes brasileiros que us.posthog.com.

## Consent flow

PR-3 ja deixou a `PrivacyConsentWindow` estruturada com 3 toggles
(auto-update + crash reports + telemetry). Esta PR descomenta
`cbTelemetry` e bumpa `CurrentConsentVersion` 2 → 3.

Usuarios da PR-3 (`ConsentVersion=2`) reabrem o dialog uma vez no
primeiro Idling do Revit pos-upgrade pra v1.7.0, escolher se aceitam
telemetria. PR-3 ja registra o `Idling` event handler — esta PR nao
duplica nem altera.

**Trade-off** (mesmo da PR-3): usuario que consentir telemetria pelo
Idling dialog **fica desabilitado ate o proximo restart do Revit**.
TelemetryReporter.Initialize eh idempotente e re-init nao eh suportado.
Usuario forca re-init reiniciando Revit; documentado em §Operational
Runbook.

## Operational Runbook

### Como Alef configura API key em producao

1. Criar projeto no PostHog (https://eu.posthog.com) e copiar a
   "Project API Key" (formato `phc_...`).

2. **Maquina dev (Alef):** definir como variavel de ambiente do sistema:
   ```
   setx EMT_POSTHOG_API_KEY "phc_xxxxx"
   ```
   Reabrir terminais para herdar. Alternativa: gravar em
   `%LocalAppData%\FerramentaEMT\posthog.apikey`.

3. **Maquinas dos clientes (futuro):** o instalador (`Setup.exe` da
   PR-5+) gravara em `%LocalAppData%\FerramentaEMT\posthog.apikey`
   durante a instalacao. Cliente pode deletar o arquivo para opt-out
   manual.

4. **Validacao:** abrir Revit, ver no log
   `%LocalAppData%\FerramentaEMT\logs\` a linha
   `[Telemetry] initialized (api key source: LocalAppDataFile, host: https://eu.posthog.com, session: 550e8400)`.

5. **Override de endpoint:** se quiser self-hosted PostHog ou regiao
   us.posthog.com, definir `EMT_POSTHOG_HOST=https://us.posthog.com`
   (ou URL custom).

### Como Alef ve os dados

PostHog dashboard: https://eu.posthog.com/project/<projectId>/events

Filtros uteis:
- `$properties.version = '1.7.0.0'` — eventos da release atual.
- `$properties.revit_version = '2025'` — so Revit 2025.
- `$properties.license_state = 'Trial'` — usuarios em trial vs Valid.
- `event = 'command.executed' AND $properties.success = false` — falhas
  silenciosas (command retornou Failed sem excecao).
- `event = 'command.failed'` — exceptions capturadas.
- `$properties.command_name = 'CmdGerarTrelica'` — uso especifico de
  comando.

Insights uteis:
- "Top commands": SELECT command_name, COUNT(*) GROUP BY command_name
- "Avg duration": AVG(duration_ms) WHERE event = 'command.executed'
- "Error rate per command": COUNT(command.failed) / COUNT(command.executed)
- DAU/WAU/MAU: distinct(session_id) por janela de tempo.

### Threshold de upgrade

PostHog plano free: **1M events/mes**. Acima disso eventos sao droppados
pelo proprio PostHog.

Sinal de alerta: ao atingir **80% (800k events)**, avaliar:
- Volume saudavel (>500 usuarios ativos)? Upgrade para plano pago
  (~U$ 0.00031/event acima de 1M — barato).
- Sample rate de `command.executed:success` ainda em 10%? Reduzir
  para 5% se necessario.
- Algum command_name spam? (loop de UI? Investigar.)

### Como desligar telemetria remotamente sem nova release

Caso de incidente (ex.: scrubber vaza PII em producao, ou PostHog
vaza dados):

1. Deletar o arquivo `%LocalAppData%\FerramentaEMT\posthog.apikey`
   (ou unsetar `EMT_POSTHOG_API_KEY` env var).
2. Reiniciar Revit.
3. Plugin volta a no-op silencioso (api key ausente → DevFallbackEmpty
   → `[Telemetry] disabled (no API key configured)`).
4. SentryReporter (PR-3) e CrashReporter local continuam funcionando.

NAO ha kill switch remoto via API — limitacao consciente. Mesmo
raciocinio do ADR-007: nao queremos dependencia online pra desligar
telemetria de seguranca/privacidade (efeito contrario ao desejado:
se PostHog estivesse comprometido, kill switch poderia ser bloqueado
pelo proprio incidente).

## Versao do SDK pinada

NAO ha SDK pinado — usamos HTTP-direct (decisao §"Decisao: HTTP-direct
vs SDK oficial"). Endpoint `/capture/` do PostHog eh REST estavel ha
anos.

Caso futuramente migremos para SDK oficial (PostHog 3.x estavel?), as
abstracoes (ITelemetryClient, TelemetryReporter, TelemetryEvent,
TelemetryOptionsBuilder, SamplingDecider) sao agnosticas — apenas
PostHogHttpTelemetryClient muda.

## Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| HTTP POST falha (network down, DNS) | Try/catch raiz em `PostHogHttpTelemetryClient.Track` (fire-and-forget Task.Run). Excecao logada via `logWarnException` delegate; nunca propaga. |
| API key commitada por engano | (a) `.gitignore` exclui `posthog.apikey` + `*.posthog.apikey`. (b) Code NUNCA tem api key literal — so via Provider. (c) ADR lista como red line. (d) `git diff --cached` antes de cada commit. |
| Free tier 1M events/mes esgotado | Sample rate 10% em `command.executed:success` reduz volume principal em 10x. Threshold de 80% documentado em Operational Runbook. PostHog SDK rate-limita internamente. |
| `PiiScrubber` deixar passar PII | 14 testes do `PiiScrubberTests` cobrem email/path/multilinhas/null. 10 testes em `TelemetryOptionsBuilderTests` cobrem integracao scrub+tag. 16 testes em `PostHogHttpTelemetryClientTests` cobrem scrub antes do POST. 3 smoke e2e cobrem pipeline completo. |
| Session ID derivado acidentalmente de hardware | `SessionIdProviderTests` tem teste explicito que UUID nunca contem substring de `MachineName` ou `UserName`. RNG via `Guid.NewGuid()` (CLR-managed). |
| Init lento atrasa boot | `TelemetryReporter.Initialize` so configura — sem rede no Init (HTTP eh fire-and-forget no primeiro Track). Lazy HttpClient. |
| LicenseService nao inicializado quando primeiro Track ocorre | Init order garante License.Init ANTES de Telemetry.Init. `LicenseStateResolver` eh Func lazy com try/catch interno; fallback `"Unknown"`. |
| Re-init nao suportado apos consent grant tardio | Mesmo trade-off da PR-3. Documentado em §Consent flow. Usuario precisa restart Revit. |
| Anti-virus bloqueia HTTPS pra eu.posthog.com | Try/catch em Track (fire-and-forget). Falha registrada via warn; plugin segue. CrashReporter local + Sentry continuam funcionando. |
| Multiplos commands em paralelo emitem telemetria simultanea | `Task.Run` cria threadpool tasks independentes. PostHog endpoint aceita concurrent requests. CapturingHandler nos testes valida concorrencia (ConcurrentBag pattern). |
| `Microsoft.Extensions.Logging` nao mais necessario | NuGet PostHog removido em commit 5a — nenhuma transitive M.E.L. introduzida. Serilog 4.2 unico logger. |

## Validacao

- 54 testes novos cobrindo subsistema completo:
  - `PostHogApiKeyProvider`: 10 (env var, cache, atomicidade, malformed file, never-throw).
  - `PostHogHostProvider`: 4 (default eu.posthog.com, override env var, whitespace, cache).
  - `SessionIdProvider`: 8 (UUID v4 format, persistencia, reuso, INVARIANTE de privacy: nao deriva de hardware, concorrencia).
  - `SamplingDecider`: 8 (rates por evento, statistical 10% com seed deterministic).
  - `TelemetryOptionsBuilder`: 10 (super props, scrub, fallbacks, combined).
  - `PostHogHttpTelemetryClient`: 16 (URL endpoint, body fields, content-type, super props merged, PII scrub, IsEnabled gate, sampling, swallow exceptions, FlushAsync no-op).
  - `TelemetryReporter`: 16 (idempotencia, no-op paths, Track via mock, swallow).
  - `TelemetryEndToEndTests`: 3 (smoke pipeline completo via CapturingHandler).

- Suite total: **641 → 716** (excede o plano de ~700).
- Build local Release + CI=true: 0 erros, 0 avisos.
- 3 runs consecutivos verdes (verifica nao-flakiness).
- `dotnet list package`: PostHog removido, apenas Sentry 5.6.0.

## Consequencias

### Positivas

- Decisoes de produto baseadas em dados reais (DAU, top commands,
  error rate, perf). Sai do "chute".
- Tag `license_state` permite distinguir uso Trial vs Valid — entender
  fluxo de conversao.
- HTTP-direct futuro-proof: nenhum SDK breaking change vai forcar
  re-emissao do plugin.
- Estrutura compartilhada com PR-3: mesma `PrivacyConsentWindow`,
  mesmo padrao de delegate-wiring, mesmo padrao de Reporter idempotente.

### Neutras

- `App.OnStartup` ganha ~1 etapa (~5ms total — Telemetry Init eh
  configuracao local + 1 evento sintetico license.state_checked).
- Test csproj cresce em 54 testes (~7% mais runtime).

### Negativas

- Dependencia de servico online (eu.posthog.com). Se PostHog cair,
  telemetria falha — mas plugin continua funcionando (no-op silencioso).
- Plano free tem teto de 1M events/mes. Acima disso eventos sao
  droppados; aceitavel ate ~500-1000 usuarios.
- ~120 linhas de cliente HTTP a manter (vs 0 com SDK). Trade-off
  consciente — preferimos manter contra ter SDK breaking.

## Rollback

Se um problema critico aparecer pos-merge:

1. Reverter os commits de `App.cs` e `FerramentaCommandBase.cs`
   (apenas os trechos `TelemetryStartupWiring.InitializeServices(...)`,
   `TrackUpdateApplied`, `TrackUpdateDetected`, `Flush em OnShutdown`,
   e os helpers `TrackCommand*` do FerramentaCommandBase — ~50 linhas
   no total).
2. Subsistema fica dormente (classes ficam mas nao sao chamadas).
3. PR de fix posterior corrige + religa.

Alternativa rapida sem revert: instruir usuarios afetados a deletar
`%LocalAppData%\FerramentaEMT\posthog.apikey` (ou unsetar
`EMT_POSTHOG_API_KEY`) e reiniciar Revit. Plugin volta ao no-op
silencioso.

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.6 (origem desta decisao).
- ADR-007 (crash reporting Sentry — mesmo padrao de consent + secret
  + delegate-wiring).
- ADR-006 (auto-update — mesmo padrao de PrivacyConsentWindow generica).
- ADR-005 (CI compila csproj — pre-requisito para que regressoes do
  subsistema de telemetria sejam detectadas no CI).
- [PostHog /capture API docs](https://posthog.com/docs/api/post-only-endpoints).
- [LGPD Art. 6º (transparencia)](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm)
  — base legal para opt-in explicito.
