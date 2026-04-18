# ADR 002: IRevitContext para isolar dependencia direta do Revit

- Status: aceita (skeleton v1)
- Data: 2026-04-18
- Autores: Alef Vieira
- Contexto relacionado: PLANO-100-100.md (fase 1, hardening arquitetural)

## Contexto

Hoje os servicos do plugin (ex.: `MarcarPecasService`, `DstvExportService`)
recebem `Document` e `UIDocument` diretamente. Isso tem dois problemas:

1. **Acoplamento forte com Revit API** — trocar de versao ou isolar para testes
   exige reescrever o codigo cliente.
2. **Impossivel testar unitariamente** — `Document` nao pode ser instanciado fora
   do processo Revit. Testes ficam restritos a integration tests (lentos, exigem
   o Revit aberto).

## Decisao

Introduzir a interface `IRevitContext` como ponto unico de acesso ao Revit a
partir dos servicos. Comecar com um skeleton v1 que apenas expoe referencias:

```csharp
public interface IRevitContext
{
    Document Document { get; }
    UIDocument UIDocument { get; }
    Application Application { get; }
    string RevitVersion { get; }
}
```

A implementacao `RevitContext` e construida a partir de `ExternalCommandData`.
Servicos novos devem receber `IRevitContext` no construtor.

## Por que skeleton v1 (em vez da versao final)?

A versao final abstrairia:

- `IElementQuery` (substitui `FilteredElementCollector`)
- `ITransactionScope` (substitui `Transaction` + `using`)
- `IParameterReader` / `IParameterWriter`
- `IPickingService` (substitui `uidoc.Selection.PickObject`)

Isso e um refactor **grande** — tocaria praticamente todos os arquivos. Em vez de
fazer tudo de uma vez, a estrategia e:

1. **Agora (v1):** interface minima + implementacao padrao. Novos servicos comecam
   a usar. Codigo legacy continua como esta.
2. **Proximo (v2):** quando tocar servicos legacy por outro motivo, migrar para
   `IRevitContext` + abstracoes de nivel mais alto conforme necessidade real.
3. **Final (v3):** servicos 100% isolados do Revit API diretamente; testes unitarios
   cobrem logica de dominio com fakes de `IRevitContext`.

## Consequencias

### Positivas

- Caminho de migracao incremental, sem PR gigante
- Fundacao para testes unitarios futuros
- Melhor modelagem: servicos dependem de **abstracoes**, nao de tipos concretos do Revit
- Facilita adicionar multi-Revit: `IRevitContext` pode esconder diferencas entre versoes

### Negativas / custo

- Por enquanto a abstracao nao melhora testabilidade (so expoe Document direto)
- Mais uma camada no grafo de dependencias para ler novos servicos
- Tentacao de expandir a interface sem plano — manter disciplina: adicionar
  metodo novo so com caso de uso concreto

## Exemplo de adocao em servico novo

```csharp
public sealed class MinhaNovaRegraService
{
    private readonly IRevitContext _ctx;
    private readonly ILogger _log;

    public MinhaNovaRegraService(IRevitContext ctx, ILogger log)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Result<int> Executar(ElementId pilarId)
    {
        Element el = _ctx.Document.GetElement(pilarId);
        if (el == null) return Result<int>.Fail("Pilar nao encontrado");
        // ...
        return Result<int>.Ok(42);
    }
}
```

E no comando:

```csharp
protected override Result ExecuteCore(UIDocument uidoc, Document doc)
{
    IRevitContext ctx = RevitContext.CreateFromCommandData(commandData);
    var svc = new MinhaNovaRegraService(ctx, Logger.Default);
    Result<int> r = svc.Executar(...);
    // ...
}
```

## Referencias

- `FerramentaEMT/Core/IRevitContext.cs` — interface e implementacao padrao
- ADR 001 — Result<T> pattern (usado em conjunto)
- PLANO-100-100.md, fase 1
