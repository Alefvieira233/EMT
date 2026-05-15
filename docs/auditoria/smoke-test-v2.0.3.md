# Smoke Test SteelBIM v2.0.3 — 2026-05-14

**Executor:** Alef
**Data/hora inicio:** _(preenchido apos teste)_
**Versao testada:** v2.0.3
**Setup utilizado:** `SteelBIM-Revit2025-Setup.exe`
**SHA256:** `3eb81285d4b4c5d1fdd6c695e4850ce0eff265f02f1913fe5b8d99aea003cbfb` (verificado pre-teste)
**Resultado geral:** _(PASS / FAIL com motivo, preenchido apos teste)_

---

## Estado pre-teste

- **Instalacao previa:** Nenhuma (fresh install)
- **Manifest `.addin`:** Ausente em `%AppData%\Autodesk\Revit\Addins\2025\`
- **DLL instalada:** Ausente
- **Logs antigos:** Ausentes (`%LocalAppData%\SteelBIM\logs\` nao existe)
- **HEAD da main:** `0f12cbe release(v2.0.3): fechamento pre-mercado`
- **Tag testada:** `v2.0.3`

> Estado limpo — sem residuos de instalacoes anteriores. Teste vai validar
> caminho de instalacao zerado.

---

## Checklist (10 passos)

### Setup do ambiente

- [ ] **Passo 1** — Desinstalacao antiga limpa (N/A neste caso, sem instalacao previa)
- [ ] **Passo 2** — Setup v2.0.3 instalou sem erro (SmartScreen "Mais informacoes -> Executar assim mesmo")
- [ ] **Passo 3** — Revit 2025 abre sem erro
- [ ] **Passo 4** — Aba "SteelBIM" aparece no ribbon (paineis: PF Construcao, PF Documentacao, PF Armaduras)
- [ ] **Passo 5** — Aba "Ferramentas ECC" aparece no ribbon (paineis: Modelagem, Estrutura, Vigas, Vista, Documentacao, Fabricacao, CNC, Verificacao, Montagem, Licenca)

### Validacao de versao

- [ ] **Passo 6** — Comando "Sobre" mostra **v2.0.3** (nao 2.0.0, nao 2.0.2, nao v1.x)

### Smoke funcional (modelo com vigas/pilares + 1+ diagonal idealmente)

- [ ] **Passo 7** — Nomear PF — cabecalho `[N elemento(s) — lista por familia/tipo para filtro; a ordem de numeracao e geometrica]` aparece no topo da lista filtrada
- [ ] **Passo 8** — Nomear PF — relatorio menciona vigas diagonais (`X viga(s) sem eixo definido foram numeradas por Id...`) — N/A se modelo nao tem diagonais
- [ ] **Passo 9** — Cortar Elementos — janela com escopo (Selecao/Vista/Modelo) + filtros funciona, aplicar nao crasha
- [ ] **Passo 10** — Ativar Licenca — janela abre sem crashar

---

## Logs capturados durante teste

_(preenchido pos-teste; trecho de `%LocalAppData%\SteelBIM\logs\steelbim-2026-05-14.log` filtrado por ERROR/WARN/EXCEPTION)_

```
(aguardando teste)
```

---

## Observacoes

_(campo livre — anote qualquer comportamento inesperado, screenshots, comparacoes vs v2.0.2, etc.)_

---

## Veredito

_(preenchido apos teste)_

- **Passos OK:** _/10
- **Passos FAIL:** _
- **Passos N/A:** _
- **Resultado:** _(PASS = 10/10 OK ou OK+N/A; FAIL = qualquer FAIL)_

### Se PASS
- Plugin pronto para apresentacao publica
- Recomendado: remover flag `--prerelease` da release v2.0.3 no GitHub
- Iniciar trabalho na pagina de vendas

### Se FAIL
- Identificar passo + screenshot + stacktrace dos logs
- Decidir: rollback para v2.0.2 (commit `3fab0cd`, release ja publicada como anterior) ou hotfix v2.0.4
- NAO desistir do v2.0.3 sem investigar causa raiz
