# ADR 006: Auto-update via GitHub Releases API com graceful degradation

- Status: aceita
- Data: 2026-04-27
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.2),
  ADR-005 (CI compila csproj com stubs Nice3point)

## Contexto

Em v1.6.0 cada usuario fica preso na versao que instalou. Quando ha bug
ou correcao de seguranca (como o `Base64Url.Decode` da v1.6.0 → corrigido
em commit `3145e2d`), todos os clientes precisam baixar e reinstalar
manualmente. Em pratica:

- Bugs em campo ficam ativos por meses
- Usuarios esquecem de atualizar
- Nao existe forma de forcar correcao de seguranca
- Versoes diferentes geram bugs incompativeis quando equipes trabalham juntas

A AUDITORIA-MERCADO-2026-04-27.md item P0.2 listou auto-update como
critico para escalar acima de poucos clientes.

## Decisao

Implementar auto-update **minimalista** baseado em GitHub Releases API,
sem framework externo (Velopack/Squirrel/ClickOnce), em 3 fases dentro
do plugin:

1. **Verificacao** (`UpdateCheckService`): consulta `api.github.com/repos/.../releases/latest`
   em background no boot. Cache 24h. Resultado em
   `App.LastUpdateCheckResult` para a UI consumir.
2. **Download** (`UpdateDownloader`): quando user confirma, baixa o asset
   `.zip` + `checksums.txt`, valida 6 invariantes, grava em
   `%LocalAppData%\FerramentaEMT\Updates\pending\{version}.zip` + marker.
3. **Aplicacao** (`UpdateApplier`): no proximo `App.OnStartup`, antes de
   carregar qualquer Service/Command, faz backup + extracao + swap.
   Graceful degradation se DLL ja carregou (3 tentativas).

### Por que NAO Velopack/Squirrel/ClickOnce

| Framework | Razao para descarte |
|---|---|
| Velopack | Adiciona ~2 MB de deps + acoplamento com modelo proprio de releases. Nossa necessidade eh simples (consulta + dl + extracao); BCL net8.0 cobre tudo. |
| Squirrel.Windows | Descontinuado. Forks (Velopack) reenderecam para o anterior. |
| ClickOnce | Built-in mas exige certificado de signing pra atualizar sem prompt — nao temos cert ate PR-5. Tambem nao integra bem com plugin Revit (requer `.application` manifest). |
| Custom (esta ADR) | HttpClient + JsonDocument + ZipArchive + SHA256 + File.* — tudo BCL. Zero deps novas. |

### Layout das classes

```
FerramentaEMT/Infrastructure/Update/
├── SemVerComparer.cs           (puro, parse + compare)
├── GitHubRelease.cs            (DTO)
├── GitHubAsset.cs              (DTO)
├── IGitHubReleaseProvider.cs   (interface, mockavel)
├── GitHubReleaseProvider.cs    (impl HttpClient — nao testavel)
├── UpdateState.cs              (enum + UpdateCheckResult DTO)
├── UpdateLog.cs                (facade puro pra evitar Serilog no test csproj)
├── UpdateCheckService.cs       (orquestrador, testavel via Moq)
├── UpdateDownloader.cs         (download + 6 validacoes — em main)
├── ZipSlipValidator.cs         (puro, pre-extraction safety)
├── Sha256Calculator.cs         (puro, hex + checksums.txt parser)
├── UpdateMarker.cs             (DTO + JSON pendente)
├── ApplyResult.cs              (enum)
└── UpdateApplier.cs            (uses UpdateLog, testavel via temp dir)

FerramentaEMT/Infrastructure/Privacy/
├── IPrivacySettingsStore.cs    (interface, mockavel)
├── PrivacySettingsStore.cs     (impl, persiste em privacy.json — em main)
└── PrivacyJson.cs              (puro, serializacao testavel)

FerramentaEMT/Models/Privacy/
├── ConsentState.cs             (enum: Unset/Granted/Denied)
└── PrivacySettings.cs          (DTO compartilhado com PR-3 e PR-4)

FerramentaEMT/Views/
└── PrivacyConsentWindow.xaml(.cs)  (1 toggle ativo, 2 dormentes p/ PR-3/4)
```

### Por que opt-in explicito (e nao silently-on)

Hoje o plugin **nao faz nenhuma chamada HTTP outbound**. License eh validada
via HMAC local; crashes vao para arquivo local. Adicionar a primeira chamada
remota eh mudanca de postura de privacidade nao-trivial:

- LGPD Art. 6º (transparencia): "tratamento" inclui transmissao a terceiros.
  Mesmo sendo servico operacional, com produto comercial (objetivo da v1.7.0)
  eh boa pratica perguntar antes da primeira chamada.
- Custo de UX: 1 dialog de 3 botoes na primeira execucao pos-upgrade. ~5s
  do usuario. Comparado a queixas potenciais ou multa, eh barato.
- Arquitetura compartilhada: PrivacyConsentWindow ja vem com estrutura de
  N toggles. PR-3 (Sentry) e PR-4 (PostHog) descomentam linhas no XAML
  sem refazer layout. Uma pergunta cobre 3 features.

ConsentVersion incrementa quando adicionamos uma feature nova: PR-2=1,
PR-3=2, PR-4=3. Quando codigo > persistido, dialog reabre.

### Restart UX: only "next-startup", no shutdown hook

Revit `IExternalApplication.OnShutdown` nao eh chamado confiavelmente em
todos os cenarios (crash, kill, "X" da janela). Estado inconsistente eh
pior que "user precisa fechar e reabrir". Logo:

- Apos download + validacao OK → marker `{version}.marker` em `pending\`
  + dialog "Atualizacao baixada. Feche e reabra o Revit pra aplicar."
- Proximo `App.OnStartup`, **PRIMEIRA** acao apos `Logger.Initialize`:
  `UpdateApplier.ApplyPendingIfAny()`

Trade-off conhecido: o swap acontece no startup do Revit que ainda vai
rodar a versao antiga. Funciona se o CLR nao carregou a DLL ainda. Se
carregou (raro mas possivel), `IOException` HResult 32/33 dispara o
graceful degradation:

| Tentativa | Resposta UX |
|---|---|
| 1ª falha | "Atualizacao pendente. Reinicie o Revit pra completar." |
| 2ª falha | "Atualizacao ainda pendente. Tente reiniciar o Revit completamente." |
| 3ª falha | "Atualizacao falhou apos multiplas tentativas. Atualize manualmente em github.com/.../releases" + delete pending |

`AttemptCount` persistido no marker. Logica em `UpdateApplier.IncrementAttemptOrAbort`.

### As 6 validacoes do download

Falha em qualquer → log + delete .zip + retorna codigo `DownloadResult`.

1. **Tamanho** em `(1 MB, 50 MB)` — sanity check (nosso .zip eh ~4 MB)
2. **ZipArchive abre** sem `InvalidDataException`
3. **Zip-slip prevention**: nenhuma entry com `..` ou path absoluto
4. **SHA256** bate com asset `checksums.txt`
5. **Top-level contem `FerramentaEMT.dll`**
6. **AssemblyName.Version do .dll == tag_name** do release

`Build-SetupExe.ps1` agora gera `checksums.txt` como asset adicional.
Workflow de release (manual ou automatizado em PR futuro) precisa incluir
o checksums.txt no `gh release upload`.

## Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| Swap falha porque CLR ja carregou DLL | Embedded graceful degradation: 3 tentativas com AttemptCount no marker (ADR-006 §"Restart UX"). Nao depende de comportamento empirico do Revit. |
| GitHub API rate limit (60 req/h sem token) | Cache 24h em `LastUpdateCheckUtc` + limite de timeout 5s + `Unknown` nao gasta cache. Re-avaliar PAT publico se base de usuarios numa mesma rede ultrapassar 1000. |
| Proxy corporativo bloqueia api.github.com | `HttpClient` herda proxy do sistema por default (`HttpClientHandler.UseProxy=true`). Documentar em RUNBOOK que firewall corporativo precisa permitir `api.github.com` e `objects.githubusercontent.com`. Sem fix code-side; eh decisao de TI. |
| Tamper offline: .zip ou marker editados antes do startup | Re-validacao SHA256 no `UpdateApplier` antes do swap (defesa em profundidade vs validacao do downloader). |
| Disco cheio durante download | `IOException` capturado, .zip parcial deletado, retorna `IoError`. Boot continua normal. |
| `%LocalAppData%\FerramentaEMT\` sem permissao de escrita | Try/catch raiz na thread de check + permissao testada em `PrivacySettingsStore.Save`. Settings ficam em memoria nesta sessao. |
| Backup `.bak\` durante swap, deletado apos sucesso | Trade-off consciente: simplicidade > rollback recovery local. Falha critica de restore loga `Logger.Error` mas instalacao pode ficar inconsistente — usuario reinstala. |
| Threading: `Task.Run` em background no boot | ADR-004 (Revit single-thread) NAO se aplica — Update nao toca Revit API. Isolar `App.OnStartup` em try/catch raiz pra que falha de Update NAO impeça boot do plugin. |
| Pre-release publicado como `latest` | `release.PreRelease == true` ignorado por padrao em `UpdateCheckService`. Usuario interessado em rc.X precisa baixar manualmente. |
| Downgrade malicioso (versao remota < local) | `UpdateCheckService` retorna `NoUpdate` + log warn quando comparacao SemVer eh negativa. |
| Multiplos markers em pending/ (race com download anterior) | `UpdateApplier` aplica o de maior versao, deleta os outros. |

## Validacao

- 117 testes novos cobrindo subsistema de Update + Privacy:
  - 34 SemVerComparer (parse, compare, edge cases)
  - 27 UpdateCheckService (Moq, todos cenarios)
  - 7 PrivacyJson (round-trip, forward-compat)
  - 14 Sha256Calculator (hex + parser)
  - 15 ZipSlipValidator (paths seguros e suspeitos)
  - 16 UpdateMarkerJson + UpdateApplier (temp dir end-to-end)
- Suite total: 465 → 576 (excede a meta de ~505 do plano original).
- Build local + CI=true: 0 erros, 0 avisos novos.

## Consequencias

### Positivas
- Fix de seguranca (como o do Base64Url) pode chegar a usuarios em horas, nao meses
- Versao base bate certa (cache 24h evita spam de chamadas)
- Estrutura de PrivacyConsentWindow generica acelera PR-3 e PR-4

### Neutras
- `App.OnStartup` ganha ~3 etapas (~50ms total — Apply + WireLog + Task.Run)
- Test csproj cresce (~117 arquivos linkados a mais)

### Negativas
- Dependencia de Alefvieira233/EMT publico no GitHub. Se o repo virar privado
  ou for renomeado, o auto-update quebra silenciosamente. Mitigacao:
  documentar no RUNBOOK que mover/privar o repo exige PR de atualizacao
  do hardcoded `"Alefvieira233", "EMT"` em `App.cs`.
- Auto-download em background ainda nao implementado nesta PR. Botao
  "Verificar atualizacoes" em `LicenseActivationWindow` abre browser
  para o usuario baixar manualmente. Auto-download fica para v1.7.x
  (escopo creep evitado).

## Rollback

Se um problema critico aparecer pos-merge:

1. Reverter o commit do `App.cs` (apenas o trecho `WireUpdateLog +
   ApplyPendingIfAny + StartUpdateCheckBackground` — ~30 linhas).
2. Subsistema fica dormente (classes ficam mas nao sao chamadas).
3. PR de fix posterior corrige + religa.

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.2 (origem desta decisao)
- ADR-005 (CI compila csproj — pre-requisito para que regressoes do
  subsistema de Update sejam detectadas no CI)
- ADR-004 (threading model — esclarece por que Task.Run eh seguro AQUI
  e nao para Services que tocam Revit API)
- [GitHub REST API: Get the latest release](https://docs.github.com/en/rest/releases/releases)
- [Zip Slip CVE-2018-1002201](https://snyk.io/research/zip-slip-vulnerability)
