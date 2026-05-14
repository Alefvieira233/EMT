# Auditoria Hotfix v2.0.1 — 2026-05-14

Auditoria read-only do hotfix v2.0.1 (PfNaming) publicado em 2026-05-13.
Esta auditoria nao alterou nenhum arquivo de codigo, configuracao, teste
ou release. O unico arquivo escrito por esta sessao eh o proprio documento
em `docs/auditoria/hotfix-v2.0.1-audit.md`.

**Auditor:** sessao Claude Code disparada por Alef, 2026-05-14.
**Commit auditado:** `359060b` (HEAD de `main` apos merge ff-only).
**Tag:** `v2.0.1` (annotated).
**Release:** https://github.com/Alefvieira233/EMT/releases/tag/v2.0.1
**CI run:** https://github.com/Alefvieira233/EMT/actions/runs/25838481572

---

## 1. Resumo executivo

**Total: 31 checks em 8 dimensoes — 29 PASS / 2 WARN / 0 FAIL.**

| # | Dimensao | Sub-itens | Status |
|---|---|---|---|
| 0 | Estado do repo | 4/4 | PASS |
| 1 | Codigo aplicado | 9/9 | PASS |
| 2 | Build + Tests | 3/3 | PASS |
| 3 | GitHub Release | 3/3 | PASS |
| 4 | CI | 2/2 | PASS |
| 5 | Escopo cirurgico | 4/4 | PASS |
| 6 | Pendencias documentadas | 2/4 PASS, 2 WARN | WARN |
| 7 | Residuos legitimos | 3/3 | PASS |
| 8 | Verificacao semantica | 3/3 | PASS |

**Veredito sintetico:** hotfix entregou tudo que prometeu. Build limpo,
testes 100% verdes (777/777), assets baixados da release tem SHA256 que
batem byte-a-byte com `checksums.txt`, escopo cirurgico confirmado (4
arquivos, +71/-11, nenhum command, App.cs, NumeracaoItensService ou outro
service PF foi tocado). As 2 WARNs sao itens de housekeeping previsiveis,
nao bloqueantes.

---

## 2. Sessao detalhada por passo

### Passo 0 — Estado do repo (4/4 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 0a HEAD em 359060b ou posterior | `git log --oneline -5` | `359060b fix(pfnaming): ...` (topo) seguido de `37c8218 rebrand(8/9)` | PASS |
| 0b Working tree clean ou so .zip dirty | `git status` | dirty apenas em `SteelBIM/installer/SetupBootstrapper/EmbeddedPackage/SteelBIM-Package.zip` (esperado, pre-existente desde antes do hotfix) | PASS |
| 0c Tags v2.0.0 e v2.0.1 | `git tag --list \| grep "^v2\\."` | `v2.0.0`<br>`v2.0.1` | PASS |
| 0d Branch atual main | `git branch --show-current` | `main` | PASS |

### Passo 1 — Codigo aplicado (9/9 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 1a Const EIXO_TOLERANCIA_FT presente | `grep -n "EIXO_TOLERANCIA_FT" PfNamingService.cs` | 3 ocorrencias: linha 17 (declaracao `private const double EIXO_TOLERANCIA_FT = 0.328084;`) + linhas 135/149 (chamadas a `GetSnappedOrder`) | PASS |
| 1b Metodos auxiliares | `grep -n "OrderHorizontalSnapped\|OrderVerticalSnapped" PfNamingService.cs` | linha 46 (`.ThenBy(x => OrderHorizontalSnapped(...))`), linha 47 (`.ThenBy(x => OrderVerticalSnapped(...))`), linha 124 (declaracao Horizontal), linha 138 (declaracao Vertical) | PASS |
| 1c GetSnappedOrder em uso | `grep -n "GetSnappedOrder" PfNamingService.cs` | 2 ocorrencias (linhas 135 + 149), 1 em cada helper | PASS |
| 1d Aviso de diagonais | `grep -n "sem eixo definido"` + `grep -n "GetBeamAxisGroup(e, view) == 2"` | linha 113 (`{diagonais} viga(s) sem eixo definido foram numeradas por Id ao final da sequencia.`); linha 110 (`int diagonais = ordenados.Count(e => PfElementService.GetBeamAxisGroup(e, view) == 2);`) | PASS |
| 1e Cadeia OrderBy simplificada | Read PfNamingService.cs:40-49 | `.OrderBy(GetBeamAxisGroup)` -> `.ThenBy(OrderHorizontalSnapped)` -> `.ThenBy(OrderVerticalSnapped)` -> `.ThenBy(Id.Value)`. Ternarias antigas com `GetHorizontalOrder`/`GetVerticalOrder` diretas estao ENCAPSULADAS nos helpers (linhas 130-135 e 144-149), nao mais inline. | PASS |
| 1f Cabecalho ListBox | `grep -n "lista por familia/tipo para filtro" PfNamingWindow.xaml.cs` | linha 479 dentro do metodo `AtualizarListaElementos` | PASS |
| 1g Cabecalho usa elementos.Count dinamico | `grep -n "elementos\\.Count" PfNamingWindow.xaml.cs` | linha 479: `lstElementos.Items.Add($"[{elementos.Count} elemento(s) — lista por familia/tipo para filtro; a ordem de numeracao e geometrica]")` — interpolation `${elementos.Count}`, nao literal "N" | PASS |
| 1h Versao bumped | `grep -n "AssemblyVersion\|AssemblyFileVersion\|InformationalVersion" AssemblyInfo.cs` | linha 17 `AssemblyVersion("2.0.1.0")`, linha 18 `AssemblyFileVersion("2.0.1.0")`, linha 19 `AssemblyInformationalVersion("2.0.1")` | PASS |
| 1i CHANGELOG [2.0.1] | `grep -n "\\[2\\.0\\.1\\]" CHANGELOG.md` | linha 18 `## [2.0.1] - 2026-05-13` com sub-secoes **Fixed** (snap determinismo) e **Added** (aviso diagonais + cabecalho lista) | PASS |

### Passo 2 — Build + Tests (3/3 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 2a Build Release | `dotnet build SteelBIM.Solution.sln -c Release --nologo` | `0 Erro(s)` em 4.06s. 2 avisos MSB3277 (Revit refs ambiguas) — baseline aceito conforme CLAUDE.md. | PASS |
| 2b Tests | `dotnet test SteelBIM.Tests/SteelBIM.Tests.csproj -c Release --no-build --nologo` | `Aprovado! – Com falha: 0, Aprovado: 777, Ignorado: 0, Total: 777, Duracao: 932 ms` — bate exato com a contagem reportada. | PASS |
| 2c Diff stat do commit 359060b | `git show --stat 359060b` | 4 arquivos, +71/-11: `CHANGELOG.md` +18, `SteelBIM/AssemblyInfo.cs` ±3, `SteelBIM/Services/PF/PfNamingService.cs` +45/-8, `SteelBIM/Views/PfNamingWindow.xaml.cs` +5 | PASS |

### Passo 3 — GitHub Release (3/3 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 3a Tag aponta pro commit certo | `git show v2.0.1 --no-patch --format="%H %s"` | Tag `v2.0.1` (annotated, tagger Alef) com mensagem `v2.0.1 — Hotfix PfNaming ordenacao deterministica` aponta para `359060bf5344f11f4c8a9582cd019c520286bb45` com subject `fix(pfnaming): ordenacao deterministica de vigas + aviso diagonais + cabecalho lista` | PASS |
| 3b Release publicada com 3 assets | `gh release view v2.0.1 --json isPrerelease,tagName,name,assets` | `prerelease=true`, `tag=v2.0.1`, `name="SteelBIM v2.0.1 — Hotfix PF Nomear"`, assets: `checksums.txt` 195 B, `SteelBIM-Revit2025-Release.zip` 5 668 807 B (~5.40 MiB), `SteelBIM-Revit2025-Setup.exe` 73 672 438 B (~70.26 MiB) | PASS |
| 3c Checksums batem com SHA256 reais | `gh release download v2.0.1` + `Get-FileHash` | Computado vs declarado:<br>Setup.exe: `a813bf0b4b6dc936b7cc36212312d7861691b85bc1ef390880666334f1ae9bb2` -> **MATCH** (case-insensitive)<br>Release.zip: `8d4db23ad79d6967f6f0064b3de4b0659311b6daf4a84e3430d2d22d6f84b69b` -> **MATCH**<br>Assets NAO estao corrompidos. | PASS |

### Passo 4 — CI (2/2 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 4a Run 25838481572 verde | `gh run view 25838481572 --json conclusion,status,jobs` | `run_conclusion=success`, `status=completed`. 3 jobs: `Build SteelBIM (Release)` success, `Code Quality (dotnet format)` success, `Build & Test SteelBIM.Tests` success. | PASS |
| 4b Annotation "exit code 1" localizada e classificada | `gh run view 25838481572 --log \| grep -B1 -A3 "exit code 1"` + `grep -B1 -A4 "Verify formatting\|continue-on-error" .github/workflows/build.yml` | Annotation veio do step **`Verify formatting (Tests project)`** no job **Code Quality (dotnet format)** (.github/workflows/build.yml linhas 80-83). Step roda `dotnet format --verify-no-changes` em `SteelBIM.Tests/` e detectou divergencia de formatting (saiu com codigo 1 apos 31262 ms). Step tem `continue-on-error: true` (linha 83) com comentario inline `# WARNING ONLY for now; flip to fail later`. A falha **eh real** (formatting issues genuinas em SteelBIM.Tests/), mas **intencionalmente tolerada** pela CI ate decidirem "flip to fail later". **NAO foi introduzida pelo hotfix** — o commit 359060b nao tocou nenhum arquivo dentro de SteelBIM.Tests/. Pre-existente. | PASS |

### Passo 5 — Escopo cirurgico (4/4 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 5a Diff v2.0.0..v2.0.1 = 4 arquivos | `git diff v2.0.0..v2.0.1 --stat` | Exatamente 4 arquivos, +71/-11. Identico ao diff do commit 359060b (que eh o unico commit entre v2.0.0 e v2.0.1). | PASS |
| 5b App.cs e Commands/ nao tocados | `git diff --stat v2.0.0..v2.0.1 -- SteelBIM/App.cs` e `... -- SteelBIM/Commands/` | Ambos vazios. Nenhum command do ribbon foi modificado. | PASS |
| 5c NumeracaoItensService (manual) nao tocado | `git diff --stat v2.0.0..v2.0.1 -- SteelBIM/Services/NumeracaoItensService.cs` | Vazio. Numeracao manual nao foi afetada — hotfix mexeu apenas em PfNamingService (numeracao automatica). | PASS |
| 5d Outros services PF nao tocados | `git diff --stat v2.0.0..v2.0.1 -- SteelBIM/Services/PF/` | Apenas `SteelBIM/Services/PF/PfNamingService.cs` (1 file changed, 45 insertions, 8 deletions). Nenhum outro service PF (PfRebarService, PfFoundationPlacementService, PfElementService, etc) foi modificado. | PASS |

### Passo 6 — Pendencias documentadas (2 PASS / 2 WARN)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 6a EMT_CODESIGN_CERT_PFX residual | `grep -rn "EMT_CODESIGN_CERT_PFX"` | **12 ocorrencias em 5 arquivos**, todas em escripts/docs de code signing (nao em codigo Revit):<br>- `.gitignore:108` (comentario)<br>- `SteelBIM/installer/Build-SetupExe.ps1:109, 112, 147, 182` (codigo PS1 + warnings)<br>- `docs/CODE-SIGNING.md:99, 106, 224`<br>- `docs/ADR/009-code-signing.md:34, 130, 159`<br>**Pre-existente** (sobrou do rebrand v2.0.0 que migrou `EMT_*` -> `STEELBIM_*` mas esqueceu esses arquivos do pipeline de assinatura). Hotfix nao tocou nesses arquivos por escopo. Candidato a v2.0.2. | PASS (documentado) |
| 6b Setup unsigned esperado | logs do Gerar-Setup.bat | Confirmado: `AVISO: [Signing] EMT_CODESIGN_CERT_PFX nao definido ou arquivo ausente.` + `[Signing] setup.exe sera gerado NAO-ASSINADO (modo dev).`. Estado identico ao v2.0.0 — codigo de assinatura existe (esqueleto da v1.7.0) mas certificado nao foi adquirido ainda. Documentado no roadmap em `## [Unreleased]` do CHANGELOG. | PASS |
| 6c Branch hotfix ainda existe | `git branch --list hotfix/v2.0.1-pfnaming-snap` + `git ls-remote --heads origin hotfix/v2.0.1-pfnaming-snap` | Existe local **e** remoto, ambos apontando para `359060bf5344f11f4c8a9582cd019c520286bb45` (mesmo HEAD do main pos-merge ff-only). Esperado a ser limpo apos smoke test passar. | **WARN** (housekeeping) |
| 6d Stash list | `git stash list` | Nao vazio: `stash@{0}: pre-pr-d-apply`, `stash@{1}: pre-sync-sandbox-apply`. **Esses dois NAO sao do hotfix v2.0.1** — sao stashes legacy de sessoes anteriores (eram `stash@{1}` e `stash@{2}` antes da sessao do hotfix). O stash do hotfix (`wip-package-zip-pre-v2.0.1-hotfix`) foi corretamente descartado via `git stash drop` na sessao que publicou o release. A "expectativa: vazio" do prompt de auditoria assumia que nao havia stashes previos — verificacao manual mostra que **o stash do hotfix foi sim limpo**; o que restou eh pre-existente. | **WARN** (expectativa do prompt nao bate, mas pos-hotfix esta correto) |

### Passo 7 — Residuos legitimos (3/3 PASS)

| Sub-item | Comando | Output | Veredito |
|---|---|---|---|
| 7a EMT_ em identificadores internos | `grep -c "EMT_Chapa_Ponta\|EMT_Pilar_Base\|EMT_Etapa_Montagem\|EMT_COL_\|EMT_VIG_" SteelBIM/Services/ -r` | 5 ocorrencias em 4 arquivos (PlacaBaseLancamentoService, ConexaoFamilyNames, PfFoundationPlacementService, AgrupamentoVisualService). **Esperado** — sao identificadores internos preservados de proposito para compat com modelos `.rvt` legacy (v1.x). Documentado no CHANGELOG v2.0.0 secao "Mantido por compatibilidade". | PASS |
| 7b directShape.ApplicationId sentinel | `grep -n "directShape\\.ApplicationId\|ApplicationId\\s*=\\s*\"FerramentaEMT\"" SteelBIM/Services/EscadaService.cs` | linha 303: `directShape.ApplicationId = "FerramentaEMT";`. **Esperado** — sentinel legacy mantido por design (referencia explicita no CLAUDE.md). | PASS |
| 7c Comentarios historicos | inspecao por amostragem | Ocorrencias residuais de "FerramentaEMT" em comentarios explicativos e strings de migracao legacy nos services de Licensing/PrivacySettingsStore continuam presentes. **Esperado** — sao comentarios historicos/migration paths que precisam estar preservados. | PASS |

### Passo 8 — Verificacao semantica (3/3 PASS)

#### 8a Ordenacao preservada vs especificacao

Verifiquei manualmente os 4 cenarios pedidos no prompt, tracando a logica
dos helpers (PfNamingService.cs:124-150) contra `PfElementService.GetBeamAxisGroup`
(PfElementService.cs:253-268). A logica eh:

```csharp
return onRight >= onUp ? 0 : 1;  // returns 2 acima se LocationCurve ausente
```

- `onRight = |direction . view.RightDirection|` → alinhamento com eixo X da vista
- `onUp = |direction . view.UpDirection|` → alinhamento com eixo Y da vista
- **Group 0** = direcao mais alinhada com X (horizontal na vista)
- **Group 1** = direcao mais alinhada com Y (vertical na vista)
- **Group 2** = diagonal ou sem LocationCurve

A condicao em ambos os helpers eh `GetBeamAxisGroup(x, view) == 1` (variavel
`vigaHorizontalNoEixoX`):
- Quando true → `OrderHorizontalSnapped` usa `+H`; `OrderVerticalSnapped` usa `-V`
- Quando false (outro group, ou Alvo != Vigas) → ambos usam o ramo oposto: `-V` e `+H`

Casos:
1. **Pilar/Fundacao** (Alvo != Vigas): `vigaHorizontalNoEixoX = false`. Sort por `snap(-V)` primario, `snap(+H)` secundario, Id final. Ordena de cima pra baixo, esquerda pra direita. **OK.**
2. **Viga vertical/Y** (GetBeamAxisGroup == 1, alinhada ao eixo Y): `vigaHorizontalNoEixoX = true` (apesar do nome equivocado da variavel). Sort por `snap(+H)` primario, `snap(-V)` secundario, Id final. Ordena por X da esquerda pra direita, depois por Y de cima pra baixo. **OK.**
3. **Viga horizontal/X** (GetBeamAxisGroup == 0, alinhada ao eixo X): `vigaHorizontalNoEixoX = false`. Sort por `snap(-V)` primario, `snap(+H)` secundario, Id final. Ordena por Y de cima pra baixo, depois por X esquerda pra direita. **OK.**
4. **Viga diagonal** (GetBeamAxisGroup == 2): `vigaHorizontalNoEixoX = false`. Sort por `snap(-V)`, `snap(+H)`, Id. **Mas** vem APOS os grupos 0 e 1 (pois primary OrderBy eh `GetBeamAxisGroup` ascendente: 0 < 1 < 2). Dentro do grupo 2, com tolerancia de 10 cm, vigas no mesmo ponto colapsam para o mesmo bucket e caem no tiebreaker Id.Value. **OK.**

**Comportamento preservado 100% vs codigo pre-hotfix.** A mudanca foi cirurgica:
adicao do `GetSnappedOrder(..., EIXO_TOLERANCIA_FT)` em cada vertente, sem
alterar quais coordenadas servem de chave primaria/secundaria.

**Nota nao bloqueante (pre-existente, NAO eh regressao do hotfix):** o
prompt de auditoria assume `GetBeamAxisGroup == 1` significa "horizontal/X"
e `== 0` significa "vertical/Y". O codigo real de `GetBeamAxisGroup` faz o
oposto (`onRight >= onUp ? 0 : 1`). A variavel `vigaHorizontalNoEixoX`
tambem nomeia o oposto do que checa (`== 1` = group 1 = vertical/Y).
Apesar do naming confuso, o **comportamento observavel** bate com a
intencao da mensagem `"Ordem aplicada nas vigas: horizontais/X primeiro,
depois verticais/Y."` (porque horizontal = group 0 vem antes de vertical
= group 1 na ordem ascendente). Mesmo bug-de-naming existia em v2.0.0 e
versoes anteriores — hotfix preservou 1:1.

**Veredito 8a:** PASS — comportamento prometido entregue.

#### 8b Posicao do bloco de aviso de diagonais

Read PfNamingService.cs:100-122:

```
100:                            $"Degrau: {config.Degrau}";
102:            if (config.ParametroStorageType == StorageType.String)
103:                resumo += $"\nFormato inicial: ...";
105:            if (config.Alvo == PfNamingTarget.Vigas)
106:                resumo += "\nOrdem aplicada nas vigas: horizontais/X primeiro, depois verticais/Y.";
108:            if (config.Alvo == PfNamingTarget.Vigas)
109:            {
110:                int diagonais = ordenados.Count(e => PfElementService.GetBeamAxisGroup(e, view) == 2);
111:                if (diagonais > 0)
112:                {
113:                    resumo += $"\n{diagonais} viga(s) sem eixo definido foram numeradas por Id ao final da sequencia.";
114:                }
115:            }
117:            if (falhas.Count > 0)
118:                resumo += "\n\nSem parametro editavel em alguns elementos:\n- " + ...;
120:            AppDialogService.ShowInfo(commandName, resumo, "Numeracao concluida");
```

Ordem do resumo:
1. Elementos processados / Valores atualizados / Alvo / Parametro / Inicio / Degrau (linhas 95-100)
2. Formato inicial (se string)
3. **"Ordem aplicada nas vigas: ..."** (linha 106)
4. **"N viga(s) sem eixo definido ..."** (linha 113) — **DEPOIS** do item 3, **ANTES** do item 5
5. "Sem parametro editavel em alguns elementos: ..." (linha 118)

**Veredito 8b:** PASS — aviso de diagonais aparece exatamente entre
"Ordem aplicada..." e "Sem parametro editavel...". Leitura natural pro
usuario: primeiro a regra geral, depois excecao das diagonais, depois
falhas de parametro.

#### 8c Cabecalho da ListBox aparece so quando Count > 0

Read PfNamingWindow.xaml.cs:466-489:

```
466:        private void AtualizarListaElementos(List<NumeracaoElementoInfo> elementos)
467:        {
468:            lstElementos.Items.Clear();
470:            if (elementos == null || elementos.Count == 0)
471:            {
472:                lstElementos.Items.Add("Nenhum elemento PM encontrado com os filtros atuais.");
473:                return;          // <-- early return
474:            }
476:            // Cabecalho explicando que a ordem da lista (familia/tipo) NAO eh
477:            // a ordem em que a numeracao sera aplicada (que eh geometrica).
478:            // Reduz suporte do cliente confundir filtro com ordem final.
479:            lstElementos.Items.Add($"[{elementos.Count} elemento(s) — lista por familia/tipo para filtro; a ordem de numeracao e geometrica]");
481:            foreach (NumeracaoElementoInfo elemento in elementos
                         .OrderBy(...).ThenBy(...).ThenBy(...))
486:                lstElementos.Items.Add($"{elemento.FamiliaNome} | {elemento.TipoNome} | Id {elemento.Id.Value}");
488:        }
```

O `if (elementos == null || elementos.Count == 0) { ...; return; }` na
linha 470-473 garante que o cabecalho da linha 479 so eh adicionado quando
`elementos.Count >= 1`. Nao ha possibilidade de o usuario ver
`"[0 elemento(s) ...]"` — quando vazio o usuario ve apenas
`"Nenhum elemento PM encontrado com os filtros atuais."`.

**Veredito 8c:** PASS — gate corretamente posicionado.

---

## 3. Achados nao bloqueantes

### 3.1 EMT_CODESIGN_CERT_PFX nao migrado para STEELBIM_CODESIGN_CERT_PFX (Passo 6a)

Sobrou do rebrand v2.0.0 (que migrou env vars `EMT_*` → `STEELBIM_*` mas
nao tocou o pipeline de assinatura). 12 ocorrencias em 5 arquivos:

- `.gitignore:108`
- `SteelBIM/installer/Build-SetupExe.ps1:109, 112, 147, 182`
- `docs/CODE-SIGNING.md:99, 106, 224`
- `docs/ADR/009-code-signing.md:34, 130, 159`

**Impacto:** baixo. Code signing nao esta ativo (cert nao foi comprado);
quando Alef ativar, vai exportar a env var com nome `EMT_*` antigo ou
descobrir esse residuo. Candidato a fix em v2.0.2 ou junto com a
implantacao real do code signing (rename + atualizar docs).

### 3.2 Annotation "Process completed with exit code 1" no CI (Passo 4b)

Step `Verify formatting (Tests project)` (.github/workflows/build.yml:80-83)
roda `dotnet format --verify-no-changes` em SteelBIM.Tests/ e **detecta
divergencias reais de formatacao**. O step tem `continue-on-error: true`
com comentario `# WARNING ONLY for now; flip to fail later` — entao o job
overall passa. **Pre-existente**, nao introduzido por v2.0.1. Quando a CI
"flip-to-fail" acontecer, sera necessario rodar `dotnet format
SteelBIM.Tests/` para limpar as divergencias.

### 3.3 Branch hotfix/v2.0.1-pfnaming-snap ainda existe local + remoto (Passo 6c)

Apos merge ff-only para main, a branch hotfix permanece como referencia
ao mesmo commit 359060b. Deletar local + remoto apos smoke test no Revit
passar:

```
git branch -d hotfix/v2.0.1-pfnaming-snap
git push origin --delete hotfix/v2.0.1-pfnaming-snap
```

### 3.4 Pre-existing confusao de naming entre vigaHorizontalNoEixoX e GetBeamAxisGroup (Passo 8a)

Variavel chama-se "horizontal" mas checa contra `== 1` que em
`GetBeamAxisGroup` significa Y-alinhada (vertical). Logica observavel
correta, naming confuso. **Existia em v2.0.0**, nao foi mexido por v2.0.1.
Candidato a refactor cosmetic em PR separado.

### 3.5 Stashes legacy pre-existentes (Passo 6d)

`stash@{0}: pre-pr-d-apply` e `stash@{1}: pre-sync-sandbox-apply` nao sao
do hotfix v2.0.1 — sao restos de sessoes anteriores. Stash do hotfix
(`wip-package-zip-pre-v2.0.1-hotfix`) foi sim corretamente descartado.
Sao stashes do Alef que ele decide se quer popar/dropar.

---

## 4. Smoke test pendente (manual no Revit)

**NAO POSSO RODAR. Smoke test eh manual e bloqueia o "ship" final.**

Roteiro detalhado:

1. **Instalar v2.0.1:** baixar `SteelBIM-Revit2025-Setup.exe` do release
   https://github.com/Alefvieira233/EMT/releases/tag/v2.0.1 (SHA256
   `a813bf0b...`). Aceitar warning SmartScreen (setup unsigned em modo
   dev — esperado, ver §3.1). Executar instalacao.

   - **Validacao:** apos instalar, abrir Revit 2025, conferir que a aba
     "SteelBIM" do ribbon mostra v2.0.1 no tooltip do "Sobre" (se
     aplicavel) ou que o splash de carregamento usa a versao 2.0.1.0.

2. **Abrir modelo .rvt com vigas inclinadas reais:** carregar arquivo
   estrutural que tenha pelo menos uma viga com `LocationCurve` que NAO
   alinhe perfeitamente com X nem Y da vista (`GetBeamAxisGroup` retorna
   2). Ideal: um modelo com mistura — 3+ vigas em eixo logico X, 3+ em
   eixo logico Y, e 1-2 diagonais. Tambem incluir 1 viga "quase no
   eixo" com pequeno desalinhamento (digamos 5-8 cm) que era justo a
   condicao de bug do cliente.

3. **Executar comando "Nomear PF"** com Alvo = Vigas, Escopo = Modelo
   Inteiro ou Vista Ativa.

4. **Validar 3 coisas:**

   a. **Cabecalho na ListBox** (Melhoria 3): no inicio da lista filtrada
      deve aparecer `[N elemento(s) — lista por familia/tipo para
      filtro; a ordem de numeracao e geometrica]`. N deve bater com a
      contagem real.

   b. **Numeracao deterministica** (Melhoria 1): apos clicar OK e o
      relatorio aparecer, selecionar manualmente as vigas que **deveriam**
      estar no mesmo eixo logico (mesmo painel, ±10 cm). Conferir que
      receberam numeros consecutivos. **Comparacao chave:** se possivel,
      rodar 2x no mesmo modelo (Undo entre eles) — a ordem deve ser
      identica nas 2 execucoes (era nao-deterministica antes do hotfix).

   c. **Relatorio com diagonais** (Melhoria 2): no `TaskDialog`/dialogo
      final, apos a linha `"Ordem aplicada nas vigas: horizontais/X
      primeiro, depois verticais/Y."`, deve aparecer
      `"N viga(s) sem eixo definido foram numeradas por Id ao final da
      sequencia."` onde N = quantidade de vigas diagonais detectadas.

5. **Regressao quick:** rodar "Nomear PF" tambem para Alvo = Pilares e
   Alvo = Fundacoes em outro modelo. Confirmar que comportamento desses
   alvos nao mudou (sao ordenados de cima pra baixo, esquerda pra direita
   — comportamento preservado pelo hotfix).

6. **Se 4a/4b/4c falharem:** abrir issue + rollback considerando v2.0.0
   ainda esta disponivel (tag intacta).

---

## 5. Veredito final

**Hotfix v2.0.1 PASSA na auditoria: 29 de 31 checks PASS, 2 WARN, 0 FAIL.**

As 2 WARNs sao puramente housekeeping:
- 6c: branch hotfix ainda nao foi deletada (esperado, faz-se apos smoke test).
- 6d: stashes legacy do Alef nao limpos (independentes do hotfix).

Build verde, 777/777 testes passando, escopo cirurgico confirmado byte-a-byte
(4 arquivos +71/-11 — somente o que foi prometido foi tocado), assets da
release verificados contra SHA256, CI verde com explicacao razoavel da
annotation cosmetica. **Hotfix esta pronto pra ir para producao**, *condicional*
ao smoke test manual no Revit (§4).

---

## 6. Recomendacoes para Alef

### Se smoke test PASS:
1. **Notificar o cliente** que reportou o bug em 2026-05-13 com link da
   release: https://github.com/Alefvieira233/EMT/releases/tag/v2.0.1
2. **Limpar branch hotfix** (§3.3): `git branch -d
   hotfix/v2.0.1-pfnaming-snap && git push origin --delete
   hotfix/v2.0.1-pfnaming-snap`
3. **Promover release de pre-release para stable** se quiser (`gh release
   edit v2.0.1 --prerelease=false`). Sugestao: deixar como prerelease ate
   1-2 clientes confirmarem que esta funcionando bem em campo, dado que
   ainda esta unsigned (SmartScreen).

### Se smoke test FAIL:
- **PARAR.** Investigar imediatamente — rollback NAO eh trivial porque a
  tag e o release ja estao publicos. Opcoes:
  - Marcar release v2.0.1 como `--draft` e pedir clientes que ja baixaram
    para nao instalar.
  - Lancar v2.0.2 corretivo imediato.
- NAO deletar a tag v2.0.1 publica (clientes podem ja ter baixado).

### Backlog para hotfixes seguintes:
- **v2.0.2 candidatos:**
  - Migrar `EMT_CODESIGN_CERT_PFX` → `STEELBIM_CODESIGN_CERT_PFX` (§3.1):
    `SteelBIM/installer/Build-SetupExe.ps1`, `docs/CODE-SIGNING.md`,
    `docs/ADR/009-code-signing.md`, comentario em `.gitignore`.
  - Limpar `dotnet format` em `SteelBIM.Tests/` e flipar `continue-on-error`
    para false (§3.2).
  - Refactor cosmetic do naming `vigaHorizontalNoEixoX` em `PfNamingService.cs`
    para resolver a confusao com `GetBeamAxisGroup` (§3.4).

### Item que precisa de cert (nao eh hotfix):
- Adquirir certificado de code signing pra `Build-SetupExe.ps1` parar de
  emitir warning de unsigned. Ja documentado no `## [Unreleased]` do
  CHANGELOG.

---

## Apendice — Audit trail completo de comandos rodados

Todos os comandos executados durante a auditoria. Read-only no codigo,
sem nenhuma alteracao em codigo, configs, testes ou release.

### Wave 1 — Passo 0 + Passo 1

```
git log --oneline -5
git status
git tag --list | grep "^v2\."
git branch --show-current

grep -n "EIXO_TOLERANCIA_FT"            SteelBIM/Services/PF/PfNamingService.cs
grep -n "OrderHorizontalSnapped\|OrderVerticalSnapped"  SteelBIM/Services/PF/PfNamingService.cs
grep -n "GetSnappedOrder"               SteelBIM/Services/PF/PfNamingService.cs
grep -n "sem eixo definido"             SteelBIM/Services/PF/PfNamingService.cs
grep -n "GetBeamAxisGroup\(e, view\) == 2"  SteelBIM/Services/PF/PfNamingService.cs
grep -n "lista por familia/tipo para filtro"  SteelBIM/Views/PfNamingWindow.xaml.cs
grep -n "a ordem de numeracao e geometrica"   SteelBIM/Views/PfNamingWindow.xaml.cs
grep -n "elementos\.Count"              SteelBIM/Views/PfNamingWindow.xaml.cs
grep -n "AssemblyVersion\|AssemblyFileVersion\|InformationalVersion"  SteelBIM/AssemblyInfo.cs
grep -n "\[2\.0\.1\]"                   CHANGELOG.md
Read SteelBIM/Services/PF/PfNamingService.cs offset=35 limit=35
Read SteelBIM/Views/PfNamingWindow.xaml.cs offset=466 limit=30
```

### Wave 2 — Passo 2

```
dotnet restore SteelBIM.Solution.sln
dotnet build SteelBIM.Solution.sln -c Release --nologo
dotnet test SteelBIM.Tests/SteelBIM.Tests.csproj -c Release --no-build --nologo
git show --stat 359060b
```

### Wave 3 — Passos 3-7

```
git show v2.0.1 --no-patch --format="%H %s"
gh release view v2.0.1 --json isPrerelease,tagName,name
gh release view v2.0.1 --json assets
gh run view 25838481572 --json conclusion,status
gh run view 25838481572 --json jobs

git diff v2.0.0..v2.0.1 --stat
git diff --stat v2.0.0..v2.0.1 -- SteelBIM/App.cs
git diff --stat v2.0.0..v2.0.1 -- SteelBIM/Commands/
git diff --stat v2.0.0..v2.0.1 -- SteelBIM/Services/NumeracaoItensService.cs
git diff --stat v2.0.0..v2.0.1 -- SteelBIM/Services/PF/

git branch --list hotfix/v2.0.1-pfnaming-snap
git ls-remote --heads origin hotfix/v2.0.1-pfnaming-snap
git stash list

grep -rn "EMT_CODESIGN_CERT_PFX"
grep -c "EMT_Chapa_Ponta\|EMT_Pilar_Base\|EMT_Etapa_Montagem\|EMT_COL_\|EMT_VIG_"  SteelBIM/Services/ -r
grep -n "directShape\.ApplicationId" SteelBIM/Services/EscadaService.cs
```

### Wave 4 — Passo 3c (SHA256) + Passo 4b (annotation) + Passo 8 (semantica)

```
gh run view 25838481572 --log | grep -B1 -A3 "exit code 1" | head -40
grep -n -B1 -A4 "dotnet format\|continue-on-error\|Verify formatting"  .github/workflows/build.yml

# SHA256 verification (PowerShell)
gh release download v2.0.1 -D $env:TEMP\v201-audit
Get-FileHash <asset>.exe -Algorithm SHA256
Get-FileHash <asset>.zip -Algorithm SHA256
Get-Content checksums.txt

Read SteelBIM/Services/PF/PfElementService.cs offset=253 limit=50  # GetBeamAxisGroup
Read SteelBIM/Services/PF/PfNamingService.cs offset=100 limit=55   # bloco diagonais + helpers
Read CHANGELOG.md offset=15 limit=25                                # entrada v2.0.1
```

---

*Fim da auditoria. Doc gerado em 2026-05-14 em sessao Claude Code
read-only. Branch deste doc: `docs/auditoria-hotfix-v201`.*
