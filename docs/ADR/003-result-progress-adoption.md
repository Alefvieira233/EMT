# ADR 003: Adoção incremental de Result<T>, IProgress e CancellationToken

- Status: aceita
- Data: 2026-04-18
- Autores: Alef Vieira
- Contexto relacionado: ADR-001, ADR-002, PLANO-100-100 fase 1

## Contexto

O repo tem hoje 3 primitivas arquiteturais novas, criadas mas **ainda não
adotadas** pelo código de produção:

- `FerramentaEMT/Core/Result<T>` — retorno tipado de serviços que podem falhar por regra de domínio.
- `FerramentaEMT/Core/IRevitContext` — wrapper de `UIDocument`/`Document` para desacoplar serviços.
- `FerramentaEMT/Core/ProgressReporter` — wrapper sobre `IProgress<ProgressReport>` com throttle e `CancellationToken`.

Se essas primitivas ficarem sem uso, viram cemitério de código. O risco é
concreto: já aconteceu antes em outros módulos do repo. Esta ADR define a
política de adoção incremental para que a próxima geração de serviços
nasça já alinhada, sem exigir refactor em massa do legado.

## Decisão

### Quando usar `Result<T>`

Em todo serviço novo (ou revisão significativa de serviço existente) cujo método
público possa falhar por **motivo previsível de domínio**:

- Seleção vazia / ausente.
- Parâmetro obrigatório vazio ou fora de range.
- Elemento não é do tipo esperado.
- Regra de negócio violada.

Continue lançando exceção para bugs e falhas de infraestrutura (API Revit
devolvendo o inesperado, IO, recursos não encontrados que deveriam existir).

Regra prática: **se o callsite consegue escrever uma mensagem amigável ao
usuário com base no erro, é `Result`. Se o callsite só saberia `LogError+
rethrow`, é exceção.**

### Quando usar `IRevitContext`

Todo serviço novo deve receber `IRevitContext` no construtor em vez de
`UIDocument` ou `Document` crus. Serviços existentes migram sob demanda —
não é bloqueio para novas features.

### Quando usar `ProgressReporter` + `CancellationToken`

Em qualquer operação que itere sobre mais de ~50 elementos ou que leve >2s.
Na prática isso cobre:

- Export DSTV/NC1 (por peça).
- Model Check (por regra × elemento).
- Cotagem automática em treliça (por banzo × segmento).
- Bill of Materials / lista de materiais (agregação).
- Plano de montagem (varredura de elementos).

O contrato é sempre o mesmo:

```csharp
public Result<int> Executar(
    IRevitContext ctx,
    MinhaConfig cfg,
    IProgress<ProgressReport> progress = null,
    CancellationToken ct = default)
{
    var reporter = new ProgressReporter(progress, throttleMs: 100, ct);
    var items = ColetarItens(ctx, cfg);
    if (items.Count == 0)
        return Result<int>.Fail("Nenhum item correspondente ao filtro.");

    int ok = 0;
    for (int i = 0; i < items.Count; i++)
    {
        reporter.ThrowIfCancellationRequested();
        ProcessarItem(items[i]);
        ok++;
        reporter.Report(i + 1, items.Count, $"Processando {items[i].Name}");
    }
    reporter.ReportFinal(ok, items.Count, "Concluído");
    return Result<int>.Ok(ok);
}
```

O comando WPF/Ribbon que chama esse serviço:

```csharp
var cts = new CancellationTokenSource();
var progress = new Progress<ProgressReport>(r => statusBar.Text = r.ToString());

Result<int> r = _service.Executar(ctx, cfg, progress, cts.Token);
if (!r.IsSuccess)
{
    AppDialogService.ShowWarning("Título", r.Error, "Não foi possível concluir");
    return Autodesk.Revit.UI.Result.Cancelled;
}
```

### Política de adoção (incremental)

- **Toda nova feature** deve usar o trio. Sem exceção.
- **Serviço existente tocado em bug fix ou refactor** migra se o diff já é grande — senão adia.
- **Não faça "big bang migration"** — um PR por serviço, com teste, CHANGELOG.

## Consequências

- Código cliente fica mais chato de escrever no começo (boilerplate de reportar progresso).
- Em troca, ganho: UI não congela, usuário pode cancelar, logs têm granularidade, testes podem reportar progresso fake sem Revit.
- Dois sabores de erro (exceção × `Result`) exigem disciplina. A regra prática acima é a linha de corte.

## Não-objetivos

- Não vamos reescrever o legado proativamente. Cada serviço migra quando for tocado.
- Não vamos criar uma camada de "orquestrador" acima dos comandos. `FerramentaCommandBase` já faz isso bem.

## Alternativas consideradas

- `Nullable<T>` como sinalizador de falha: perde a mensagem, perde composição via `Map`/`Match`.
- `try/catch` everywhere: funciona mas mistura bugs com regras de negócio, dificulta log.
- `Either<Error, T>` funcional puro: útil mas introduce dependência em biblioteca (LanguageExt) e curva de aprendizado maior — preferimos struct caseiro simples.

## Referências

- `FerramentaEMT/Core/Result.cs` — implementação.
- `FerramentaEMT/Core/IRevitContext.cs` — implementação.
- `FerramentaEMT/Core/ProgressReporter.cs` — implementação.
- ADR-001 (motivação do Result<T>).
- ADR-002 (motivação do IRevitContext).
