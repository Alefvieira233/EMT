# ADR 005: CI compila o csproj principal usando stubs Nice3point para a Revit API

- Status: aceita
- Data: 2026-04-27
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.4)

## Contexto

Ate v1.6.0 o workflow `.github/workflows/build.yml` admitia explicitamente que
pulava o csproj principal:

> "We don't actually compile against shims - we just skip the main project on CI
> and only build the test project, which should not depend on RevitAPI."

O motivo: `FerramentaEMT.csproj` referencia `RevitAPI.dll` e `RevitAPIUI.dll`
direto de `C:\Program Files\Autodesk\Revit 2025\`, caminho que nao existe em
`windows-latest` runner do GitHub Actions. Sem Revit instalado, `dotnet build`
falhava com `MSB3245: Could not resolve this reference`.

Resultado pratico: regressoes no projeto principal so eram detectadas quando o
desenvolvedor rodava `Compilar-e-Instalar.bat` localmente. Em PRs de quem nao
tem Revit instalado (CI agents, contribuidores externos), bug que quebrava o
build podia passar despercebido ate o release.

A AUDITORIA-MERCADO-2026-04-27.md item P0.4 listou esse buraco como
**critico para mercado**: "Senão releases podem sair quebradas".

## Decisao

Usar **referencias condicionais** baseadas na env var `CI`:

- **Local** (Alef ou qualquer dev com Revit 2025 instalado): o csproj usa
  `<Reference HintPath="C:\Program Files\Autodesk\Revit 2025\RevitAPI.dll">`
  exatamente como antes. Comportamento preservado.

- **CI** (`CI=true`): o csproj usa `<PackageReference>` para os pacotes
  [Nice3point.Revit.Api.RevitAPI](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI/)
  e [Nice3point.Revit.Api.RevitAPIUI](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPIUI/),
  pinados na versao **2025.4.41** (publicada 2026-04-22, ultima estavel
  Revit 2025 no momento desta decisao).

### Por que Nice3point especificamente

- Mantido por terceiros confiaveis na comunidade Revit OSS desde Revit 2020
- Stubs **reference-only** que mirram a API publica do Autodesk byte-a-byte
  (compile-time apenas, nao funcionam em runtime — exato comportamento que
  precisamos)
- Compativel com `net8.0-windows7.0` (TFM do nosso projeto)
- Versionamento alinhado com o numero da Revit (2025.x.y) — facil de pinar
- Footprint: 50 MB (RevitAPI) + 4.7 MB (RevitAPIUI) no cache NuGet, restore
  + build do csproj principal em ~6s local

### Por que pin exato (2025.4.41) e nao range flutuante

Range tipo `2025.*` correria o risco de quebrar o CI silenciosamente se a
Nice3point publicar uma 2025.5.0 com mudanca de API que conflite com nosso
codigo. Pin explicito garante reprodutibilidade — o dia que precisarmos
atualizar (provavelmente Revit 2026), e uma decisao consciente, nao um upgrade
acidental.

### Configuracao no csproj

```xml
<ItemGroup Condition="'$(CI)' != 'true'">
  <Reference Include="RevitAPI">
    <HintPath>C:\Program Files\Autodesk\Revit 2025\RevitAPI.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <!-- idem RevitAPIUI -->
</ItemGroup>

<ItemGroup Condition="'$(CI)' == 'true'">
  <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2025.4.41">
    <PrivateAssets>all</PrivateAssets>
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <!-- idem RevitAPIUI -->
</ItemGroup>
```

`PrivateAssets=all` impede que a referencia se propague como dependencia
transitiva. `ExcludeAssets=runtime` impede que os DLLs Nice3point sejam
copiados para o `bin/` (espelha o `<Private>false</Private>` da referencia
local). Resultado: o csproj compila no CI sem deixar lixo no output.

## Plano-B (descartado)

Avaliamos gerar stubs Roslyn manualmente em `tools/ci-stubs/` (vasculhar tipos
Revit usados no codigo via grep, criar um projeto secundario que emite DLLs
vazios com os mesmos type-names, commitar binarios). Foi descartado porque:

- Manutencao: cada novo tipo Revit usado exige regerar o stub
- Pacote Nice3point ja faz isso publicamente, gratuitamente, melhor mantido
- Footprint comparavel (binario stub teria ~10-30 MB tambem)
- Gera dependencia de ferramenta extra (Roslyn API) que nao temos hoje

Plano-B fica documentado aqui caso a Nice3point seja descontinuada ou tenha
breaking change agressivo no futuro.

## Footprint do build no CI

Medido localmente (Windows + .NET 8.0.419 + cache frio):

- `dotnet restore FerramentaEMT.csproj` (com pacotes Nice3point) → ~4-5s
- `dotnet build FerramentaEMT.csproj -c Release --no-restore` → ~5-6s
- Tamanho do download: ~55 MB (sera cacheavel em CI futuras com `actions/cache`
  do diretorio `~/.nuget/packages` se virar gargalo)

Em CI com runner frio + download da rede, esperar ~30-60s para o job
`build-main`. Se passar de 2 minutos, otimizar com `actions/cache@v4`.

## Consequencias

### Positivas
- PRs em branches sem Revit instalado agora validam o csproj principal
- Quebra de build (CS0117/CS0246/CS1503 etc) e detectada antes do merge
- Fundacao para Fase 2 da auditoria (auto-update, telemetria, etc) que dependem
  do CI confiavel

### Neutras
- Build local do Alef inalterado — nada a fazer da parte dele
- Tempo de CI aumenta ~30-60s (job `build-main` paralelo ao `build-tests` ja
  existente — o tempo total cresce pouco)

### Negativas
- Dependencia externa (Nice3point) introduz risco se o pacote for tirado do
  ar. Mitigacao: pin exato + Plano-B documentado acima
- `TreatWarningsAsErrors` em Release continua valendo no CI: se a Nice3point
  publicar versao com warnings novos, o build quebra. Resposta correta sera
  adicionar o codigo do warning em `<NoWarn>` do csproj (linha existente
  `$(NoWarn);CS1591;NU1701`) — **nao** desabilitar TreatWarningsAsErrors

## Validacao

- `dotnet build FerramentaEMT.csproj -c Release` (sem `CI=true`): 0 erros,
  2 avisos MSB3277 pre-existentes nao-impeditivos
- `CI=true dotnet build FerramentaEMT.csproj -c Release`: 0 erros, 0 avisos
- `dotnet test` em `FerramentaEMT.Tests`: 465 aprovados, 0 falhas (preservado)
- CI: branch `ci/main-csproj-validation` com workflow verde nos 2 jobs novos
  (`build-main`, `build-tests`) — verificar antes de merge em main

## Rollback

Se o pacote Nice3point falhar em algum cenario futuro, basta:

1. Reverter este ADR + os 2 arquivos modificados (csproj, build.yml)
2. Adotar Plano-B (stubs Roslyn em `tools/ci-stubs/`)

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.4 (origem desta decisao)
- [Nice3point.Revit.Api.RevitAPI 2025.4.41](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI/2025.4.41)
- [Nice3point.Revit.Api.RevitAPIUI 2025.4.41](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPIUI/2025.4.41)
- ADR 003 (Result Pattern + IProgress + CancellationToken) — fundacao para os
  testes unitarios que correm no CI
