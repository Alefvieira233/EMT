# SUPER RELATÓRIO — Análise Sênior Completa do SteelBIM

**Data:** 2026-05-31 · **Commit:** `99b8be7` (v2.8.9) · **Branch:** `claude/great-turing-Vqlig`
**Método:** 4 revisores sênior em paralelo (arquitetura/qualidade, segurança/licença, correção de domínio, testes/CI/release/docs) + métricas quantitativas. Análise por leitura estática — o ambiente não compila (.NET/Revit ausentes); o **build e os ~1.300 testes são validados pelo CI** (GitHub Actions + stubs Nice3point), atualmente **4/4 verde**.

---

## 1. Veredito executivo

> **Nota global: 7,6 / 10** — *Produto sério, maduro e bem arquitetado, com dívida bem localizada e visível. Já saiu de "esqueleto" para "quase market-ready". Os bloqueios restantes são poucos, conhecidos e endereçáveis incrementalmente — nenhum exige reescrita.*

| Dimensão | Nota | Resumo |
|---|:---:|---|
| 🏛️ Arquitetura & Qualidade & Manutenibilidade | **8.0** | Espinha dorsal rara no nicho; dívida concentrada em poucos arquivos-monstro |
| 🔐 Segurança & Licenciamento & Privacidade | **7.5** | Cutover ECDsa correto e completo; faltam guardas operacionais (CI) |
| 🧱 Correção de Domínio (Metálico/PF/CNC/Doc) | **7.0** | Núcleo metálico + NBR sólidos; **DSTV incompleto** e armadura sem disclaimers |
| 🧪 Testes / CI / Release / Docs | **7.5** | Boa cobertura e CI hardened; faltam gates (format/secret) e testes de integração |

**O que mudou recentemente (esta linha de trabalho — 22 commits):** "Cotar Treliça" (estava 100% inoperante) consertada; licença migrada de HMAC simétrico para **ECDsa P-256 assimétrico** (chave pública embarcada, privada só com o produtor); ModelCheck (unidade de volume ft³/m³), parsing decimal PF, MarcarPeças (perda de dado), hardening de boot, PII no crash dump, branding PF, cabeçalho DSTV ST na ordem padrão. Esses fixes elevaram a nota global de forma significativa.

---

## 2. Métricas

| | Valor |
|---|---|
| Código-fonte | **328 arquivos / 54.380 linhas** |
| Testes | 99 arquivos / 14.777 linhas — **895 `[Fact]` + 87 `[Theory]` (434 casos) ≈ ~1.330 testes** |
| Comandos / Services / Models / Janelas WPF | 51 / 45 / 56 / 47 |
| Qualidade dos testes | **1.570 `.Should()`** (FluentAssertions) vs 38 `Assert.True` — asserts significativos; **0 golden files frágeis** |
| Conformidade de convenções | 0 `Console.*`, 0 `TaskDialog` direto, 58/58 transações com `using`, **só 5 TODO/FIXME em 54k LOC** |
| Cobertura nullable | **76/328 arquivos (23%)** com `#nullable enable` |
| Governança | **11 ADRs**, 6 docs de auditoria, 4 reference-projects |
| **Hotspots (maiores arquivos)** | ListaMateriais **2.235**, PfRebar **2.168**, Cotas 1.240, DiagramaMontagem 1.222, App.cs 1.204, AutoVista 988 |

---

## 3. Pontos fortes (o que está excelente)

1. **`FerramentaCommandBase` torna licença, tratamento de erro e telemetria estruturalmente impossíveis de esquecer** num comando novo — `Execute()` sealed + `ExecuteCore` abstrato + license-gate + 3 níveis de catch + Stopwatch num único ponto. Só 2 comandos burlam a base, por design.
2. **`Result<T>` default-safe** (`default(Result)` = Ok, `Fail("")` lança) e **11 ADRs** justificando cada decisão estrutural — raríssimo num projeto deste porte.
3. **Cutover de licença ECDsa correto e completo:** assimétrico de verdade, fail-closed, privada fora do binário, **zero caminho HMAC alcançável**. Subiu o licenciamento de "brinquedo" (segredo no cliente) para "sério" — forja agora é computacionalmente inviável.
4. **Privacidade/LGPD madura:** Sentry `SendDefaultPii=false` + `PiiScrubber` abrangente; telemetria PostHog opt-in estrito; `SessionId` UUID v4 anônimo; `UserName` removido de log e crash dump.
5. **NBR 6118 (ancoragem PF) genuinamente bem implementado** — `fbd`, `lb`, `lb,min`, η1/η2/η3, traspasse — acima da média de mercado.
6. **"Cotar Treliça" pós-fix está geometricamente coerente** (classificação por altura reusada, altura por separação 2D, References legais).
7. **Boot defensivo** ("ribbon parcial logado > plugin que não carrega") e **CI hardened** (lock files, `if-no-files-found: error`, concurrency, Dependabot, release de signing defensivo).
8. **~1.300 testes com asserts significativos**, estratégia Strangler-Fig de helpers puros funcionando, sem golden files frágeis.

---

## 4. Achados consolidados por severidade

### 🔴 P0 (crítico) — **NENHUM no estado atual**
Os P0 conhecidos (Cotar Treliça quebrada, licença HMAC forjável) **já foram fechados** nesta linha de trabalho.

### 🟠 P1 (alto)
| # | Achado | Dimensão | Status |
|---|---|---|---|
| 1 | **`license.private.key` fora do `.gitignore`** — provider procura "ao lado do executável" → `git add .` poderia commitar a privada de produção (forja ilimitada) | Segurança | ✅ **CORRIGIDO nesta sessão** |
| 2 | **Sem secret-scanning no CI** (gitleaks/push-protection) — única defesa é o `.gitignore` | Segurança/CI | ⏳ recomendado |
| 3 | **`dotnet format` só roda no projeto de Testes**, não no principal (~300 arquivos sem gate de estilo) | CI | ⏳ recomendado |
| 4 | **`SetupBootstrapper.csproj` fora de toda solution e CI** — quebra do instalador só apareceria no release | CI/Release | ⏳ recomendado |
| 5 | **DSTV NC1 sem bloco AK (contorno externo)** — muitas controladoras recusam o arquivo | Domínio/CNC | ⏳ (geração é pura; validação PRECISA-REVIT) |
| 6 | **Bloco SC do DSTV fora da gramática NC1** — cortes de extremidade saem errados/ignorados | Domínio/CNC | ⏳ PRECISA-REVIT |
| 7 | **ADR-002 `IRevitContext` com 0% de adoção** — abstração "morta no papel" | Arquitetura | ⏳ decidir: adotar ou aposentar |

### 🟡 P2 (médio)
- **Go-live guard só loga, não falha o CI** → ✅ **mitigado nesta sessão** (teste `ChavePublicaEhPlaceholder == false` adicionado).
- **3 arquivos-monstro >2.000 LOC** (ListaMateriais, PfRebar) nos hotspots de churn → continuar Strangler-Fig.
- **Migração nullable em 23%** → `TreatWarningsAsErrors` cobre só 1/4 do código; priorizar top-10 services.
- **Sem testes de integração** (fakes de `IRevitContext`/`IUIDecisionService`) — exatamente a classe de bug que quebrou Cotar Treliça e Diagrama inteiros.
- **Dívida de testes "fantasma":** 3 arquivos `Compile Remove`d + ~50 `[Skip]` que inflam a percepção de cobertura (extrair os `*Report` aninhados para POCOs e reativar).
- **Crash dump local não passa pelo `PiiScrubber`** (`MachineName` + `ex.ToString()` com paths de cliente).
- **`MachineFingerprint` fraco/resetável** (MachineGuid + UserName, truncado a 64 bits) — barreira "anti-casual", não anti-adversário.
- **Bypass por patch de bool** (gate in-process sem anti-tamper) — teto inerente ao modelo offline; aceitar conscientemente ou ofuscar.
- **Armadura normativamente incompleta:** estribos sem zonas de adensamento NBR; bloco-2-estacas sem estribos/suspensão → exige **disclaimer forte na UI** ("verificar conforme projeto estrutural").
- **CHANGELOG `[Unreleased]` desatualizado** e comentário `LICENSE_HMAC_SECRET` obsoleto no `build.yml` (pós-cutover ECDsa).
- **`AppSettings` god-object** (57 props flat) → agrupar por feature.
- **Módulo PF em inglês** vs trunk em PT-BR (regra do CLAUDE.md violada por ~15 arquivos) → formalizar exceção ou normalizar.

### 🟢 P3 (nits)
Dead code HMAC (`LicenseSecretProvider` ainda compilado — mas usado pelo Sentry DSN), comentários históricos inline em excesso, duas `.sln` com escopos divergentes, `AssemblyAdjacentPath` como fonte da privada, multiplicador de gancho de estribo conservador vs NBR 9.4.6.1, placas face-based rejeitadas indevidamente.

---

## 5. Corrigido nesta sessão (resumo)

22 commits, CI 4/4 verde. Além dos fixes funcionais (Cotar Treliça, ModelCheck, PF parsing, MarcarPeças, boot, PII, branding PF, DSTV ST) e da migração de licença ECDsa, **neste relatório**:
- ✅ `.gitignore` agora bloqueia `license.private.key` / `*.private.key` / `*.key` (fecha o P1 #1).
- ✅ Teste de CI `LicenseKeysTests` que falha se a chave pública voltar a ser placeholder (mitiga o go-live guard).

---

## 6. Roadmap priorizado para 10/10

**Onda 1 — Segurança/Processo (baixo esforço, alto valor):**
1. ✅ `.gitignore` da chave privada *(feito)*.
2. Adicionar job **gitleaks** (ou Push Protection) + **CodeQL** no CI.
3. Estender `dotnet format --verify` ao `SteelBIM.csproj` (após uma limpeza inicial).
4. Incluir `SetupBootstrapper` na solution + job de CI dedicado.
5. Limpar CHANGELOG `[Unreleased]` e o comentário HMAC do `build.yml`.

**Onda 2 — DSTV/CNC (maior risco de imagem):**
6. Gerar bloco **AK** (contorno retangular da face `v` a partir de L×h) e mover ângulos de corte para os campos 17-20 do ST (remover o SC malformado). **Validar com 1 `.nc1` real do fabricante** ou test-cut.

**Onda 3 — Qualidade estrutural:**
7. Decidir o destino do `IRevitContext` (adotar incremental ou aposentar o ADR-002).
8. Harness de **testes de integração** com fakes (`IRevitContext`/`IUIDecisionService`) cobrindo os pipelines críticos (Cotar Treliça, Diagrama de Montagem).
9. Quebrar os 3 arquivos >2.000 LOC via Strangler-Fig; extrair os `*Report` aninhados e reativar os testes.
10. Avançar a migração nullable nos top-10 services.

**Onda 4 — Domínio/UX:**
11. **Disclaimers na UI** de armadura ("detalhamento gerado — verificar zonas de adensamento e suspensão conforme projeto") e de conexões ("geométrico/documental, sem verificação de capacidade").
12. Endurecer machine binding e crash-dump scrubbing (P2 de segurança).

**Externo (não-código):** cert de code-signing Sectigo (ADR-009) — último bloqueio de distribuição contra SmartScreen.

---

## 7. Conclusão

O SteelBIM é, hoje, um **produto de engenharia sério e bem fundamentado** — arquitetura disciplinada, licenciamento criptograficamente correto, privacidade madura, NBR bem implementada e ~1.300 testes com CI hardened. A nota **7,6/10** reflete um codebase maduro cuja dívida é **localizada, visível e sem nenhum P0 ativo**.

Os três temas que separam o produto de um "10/10" são claros: **(a) fechar os gates de CI** (secret-scanning, format no projeto principal, bootstrapper), **(b) completar a conformidade DSTV** (AK + SC, validado em máquina), e **(c) blindar a entrega de armadura/conexão com disclaimers normativos**. Nenhum deles é reescrita; todos são incrementais. Com a Onda 1+2 executadas, este produto está pronto para escalar a centenas de alunos com confiança.
