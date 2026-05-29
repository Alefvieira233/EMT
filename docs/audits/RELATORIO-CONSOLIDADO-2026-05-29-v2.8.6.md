# Relatório Consolidado de Análise Geral — SteelBIM v2.8.6

**Data:** 2026-05-29
**Versão analisada:** v2.8.6 (commit `5573153`)
**Metodologia:** 5 sub-agents (dev seniors) em paralelo cobrindo Arquitetura, Performance, UX, Segurança, Code Quality.

---

## Sumário executivo (TL;DR)

O plugin está **maduro e saudável**: 1223 testes verdes, build Release sem warnings, ADRs documentadas, sistema de licenciamento HMAC robusto, postura de segurança acima da média do mercado de Revit add-ins. **Não há blockers críticos** (P0/P1) em nenhuma dimensão.

**Os 3 problemas mais valiosos identificados** convergem entre os 5 agents:

1. **Padrão Progress/Cancel duplicado** entre `RevitProgressHost` (síncrono modal) e ConverterPerfilIfcWindow (modeless reimplementado). O bug do v2.8.6 foi sintoma dessa duplicação. **Fix arquitetural:** `RevitProgressHost.RunModeless`.
2. **Logger sync sem `Sinks.Async`** — pós-v2.8.6 cada ignorado faz `File.Write` síncrono na thread Revit API. Em galpão 6000 elementos com 30% ignorados, **270ms-9s** de overhead invisível.
3. **Adoção parcial do `#nullable enable`** (23% dos arquivos) e do ADR-003 — 13 services antigos ainda chamam `AppDialogService` direto, fragilizando garantia de TreatWarningsAsErrors.

**Quick wins de 1h-3h cada com alto ROI** (detalhados abaixo): adicionar `Sinks.Async`, pré-ativar símbolos no Conversor IFC, cachear `existentes` em AutoVista, scrubbing PII no Logger local, `IsDefault`/`IsCancel` em massa.

---

## Sprint recomendado — 3 dias (priorizado por impacto × esforço)

### Dia 1 — Hardening defensivo (≤4h)

| # | Ação | Origem | Esforço | Impacto |
|---|------|--------|---------|---------|
| 1 | Adicionar `Serilog.Sinks.Async` + envolver File sink | Performance | 15min | Alto (desbloqueia thread API em qualquer log spike) |
| 2 | Agregar Logger.Warn por elemento em ConverterPerfilIfc → 1 Warn final + Logger.Debug por item | Performance | 30min | Alto (cura raiz do risk de spike v2.8.6) |
| 3 | Substituir todos `catch { }` em services por `catch (Exception ex) { Logger.Warn(ex, ...) }` (12 ocorrências) | Arquitetura | 1h | Alto (impede repetição da saga v2.8.6 noutros services) |
| 4 | Aplicar `PiiScrubber.Scrub` em `LicenseService.cs:110` + email/UserName no Logger | Segurança | 20min | Médio (P2-3) |
| 5 | Whitelist `https` em `Process.Start(ReleaseUrl)` (`LicenseActivationWindow.xaml.cs:172`) | Segurança | 10min | Médio (P2-4) |
| 6 | `Guid.NewGuid()` no `sharedFile` temp (`PlanoMontagemService.cs:62`) | Segurança | 5min | Baixo (P2-5) |
| 7 | `ex.Message` em vez de `ex.ToString()` em `TrelicaWindow.xaml.cs:30`, `TravamentoWindow.xaml.cs:31`, `TercasWindow.xaml.cs:36` | UX | 15min | Alto (não mostra mais stack trace ao engenheiro) |

**Total dia 1: ~3h** | Risco: zero (mudanças aditivas + 1-liners)

### Dia 2 — Performance + UX consistency (≤6h)

| # | Ação | Origem | Esforço | Impacto |
|---|------|--------|---------|---------|
| 8 | Pré-ativar símbolos do Conversor IFC fora do loop (`ConverterPerfilIfcService.cs:113-117`) | Performance | 30min | Alto em galpões heterogêneos (segundos economizados) |
| 9 | Cachear `existentes` em `AutoVistaService.GerarNomeUnico` (`:950-967`) | Performance | 45min | Alto em batch (100× collectors → 1) |
| 10 | Cachear `RebarBarType`/`RebarShape` em `PfRebarService.ExecuteForHosts` | Performance | 1h | Alto em armaduras batch |
| 11 | Adicionar `IsDefault="True"` / `IsCancel="True"` em 41 janelas faltantes | UX | 1h | Alto (Enter submete forms em todas as janelas) |
| 12 | Promover `PredicateSelectionFilter` (`PfElementService.cs:379`) para `Utils/` e eliminar 6 ISelectionFilter ad-hoc | Code Quality | 30min | Médio (−80 LOC duplicadas) |
| 13 | Auditar `ex.ToString()` no codebase + helper `AppDialogService.ShowExceptionFriendly` | UX | 1h | Alto (mensagens amigáveis universais) |

**Total dia 2: ~5h** | Risco: baixo (encapsulamentos puros, sem refactor estrutural)

### Dia 3 — Refactor arquitetural focado (≤8h)

| # | Ação | Origem | Esforço | Impacto |
|---|------|--------|---------|---------|
| 14 | Criar `RevitProgressHost.RunModeless<T>(ExternalEvent, ...)` que encapsule CTS+Window+cleanup | Arquitetura | 4h | Muito Alto (elimina classe inteira de bugs v2.8.6) |
| 15 | Migrar `ConverterPerfilIfcWindow` para usar `RunModeless` | Arquitetura | 2h | Alto (consolida pattern) |
| 16 | Extrair `Utils/UnitConversions.cs` (`MmFromFeet`, `FeetFromMm`, `MmPorPe=304.8`) + substituir em 10 arquivos | Code Quality | 30min | Médio (consistência semântica) |
| 17 | Adicionar `<Nullable>enable</Nullable>` global no csproj + `<WarningsAsErrors>` desligado fase 1 | Code Quality | 1h | Médio (fundação pra futuro) |

**Total dia 3: ~7h** | Risco: médio (item 14-15 toca arquitetura — exige PR isolado + validação manual no Revit)

---

## Backlog estratégico (não-imediato)

- **Auditoria DPI das 9 janelas legacy** (`AppMessageWindow`, `NumeracaoItensWindow`, `PfNamingWindow`, `VerificarModeloReportWindow`, `CortarElementosWindow`, `ContraventamentoPlanoWindow`, `TravamentoWindow`, `TrelicaWindow`, `ConexaoConfigWindow`) — aplicar template DockPanel+ScrollViewer canônico de `ConexaoTercasWindow.xaml`. Prioridade alta no `AppMessageWindow` (afeta todos os erros do plugin).
- **Eliminar God classes**: split `PfRebarService` (2.159 LOC) e `ListaMateriaisExportService` (2.225 LOC). Estratégia: continuar o padrão Strangler Fig iniciado com `PfRebarServicePure`.
- **`SchemaVersion` em `AppSettings`** + método `Migrate()` — antes da primeira mudança de semântica de prop (P2-3 latente da auditoria arquitetural).
- **Bucket espacial em `OverlappingElementsRule`** — O(N²) com solid boolean é catastrófico em galpão 6000+. Prioridade alta se próximo cliente rodar ModelCheck em projeto grande.
- **`jti` no LicensePayload** + arquivo `revoked.txt` esqueleto — pré-distribuição comercial pra fechar P2-1.

---

## Findings que NÃO são problema (boa notícia)

- ✅ **Zero P0/P1 de segurança.** HMAC com `FixedTimeEquals`, DPAPI, Base64URL canônico, 6 validações no auto-update, `PiiScrubber` no canal de telemetria — postura madura.
- ✅ **Zero `BinaryFormatter`/MD5/SHA1/`new Random()` para crypto.**
- ✅ **Zero secrets em commits**, secret HMAC exclusivamente externalizado.
- ✅ **JSON via `System.Text.Json` sem `TypeNameHandling`** — sem RCE-via-deserialization.
- ✅ **Apenas 6 TODOs no codebase inteiro**, nenhum HACK/FIXME — débito técnico explícito mínimo.
- ✅ **ADR-004 (cancellation) bem implementado** em features novas (`RevitProgressHost`, `IfcConversionHandler`).
- ✅ **Sistema de tema centralizado** + ESC global + ProgressWindow honesto — UX base sólida.
- ✅ **`FerramentaCommandBase`** centraliza try/catch+license+telemetria → adotado por TODOS os Commands.

---

## Métricas de codebase (snapshot v2.8.6)

- **Testes:** 1223/1223 verdes (882ms)
- **Build Release:** 0 erros, 0 warnings
- **Arquivos:** 323 C# files
- **TODO/FIXME/HACK:** 6 ocorrências (5 arquivos, ~2%)
- **Top 5 arquivos por LOC:** `ListaMateriaisExportService` 2.225, `PfRebarService` 2.159, `CotasService` 1.240, `App.cs` 1.195, `DiagramaMontagemService` 1.035
- **Top 1 método por LOC:** `App.BuildAbaModelagem` 385 LOC (candidato a tabela de specs)
- **Nullable annotation adoption:** ~23% dos arquivos opt-in manual

---

## Conclusão

O plugin SteelBIM está em **estado de produção saudável**. Pode continuar sendo distribuído sem ações corretivas urgentes. As recomendações deste relatório são **otimizações** e **hardenings** — não fixes de bugs ativos. O bug do v2.8.6 foi a única manifestação séria recente e já está corrigido.

**Próxima decisão do Alef:** validar v2.8.6 manualmente no Revit (mesmo arquivo IFC do bug). Após confirmação, priorizar Sprint Dia 1 deste relatório (3h de hardening defensivo zero-risco) antes de qualquer feature nova.

---

*Relatório gerado por análise paralela de 5 agents — outputs originais arquivados em `.tasks/` temporários da sessão. Consultar AUDITORIA-SENIOR-2026-05-19-v2.6.0.md para contexto histórico.*
