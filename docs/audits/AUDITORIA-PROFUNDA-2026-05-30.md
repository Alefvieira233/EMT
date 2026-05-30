# Auditoria Profunda — SteelBIM v2.8.8 → v2.8.9

**Data:** 2026-05-30
**Escopo:** codebase inteiro (~326 arquivos `.cs`, ~54k LOC, ~1.2k testes)
**Método:** 6 agentes de auditoria sênior em paralelo, cobrindo domínios
não-sobrepostos, seguidos de verificação manual dos achados de maior severidade
e correção dos itens de alta confiança.

> **Restrição do ambiente:** esta auditoria foi feita por **leitura e raciocínio**,
> sem compilar nem executar o Revit (o ambiente não tem .NET SDK nem as DLLs do
> Revit 2025). Os achados de lógica pura são confiáveis e foram cobertos por
> testes; os achados marcados **PRECISA-REVIT** descrevem comportamento da API
> que precisa de validação num modelo real antes de fechar. O build e a suíte de
> testes são verificados pelo CI (GitHub Actions, stubs Nice3point — ver ADR-005).

---

## 1. Veredito executivo

O SteelBIM é um produto **maduro e de alta qualidade de engenharia**: quality
gates (`TreatWarningsAsErrors` em Release), build reproduzível (`packages.lock`),
CI com stubs Revit, 10 ADRs, Serilog assíncrono, Sentry, telemetria opt-in
LGPD-aware, sistema de licença com DPAPI, migração nullable gradual e uma
disciplina exemplar de extrair helpers puros para testar sem Revit.

A auditoria encontrou **1 função completamente quebrada** (Cotar Treliça), **2
bugs de unidade/dado de campo**, várias **inconsistências de documentação** e
**2 questões estratégicas** que exigem decisão do Alef (licenciamento e DSTV).
Nada disso é incomum para um produto nesta fase; o conjunto abaixo leva o produto
de "muito bom" para "pronto para escalar a centenas de alunos".

| Severidade | Achados | Corrigidos na v2.8.9 | Backlog |
|---|---:|---:|---:|
| P0 | 4 | 2 | 2 (1 decisão, 1 arquitetura) |
| P1 | ~10 | 4 | ~6 (maioria PRECISA-REVIT) |
| P2 | ~14 | 3 | ~11 |
| P3 | ~10 | docs | resto |

---

## 2. Corrigido na v2.8.9 (alta confiança)

### 2.1 Cotar Treliça — estava 100% inoperante (P0) ✅
Raiz: o passo 4 (`ExtrairNosBanzo`) reclassificava barras com
`ClassificarPorInclinacao`, que **nunca** devolve `BanzoSuperior`/`BanzoInferior`
→ comparação sempre falsa → banzos sempre vazios → **pipeline abortava para toda
treliça**. Corrigido reutilizando a classificação por altura do passo 3 + helper
puro novo `TrelicaGeometria.ColetarNosDoBanzo` (testado). Pacote de 7 correções
no pipeline — detalhe completo no CHANGELOG `[2.8.9]`.
Arquivos: `Services/Trelica/CotarTrelicaService.cs`, `TrelicaRevitHelper.cs`,
`TrelicaGeometria.cs`; testes em `SteelBIM.Tests/Services/Trelica/TrelicaGeometriaTests.cs`.

### 2.2 Verificar Modelo — falsos positivos de sobreposição (P1) ✅
`OverlappingElementsRule`: `intersection.Volume` (pés³) comparado a limiar em m³
sem conversão → ~35× mais sensível. Corrigido convertendo para m³.
Arquivo: `Services/ModelCheck/ModelCheckRules/OverlappingElementsRule.cs`.

### 2.3 Parsing decimal das janelas PF (P1) ✅
`PfEstacaRebarWindow`/`PfColumnStirrupsWindow` usavam parsers próprios (um tentava
`CurrentCulture` primeiro, violando a regra de ouro e dando NRE em texto null).
Padronizado em `SteelBIM.Utils.NumberParsing`.

### 2.4 Marcar Peças — perda de dado (P2) ✅
`GravarMarca` checava só `param.AsString()` (null em parâmetro numérico) →
sobrescrevia marca existente mesmo com `SobrescreverExistentes=false`. Agora
`AsString() ?? AsValueString()`.

### 2.5 Hardening de boot + LGPD ✅
- `App.OnStartup`: construção do ribbon protegida por try/catch raiz (falha de um
  botão não desabilita mais o add-in inteiro).
- `CrashReporter`: `Environment.UserName` (PII) removido do crash dump local.
- `TravamentoService`: `catch {}` genérico → só `OperationCanceledException`.

### 2.6 Sincronização de documentação ✅
`CLAUDE.md`, `README.md` (badge + contradição 1223 vs 1241 + comandos 48→50),
`docs/ROADMAP.md`, `SECURITY.md` atualizados para v2.8.9. Órfãos v1.6.0 movidos
para `docs/historico/`.

---

## 3. Backlog — decisões estratégicas (precisam do Alef)

### 3.1 [P0] Licenciamento: esquema HMAC simétrico é forjável pelo cliente
**Local:** `Licensing/KeySigner.cs`, `LicenseSecretProvider.cs`, `tools/EmtKeyGen/`.
**Problema:** o segredo que **verifica** a chave é o **mesmo** que a **assina**
(HMAC-SHA256, simétrico) e precisa estar no plugin instalado para validar offline.
Qualquer aluno técnico lê esse segredo do próprio disco
(`%LOCALAPPDATA%\SteelBIM\license.secret` ou ao lado do `.dll`), recompila o
EmtKeyGen (código aberto no repo) e **emite chaves ilimitadas** para qualquer
e-mail/validade. Indetectável e irreversível sem rotação de segredo (que
invalidaria todos os clientes legítimos).
**Pontos bons já existentes:** nenhum segredo commitado; DPAPI impede edição
ingênua do `.lic`; comparação HMAC em tempo constante; fail-closed em todos os
erros.
**Recomendação:** migrar para **assinatura assimétrica** (Ed25519 ou RSA-2048):
EmtKeyGen assina com chave **privada** (nunca sai da máquina do Alef); o plugin
embute só a chave **pública** e verifica. O segredo deixa de existir no cliente.
Opcionalmente, ativação online leve para permitir revogação. **Não implementado
nesta release — muda o formato da chave e exige reemissão; precisa da sua decisão.**

### 3.2 [P1] DSTV/NC1 não-conforme à spec
**Local:** `Services/CncExport/DstvFileWriter.cs`.
**Problema:** o `.nc1` emitido diverge da spec NC1 (ordem de campos do bloco ST;
blocos AK/BO/SC simplificados). Abre como texto mas a maioria das máquinas/leitores
CNC recusa ou lê dimensões erradas — provável causa do "CNC não consigo avaliar".
Os testes atuais só fixam o formato simplificado, não validam contra a spec.
**Recomendação:** implementar ST/EN/AK/BO/SC conforme DSTV 7ª edição, validando
contra **um arquivo NC1 de referência real do escritório** numa máquina/visualizador.
**Precisa de Revit + arquivo de referência — não dá para fechar sem isso.**

---

## 4. Backlog — PRECISA-REVIT (validar em modelo real)

| # | Item | Local | Severidade |
|---|---|---|---|
| 4.1 | Cotas verticais do Diagrama de Montagem usam world-space; desalinham em vistas ao longo de Y | `DiagramaMontagemService.CriarCotasVerticais` | P1 |
| 4.2 | `EncontrarBarraNoNo` casa nó só por X 2D → pode misturar banzo sup/inf nas cotas de painel | `TrelicaRevitHelper.cs:137` | P1 |
| 4.3 | Classificação banzo sup/inf por Z médio da bbox é frágil em duas águas alta | `CotarTrelicaService`/`TagearTrelicaService` | P1 |
| 4.4 | Nomes de parâmetro com mojibake ("DimensÃ£o…") no bloco de 2 estacas → cai no fallback do bbox | `PfTwoPileCapRebarService.cs:198` | P1 |
| 4.5 | Estribos PF (viga/pilar) podem duplicar 1 estribo na junção das zonas NBR | `PfRebarService.cs:539,303` | P1 |
| 4.6 | Trial de licença sem binding de máquina + clock rollback revive trial/licença | `LicenseService.cs` | P1 |
| 4.7 | AutoVista: SheetNumber por timestamp de segundo pode colidir em lote (folha some) | `AutoVistaService.cs:874` | P2 |
| 4.8 | Diagrama: contagem "Eixos visíveis" conta grids de todo o projeto, não da vista | `DiagramaMontagemService.cs:418` | P2 |
| 4.9 | DSTV: furos só via parâmetro "Hole N…"; furação modelada (void) é descartada sem aviso | `DstvHoleExtractor.cs` | P2 |

---

## 5. Backlog — SAFE (lógica/UX, sem Revit) não incluídos nesta release

Pequenos e seguros, deixados fora da v2.8.9 só para manter o PR focado e revisável:

- Excel (LDM/ModelCheck): distinguir "arquivo aberto no Excel" com mensagem amigável
  (`IOException`/`UnauthorizedAccessException`) — `ListaMateriaisExportService.cs:193`.
- DSTV: avisar quando PieceMark/Notes com acento é degradado por `Encoding.ASCII`.
- `GuardaCorpoService`: null-check de `config.SymbolSelecionado`/`NivelReferencia`.
- PF: padronizar branding "PM -"/"ECC -" → "PF -" nos títulos/transações (resquício de fork).
- PF: `PfConsoloRebarConfig` sem campo de cobrimento (fixo 30 mm).
- `RevitWindowThemeService`/`WindowExtensions`: corrigir doc de idempotência do `Attach`.
- App.cs: persistir consent quando a janela é fechada no "X" (hoje reabre a cada boot).
- Licença: cache em memória não reavalia expiração no meio da sessão; comentário de
  `KeySigner` descreve fallback "DEV_ONLY" inexistente (corrigir para não reintroduzir).

---

## 6. Cobertura de teste — lacunas de alto valor (lógica pura, testável sem Revit)

- `NumeracaoItensService`/`NumeracaoItensCatalog` (numeração de marcas — determinística).
- `PfNamingService`/`PfNamingCatalog` (só o `PfNamingFormatter` é testado).
- `DstvHeaderBuilder`/`DstvHoleExtractor` (resto do DSTV é testado).
- Helpers novos da v2.8.8 do Diagrama (`ProjetarPontoEm2DDaVista`/`ReconstruirPonto3DDaVista`).

**Dívida documental:** `TrelicaRevitHelperTests.cs` (50 testes) e
`TagearTrelicaReportTests.cs` têm `[Skip]` **e** são `Compile Remove`d — nunca
compilam nem aparecem como skipped. Escolher um mecanismo só.

---

## 7. Nota sobre a contagem de testes

O agente de build mediu **900 atributos** `[Fact]`/`[Theory]` compilados. Isso
**não** é a contagem de casos do `dotnet test`, porque cada `[Theory]` expande por
`[InlineData]`. Os números 1223/1241 do README/CHANGELOG provavelmente vêm da saída
real do CI e não são "falsos" — o problema real era a **contradição interna**
(badge 1223 vs prosa 1241). Nesta release alinhamos tudo a **1247** (1241 da v2.8.8
+ 6 novos testes). **Ação recomendada:** confirmar o número exato contra o relatório
do `dotnet test` no CI e ajustar badge/prosa se divergir.

---

## 8. Pontos fortes confirmados (não mexer)

- Privacidade/telemetria: opt-in estrito, sem DSL/API key hardcoded, `PiiScrubber`
  em 5 vetores, `SessionId` UUID v4 anônimo.
- Conversões ft↔mm corretas em todo o domínio metálico (`RevitUtils`, `UnitUtils`).
- Helpers puros de terças/conexões (`ConexaoTercasMath/Geometry`) — exemplares.
- Ancoragem NBR 6118 do PF (fctk/fbd/η1-η3/lb/l0/mínimos) implementada e testada.
- Ribbon íntegro: 50/50 botões mapeiam para classes existentes; 70/70 ícones existem.
- Migração de rebrand: paths legados `FerramentaEMT` preservados onde são
  load-bearing (migração de licença v1.x).
- CI/release maduros: secret-gating do codesign, `signtool verify`, lock files.
