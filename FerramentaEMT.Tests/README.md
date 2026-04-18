# FerramentaEMT.Tests

Projeto de testes unitarios e de integracao do FerramentaEMT.

## Estrategia

Testes diretos contra `FerramentaEMT.csproj` sao **dificeis** porque o projeto principal
depende do `RevitAPI.dll`, que so existe em maquinas com Revit instalado.

Por isso, **a estrategia e extrair logica pura** (sem dependencia de Revit) para classes
testaveis:

```
FerramentaEMT/
├── Services/
│   ├── CotasService.cs                    # depende de Revit (NAO testado aqui)
│   └── Shared/
│       ├── FabricacaoSignatureBuilder.cs  # logica pura (TESTADA aqui)
│       └── PerfilMetadataExtractor.cs     # logica pura (TESTADA aqui)
```

A medida que avancamos os Sprints 2-3 e quebramos as God Classes, iremos:

1. Mover logica pura para `Services/Shared/`
2. Criar testes correspondentes em `FerramentaEMT.Tests/Services/Shared/`
3. Cobertura alvo: **70%** da logica pura

## Como rodar

### CLI
```bash
cd FerramentaEMT.Tests
dotnet test
```

### Visual Studio
- Test Explorer (Ctrl+E, T)
- Run All

### Com cobertura
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Resultado em `TestResults/<guid>/coverage.cobertura.xml`.

## Estrutura

```
FerramentaEMT.Tests/
├── Smoke/                      # Sanity checks (rodam sempre)
├── Services/Shared/            # Testes de logica pura (a popular)
└── Helpers/                    # Test fixtures, mocks, builders
```

## Convencoes

- Nome de classe: `<ClasseTestada>Tests`
- Nome de metodo: `<Cenario>_<Quando>_<EsperaQue>`
  - Ex: `BuildSignature_DuasVigasIguais_RetornaMesmaChave`
- AAA: Arrange / Act / Assert
- Use `FluentAssertions` (`.Should().Be(...)`)
- Use `[Theory]` + `[InlineData]` para varios casos do mesmo cenario
