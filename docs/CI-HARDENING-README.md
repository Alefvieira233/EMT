# CI Hardening — aplicacao manual

Este diretorio contem `CI-HARDENING-APPLY-MANUALLY.patch` com melhorias para
`.github/workflows/build.yml` que nao puderam ser pushadas automaticamente
porque o PAT usado pelo bot nao tem o escopo `workflow`.

## O que o patch melhora

- Cache de NuGet (reduz tempo de restore em PRs repetidos)
- `concurrency` group cancelando runs obsoletos no mesmo ref
- Timeout explicito por job
- Job dedicado que compila `tools/EmtKeyGen/` e faz upload do artefato
- `dorny/test-reporter` publicando resultados xUnit como check na PR
- `EMT_LICENSE_SECRET` injetado via `secrets.LICENSE_HMAC_SECRET` (opcional —
  se nao configurar nos secrets do repo, o build cai no fallback DEV_ONLY,
  que funciona para testes)
- Comentario `TODO` marcando onde plugar a matrix multi-Revit (fase 1 do plano)

## Como aplicar

### Opcao A — aplicar o patch (preserva a mensagem de commit)

```bash
cd /caminho/para/EMT
git am docs/CI-HARDENING-APPLY-MANUALLY.patch
git push origin main
```

### Opcao B — regerar PAT com escopo workflow

1. GitHub -> Settings -> Developer settings -> Personal access tokens -> Fine-grained
2. Editar o token usado pelo bot e adicionar o escopo **Workflows**
3. Re-rodar o push automatizado

### Opcao C — pushar manualmente da sua maquina (conta pessoal)

```bash
cd /caminho/para/EMT
git pull
# edite .github/workflows/build.yml aplicando o conteudo do patch
git add .github/workflows/build.yml
git commit -m "ci: hardening do workflow de build & test"
git push origin main
```

## Configurar o secret LICENSE_HMAC_SECRET (opcional mas recomendado)

1. GitHub -> repo -> Settings -> Secrets and variables -> Actions
2. New repository secret
3. Nome: `LICENSE_HMAC_SECRET`
4. Valor: string aleatoria de 32+ caracteres (pode gerar em PowerShell com
   `[System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Min 0 -Max 256 } | ForEach-Object { [byte]$_ }))`)
5. Save

Apos configurar, cada build do CI vai usar este secret em vez do fallback
DEV_ONLY — os warnings `[Licensing] Segredo HMAC nao externalizado` somem
dos logs de CI.

## Depois de aplicar, remova este arquivo

Estes arquivos (o `.patch` e este README) sao temporarios. Apos aplicar
o patch e verificar que o CI esta verde, remova ambos:

```bash
rm docs/CI-HARDENING-APPLY-MANUALLY.patch docs/CI-HARDENING-README.md
git commit -m "chore: remover artefatos de patch ja aplicado"
```
