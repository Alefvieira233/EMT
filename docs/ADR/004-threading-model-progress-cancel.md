# ADR 004: Modelo de threading para operacoes longas com progress e cancelamento

- Status: aceita
- Data: 2026-04-18
- Autores: Alef Vieira
- Contexto relacionado: ADR-003 (Result + IProgress + CT)

## Contexto

O ADR-003 obriga novos servicos a aceitar `IProgress<ProgressReport>` e
`CancellationToken`. Mas a primitiva so tem valor se chegar ate o usuario final:
feedback visual em tempo real e um botao de **Cancelar** que efetivamente
interrompe o trabalho.

Em qualquer app WPF moderno a resposta seria "rode o trabalho em `Task.Run`
e marshalle updates pro UI thread". Dentro do Revit isso nao funciona:

**A Revit API e single-threaded.** `FilteredElementCollector`, `Transaction`,
`Parameter.Get/Set`, geometria — tudo precisa ser chamado do thread principal
do Revit. Chamar qualquer uma dessas coisas de `Task.Run` lanca
`InvalidOperationException` ou, pior, corrompe o documento silenciosamente.

## Decisao

Servicos longos correm **no mesmo thread** que o comando (thread principal do
Revit). Para que a UI responda e o Cancelar seja processado sem violar a regra
da API, introduzimos um "host" que bombeia o Dispatcher entre pulsos de progresso.

### Primitiva: `FerramentaEMT.Utils.RevitProgressHost`

```csharp
TResult Run<TResult>(
    string title,
    string headline,
    Func<IProgress<ProgressReport>, CancellationToken, TResult> work,
    Window owner = null)
```

O que acontece dentro:

1. Abre um `ProgressWindow` (barra + label + botao Cancelar).
2. Cria um `CancellationTokenSource`. Evento `Cancelled` da janela dispara `cts.Cancel()`.
3. Cria um `Progress<ProgressReport>` cujo handler (i) atualiza a janela e (ii) bombeia o Dispatcher (`DispatcherFrame` + `BeginInvoke(Background, () => frame.Continue = false)`).
4. Chama `work(progress, cts.Token)` **sincronamente**.
5. Servico dentro do loop chama `ThrowIfCancellationRequested()` entre itens. Se o usuario clicou Cancelar, o token ja esta sinalizado (porque foi bombeado) e a exception sobe.
6. Comando chamador traduz `OperationCanceledException` em `Autodesk.Revit.UI.Result.Cancelled`.

### Regras de engajamento

- **Todo servico que reporta progresso** deve chamar `reporter.Report(...)` com frequencia (cada item do loop, ou a cada 100 ms). Sem isso o dispatcher nao bombeia e a UI congela.
- **Todo servico que itera** deve chamar `reporter.ThrowIfCancellationRequested()` no topo do loop. Cancelar sem essa chamada nao tem efeito.
- **Gravacao de arquivo / operacao critica** deve chamar `window.DisableCancel()` antes da fase nao-interruptivel. Isso protege contra arquivo meio gravado.
- **Proibido** `Task.Run` de qualquer codigo que toque Revit API. `Task.Run` e aceitavel **apenas** para trabalho puro (arquivo, CPU, rede).

### Quando *nao* usar o host

- Operacoes que levam <2s — a janela apareceria e sumiria piscando.
- Operacoes que nao tem loop natural (single call que demora) — progress indeterminado e aceitavel, mas considere se ha valor.
- Comandos que ja abrem um modal com progress embutido (ex.: relatorios que rolam num scrollview).

## Alternativas consideradas

- **`Task.Run` + marshal via Dispatcher.Invoke:** quebra o contrato da Revit API.
- **`IExternalEventHandler` assincrono:** util para trabalho fora de um command (ex.: integracoes por socket), mas adiciona ~100 LOC de cerimonia e nao ajuda no caso sincrono do command.
- **DevExpress `ProgressDialog`/similar:** dependencia externa pesada para um problema simples.
- **Sem UI, so Revit progress bar nativa (`UIApplication.StatusBarText`):** pobre, sem cancel, sem visual. Aceitavel como fallback em operacoes <5s.

## Consequencias

- Ganho: UX real. Usuario ve progresso, pode cancelar com certeza de que vai parar.
- Preco: padrao de `DoEvents()` (bombeamento explicito do dispatcher) e esquisito para quem vem de UI moderna. Documentado aqui e na docstring do `RevitProgressHost` para nao virar WTF.
- Nao-objetivo: nao vamos refatorar servicos para rodarem em background thread. O ganho nao compensa o risco de chamar Revit API fora do thread principal.

## Estado atual de adocao

Consumidores do host:
- `CmdVerificarModelo` (primeiro consumidor, 2026-04-18).

Candidatos proximos:
- `CmdExportarDstv` (ja migrou pro ADR-003).
- Qualquer comando que tenha servico migrado para ADR-003 e itere >50 items.

## Referencias

- `FerramentaEMT/Utils/RevitProgressHost.cs` — implementacao.
- `FerramentaEMT/Views/ProgressWindow.xaml(.cs)` — janela.
- `FerramentaEMT/Core/ProgressReporter.cs` — o lado do servico.
- ADR-003 — por que o contrato do servico pede `IProgress` + CT em primeiro lugar.
