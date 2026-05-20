# Auditoria Sênior — SteelBIM v2.6.0

**Data:** 2026-05-19
**Versão auditada:** v2.6.0 (commit `cbd11b7`, ribbon split em duas abas)
**Modo:** Read-only, brutal-mas-construtivo, foco em "tudo que pode melhorar" antes do soft launch comercial
**Auditor:** Arquiteto sênior (papel) — perspectiva fresca pós pre-launch audit (8 PASS / 5 WARN / 0 CRITICAL)
**Distinção vs auditorias anteriores:** A auditoria pré-launch perguntou *"está pronto?"* (sim). Esta pergunta é *"o que vamos ouvir do cliente que ainda dá pra prevenir?"*

---

## 0. Sumário Executivo

**Resultado em uma linha:** O SteelBIM v2.6.0 está tecnicamente apto a entrar em soft launch, mas carrega **dívida de domínio e cobertura suficiente para gerar pelo menos 3 incidentes-classe-`v2.4.1` nos próximos 6 meses** se nada for endereçado antes da ampliação comercial.

**Números brutos:**

| Métrica | Valor | Comentário |
|---|---|---|
| Achados totais | **~212** | distribuídos em 10 fases (ver detalhamento) |
| CRÍTICO (libera bug em produção ou compromete viabilidade) | **15** | concentrados em PHASE 5 (NBR), 6 (security) e 7 (testes) |
| ALTO (acumula dívida grave / risco de cliente perdido) | **52** | espalhados, dominados por ADR-003/004 e performance |
| MÉDIO | **84** | qualidade, cleanup, UX |
| BAIXO / cosmético | **41** | |
| OK confirmados explicitamente | **20+** | bom o que está bom — ver §11 Apêndice |
| Esforço total estimado para zerar CRÍTICO + ALTO | **~12-15 semanas-dev** | priorização proposta em §10 |

**Top-5 achados que mais me preocupam (ordem de severidade x probabilidade de cliente reclamar):**

1. **🔴 CRÍTICO — `RebarCreationService.GetHookTypeByAngle` (Bloco) repete o bug-pattern do v2.4.1.** Filtra `RebarHookType` por ângulo sem checar `MultiplierForTailLength`, e NÃO chama `PfStirrupHookRules`. Bloco de fundação está numericamente exposto à mesma violação NBR 6118 9.4.6.1 que motivou o hotfix v2.4.1. (PHASE 5 §5.1 #1)
2. **🔴 CRÍTICO — `PfRebarService.GetHookTypeByAngle:912-918` (mesmo após o fix v2.4.1) reusa hooks pré-existentes sem validar multiplier.** Em projeto que tem `RebarHookType` chamado "EMT Gancho 135 graus" criado por v2.4.0 (com tail=6Ø), o serviço encontra-o primeiro e devolve o gancho errado, sem chamar `PfStirrupHookRules`. O fix do v2.4.1 só atua quando *nenhum* hook 135° existir. (PHASE 5 §5.1 #2)
3. **🔴 CRÍTICO — Nenhuma implementação de CAA (Classe de Agressividade Ambiental) em todo o projeto.** `grep` global de `CAA|Agressividade` retorna ZERO. Defaults de cobrimento 30mm/50mm não cobrem CAA II/III/IV. Engenheiro que aceita default em obra costeira viola NBR 6118 Tabela 7.2 sem aviso. (PHASE 5 §5.2)
4. **🔴 CRÍTICO — Marca de peça (`MarcarPecasService:398-418`) usa `Element.Parameters.Cast<Parameter>()` (ordem NÃO determinística no Revit) + `ElementId.Value` (per-document).** Rodar 2x no mesmo modelo pode gerar marcas diferentes; rodar em projetos espelhados gera marcas distintas para peças idênticas. Quebra rastreabilidade de fabricação. (PHASE 5 §5.6)
5. **🔴 CRÍTICO — Auto-update sem verificação Authenticode + `UpdateDownloader` sem assertion de timeout no HttpClient.** Após code-signing ser implementado, falta validar assinatura do `SteelBIM.dll` extraído contra cert pinned. Se o GitHub release token for comprometido (ataque supply-chain), todos os clientes auto-aplicam RCE no próximo boot. (PHASE 6 §6.5)

**O padrão por trás dos top-5:** quatro dos cinco vivem em **services >2000 LOC sem teste dedicado** (`PfRebarService`, `MarcarPecasService`). O bug do gancho 135 não foi acidente — foi o resultado natural de **regra de norma misturada com 2139 linhas de orquestração que só é testável manualmente no Revit**. Sem extrair helpers puros como `PfStirrupHookRules`, o próximo v2.4.1 está garantido. Ver §10 (recomendações priorizadas).

---

## PHASE 1 — Arquitetura + ADR Conformance

**Resumo:** 49 achados (18 ALTO / 24 MÉDIO / 7 BAIXO). Conformidade por ADR:

| ADR | Conformidade | Comentário |
|---|---:|---|
| ADR-001 (Result pattern) | ~85% | bem disseminado; algumas exceções servem chamada errada |
| ADR-003 (services mudos) | ~62% | **13 dos ~25 services ainda chamam dialogs direto** |
| ADR-004 (IProgress + CancellationToken) | ~25% | apenas 4 de 12 services-alvo (Lista, ModelCheck, Dstv, AgrupamentoVisual) |
| ADR-006 (auto-update 3-tentativas) | ~95% | sólido; falta apenas notificar usuário na 3ª falha |
| ADR-007 (PiiScrubber) | ~70% | gate correto, mas **3 gaps reais**: `evt.ServerName`, breadcrumbs, `.rvt` filename |
| ADR-008 (PostHog opt-in) | ~95% | gate correto e testado; falta circuit breaker em outage |

### ADR-003 (services mudos) — 13 violadores

| Sev | Arquivo:Linha | Achado | Esforço |
|---|---|---|---|
| ALTO | `Services/PF/PfRebarService.cs:176,236` | `ExecuteForHosts` chama `ShowWarning`/`ShowInfo` direto. Único entrypoint público | 6h |
| ALTO | `Services/MarcarPecasService.cs:110,120,640,700` | 4 dialogs no `Executar` + helper `ExibirResumo` (linha 636) | 4h |
| ALTO | `Services/CotarPecaFabricacaoService.cs:41,50,59,107,109` | 5 dialogs, retorna `void` — comando não tem como propagar erro | 6h |
| ALTO | `Services/AutoVistaService.cs:34,42,52,65,167` | 5 dialogs, `void`. Texto de UI no serviço | 6h |
| ALTO | `Services/TercasService.cs` (9 ocorrências) | Maior densidade. Mistura `ShowError` + retorno `Result.Fail` (duplo canal) | 4h |
| MÉDIO | `Services/TrelicaService.cs:20,119` | 2 dialogs (v1.1.0 já contradiz ADR de v1.0.5) | 2h |
| MÉDIO | `Services/EscadaService.cs:35,41,121` | Pior: `ShowConfirmation` no meio — serviço "pergunta" ao usuário | 4h |
| MÉDIO | `Services/TravamentoService`, `GuardaCorpoService`, `PipeRackService`, `NumeracaoItensService`, `PF/PfIsolationService`, `PF/PfNamingService`, `Bloco/BlocoFundacaoRebarOrchestrator` | 2-5 dialogs cada | 2-4h cada |

**Bem feito (zero diálogos, retornam `Result<T>` ou DTO):** `ListaMateriaisExportService`, `CortarElementosService`, `ModelCheckService`, `DstvExportService`, `Trelica/CotarTrelicaService`, `Trelica/TagearTrelicaService`, `IdentificacaoPerfil/IdentificarPerfilService`, `Conexoes/PlacaBaseLancamentoService`, `DiagramaMontagem/DiagramaMontagemService`, `Montagem/PlanoMontagemService`, `ContraventamentoPlanoService`.

### ADR-004 (IProgress + CT) — 6 services-alvo sem progresso/cancel

| Sev | Arquivo | Impacto |
|---|---|---|
| ALTO | `PfRebarService.cs` (2139 LOC, 6 entry points) | UI congela em batch grande PF; sem cancel = matar Revit |
| ALTO | `CotasService.cs` (1459 LOC) | UI congela em cotagem automática de modelo grande |
| ALTO | `MarcarPecasService.cs` (722 LOC) | Marcação de modelo inteiro pode levar minutos sem feedback |
| ALTO | `CortarElementosService.cs` (717 LOC) | Cumpre ADR-003, faltou só ADR-004 |
| ALTO | `CotarPecaFabricacaoService.cs` (749 LOC) | Caso de uso comum em batch |
| ALTO | `DiagramaMontagem/DiagramaMontagemService.cs` (881 LOC) | Recém v2.4.0 — virou dívida no nascimento |

### ADR-007 (PiiScrubber) — 3 gaps reais

**O que É scrubado:** email (regex), path `[A-Z]:\Users\<user>\` (regex). Aplicado em `Message`, `Formatted` e `SentryExceptions[].Value`. Ordem correta (BeforeSend).

**O que ESCAPA:**

| Sev | Gap | Risco |
|---|---|---|
| ALTO | `evt.ServerName` (= `Environment.MachineName`) não zerado | Hostname `WS-EMTBRASIL-01` vaza |
| ALTO | Paths UNC `\\nas01\projetos\<cliente>\` não cobertos pela regex | Nome de cliente via NAS |
| ALTO | Filename `.rvt`/`.rfa` não scrubado (e.g. "Projeto Vulcaflex.rvt") | Cliente identificável |
| ALTO | `evt.SentryExceptions[].Stacktrace.Frames[].AbsoluteFilePath` não scrubado | Username + cliente em stack frames |
| MÉDIO | `evt.Breadcrumbs[]` não scrubados | PII em breadcrumbs auto-coletados |
| MÉDIO | Username localizado `C:\Usuários\<x>\` (ptBR) não casa com regex | Usuários Windows em ptBR vazam |

### God classes & SRP violations

| Sev | Arquivo (LOC) | Achado |
|---|---|---|
| ALTO | `ListaMateriaisExportService.cs` (2225) | 5 responsabilidades em uma classe (coleta, classificação, formatação Excel, peso/área, capa). Split em `ColetorElementosLDM`, `ClassificadorCategoria`, `ExcelLdmRenderer`, `ResumoBuilder` (~1d) |
| ALTO | `PF/PfRebarService.cs` (2139) | 5 famílias de armadura, 6 entry points. Split por host (`PfColumnRebarService`, `PfBeamRebarService`, `PfConsoloRebarService`, `PfEstacaRebarService` + base) — ~1d |
| ALTO | `CotasService.cs` (1459) | 50+ métodos privados; comentário do autor (l.41) admite "dead-code possível em `ExecutarCotagemAlinhada`" — issue nunca fechada. Split em Collector/EixoResolver/Creator — ~1.5d |
| MÉDIO | `DiagramaMontagemService.cs` (881) | God class de nascença |
| MÉDIO | `AgrupamentoVisualService.cs` (773) | Aceitável — escopo coerente |
| BAIXO | `App.cs` (1081) | Boa parte é ribbon (~870 LOC). Extrair `RibbonBuilder` em `Infrastructure/Ribbon/` |

### Commands com lógica de service (Command-vs-Service)

| Sev | Arquivo (LOC) | Achado |
|---|---|---|
| ALTO | `Commands/CmdCortarPerfilPorInterferencia.cs` (768) | **28 métodos privados de lógica geométrica pura no Command** (5 estratégias fallback de intersecção). Impossível testar sem instanciar Revit. Extrair `SeccionarVigaService` retornando `Result<SeccionarResultado>` — 1d |

### Top-3 prioridades PHASE 1

1. **Fechar gaps do PiiScrubber (ADR-007)** — 6h, único achado ALTO com consequência LGPD direta.
2. **Migrar 5 services principais para ADR-003+004** (`PfRebarService`, `CotasService`, `MarcarPecasService`, `CotarPecaFabricacaoService`, `DiagramaMontagemService`) — 3d. Os 4 já refatorados (Lista, ModelCheck, Dstv, Agrupamento) provam que o playbook funciona.
3. **Extrair `SeccionarVigaService` de `CmdCortarPerfilPorInterferencia`** — 1d. Maior violação Command-vs-Service; bloqueia testes da intersecção 3D/2D fallback.

---

## PHASE 2 — Qualidade de Código C#

**Resumo:** 47 achados (12 ALTO / 22 MÉDIO / 13 BAIXO).

### 2.1 Nullable annotations — inconsistência

`SteelBIM.csproj:7` define `<Nullable>disable</Nullable>` projeto-wide, mas **60+ arquivos opt-in via `#nullable enable`**. Mistura cria armadilha: nova classe sem pragma perde garantias silenciosamente. Decidir: ativar global e corrigir warnings, OU remover opt-ins individuais. (4-6h)

### 2.2 CultureInfo — regressões pontuais (datas)

A lição da vírgula pt-BR foi aprendida — formatadores numéricos críticos usam `CultureInfo.InvariantCulture` (`NumberParsing.cs`, `DstvFileWriter.cs:35`, `ListaMateriaisExportService.cs:1241`, etc). **Regressões restantes são em datas:**

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| ALTO | `Services/CncExport/DstvExportService.cs:442,445` | `DateTime.Now` em nome de arquivo + cabeçalho enviado à fábrica (TZ-dependente, viaja por email) |
| ALTO | `Services/Montagem/PlanoMontagemService.cs:62` | `SteelBIM_SharedParams_{DateTime.Now:yyyyMMddHHmmss}.txt` em pasta de rede — colisão de versão |
| ALTO | `Views/PlanoMontagemWindow.xaml.cs:153`, `Services/...:439` | `dd/MM/yyyy` sem `CultureInfo.InvariantCulture` |
| MÉDIO | `Services/AutoVistaService.cs:605` | `SheetNumber = "SD-{DateTime.Now}"` — TZ-dependente em identificador de prancha |

### 2.3 Magic numbers — `PfRebarService` é o pior

| Sev | Arquivo | Achado |
|---|---|---|
| MÉDIO | `PfRebarService.cs` (30+ literais) | `ToFeetMm(100)`, `ToFeetMm(20)`, `ToFeetMm(50)`, `ToFeetMm(0.25)`, `ToFeetMm(300.0)` espalhados sem nome. Tolerâncias geométricas sem semântica explícita |
| MÉDIO | `DiagramaMontagem:340-341` | `section.Scale = 75` hard-coded — sem `DiagramaMontagemConfig.EscalaPadrao` |
| MÉDIO | `DiagramaMontagem:583,608,669,708` | Offsets `800mm`, `200mm`, `1000mm` hard-coded |
| OK | `CotasService.cs:17-33` | **Modelo exemplar** — 6 constantes nomeadas no topo (`OffsetLinha`, `ToleranciaAgrupamento`, etc) |
| OK | `PfRebarService.cs:49-51` | 3 constantes nomeadas — padrão a estender para os outros 30 literais |

### 2.4 Exception handling — `catch{}` silenciosos no caminho crítico

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| **ALTO** | `PF/PfRebarService.cs:1079,1090` | `catch { }` em `Rebar.CreateFromCurves` — engole exception sem log nem fallback rastreável. Se 3 tentativas falham silenciosamente, estribo não é criado e ninguém sabe |
| ALTO | `PF/PfRebarService.cs:935,1171,1462` | `catch { }` em `Name=...`, `SetRebarShapeId`, `SetLayoutAsMaximumSpacing` — falha de config silenciada |
| ALTO | `DiagramaMontagem:336,341` | `catch { }` em `section.Name`/`section.Scale` — escala errada na prancha do cliente sem warning |
| MÉDIO | `AutoVistaService.cs:403,485,631` | `catch (Exception) { return null; }` engole exception inteira sem `Logger.Warn` — usuário não sabe quais elementos falharam |
| MÉDIO | `Trelica/CotarTrelicaService.cs:299,636,659` | `catch { }` em loops de classificação — comentário justifica mas dificulta debug em campo |

### 2.5 IDisposable

| Sev | Achado |
|---|---|
| OK | **56 ocorrências** de `new Transaction(...)` — todas em `using` ✓ |
| MÉDIO | **105 ocorrências** de `new FilteredElementCollector(doc)` em 48 arquivos — **zero usam `using`**. FEC implementa IDisposable (libera handles nativos). Em comandos batch (LDM, ModelCheck, Marcar Peças) o leak é cumulativo. Não-crítico mas má prática consolidada (4-8h pra varrer) |

### 2.6 LINQ em hot paths

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| MÉDIO | `Services/AgrupamentoVisualService.cs:706-711` (`RenomearGrupo`) | FEC inteiro `OfClass(GroupType)` por grupo criado. N grupos × M tipos = O(N·M). Lentidão com >100 grupos |
| MÉDIO | `Services/Trelica/CotarTrelicaService.cs:453` | `.Last()` chamado uma vez por iteração no loop sobre `faixa.Segmentos` |
| BAIXO | `AgrupamentoVisualService.cs:242-245` | `GroupBy().OrderBy(x => DescreverElemento(x.FirstOrDefault(), doc))` — `DescreverElemento` chama `doc.GetElement` N·log(N) vezes |

### 2.7 String concat em loops

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| MÉDIO | `Trelica/CotarTrelicaReport.cs:35-36` | `msg += $"• {w}\n"` em loop. Trocar por `StringBuilder` ou `string.Join` |

### 2.8 DateTime vs DateTimeOffset — vários nomes de arquivo/identificadores

Detalhes em §2.2. **3 ALTO + 4 MÉDIO** consolidados. Convenção `UtcNow` está bem aplicada em Crash/License; `Now` aparece onde TZ importa (DSTV, ModelCheck visualization, SheetNumber, file name compartilhado).

### 2.9 Dead code — arquivos `.bak-alef-v1.5` em produção

| Sev | Arquivo | Achado |
|---|---|---|
| **ALTO** | `Services/PF/PfRebarService.cs.bak-alef-v1.5` | Backup de 36KB pré-Wave2 dentro do source tree |
| **ALTO** | 4 `Cmd*.cs.bak-alef-v1.5` em `Commands/PF/` | 4 backups de comandos. csproj não exclui `**/*.bak*` — risco de double-class collision |
| MÉDIO | `CotasService.cs:54` | `ExecutarCotagemAlinhada` private de 400+ linhas; comentário l.41 admite "possivel dead-code" |
| MÉDIO | `AutoVistaService.cs:212-214` | Overload `ColetarElementosDaVista(doc)` possivelmente não chamado |

**5 minutos** pra deletar os 5 `.bak-alef-v1.5` e zero risco.

### 2.10 DRY violations

| Sev | Achado |
|---|---|
| ALTO | "Is elemento estrutural" reimplementado em 4+ lugares (`PfElementService.cs:69-81` é o canônico). Checagem inline `cat == OST_StructuralFraming \|\| OST_StructuralColumns` aparece em **41+ arquivos** |
| MÉDIO | `element.LookupParameter("Mark")?.AsString()` cru em 41 ocorrências; `MarcarPecasService` e `ListaMateriaisExportService` cada um tem seu próprio helper `ObterMarca`. Centralizar em `RevitUtils.GetMark()` |
| MÉDIO | Parsing dual-culture (`InvariantCulture` → `CurrentCulture` → `Replace ','`) duplicado em 3 PfWindows; `NumberParsing.TryParseDouble` já faz |

### 2.11 Naming

| Sev | Achado |
|---|---|
| BAIXO | `ConexaoCalculator.GenerarMarcadorXxx` — "Generar" (espanhol/typo), resto do código usa "Gerar" |
| OK | Zero Hungarian, zero `m_` / `sz_` / `lp_` |

### Top-3 prioridades PHASE 2

1. **Apagar os 5 `.bak-alef-v1.5`** — 5 min, alto risco se ficar.
2. **Padronizar Nullable** — 4-6h, evita armadilha em código novo.
3. **Logar os `catch{}` silenciosos em `PfRebarService`** (l.935, 1079, 1090, 1171) — 30 min, evita horas de suporte em campo.

---

## PHASE 3 — Performance Revit-Specific

**Resumo:** 37 achados (12 ALTO / 18 MÉDIO / 7 BAIXO). Estimativas para modelo de 500-1000 elementos.

### 3.1 FilteredElementCollector dentro de loop

| Sev | Arquivo:Linha | Custo estimado | Fix sketch |
|---|---|---|---|
| ALTO | `PF/PfRebarService.cs:841,859,909,1183` (`GetBarType`, `GetBarTypeByDiameter`, `GetHookTypeByAngle`, `GetRebarShape` em `ExecuteForHosts`) | 100 pilares × 3 FECs × 30ms = **+9s** | Cachear tipos uma vez antes do `foreach host` |
| ALTO | `Bloco/RebarCreationService.cs:138-156` chamadores em 6 services | 20 blocos × 6 svc × 1.5 FECs = **+5s** | Cache em `BlocoRebarTypeCatalog` resolvido uma vez |
| ALTO | `AutoVistaService.cs:684` (`GerarNomeUnico`, 2× por peça) | 50 peças × 2 = **+3s** | `HashSet<string>` de nomes antes do loop |
| ALTO | `ModelCheck/OverlappingElementsRule.cs:91-92` | 500 elementos = 124k pares × 2 geom extracts = **>30s catastrófico** | Cache `List<Solid>` por id; usar `ElementIntersectsElementFilter` |
| ALTO | `IdentificacaoPerfil/IdentificarPerfilService.cs:181` (`JaTemTagNaVista`) | 100 elementos × FEC tags = **+2-4s** | `HashSet<ElementId>` cached antes |

### 3.5 Transaction granularidade

| Sev | Arquivo:Linha | Custo | Fix |
|---|---|---|---|
| ALTO | `DiagramaMontagem:50,67,77,88,99,113,125,136,147,158` (**10 transactions sequenciais**) | +500ms + 10 entries no Undo | `TransactionGroup.Assimilate()` |
| ALTO | `AutoVistaService:94` (Transaction **dentro do foreach elem**) | 50 peças × 50ms = **+2.5s** + 50 Undo | TransactionGroup envolvendo loop |
| ALTO | `NumeracaoItensService:374,437,484` (Transaction + Regenerate por clique do usuário) | 100 itens = **+15s acumulados** | Modo "batch commit" |

### 3.6 `doc.Regenerate()` em loops

| Sev | Arquivo:Linha | Custo |
|---|---|---|
| ALTO | `AutoVistaService.cs:393,475` (**2 Regenerates dentro de loop sobre 50 peças**) | **+15s catastrófico** |
| ALTO | `AgrupamentoVisualService.cs:309` (Regenerate por grupo, 50 grupos) | **+10s** |
| ALTO | `AjustarEncontroService.cs:43,48,54,60` (4 Regenerate sequenciais) | +600-800ms por click |

### Commands com risco de lentidão percebida (top-3)

1. **`/CmdGerarVistaPeca` (AutoVistaService)** — 50 peças = **20-30s blocking + 50 undos**. Mais usado em detalhamento real.
2. **`/CmdModelCheck` rule `OverlappingElementsRule`** — algoritmo O(n²) com geometry boolean. 500 elementos = **>30s**. Provavelmente nunca foi rodado em modelo real grande.
3. **`/CmdPfInserirEstribosPilar` / `/CmdPfInserirAcosPilar`** — FECs por host = **+9s** em 100 pilares. Em prédio PF inteiro (~300 pilares) escala linear.

### Top-3 prioridades PHASE 3

1. **`AutoVistaService`** — remover Regenerate intra-loop + hoist FECs + TransactionGroup. 4h, ganho **-15 a -25s**.
2. **`OverlappingElementsRule`** — cachear geometry + `ElementIntersectsElementFilter`. 4h, ganho **catastrófico → aceitável** (>30s → ~3s).
3. **`PfRebarService`** — hoistar `GetBarType`/`GetHookType`/`GetRebarShape` fora do loop. 2h, ganho **-9s** nos comandos PF mais usados.

**Quick wins (<2h cada, ganho 1-3s):** Agrupamento Regenerate (30min, -10s); Diagrama transações em TransactionGroup (3h, +1 undo entry); IdentificarPerfil cache de tags (1h, -3s); AjustarEncontro 4→1 Regenerate (1h, -600ms).

---

## PHASE 4 — UX/UI Ribbon e Dialogs

### 4.1 Distribuição dos botões no ribbon split (v2.6.0)

| Aba / Painel | # botões | Diagnóstico |
|---|---:|---|
| **Modelagem / Modelagem Geral** | 3 | OK |
| **Modelagem / Estrutura Metálica** | 5 | OK (limite confortável) |
| **Modelagem / Operações em Vigas** | 3 + 1 stacked pair | OK |
| **Modelagem / Conexões** | **1** | ⚠️ painel órfão — visualmente estranho |
| **Modelagem / PF Construção** | **1** | ⚠️ painel órfão (perdeu 3 botões no split — moveram p/ Detalhamento/Visualização) |
| **Modelagem / PF Armaduras** | **7** | ⚠️ excede 5 — risco de overflow em ribbon estreito |
| **Modelagem / Visualização** | **7** | ⚠️ excede 5 — mesmo risco |
| **Detalhamento / Vistas** | 3 | OK |
| **Detalhamento / Cotagem** | 4 | OK |
| **Detalhamento / Anotação** | 5 | OK |
| **Detalhamento / Fabricação CNC** | 2 | OK |
| **Detalhamento / Montagem e Sequenciamento** | 2 | OK |
| **Detalhamento / Verificação** | **1** | ⚠️ painel órfão |
| **Detalhamento / Licença** | 1 stacked pair | OK (stacked é compacto) |

**Conclusão:** **3 painéis órfãos** (Conexões, PF Construção, Verificação) e **2 painéis sobrecarregados** (PF Armaduras, Visualização). Painéis com 1 botão único parecem "sob construção" para o usuário; com 7 botões podem overflow em laptops 14" / ribbon recolhido.

**Recomendações (MÉDIO):**

- Consolidar "Conexões" + "PF Construção" + (talvez) última posição da "Estrutura Metálica" em um painel "Estruturas Especiais"? Ou aceitar como design intencional e documentar.
- Quebrar "PF Armaduras" em "PF Armaduras Long. (Acos)" + "PF Armaduras Transv. (Estribos+Bloco)".
- "Visualização" pode virar "Isolar" (4 botões: Vigas/Pilares/PF P+Cons/PF Lajes) + "Agrupar" (Pilares/Vigas/Limpar) — 3 painéis pequenos > 1 painel grande, e semanticamente correto.

### 4.2 Tooltips & LongDescription

| Sev | Achado |
|---|---|
| OK | **100% dos 48 botões têm tooltip não-vazio** (verificado em App.cs). Texto é descritivo, 1-3 frases, ação-orientado |
| MÉDIO | **Nenhum botão usa `LongDescription` separado** — `AddButton` só seta `button.ToolTip = tooltip`. Revit suporta `PushButtonData.LongDescription` para tooltip rico (3+ parágrafos com header). Oportunidade de melhorar UX sem custo grande |

### 4.3 Dialogs e Windows code-behind

| Arquivo | LOC | Comentário |
|---|---:|---|
| `Views/PfColumnBarsWindow.xaml.cs` | **774** | Code-behind enorme — mistura cálculos PF, parsing pt-BR/InvariantCulture (duplicado em 3 windows PF), validação |
| `Views/PfBeamBarsWindow.xaml.cs` | **754** | Idem — mesmo padrão |
| `Views/NumeracaoItensWindow.xaml.cs` | 548 | Sessão modal persistente com 500+ LOC de orquestração na View |
| `Views/PfNamingWindow.xaml.cs` | 547 | |
| `Views/VerificarModeloReportWindow.xaml.cs` | 494 | |
| `Views/PfEstacaRebarWindow.xaml.cs` | 474 | |

**MVVM ausente.** Code-behind faz validação, parsing, formatação, conversão de unidades. Repercussão direta na cobertura de testes (Views não têm testes — então 500-700 LOC × 6 windows = ~3000 LOC de domain logic não testada).

### 4.4 TaskDialog / AppDialogService

- **Apenas 2 `TaskDialog.Show` direto** (legados) — 99% canaliza via `AppDialogService` (84 chamadas). Bom isolamento.
- Qualidade das mensagens não auditada exaustivamente — recomendo amostragem em fase próxima focando em mensagens de erro: dizem **O QUE deu errado E COMO corrigir**? Ou genérico "Operação falhou"?

### 4.5 PrivacyConsentWindow

| Sev | Achado |
|---|---|
| ALTO | `App.cs:982-1010` — `consent.ShowDialog()` chamado **sem `Owner` setado** — janela WPF pode ficar atrás do Revit (Alt+Tab necessário pra achar). 30min fix |
| INFO | Modal correto no primeiro Idling; auto-desinscreve; fail-safe se user fecha Revit antes de decidir |

### Top-3 prioridades PHASE 4

1. **Splitar "PF Armaduras" e "Visualização" em painéis menores** (≤4 botões) — 2h. Resolve overflow e melhora cognição.
2. **Setar `Owner` no `PrivacyConsentWindow`** (`App.cs:982`) — 30min. UX crítico no primeiro boot.
3. **Adotar `LongDescription` em comandos complexos** (Diagrama, DSTV, Cotagem, ModelCheck, PF Armaduras) — 4h. Rich tooltip é onboarding gratuito.

---

## PHASE 5 — Domínio: NBR 6118, Fabricação, Detalhamento BR

**Resumo:** 31 achados (10 CRÍTICO / 12 ALTO / 9 médios+baixos). **Esta é a fase com maior densidade de achados críticos.**

### 5.1 NBR 6118 — multipliers binários (regressão tipo v2.4.1) — **2 críticos ainda vivos**

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| **🔴 CRÍTICO** | `Services/Bloco/RebarCreationService.cs:153-161` | `GetHookTypeByAngle` retorna `OrderBy(h => Math.Abs(h.HookAngle - rad)).FirstOrDefault()` — **não impõe o multiplier do rabo**. Se o `RebarHookType` existente foi criado com `MultiplierForTailLength` errado (ex: 6×Ø para 135°), o estribo do Bloco reproduz a regressão v2.4.1. **`PfStirrupHookRules.NbrStirrupHookMultiplier` NÃO é consultado neste caminho.** |
| **🔴 CRÍTICO** | `Services/PF/PfRebarService.cs:906-941` | Fluxo correto após v2.4.1, MAS: filtro inicial `.Where(x => Math.Abs(RadiansToDegrees(x.HookAngle) - target) <= 0.5)` retorna o primeiro hook com ângulo certo SEM verificar o `MultiplierForTailLength`. **Em projeto com template antigo que já tem "EMT Gancho 135 graus" do v2.4.0 (multiplier=6), o sistema reusa-o sem corrigir.** A regra do v2.4.1 só atua quando NENHUM hook 135° existir |

**Ação:** adicionar `.Where(x => x.MultiplierForTailLength >= PfStirrupHookRules.NbrStirrupHookMultiplier(angleDegrees))` em ambos os pontos. E em `RebarCreationService.cs:153-161`, espelhar o pattern de `PfRebarService.GetHookTypeByAngle:923` (criar quando não houver compliant). Estimativa: 2h cada.

### 5.2 NBR 6118 — Cobrimento (CAA) — **AUSENTE TOTAL**

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| **🔴 CRÍTICO** | global | **NENHUMA implementação de Classe de Agressividade Ambiental.** `grep "CAA\|Classe.*Agressividade"` retorna ZERO matches |
| **🔴 CRÍTICO** | `Models/PF/PfRebarConfigs.cs:116,144,169,185` | Default `CobrimentoCm = 3.0` (=30mm). NBR 6118 Tabela 7.2 só admite 30mm em lajes CAA I. Para vigas/pilares: CAA I=25, II=30, III=40, IV=50mm. **Sem seletor de CAA, engenheiro pode aceitar default e violar a norma para qualquer obra costeira/industrial sem aviso** |
| ALTO | `Services/PF/PfRebarService.cs:1120-1123` | `GetCover()` faz `Math.Max(1.0, coverCm)` — clampa silenciosamente para >=1cm sem avisar. NBR mínimo é 15mm (CAA I + laje) |
| MÉDIO | 6 Windows PF | Única validação: `CobrimentoCm <= 0`. Nada impede 5mm |

**Ação:** adicionar `enum Caa { I, II, III, IV }` em `PfRebarConfigs`, tabela `MinimumCoverByCaa[caa, elementType]` baseada em NBR 6118 Tabela 7.2, validação que rejeita ou avisa quando `coverInformado < minimoNbr`. **Sem isso, plugin não é defensável tecnicamente perante CREA em projetos costeiros/industriais.** Estimativa: 1d.

### 5.3-5.5 Outras questões NBR

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| ALTO | `PF/PfRebarService.cs:1431` e `PfTwoPileCapRebarService.cs:359` | `minimumClearSpacing = Math.Max(ToFeetMm(20.0), barDiameter)` — implementa NBR 18.3.3.2 **PARCIALMENTE**. Falta o termo `1.2 × Dmáx_agregado` (brita 2 = 30mm > 20mm aplicado) |
| ALTO | `Services/PF/PfNbr6118AnchorageService.cs:36` | `lb,mín` mistura mm com cm: `Math.Max(diameterMm, 10.0)` compara mm (Ø=20) com 10 (cm). **Bug numérico real** — para Ø>10mm SUBESTIMA o mínimo NBR 9.4.2.5 |
| BAIXO | global | Não há checagem de espaçamento MÁXIMO (NBR 18.3.5.1: smax,viga = 30cm; 18.4.2.2: smax,pilar ≤ 2b, ≤ 40cm; 18.4.3: estribo s ≤ 12φℓ para CA-50) |

**Pontos positivos (NBR):** `PfNbr6118AnchorageService` implementa corretamente NBR 6118 9.4.2.4 (fctk, η1·η2·η3, fbd, lb), exceto o bug dimensional em `lb,mín`.

### 5.6 Fabricação — Piece mark stability — **CRÍTICO**

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| **🔴 CRÍTICO** | `MarcarPecasService.cs:398-418` | `AppendParametrosDimensao` itera `elemento.Parameters.Cast<Parameter>()` — **a ordem de iteração de `Element.Parameters` no Revit NÃO é estável entre versões/sessões/projetos**. A mesma peça gera assinaturas diferentes em projetos espelhados. Duas execuções consecutivas no mesmo modelo podem produzir marcas distintas |
| **🔴 CRÍTICO** | `MarcarPecasService.cs:380,383` | Assinatura inclui `tipo?.Id?.Value` (ElementId do RebarType) e `material?.Id?.Value`. **ElementIds são per-document.** Duas peças idênticas em projetos diferentes geram marcas diferentes — quebra deduplicação inter-projeto (lote misto) |

**Ação:** substituir iteração de `Parameters` por lista ordenada por `Definition.Name`. Trocar `tipo.Id.Value` por `Family.Name + "|" + Type.Name`. Trocar `material.Id.Value` por `material.Name`. Estimativa: 1d + tests.

### 5.7 Fabricação — DSTV/NC1 — **3 críticos**

| Sev | Arquivo | Achado |
|---|---|---|
| **🔴 CRÍTICO** | `DstvFileWriter.cs` (comment l.19) | **AUSENTE bloco AK (outer contour)** — admitido no comentário. Sem AK, máquinas cortam por contorno padrão do header; perfis com chanfros/cortes biselados (notches) não saem |
| **🔴 CRÍTICO** | `DstvFileWriter.cs:19-20` | **AUSENTE bloco IK (inner contour)** — qualquer recorte interno (notch web, copings) impossível |
| **🔴 CRÍTICO** | `DstvFileWriter.cs:109-117` (SC) | Formato SC simplificado: 2 ângulos. DSTV V2.5 §3.4 exige `<face>n <x> <y> <z> <alpha> <beta>`. **Ficep/Voortman rejeitam o SC atual** |
| ALTO | `DstvFileWriter.cs:138` | Ordem `{Web Front, Back, Top Flange, Bottom Flange, Side}` em vez de DSTV oficial `v/o/u/h`. Abre `BO ... EN` por face (DSTV exige BO único por linha de furo) |
| ALTO | `DstvFileWriter.cs:199-214` | `FormatNumber` arredonda 2 casas → Ø furo 15.875mm (5/8") vira 15.88. Erro acumulado em padronizações americanas |
| MÉDIO | `DstvHoleExtractor.cs:34-43` | Lê `Hole 1 Diameter`…`Hole 60 Diameter` — convenção EMT proprietária. **Não suporta voids/holes via geometria Revit nativa** (que é como 99% das famílias Detail.com/AGA fazem furos). Para Tekla-refugees: exporta vazio |
| BAIXO | `DstvHeaderBuilder.cs:64` | `TryReadSharedLengthMm(type, "Web Thickness", ...)` — variantes customizadas. **Não usa `STRUCTURAL_SECTION_I_SHAPE_WEB_THICKNESS` BuiltInParameter** — header sai com `tw=0` em famílias OOTB do Revit |

### 5.8 Lista de Materiais XLSX

| Sev | Achado |
|---|---|
| OK | Células numéricas via `double` (ClosedXML preserva tipo). Totais via FORMULA (`SUMPRODUCT`/`SUM`). Format `0.000` em comprimentos. Comprimento em metros consistente |

### 5.9 Diagrama de Montagem

| Sev | Arquivo:Linha | Achado |
|---|---|---|
| **🔴 CRÍTICO** | `DiagramaMontagemService.cs:50-205` | **As 10 transações NÃO são fault-tolerant.** Estão dentro de um único `try` externo (l.26) com `catch (Exception ex)` na l.200. Se tx5 (tags) lançar exceção, a função retorna ali — tx6..tx10 NÃO executam. Cada `using (Transaction)` precisa do seu try/catch interno + `Logger.Warn` |
| ALTO | `:340` | `section.Scale = 75` hard-coded — pranchas EMT usam 1:50/1:100 também |
| ALTO | `:424,583` | Offsets `500mm`, `800mm` hard-coded — não em `MargemMm` do config |
| MÉDIO | `:568-576` | Limita clusters de SpotElevation a 15 e reamostra antes — em galpão com 18 níveis perde precisão |
| MÉDIO | vs EM-08 EMT | **Ausentes:** cotagem de altura de cada elemento (banzo, montante); cotagem de diagonais; títulos automáticos abaixo de viewport; legenda de materiais/marca |

### 5.10 PF naming

| Sev | Achado |
|---|---|
| OK | Ordenação determinística: `BeamAxisGroup → HorizontalSnapped (tol 10cm) → VerticalSnapped → Id.Value`. Re-execução = mesma ordem. **ESTÁVEL** |
| OK | `Prefixo`/`Sufixo` configuráveis; helper `PfNamingFormatter` puro testado |
| BAIXO | Sem padding configurável (P1 vs P01 vs P001) — em obras com >99 pilares gera ordenação alfabética ruim |

### Achados de altíssima prioridade PHASE 5

1. **`Bloco/RebarCreationService.cs:153-161`** — bug-pattern v2.4.1 esperando para acontecer em outro service
2. **`PfRebarService.cs:912-918`** — mesmo após v2.4.1, reusa hooks pré-existentes sem validar multiplier
3. **`PfNbr6118AnchorageService.cs:36`** — bug dimensional (mm vs cm)
4. **`PfRebarService:1431` e `PfTwoPileCapRebarService:359`** — espaçamento sem termo `1.2·Dmáx`
5. **`PfRebarService:1120-1123`** — `GetCover` clampa silenciosamente
6. **AUSÊNCIA TOTAL de CAA** — todo o projeto
7. **`MarcarPecasService:398-418`** — assinatura não-determinística
8. **`DiagramaMontagemService` — 10 txs sem fault-tolerance individual**

### Top-3 prioridades PHASE 5

1. **CAA + cobrimento mínimo** — adicionar `enum Caa` + tabela NBR Tabela 7.2 + validação. Sem isso, obra costeira/industrial usa cobrimento ilegal por default. **1d**
2. **Hooks no Bloco + GetHookTypeByAngle no PF** — bug-pattern v2.4.1 vivo em 2 services. Aplicar `PfStirrupHookRules` em TODOS os caminhos que materializam `RebarHookType`. **3h**
3. **Estabilidade de marca de peça** — substituir iteração não-determinística + `Id.Value` por `Family.Name|Type.Name` alfabético. Idempotência cross-project é requisito de fabricação em lote. **1d + tests**

---

## PHASE 6 — Segurança e Privacidade

**Resumo:** 17 achados (2 CRITICAL / 5 ALTO / 6 MÉDIO / 4 BAIXO) + 8 OK confirmados.

### 6.1 HMAC license secret — OK

`Licensing/LicenseSecretProvider.cs:71-122` resolve secret via env var → `%LOCALAPPDATA%\SteelBIM\license.secret` → ao lado do DLL → legacy → `InvalidOperationException` (fail-closed). **Nunca hardcoded.** `.gitignore:88-89` cobre. `KeySigner.cs:91` usa `CryptographicOperations.FixedTimeEquals` (constant-time).

**Único MÉDIO:** comentário stale "fallback DEV_ONLY hardcoded" em `KeySigner.cs:37` é enganoso (não existe mais). 5min pra apagar.

### 6.2 DPAPI scope — OK

Todos os 6 call sites em `LicenseStore.cs` usam `DataProtectionScope.CurrentUser`. **Sem achados.**

### 6.3 Sentry PiiScrubber — 3 gaps reais

(Detalhes em PHASE 1 §ADR-007.) Resumo: scrubber cobre email + path Windows com username em `Message`/`Formatted`/`Exception.Value`. **Escapa:**

- `evt.ServerName` (hostname) ✗
- Paths UNC `\\nas\share\<user>\` ✗
- Filename `.rvt`/`.rfa` (nome do cliente) ✗
- `evt.SentryExceptions[].Stacktrace.Frames[].AbsoluteFilePath` ✗
- `evt.Breadcrumbs[]` ✗
- Username Windows ptBR (`C:\Usuários\<x>\`) ✗

**Ação:** expandir regexes + iterar Stacktrace.Frames + `SetBeforeBreadcrumb`. **~6h.** Único gap LGPD-relevante real do plugin.

### 6.4 PostHog opt-in — OK com 2 reparos

- Gate funcional, no-op em consent denied/unset ✓
- API key via env/file, nunca hardcoded ✓
- Default host `https://eu.posthog.com` (HTTPS, LGPD-friendly) ✓
- `SessionIdProvider` puramente anônimo (`Guid.NewGuid()`, sem MachineName) ✓

**Reparos:**
- **ALTO** — Re-consent v3 (`App.cs:1005`) deixa Sentry+Telemetry desligados até próximo restart se user aceita pela 1ª vez. UX ruim, fix simples: re-chamar `*StartupWiring.InitializeServices` após `store.Save(consent.Result)`. **15min**
- **MÉDIO** — `PostHogHttpTelemetryClient.NormalizeHost:155-159` **aceita `http://` sem rewrite para HTTPS** — se user/CI seta `STEELBIM_POSTHOG_HOST=http://attacker.local`, telemetria vai plaintext. **5min** (rejeitar ou forçar https://)

### 6.5 Auto-update — **2 CRITICAL**

`UpdateDownloader` aplica 6 validações antes de aceitar zip (tamanho 1-50MB, ZipArchive ok, **SHA-256 contra checksums.txt do mesmo release**, ZipSlipValidator, top-level contém `SteelBIM.dll`, versão da DLL == `tag_name`). `UpdateApplier` re-valida SHA-256 antes de aplicar.

| Sev | Achado |
|---|---|
| **🔴 CRÍTICO** | `checksums.txt` é baixado do **mesmo GitHub Release** que o `.zip`. Atacante com release publish (token comprometido, account takeover) controla **ambos**. **Não há verificação Authenticode no `SteelBIM.dll` extraído** (code-signing pendente). Após code-signing aterrar, **`UpdateDownloader` precisa validar assinatura contra cert pinned** (Subject ou thumbprint). **4h após code-signing existir** |
| **🔴 CRÍTICO** | `UpdateDownloader.cs:75-89` — `_http.GetAsync(...)` **sem assertion de timeout no HttpClient injetado**. Se caller passar default (Timeout=100s), download 50MB sob slowloris trava UI por minutos. **5min** (assertion no construtor) |

### 6.6 Logger PII leakage

`Logger.cs:59-60` enriquece TODA log line com `MachineName` + `UserName`. Logs locais em `%LocalAppData%\SteelBIM\logs\` carregam isso. **Não vaza por si só** (log não vai pro Sentry — verificado em ADR-007), mas vira PII problem se user envia log file por email pro suporte. Mitigar via runbook ou remover Enrich.

### 6.7 Credenciais em config — OK

Zero `appsettings*.json` com credentials. Zero `.env`. `.gitignore:80-112` cobre `.pfx`/`.env`/`license.secret`/`sentry.dsn`/`posthog.apikey`.

### 6.8 Telemetry timeout — OK com nuance

| Sev | Achado |
|---|---|
| OK | `_telemetryHttp.Timeout = 10s` (App.cs:36); `TelemetryReporter.Flush(2000)` |
| ALTO | `SentryReporter.cs:194-210` — `Flush(2000)` chama `_hub.FlushAsync().Wait(2000)` na **UI thread em OnShutdown**. Freeze até ~2-3s no fechamento do Revit (aceitar) |

### 6.10 First-boot consent flow — 1 ALTO

| Sev | Achado |
|---|---|
| ALTO | `App.cs:982-1010` — `consent.ShowDialog()` sem `Owner` setado — janela pode ficar atrás do Revit (deadlock visual). **30min fix** (setar Owner via Revit interop) |

### Riscos críticos PHASE 6

1. **🔴 Auto-update sem Authenticode check** — supply-chain RCE máxima após code-signing existir
2. **🔴 UpdateDownloader sem timeout assertion** — DoS/UI freeze
3. **ALTO — Sentry stack frames/breadcrumbs/ServerName escapam scrubber** — LGPD-relevante
4. **ALTO — PiiScrubber não cobre paths fora `C:\Users\`** (NAS, Usuários ptBR)
5. **ALTO — PostHog host aceita http://** — vazamento em transit

### Top-3 prioridades PHASE 6

1. **Fechar gap de code-signing + Authenticode-check no UpdateApplier** — aterrar junto com milestone code-signing planejada
2. **Expandir PiiScrubber + scrubar Sentry stack frames/breadcrumbs/ServerName** — 4h. Quebra de LGPD com cliente é risco legal direto
3. **Assertion de timeout no `UpdateDownloader` + forçar HTTPS no PostHog host** — 15min total, fail-fast em config errado

---

## PHASE 7 — Testes e Cobertura

**Resumo:** 31 gaps críticos identificados. 787 testes parece market-ready, mas distribuição revela problema.

### 7.1 Distribuição (test files vs source files)

| Área | Source | Tests | Cobertura aprox |
|---|---:|---:|---|
| Infrastructure/ | 38 | 26 | **~80%** — quase 100% nos módulos novos (Telemetry/Sentry/Update/Privacy) |
| Models/ | 51 | 22 | **~43%** — testes existem mas são quase todos "default + setter round-trip" |
| Services/ | 77 | 26 (helpers/parser/catalog/report) | **~10% do código de serviço real** — **apenas 1 arquivo `*ServiceTests.cs` real** (`PfNbr6118AnchorageServiceTests`) |
| Commands/ | 50 | 0 | 0% (aceitável — thin shells) |
| Views/ | 42 | 0 | 0% (aceitável) |
| Core/, Licensing/ | 4+4 | 100% | |

**Veredito macro:** 787 testes vivem em **3 ilhas saudáveis** (Infra, Licensing, helpers Trelica/PF) e **uma sombra enorme** em `Services/` praticamente sem cobertura unitária. **Relação "1 teste de serviço real para 42 serviços" é o número mais preocupante de toda a auditoria.**

### 7.2 Services críticos SEM teste dedicado

| Sev | Serviço (LOC) | Risco regressão |
|---|---|---|
| **🔴 CRÍTICO** | `PfRebarService.cs` (2139) | O bug do gancho 135 nasceu aqui. Sem teste: zoneamento NBR, `GetHookTypeByAngle`, `ValidateMinimumBarSpacing`, `CreateVerticalBarsWithLap`, `BuildPerimeterPositions`, fluxo de cobrimento |
| **🔴 CRÍTICO** | `ListaMateriaisExportService.cs` (2225) | Maior arquivo. Gera entregável financeiro. **Sem helper puro extraído, sem teste de formatação, sem snapshot XLSX** |
| ALTO | `CotasService.cs` (1459) | Cotagem errada = projeto sai errado |
| ALTO | `DiagramaMontagemService.cs` (881), `AgrupamentoVisualService.cs` (773), `CotarPecaFabricacaoService.cs` (749), `MarcarPecasService.cs` (722), `CortarElementosService.cs` (717), `AutoVistaService.cs` (719), `NumeracaoItensService.cs` (701), `Trelica/CotarTrelicaService.cs` (720) | Cada um carrega risco análogo |
| MÉDIO | `EscadaService` (500), `PfTwoPileCapRebarService` (502), `DstvExportService` (482, orquestração), `PlanoMontagemService` (477), `PlacaBaseLancamentoService` (418), `PipeRackService` (411), `ContraventamentoPlanoService` (364), `TagearTrelicaService` (375), `ModelCheckService` (541, mas Rules sem teste), `TrelicaRevitHelper` (459, "cobertura fantasma") | |

### 7.3 Helpers puros sem teste (gap)

`DstvHeaderBuilder` (365 LOC, sem teste — só Writer), `DstvHoleExtractor` (sem teste), `NumeracaoItensCatalog`, `PfNamingCatalog`, `PfRebarShapeCatalog`, `PfRebarTypeCatalog`, `ModelCheck/ModelCheckCollector`, **10 `ModelCheckRules/*Rule.cs` (zero testes)**.

### 7.4 Testes fracos

| Padrão | Frequência | Comentário |
|---|---|---|
| `Constructor_Default_*` em POCO | ~30 testes | Testa o compilador, não o código |
| `Assert.NotNull` / `Should().NotBeNull` / NotThrow | 67 ocorrências em 28 arquivos | ~1 em cada 12 asserts checa só nulidade |
| **`[Fact(Skip="Requer Revit")]` com corpo vazio** | **50 em `TrelicaRevitHelperTests.cs`** | **"Cobertura fantasma" — engorda 787 sem rodar nada** |
| `Resumo_ComZeroElementos` testando string exata "Cotas criadas:        0" | brittle | Quebra com mudança cosmética |
| `Smoke/SmokeTests.cs` — `Sanity_Check_Passa` testa `2+2=4` | placeholder | Não toca SteelBIM.dll |

### 7.5 Test factories / builders — **NÃO EXISTEM**

Grep `Builder|Factory|TestData|Fixture` retorna só matches do código de produção. **Nenhum helper `MakeBeam()`, `MakeColumn()`, `MakePileCap()` compartilhado.** Adicionar campo numa config = editar ~30 testes a mão = friction = pressão para não escrever teste novo.

### 7.8 Mocks/fakes para Revit — **ZERO**

Sem framework de mock Revit. **Toda lógica que toca Revit roda 0 vezes em CI.** O teste do `PfStirrupHookRules` protege o cálculo do multiplier, mas NÃO protege o `FilteredElementCollector.FirstOrDefault()` retornando hook antigo com multiplier 6 (exatamente o risco identificado em PHASE 5 §5.1 #2).

### 7.9 Gaps específicos ("outro v2.4.1 escondido")

Lista priorizada de testes que deveriam existir:

1. **`PfRebarService.GetHookTypeByAngle` retorna gancho antigo cached** — cenário com hook pre-existente multiplier=6
2. **`InsertColumnStirrups` zoneamento — pilar curto** (clearHeight < 2 × AlturaZona)
3. **`ValidateMinimumBarSpacing` — limite NBR não verificado**
4. **`MarcarPecasService` — colisão de número entre duas rodadas**
5. **`NumeracaoItensService` — colisão de marca com `V-001` manual pré-existente**
6. **`ListaMateriaisExportService` — soma de peso cross-cultura pt-BR/en-US** (risco financeiro real)
7. **`CotasService`/`CotarPecaFabricacaoService` — cota com texto vazio quando elemento sem perfil**
8. **`DiagramaMontagemService` — ordem de eixos com `AA` ou `0`**
9. **`AutoVistaService` — duplica vista se já existe nome igual**
10. **`DstvHeaderBuilder` — formato de data pt-BR vs DSTV `YYYY-MM-DD`**

### Top-3 prioridades PHASE 7

1. **Extrair lógica pura de `PfRebarService` para 3-4 helpers testáveis** (`PfRebarSpacingValidator`, `PfRebarZoneCalculator`, `PfRebarBarPositionBuilder`, `PfHookTypeResolver` com cache-busting). **12h**
2. **Criar `SteelBIM.Tests.Builders/`** — factories `BeamFactory.WithLength(6000)`, `ColumnFactory.Rectangular(...)`, `ConfigBuilders.PfColumnStirrups(...)`. **6h**
3. **Substituir os 50 `[Fact(Skip="Requer Revit")]`** — usar RvtTestRunner OU remover e mover conteúdo para docs XML. Cobertura fantasma é pior que cobertura zero (mascara o problema). **4-24h**

**Estimativa total para cobrir 5 CRÍTICO + top-3:** **40-50h** (1.5-2 semanas dev focado).

---

## PHASE 8 — Documentação

### Estado atual

| Item | Status |
|---|---|
| README.md | v2.6.0 OK (47 buttons → 48 fix, recém-reescrito em v2.5.0) — **ZERO screenshots/GIFs de produto** (apenas 4 badges) |
| CHANGELOG.md | Honesto, com BREAKING (UI) marcado em v2.6.0, hotfix v2.4.1 com nota "fixed (CRITICAL)" |
| ADRs (`docs/ADR/`) | 10 ADRs (001-010). Bem distribuídos (88-427 linhas cada) |
| `docs/legal/` | EULA.md, PRIVACY.md, TOS.md — **DRAFTS pendentes de revisão jurídica** (documentado em ADR-010) |
| XML docs em Services | 48 public types, 194 `/// summary` blocks (~4 por tipo) — decent, distribuição desconhecida |
| Code comments | Em geral explicam **why**; alguns inline são redundantes (cosmético) |

### Gaps específicos

| Sev | Achado |
|---|---|
| MÉDIO | **README sem screenshot/GIF de produto.** Para soft launch comercial em plugin Revit, comprador típico precisa ver o ribbon e pelo menos 2 outputs (Diagrama de Montagem, DSTV) antes de baixar. 4-8h pra capturar + comprimir |
| MÉDIO | **Faltam ADRs para decisões importantes recentes:** (a) ribbon split em 2 abas (decisão Alef v2.6.0); (b) helper `PfStirrupHookRules` extraído (v2.4.1); (c) PiiScrubber design (regex vs whitelist). 2h cada |
| MÉDIO | **`docs/legal/*.draft.md` ainda em draft** — backlog conhecido. Bloqueia distribuição massiva a empresas (departamento jurídico do cliente vai exigir) |
| BAIXO | Sem manual de usuário "Get Started in 10 minutes" — onboarding fica nas tooltips |
| BAIXO | Sem changelog de breaking changes em formato consumível por usuário final (CHANGELOG.md é técnico) |
| BAIXO | Sem páginas individuais de tooltip-rico (`LongDescription`) — ver PHASE 4.2 |

### Top-3 prioridades PHASE 8

1. **Adicionar screenshots do ribbon + 2-3 outputs no README** — 4h. Maior impacto/esforço para soft launch
2. **Revisão jurídica de EULA/PRIVACY/TOS** — semanas (não-dev). Não bloqueia soft launch a alunos, bloqueia ampliação a empresas
3. **Escrever ADR-011 (Ribbon split v2.6.0) + ADR-012 (PfStirrupHookRules pattern)** — 4h. Pattern do `PfStirrupHookRules` deveria virar template para próximos helpers NBR

---

## PHASE 9 — Workflow do Usuário (Heuristic Walks)

### Cenário A — Engenheiro recebe Revit pronto, precisa entregar prancha de obra

**Fluxo provável:**
1. Abre Revit, modelo carregado → **2 abas visíveis**. Qual clicar? Provavelmente Detalhamento. ✓ (mais intuitivo após split)
2. Provavelmente quer rodar `Marcar Peças` primeiro (na aba Detalhamento → Anotação). Window abre (722 LOC code-behind). Click OK.
3. `Diagrama de Montagem` (Detalhamento → Montagem e Sequenciamento) → 10 transactions executam. **🔴 Se tx5 falhar, tx6-10 NÃO rodam** (PHASE 5 §5.9). Usuário recebe resultado parcial sem aviso claro.
4. `Cotar Fabricação` para shop drawings.

**Friction points sintetizados:**
- **Sem indicação de ordem** dos comandos (qual rodar primeiro?)
- **DiagramaMontagem partial-fail** = produto incompleto silenciosamente
- **Long-running services sem progress/cancel** (PHASE 1 ADR-004) = freeze de UI em modelo grande
- **Em 1000 elementos:** ~30+ segundos cumulativos de freeze por PHASE 3 findings
- **Sem `LongDescription`** = onboarding via tentativa-e-erro

### Cenário B — Detalhador novo, primeiro projeto

1. **Primeiro boot:** consent dialog aparece — **🔴 provavelmente atrás do Revit** (PHASE 6 §6.10, sem `Owner` setado). User precisa Alt+Tab pra achar.
2. 48 botões em 2 abas. Tooltips ajudam (100% têm), mas sem rich `LongDescription`.
3. **"PF Construção" com 1 botão único + "Verificação" com 1 botão único + "Conexões" com 1 botão único** (PHASE 4 §4.1) = parece "sob construção"
4. Primeira tentativa de PF estribo: se modelo não tem RebarHookType 135° pre-configurado, fix v2.4.1 cria certo. **🔴 Se modelo é old com hook 135° stale (multiplier=6 do v2.4.0), bug-class repete** (PHASE 5 §5.1 #2)
5. **Sem in-app tour, sem first-run wizard.** User pokes around.

**Friction:** painéis órfãos passam impressão de produto incompleto; primeiro consent dialog visualmente escondido = impressão de "travou"; potencial regressão NBR silenciosa em template antigo.

### Cenário C — Cliente Tekla migrando, modelo existente

1. Tekla model importado pro Revit. **Provavelmente lacks** famílias EMT-standard (parâmetros `Modelo = Consolo`, hole-by-parameter convenção EMT).
2. `Isolar Pilares Estruturais` → funciona, é genérico. ✓
3. `PF Isolar Pilares + Consolos` → empty result (sem famílias EMT). Mensagem provável: `AppDialogService.ShowWarning`. **Qualidade da mensagem determina se user entende ou pensa "plugin não funciona".**
4. `Exportar DSTV/NC1` → `DstvHoleExtractor` lê `Hole 1 Diameter`...`Hole 60 Diameter` (PHASE 5 §5.7). Tekla-imported families não têm. **🔴 Exporta sem furos. Tekla-refugee conclui: "SteelBIM não funciona".**
5. `Marcar Peças` → assinatura usa `Element.Parameters` iteration (PHASE 5 §5.6). Roda 2x, recebe marcas diferentes. **🔴 Confiança morta.**

**Friction:** plugin tem premissas implícitas sobre famílias EMT que **não estão documentadas no onboarding**; o caminho Tekla-refugee é o caminho de pior impressão.

### Recomendações PHASE 9

| Sev | Recomendação | Esforço |
|---|---|---|
| ALTO | **Mensagens de erro orientadas:** quando `PfIsolarPilaresConsolos` não acha famílias EMT, mensagem deve dizer: *"Nenhuma família com parâmetro 'Modelo' = 'Consolo' encontrada. Este comando espera famílias EMT-standard (ver docs/PF-FAMILIAS.md). Para usar com famílias customizadas, ajuste o parâmetro `NomeParametroModelo` em config."* (e criar `docs/PF-FAMILIAS.md`) | 4h |
| ALTO | **Onboarding screen no primeiro boot** com 5 cards: "Você quer modelar?" / "Detalhar?" / "Exportar DSTV?" / "Gerar Diagrama BR?" / "PF Concreto?" cada link levando a 30s tour. Drastically reduce time-to-first-success | 1-2d |
| MÉDIO | **Documentar que `Diagrama de Montagem` tem partial-fail recovery** (após fix PHASE 5 §5.9) | docs |
| MÉDIO | **Setar `Owner` no `PrivacyConsentWindow`** (já listado em PHASE 6) | 30min |

---

## 10. Recomendações Priorizadas — Pré-Launch

### 🚨 P0 — Hotfix imediato (< 1 semana, ~3 dias dev)

1. **🔴 `Bloco/RebarCreationService.cs:153-161` + `PfRebarService.cs:912-918`** — adicionar `.Where(x => x.MultiplierForTailLength >= PfStirrupHookRules.NbrStirrupHookMultiplier(angleDegrees))`. **Bug-pattern v2.4.1 ativo. Inadmissível ir a soft launch sem corrigir.** **3h + tests**
2. **🔴 `MarcarPecasService:398-418` + `:380,383`** — assinatura determinística (Definition.Name ordenado; Family.Name+Type.Name em vez de ElementId). **Idempotência cross-project é requisito de fabricação. 1d + tests**
3. **🔴 `DiagramaMontagemService:50-205`** — try/catch por transaction (10 transactions independentes de verdade). **2h**
4. **🔴 `UpdateDownloader.cs:75-89`** — assertion de timeout no HttpClient. **5min**
5. **🔴 `KeySigner.cs:37`** — apagar comentário stale "fallback DEV_ONLY". **1min**
6. **🔴 Apagar 5 `.bak-alef-v1.5`** — `Services/PF/PfRebarService.cs.bak-alef-v1.5` + 4 em `Commands/PF/`. **5min**
7. **🔴 PostHog host force HTTPS** (`PostHogHttpTelemetryClient:155-159`). **5min**
8. **🔴 PiiScrubber expandir** — `evt.ServerName`, regex UNC, regex `.rvt`/`.rfa`, regex `C:\Usuários\` ptBR, iterar Stacktrace.Frames. **4h**

### 🟠 P1 — Pré-comercial ampliação (< 1 mês, ~3 semanas dev)

9. **🔴 CAA + cobrimento mínimo NBR** — `enum Caa`, tabela NBR Tabela 7.2, validação. **Sem isso plugin é indefensável tecnicamente em CREA para obras costeiras/industriais.** **1d**
10. **🟠 `PfNbr6118AnchorageService:36`** — corrigir bug dimensional `lb,mín` (mm vs cm). **1h**
11. **🟠 Espaçamento mínimo com `1.2·Dmáx`** — `PfRebarService:1431`, `PfTwoPileCapRebarService:359`. Aceita parâmetro `DmaxAgregadoMm` (default 20mm = brita 1). **2h**
12. **🟠 Migrar 5 services para ADR-003+004** (`PfRebarService`, `CotasService`, `MarcarPecasService`, `CotarPecaFabricacaoService`, `DiagramaMontagemService`). **3d** — desbloqueia confiança em batch + cancel + progress
13. **🟠 Extrair `SeccionarVigaService` de `CmdCortarPerfilPorInterferencia`**. **1d**
14. **🟠 Performance Top-3 PHASE 3:** AutoVistaService (-25s), OverlappingElementsRule (-30s catastrófico), PfRebarService FECs (-9s). **10h total**
15. **🟠 Code-signing efetivo + Authenticode-check no UpdateApplier** (já em backlog). **Dependência externa**
16. **🟠 README com screenshots/GIFs do produto.** **4h**
17. **🟠 `LongDescription` em comandos complexos** (Diagrama, DSTV, Cotagem, ModelCheck, PF Armaduras). **4h**
18. **🟠 Splitar `PF Armaduras` (7→4+3) e `Visualização` (7→4+3)**. **2h**
19. **🟠 `PrivacyConsentWindow.Owner` setado**. **30min**
20. **🟠 Re-inicializar Sentry/Telemetry após consent v3 aceitar**. **15min**

### 🟡 P2 — Sustentação (próximos 3 meses, ~6 semanas dev)

21. **🟡 Refatorar `PfRebarService` (2139 LOC) em helpers puros** (`PfRebarSpacingValidator`, `PfRebarZoneCalculator`, `PfRebarBarPositionBuilder`, `PfHookTypeResolver`). **12h** — o investimento que mais reduz risco de "v2.4.1 #2"
22. **🟡 Criar `SteelBIM.Tests.Builders/`** com factories. **6h**
23. **🟡 Substituir 50 `[Fact(Skip="Requer Revit")]`** por RvtTestRunner ou docs XML. **24h** se for RvtTestRunner; 4h se for limpeza
24. **🟡 Split `ListaMateriaisExportService` (2225 LOC)** em 4 classes SRP. **1d**
25. **🟡 Migrar 13 services restantes para ADR-003** (Tercas, Trelica, Travamento, GuardaCorpo, PipeRack, Escada, NumeracaoItens, AutoVista, BlocoFundacaoOrchestrator, PfIsolation, PfNaming, Conexoes/PlacaBase, etc.) **3d**
26. **🟡 DSTV blocos AK + IK + SC oficial** — para compatibilidade Ficep/Voortman real. **2d**
27. **🟡 DSTV hole extraction via geometria Revit nativa** — caminho Tekla-refugee. **2d**
28. **🟡 Espaçamento máximo NBR 6118 18.3.5.1/18.4.2.2/18.4.3** — validação. **4h**
29. **🟡 `IDisposable` em FilteredElementCollector** (105 ocorrências). **4-8h**
30. **🟡 Cleanup comentários stale + Generar→Gerar typo + dead code (`CotasService.ExecutarCotagemAlinhada`)**. **4h**
31. **🟡 Revisão jurídica EULA/PRIVACY/TOS** (não-dev). **Semanas**
32. **🟡 Onboarding wizard primeiro boot** (5 cards). **1-2d**
33. **🟡 docs/PF-FAMILIAS.md** — premissas de família EMT-standard. **2h**
34. **🟡 ADR-011 Ribbon split + ADR-012 PfStirrupHookRules pattern**. **4h**

### Estimativa total

| Bloco | Esforço |
|---|---|
| P0 (hotfix) | 3 dias dev |
| P1 (pré-comercial) | 3 semanas dev + dependências externas (code-signing) |
| P2 (sustentação) | 6 semanas dev + revisão jurídica |
| **TOTAL** | **~12-15 semanas dev** |

---

## 11. Apêndice — Pontos positivos confirmados (mantenha o que está bom)

- ✅ **HMAC secret nunca hardcoded** (env/file only) + constant-time compare (`CryptographicOperations.FixedTimeEquals`)
- ✅ **DPAPI CurrentUser** em 100% dos call sites (6/6)
- ✅ **Base64URL canônico** forçado (anti-replay no licensing)
- ✅ **Consent gating funcional** em Telemetry+Sentry (no-op silencioso quando denied/unset)
- ✅ **`SessionIdProvider`** puramente anônimo (Guid.NewGuid, sem MachineName/MAC)
- ✅ **`.gitignore`** cobre exaustivamente (`.pfx`, `.env`, `license.secret`, `sentry.dsn`, `posthog.apikey`)
- ✅ **Zero `appsettings*.json` com credentials** commitado
- ✅ **PostHog host default EU** (LGPD-friendly) em HTTPS
- ✅ **Telemetry HttpClient com `Timeout=10s`** singleton lazy
- ✅ **ADR-006 (auto-update)** sólido: 3-attempt real com persistência JSON, SHA-256 re-validação, ZipSlipValidator, top-level check, version check, IsFileInUseException específico
- ✅ **ADR-007 PiiScrubber** está no lugar certo (BeforeSend hook), é puro, determinístico, com 14 testes
- ✅ **4 services compliant com ADR-003+004** (`Lista`, `ModelCheck`, `Dstv`, `Agrupamento`) — playbook funciona, replicar
- ✅ **`PfStirrupHookRules` extraído + testado** — modelo a seguir para outros helpers NBR
- ✅ **`PfNbr6118AnchorageService`** implementa NBR 6118 9.4.2.4 corretamente (η1·η2·η3·fctd, fyd/fbd) — exceto bug `lb,mín`
- ✅ **`CotasService.cs:17-33`** — 6 constantes nomeadas no topo — modelo exemplar de gestão de magic numbers
- ✅ **`NumberParsing.cs`** — parsing dual-culture centralizado (lição da vírgula pt-BR aprendida)
- ✅ **`ListaMateriaisExportService`** — células numéricas via `double` (ClosedXML preserva tipo) + FORMULAS (SUMPRODUCT/SUM); comprimentos em metros consistente
- ✅ **`PfNamingService`** — ordenação determinística (BeamAxisGroup → snapped → Id), helper formatter puro testado
- ✅ **`PfTwoPileCapBarCatalog`** — snapshot test cobre o catálogo
- ✅ **Todos os 48 botões com tooltip não-vazio** (verificado em App.cs)
- ✅ **99% dos diálogos via `AppDialogService`** (84 chamadas vs 2 `TaskDialog.Show` legados)
- ✅ **`AddInId` GUID preservado** através de todos os refactors (zero risk de re-registrar como plugin novo)
- ✅ **Todas as `Transaction` em `using`** (56/56) — IDisposable bem aplicado nesse caso
- ✅ **`ContraventamentoPlanoService`** — único uso correto de `TransactionGroup` no projeto; bom exemplo a replicar
- ✅ **CI 3 jobs (Build/Test/Format)** verde em todos os releases de v2.0.0 a v2.6.0 (consistência)

---

## 12. Metodologia & Limitações

**Modo:** Read-only. Zero código alterado, zero testes executados destrutivamente. Apenas Read/Grep/Glob + leitura de docs.

**Cobertura:** Auditei `SteelBIM/App.cs`, `SteelBIM/Services/**` (76 arquivos), `SteelBIM/Commands/**` (50 arquivos), `SteelBIM/Models/**` (51 arquivos), `SteelBIM/Infrastructure/**` (38 arquivos), `SteelBIM/Views/**` (45 arquivos), `SteelBIM/Licensing/**`, `SteelBIM.Tests/**` (77 arquivos), `docs/ADR/` (10 ADRs), `docs/legal/`, `CHANGELOG.md`, `README.md`, `SteelBIM.csproj`, `.gitignore`, `SteelBIM/installer/Build-SetupExe.ps1`.

**6 agentes paralelos** executaram PHASES 1, 2, 3, 5, 6, 7 com prompts estruturados e formato de saída padronizado. PHASE 0 (foundational), 4 (UX), 8 (Docs), 9 (Workflow), 10 (síntese) executados na thread principal.

**Limitações conhecidas:**
- Sem execução de testes — não detecta flaky tests, real-world performance numérica
- Sem instalação no Revit — workflow walks são heurísticos, não empíricos
- Sem inspeção de telemetria/Sentry real — confiança no que código diz, não no que servidor recebe
- Sem testes de penetração ativos (DPAPI cracking, license forgery) — só code review
- Sem revisão jurídica de EULA/PRIVACY/TOS (não é escopo de eng)
- Sem comparação Tekla→SteelBIM com dataset real (caminho Cenário C é hipotético)

**Próxima auditoria recomendada:** após P0+P1 implementados (~1 mês), focar em (a) verificação Authenticode pós code-signing, (b) compatibilidade DSTV com máquina Ficep/Voortman real, (c) cobertura efetiva pós-extração de helpers do `PfRebarService`.

---

*Auditoria conduzida em modo autônomo (sem stops para clarificação) por arquiteto sênior. Disposição brutal-mas-construtiva. Foco em "tudo que pode melhorar antes do mercado". Cada finding tem file:line concreto. Severidade conservadora: o que está marcado CRÍTICO é fim-de-mundo se ignorado, não exagero retórico.*
