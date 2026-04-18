# Contribuindo para o FerramentaEMT

Obrigado pelo interesse. Este documento cobre o fluxo de contribuicao, convencoes
de codigo e como levantar um ambiente de desenvolvimento do plugin.

## Requisitos

- Windows 10/11 x64
- Revit 2025 instalado (para testes end-to-end)
- .NET 8 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Visual Studio 2022 17.5+ ou Rider (recomendados)
- Git

## Setup local

```powershell
git clone https://github.com/Alefvieira233/EMT.git
cd EMT

# Testes unitarios (nao dependem do Revit)
cd FerramentaEMT.Tests
dotnet test

# Build do plugin (depende do Revit 2025 instalado)
cd ..\FerramentaEMT
dotnet build --configuration Release
```

### Opcional: segredo HMAC de licenciamento

Para rodar com um segredo proprio (recomendado em desenvolvimento separado
do ambiente de producao):

```powershell
# Opcao 1: via variavel de ambiente (escopo do shell atual)
$env:EMT_LICENSE_SECRET = "<sua-string-aleatoria-de-32+-chars>"

# Opcao 2: arquivo persistente por usuario
$path = "$env:LOCALAPPDATA\FerramentaEMT\license.secret"
New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
Set-Content -NoNewline -Encoding UTF8 -Path $path -Value "<sua-string>"
```

Sem configurar, o plugin usa o fallback DEV_ONLY — util so para experimentar,
nao para validar licencas de producao.

## Fluxo de contribuicao

1. Abra uma issue descrevendo o problema/proposta antes de abrir PR grande
2. Crie uma branch de feature: `git checkout -b feat/minha-mudanca`
3. Implemente + testes
4. Rode `dotnet test` e `dotnet format --verify-no-changes` antes do push
5. Abra PR contra `main` seguindo o template

### Convencao de commit

Seguimos [Conventional Commits](https://www.conventionalcommits.org/pt-br/) com tipos:

- `feat(escopo):` nova funcionalidade
- `fix(escopo):` correcao de bug
- `refactor(escopo):` refatoracao sem mudanca de comportamento
- `docs(escopo):` so documentacao
- `test(escopo):` adicao/ajuste de testes
- `ci:` mudanca no workflow de CI
- `chore(escopo):` tarefa interna (deps, versao, etc)
- `security(escopo):` correcao/melhoria de seguranca
- `arch(escopo):` mudanca arquitetural (ADR)

Escopos comuns: `modelcheck`, `pf`, `licensing`, `trelica`, `wpf`, `core`, `ci`.

### Testes obrigatorios

Todo novo servico ou logica de dominio precisa de teste em `FerramentaEMT.Tests/`.
Se o servico depende de API do Revit, extraia a logica pura para uma classe
sem dependencia de `Autodesk.Revit.*` e teste essa parte. Exemplos:
`PfNamingFormatter`, `TrelicaClassificador`, `DstvProfileMapper`.

### Coding style

Seguir `.editorconfig` na raiz. Regras-chave:
- `this.` so quando necessario
- `var` so quando o tipo e obvio do lado direito
- Indentacao com 4 espacos em `.cs`, 2 em XML/YAML/JSON
- Ingles para nomes de tipo e API, portugues nos comentarios e mensagens de UI

## Architecture Decision Records (ADR)

Mudancas arquiteturais significativas sao documentadas em `docs/ADR/`.
Use os ADRs existentes como template. Formato:

```
# ADR NNN: titulo
- Status: proposta | aceita | rejeitada | superada por ADR-XXX
- Data: YYYY-MM-DD
- Autores: nome

## Contexto
## Decisao
## Consequencias (positivas / negativas)
## Alternativas consideradas
## Referencias
```

## Codigo de conduta

Seja respeitoso. Critique ideias, nao pessoas. Assuma boa-fe.

## Licenca

Ao contribuir voce concorda que sua contribuicao sera licenciada sob os
mesmos termos do projeto (ver `LICENSE`).
