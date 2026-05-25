<!--
  Obrigado pela contribuicao. Complete as secoes abaixo o melhor possivel.
  Sinta-se livre para remover secoes irrelevantes.
-->

## Descricao

<!-- O que esta mudanca faz? Por que e necessaria? -->

## Tipo

- [ ] feat — nova funcionalidade
- [ ] fix — correcao de bug
- [ ] refactor — refatoracao sem mudanca de comportamento
- [ ] docs — so documentacao
- [ ] test — adicao/ajuste de testes
- [ ] ci — mudanca no workflow de CI
- [ ] chore — tarefa interna
- [ ] security — melhoria de seguranca
- [ ] arch — mudanca arquitetural (requer ADR)

## Checklist

- [ ] Segui as convencoes de commit (Conventional Commits)
- [ ] `dotnet test SteelBIM.Tests/SteelBIM.Tests.csproj` passa local
- [ ] `dotnet build SteelBIM/SteelBIM.csproj -c Release` passa (TreatWarningsAsErrors)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Testei manualmente no Revit quando a mudanca toca em codigo Revit-bound
- [ ] Atualizei o `CHANGELOG.md` (secao `[Unreleased]`)
- [ ] Atualizei documentacao afetada (README, comentarios XML, docs/)
- [ ] Se mudanca arquitetural: abri ADR em `docs/ADR/`
- [ ] Se afeta seguranca: alinhado com [SECURITY.md](../SECURITY.md) threat model

## Impacto

- [ ] Sem breaking change (compat 100% com licencas/projetos existentes)
- [ ] Breaking change documentado abaixo
- [ ] Afeta performance (detalhar abaixo)

## Como testar

<!-- Passos para o reviewer reproduzir a mudanca -->

## Screenshots / logs (se aplicavel)
