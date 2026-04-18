# Playbook: migrando um servico para ADR-003 + ADR-004

**Publico:** dev novo chegando no projeto que precisa migrar mais um servico para o pattern
`Result<T>` + `IProgress<ProgressReport>` + `CancellationToken`.

**Base:** 3 adocoes ja feitas — `DstvExportService`, `ModelCheckService`,
`ListaMateriaisExportService`. Este playbook condensa o aprendizado.

---

## Quando aplicar

**Aplicar** quando o servico:

- e um **batch loop** que processa uma colecao (>100 elementos em casos reais);
- pode **durar mais de 2 segundos** em modelos grandes;
- e chamado de **um unico comando** (ou poucos), o que facilita atualizar o callsite em paralelo;
- hoje usa `AppDialogService` ou `ref string message` para reportar erro.

**Nao aplicar** quando o servico:

- e **interativo** (usuario clica elemento por elemento via `ExternalEvent`) — caso do
  `NumeracaoItensService`. Progresso e cancelamento nao fazem sentido numa sessao stateful.
- e **atomico e rapido** (<500 ms em modelos grandes) — custo da refatoracao nao compensa.
- manipula **transacoes criticas** onde cancelar no meio deixa o modelo Revit em estado
  inconsistente. Use `GroupTransaction` com escopo fechado ou marque a fase inteira como
  nao-interrompivel.

---

## Pre-requisitos

Antes de comecar, confirmar que o projeto tem:

- `FerramentaEMT/Core/Result.cs` — struct `Result<T>` e `Result`.
- `FerramentaEMT/Core/ProgressReport.cs` — struct imutavel.
- `FerramentaEMT/Core/ProgressReporter.cs` — wrapper com throttle e CT.
- `FerramentaEMT/Utils/RevitProgressHost.cs` — host de `Run<T>(title, headline, work)`.
- `FerramentaEMT/Views/ProgressWindow.xaml(.cs)` — dialogo visual.
- Comando herda de `FerramentaCommandBase` (que ja captura `OperationCanceledException` do
  Revit, mas **nao** captura `System.OperationCanceledException` — use try/catch explicito).

---

## Fluxo da migracao (6 passos)

### 1. Mapear AppDialogService no servico

```bash
grep -n "AppDialogService" FerramentaEMT/Services/NovoService.cs
```

Para cada chamada, classifique:

- **Validacao de input** (uidoc nulo, config invalida): vira `Result.Fail(msg)`.
- **Falha de dominio** (nenhum elemento, caminho vazio): vira `Result.Fail(msg)`.
- **Erro de infra** (IO, Revit API): `try/catch + Logger.Error + Result.Fail(msg)`.
- **Resumo de sucesso**: move para um `static BuildResumoText(ResultadoXxx)` e o comando
  chama `AppDialogService.ShowInfo`.

### 2. Definir `ResultadoXxx` no servico

```csharp
public sealed class ResultadoXxx
{
    public int TotalProcessados { get; set; }
    public int ArquivosGerados { get; set; }
    public List<string> Warnings { get; } = new();
    public string PastaDestino { get; set; } = string.Empty;
    public TimeSpan Duracao { get; set; }
    // Adicione sinais de falha parcial (ex.: ExportError, ExportedToPath) para
    // permitir que o comando decida se foi sucesso, warning ou erro.
}
```

Regra: **falha parcial nao invalida o Result<T> inteiro**. Ex.: se o Excel nao gravou mas
a analise rodou ok, retorne `Result.Ok(resultado)` e **marque** o erro no `resultado`.

### 3. Refatorar assinatura do metodo publico

```csharp
// Antes:
public Revit.UI.Result Executar(UIDocument uidoc, Config config, ref string message)

// Depois:
public Core.Result<ResultadoXxx> Executar(
    UIDocument uidoc,
    Config config,
    IProgress<ProgressReport> progress = null,
    CancellationToken ct = default)
```

**Nao renomeie o metodo** — so muda assinatura. Assim o blame fica limpo e o diff legivel.

### 4. Adicionar reporter + checks de CT no loop

```csharp
ProgressReporter reporter = new ProgressReporter(progress, throttleMs: 100, ct);
int i = 0;
foreach (var item in colecao)
{
    reporter.ThrowIfCancellationRequested();
    i++;
    // ... processar ...
    reporter.Report(i, colecao.Count, $"Processando {i}/{colecao.Count} — {item.Nome}");
}
reporter.ReportFinal(colecao.Count, colecao.Count, "Concluido");
```

**Throttle 100 ms** e o padrao — floodar a UI com milhares de eventos em <10ms deixa a
barra travada. `ReportFinal` ignora o throttle para garantir que a ultima mensagem apareca.

### 5. Dividir em fases (interrompivel vs nao)

Fase **interrompivel**: coleta, classificacao, processamento item-a-item. Checa CT no topo
do loop.

Fase **nao interrompivel**: gravacao de arquivo final (Excel/DSTV/JSON), commit de
transacao Revit. Nao checa CT — abortar no meio deixa arquivo corrompido ou modelo sujo.

Se o usuario cancelar durante a fase nao interrompivel, **ignore** — deixe terminar.
A barra de progresso deve sinalizar "finalizando..." para o usuario entender.

### 6. Atualizar o comando

```csharp
public class CmdNovoCommand : FerramentaCommandBase
{
    protected override Result ExecuteCore(UIDocument uidoc, Document doc)
    {
        // Fase 1: config via ShowDialog (modal Revit nativo) ANTES da UI de progresso.
        // Abrir a barra agora deixaria ela vazia atras do modal.
        var janela = new NovoCommandWindow(uidoc);
        if (janela.ShowDialog() != true) return Result.Cancelled;
        var config = janela.BuildConfig();

        var service = new NovoService();

        // Fase 2: processamento com progress + cancel.
        Core.Result<NovoService.ResultadoXxx> outcome;
        try
        {
            outcome = RevitProgressHost.Run(
                title: CommandName,
                headline: "Processando...",
                work: (progress, ct) => service.Executar(uidoc, config, progress, ct));
        }
        catch (OperationCanceledException)
        {
            return Result.Cancelled;
        }

        if (outcome.IsFailure)
        {
            AppDialogService.ShowWarning(CommandName, outcome.Error, "Nao foi possivel processar");
            return Result.Failed;
        }

        // Pos-processamento: comando decide UX de sucesso.
        string resumo = NovoService.BuildResumoText(outcome.Value);
        AppDialogService.ShowInfo(CommandName, resumo, "Concluido");
        return Result.Succeeded;
    }
}
```

---

## Armadilhas comuns

### 1. `System.OperationCanceledException` vs `Autodesk.Revit.Exceptions.OperationCanceledException`

Sao **tipos diferentes**. `CancellationToken.ThrowIfCancellationRequested()` lanca o
`System.`. `PickObjects` quando o usuario aperta ESC lanca o `Autodesk.`. O comando
precisa de **catch para os dois** (ou confiar no `FerramentaCommandBase` para o `Autodesk.`
e capturar explicitamente o `System.` perto do `RevitProgressHost.Run`).

### 2. Colisao de nomes `Result`

`using Autodesk.Revit.UI;` (enum `Result`) + `using FerramentaEMT.Core;` (struct
`Result<T>`). Para acessos como `Result.Cancelled` (membro so existe no enum) o compilador
resolve sozinho. Para o generico, **qualifique** — `Core.Result<T>` ou
`FerramentaEMT.Core.Result<T>`. Para o nao-generico em casos de ambiguidade, qualifique
tambem — `Core.Result.Ok()`.

### 3. Revit API **e thread-unica**

**Nunca** use `Task.Run` ou similar dentro do servico. `RevitProgressHost` **corre o work
no mesmo thread** e bombeia o Dispatcher via `DispatcherFrame + BeginInvoke(Background)`.
Isso permite a UI atualizar sem violar o contrato da Revit API.

### 4. Servicos "quase mudos"

As vezes sobra uma chamada `AppDialogService` em caso raro. **Nao deixe** — converta para
`Logger.Warn` + sinalize no `ResultadoXxx`. O servico ser 100% mudo permite futuramente
expor o servico via outra UI (console, web, etc.).

### 5. Renomear helpers publicos quebra callers

Se o servico expoe metodos `public static` (ex.: `MontarAssinaturaFabricacao` em
`ListaMateriaisExportService`), **nao mude assinatura** deles na migracao. Outros servicos
ou comandos podem depender. Se precisar mudar, faca em PR separado.

---

## Contra-exemplo: quando NAO migrar

`NumeracaoItensService` usa `ExternalEvent` + `NumeracaoItensSessao` stateful:

- Usuario clica **um** elemento por vez via `ISelectionFilter`.
- Cada clique = uma transacao separada.
- Nao ha "loop de N elementos" — e uma sessao interativa aberta ate o usuario fechar.

`RevitProgressHost.Run<T>` pressupoe work **sincrono com inicio e fim claros**, que aceita
progress/cancel. `ExternalEvent` e async e stateful. Forcar o pattern aqui seria:

1. Confuso — a UI de progresso ficaria aberta durante a sessao inteira (minutos).
2. Errado — "cancelar" ja e feito fechando a janela de controle.
3. Sem ganho — a sessao nao tem unidade de trabalho para contar ("N/Total").

**Decisao documentada:** `NumeracaoItensService` pode receber polish ADR-003-lite (remover
`AppDialogService`, retornar `Result`), mas **nao** ADR-004.

---

## Checklist antes de fechar PR

- [ ] Zero `AppDialogService.*` no servico (grep confirma).
- [ ] `Logger.Info` no sucesso, `Logger.Warn`/`Logger.Error` em falhas parciais/totais.
- [ ] `ThrowIfCancellationRequested()` no topo de cada loop > 10 iteracoes.
- [ ] `reporter.Report` em cada iteracao de loop longo; `ReportFinal` ao final.
- [ ] Comando envolve `service.Executar` em `RevitProgressHost.Run`.
- [ ] Comando captura **`System.OperationCanceledException`** e retorna `Result.Cancelled`.
- [ ] Comando captura `IsFailure` e mostra `outcome.Error` via `AppDialogService.ShowWarning`.
- [ ] Comando mostra `BuildResumoText(outcome.Value)` no sucesso.
- [ ] `CHANGELOG.md` recebe entrada sob `[Unreleased]`.
- [ ] CI verde (GitHub Actions build + testes).
- [ ] Teste manual no Revit com modelo grande (>500 elementos) — progresso aparece, cancelar
  funciona, nenhum "UI freeze" percebido.

---

## Referencias

- ADR-001 — Result<T> pattern (`docs/ADR/001-result-pattern.md`)
- ADR-003 — Service contract (`docs/ADR/003-result-progress-adoption.md`)
- ADR-004 — Threading model e progress/cancel UI (`docs/ADR/004-threading-model-progress-cancel.md`)
- Template de referencia — `DstvExportService.cs` (primeira adocao, mais completa)
- Caso duas-fases — `DstvExportService.ColetarElementos` + `.Executar` (quando coleta abre PickObjects)
