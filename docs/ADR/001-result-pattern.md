# ADR 001: Result<T> para fluxos previsiveis de dominio

- Status: aceita
- Data: 2026-04-18
- Autores: Alef Vieira
- Contexto relacionado: PLANO-100-100.md (fase 1, hardening arquitetural)

## Contexto

Hoje o codigo mistura dois estilos de tratamento de erro:
1. Excecoes lancadas para qualquer coisa que nao deu certo — inclusive situacoes
   esperadas como "usuario nao selecionou pecas" ou "parametro obrigatorio vazio".
2. Alguns comandos verificam condicoes e devolvem `Result.Cancelled` diretamente.

O problema do estilo 1 e que excecoes sao **caras** (stack walk) e tornam o fluxo
de controle dificil de ler: um `try/catch` generico acaba capturando tanto bugs
quanto condicoes de borda normais. No Revit isso e pior porque algumas APIs lancam
`Autodesk.Revit.Exceptions.InvalidOperationException` que geralmente sao do usuario
(tentar acessar parametro que nao existe), nao erros de infra.

## Decisao

Adotar `Result<T>` / `Result` (structs imutaveis) para operacoes que tem falha esperada
do dominio. Excecoes ficam reservadas para **bugs** e **falhas de infraestrutura**.

### Quando usar `Result<T>`

- Input do usuario invalido: "numero negativo", "texto vazio", "email malformatado"
- Regras de negocio: "este elemento nao e uma viga de aco", "peca ja esta marcada"
- Selecao vazia: "nenhum elemento selecionado"
- Validacoes estruturadas que o chamador **precisa** tratar

### Quando usar excecoes

- Bugs: invariantes violadas, estado corrompido
- Falhas de infraestrutura: IO, rede, API do Revit falhando de forma inesperada
- Falhas que **nao** fazem sentido o chamador tratar localmente

## Exemplo canonico

```csharp
// servico: retorna Result<T>, nunca lanca para input invalido
public Result<int> MarcarPeca(Element el)
{
    if (el == null)
        return Result<int>.Fail("Elemento nulo.");
    if (!IsPecaAco(el))
        return Result<int>.Fail("Elemento nao e uma peca de aco.");

    int marca = CalcularProximaMarca();
    GravarParametro(el, "MarcaFabricacao", marca);
    return Result<int>.Ok(marca);
}

// comando: consome Result<T>, decide UI
public override Result Execute(...)
{
    Result<int> r = _service.MarcarPeca(el);
    if (r.IsFailure)
    {
        AppDialogService.ShowWarning("Marcar Peca", r.Error, "Nao foi possivel marcar");
        return Result.Cancelled;
    }
    TaskDialog.Show("OK", $"Peca marcada: {r.Value}");
    return Result.Succeeded;
}
```

## Consequencias

### Positivas

- Fluxo de erro explicito — o tipo de retorno mostra que pode falhar
- Teste fica mais simples: `result.IsSuccess.Should().BeTrue()` em vez de `Should().NotThrow()`
- Performance: `Result<T>` e struct, zero alocacao e zero stack walk
- Mensagem de erro embutida — pronta para exibir ao usuario

### Negativas / custo

- Chamadores esquecem de checar `IsSuccess` — mitigavel com convencao + code review
- Cadeia longa de `Map`/`Match` pode virar "railway hell"; manter fluxo simples
- Migrar codigo existente e trabalho — **nao e objetivo desta ADR**, adoptar em
  novos comandos; migrar antigos oportunisticamente

## Migracao

- Codigo existente continua como esta
- Comandos/servicos novos usam `Result<T>` desde o primeiro dia
- Quando tocar codigo legacy por outro motivo, migrar o caminho feliz para `Result<T>`
- Nao abrir PR "migrar tudo para Result<T>" — esse PR nao passaria em review por tamanho

## Alternativas consideradas

1. **Continuar so com excecoes** — rejeitada: mistura de bugs com condicoes esperadas,
   performance ruim em laco, `try/catch` vira captura-tudo.
2. **OneOf / language-ext** — rejeitada por ora: adiciona dependencia grande por
   pouco ganho. Podemos adotar depois se virar comum.
3. **Tuple `(bool ok, T value, string error)`** — rejeitada: nao tem encadeamento,
   menos auto-documentada.

## Referencias

- Enterprise Craftsmanship — "Exceptions for flow control" (Vladimir Khorikov)
- Railway Oriented Programming (Scott Wlaschin)
- `FerramentaEMT/Core/Result.cs` — implementacao
- `FerramentaEMT.Tests/Core/ResultTests.cs` — contratos testados
