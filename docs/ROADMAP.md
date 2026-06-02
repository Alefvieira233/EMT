# Roadmap — SteelBIM

> **Nota histórica:** o projeto chamou-se FerramentaEMT até v1.x. Rebrand
> para **SteelBIM** em v2.0.0 (2026-05-13). Versões pré-v2.0.0 usam o
> nome antigo intencionalmente.

## Status atual

- **Versão estável:** v2.8.9 (2026-05-30)
- **Fase:** soft launch funcional com beta selecionado
- **Próxima major:** a definir (série v2.8.x já em produção; v2.8.0→v2.8.9 entregues)
- **Auditoria técnica:** 2026-05-25 (5 dimensões, 3 senior reviewers)
  → roadmap consolidado abaixo

---

## O que já foi entregue (v0.9 → v2.7.6)

Resumo cronológico dos marcos. Histórico detalhado em [CHANGELOG.md](../CHANGELOG.md).

### Fundação (v1.0 → v1.5)

- ✅ Logging estruturado (Serilog) + `FerramentaCommandBase` + Constants
- ✅ 22 → 33 commands no ribbon
- ✅ DSTV/NC1 export (CNC direto do Revit, sem Tekla)
- ✅ ModelCheck (10+ regras de validação)
- ✅ Cotagem de treliça (5 faixas padrão BR)
- ✅ Refactor god services (parcial — ainda em curso)

### Pré-fabricado + features killers (v1.5 → v2.4)

- ✅ Módulo PF completo (13 commands, armaduras NBR 6118)
- ✅ Diagrama de Montagem padrão BR EM-08
- ✅ Sequenciamento BIM (4D phasing — Synchro/Navisworks)
- ✅ Gerador de Conexões (chapa ponta, dupla cantoneira, gusset)
- ✅ Rebranding FerramentaEMT → SteelBIM (v2.0.0)
- ✅ Ribbon split (Modelagem + Detalhamento — v2.6.0)

### Conversor IFC + maturidade (v2.7.0 → v2.7.6)

- ✅ **Conversor IFC → Perfis Nativos** (feature maior, co-autoria com Victor)
- ✅ Dialog modeless + ExternalEvent + filtro estrutural
- ✅ Section orientation preservada (U/L/T rotations)
- ✅ Colunas inclinadas (`StructuralType.Brace`)
- ✅ Canonicalização de ícones (84% → 88% conformidade)
- ✅ 954 testes automatizados, build Release 0 warnings, TreatWarningsAsErrors

---

## Roadmap próximo — v2.8.0 (~10 semanas)

Objetivo: **production-grade**. Plugin pronto para distribuição comercial
B2B com cert digital, EULA aprovada, auto-update seguro, manual mínimo
e cobertura de testes do núcleo PF.

### Sprint 0 — Hotfix imediato (2-3 dias)

- [x] **CI hardening** — cache NuGet, timeouts, dorny test-reporter, job EmtKeyGen (v2.7.7, PR #20)
- [x] **README sync** v2.6.4 → v2.7.6 + roadmap honesto (PR #21)
- [x] **Release publish workflow** — `softprops/action-gh-release` (PR #22, ativa quando cert chegar)
- [ ] **ROADMAP rewrite** (este arquivo)
- [ ] **Smoke tests reais** — substituir `2+2=4` por plugin load + 49 commands instanciáveis
- [ ] **Conversor IFC: `IProgress` + `CancellationToken`** — resolve trava em modelos > 5000 elementos
- [ ] **Disparar Sectigo OV cert** (ação Alef — lead time 7 dias úteis)
- [ ] **Contatar advogado TI** (ação Alef — lead time 1-2 semanas)
- [ ] **Decisão de pricing** (ação Alef — tiers + valores)

### Sprint 1 — Hardening security (2-3 semanas, paralelo com terceiros)

- [ ] **Code signing efetivo** — Sectigo OV cert configurado em GitHub Secrets, release.yml assina setup.exe
- [ ] **Re-publish v2.7.5/v2.7.6 com assets** assinados (`gh workflow run release.yml -f tag_name=v2.7.6`)
- [ ] **Authenticode verification pós-extract** no `UpdateDownloader` (`X509Certificate2.Verify()`)
- [ ] **EULA + Privacy + TOS** revisados e ativados (`EulaConfirmation.ShowEulaPrompt = true`)
- [ ] **Sentry breadcrumbs scrubbing** (LGPD: previne vazamento de filenames de cliente em crash reports)

### Sprint 2 — Arquitetura ADR-003 (2-3 semanas)

Template: refatorar `AutoVistaService` como modelo de service mudo (Result<T> + IProgress + CT, sem AppDialogService inline). Aplicar pattern em onda em outros services.

- [ ] **`IUIDecisionService` interface** + injeção em `AutoVistaService`
- [ ] **Onda 1:** 6-7 services migrados pro pattern
- [ ] **Top-3 windows → MVVM** (`PfColumnBars`, `PfBeamBars`, `NumeracaoItens` — 774, 754, 548 LOC)
- [ ] **`ListaMateriaisExportService` Strangler Fig** — extrair `BomAggregator` + `ExcelFormatter` (2225 LOC → 3 services finos)
- [ ] **Quick wins:** delete dead code (`ExecutarCotagemAlinhada`), extrair `CoordinateParsingService`, `#nullable enable` em top 20 arquivos

### Sprint 3 — Testes + performance (3-4 semanas)

- [ ] **`PfRebarServiceTests.cs`** — 100+ tests cobrindo `GerarEstribosPilar`/Viga/Consolo/Bloco/Estaca (2178 LOC sem cobertura hoje)
- [ ] **Performance hot paths:** cache `CalcularScore`, `Materials` dict, filtro estrutural IFC, image cache no boot
- [ ] **ExternalEvent em 5-10 commands >1s** (CotarTrelica, ModelCheck, DSTV, DiagramaMontagem)
- [ ] **`CancellationToken` compliance batch** (30+ services — target 80% adoção ADR-004)
- [ ] **`#nullable enable` 100%** + resolver warnings em batch
- [ ] **E2E integration tests** — 3-5 testes (Cotar Treliça, PfRebar, IFC Conversion)

### Sprint 4 — Release + GTM (1-2 semanas)

- [ ] Bump **v2.8.0** + CHANGELOG consolidado
- [ ] **Dependabot config** para SHA pinning automático de GitHub Actions
- [ ] **`packages.lock.json`** committed (`dotnet restore --lock-mode strict`)
- [ ] README com **pricing público** + 5 screenshots + 1 GIF demo + comparison table
- [ ] **Landing page mínima** (site próprio + Stripe button) — opcional, pode ficar pra v2.8.1
- [ ] **Sentry KPI dashboard** + **PostHog funnel** semanais
- [ ] **Sales playbook** (FAQ + email templates)

---

## Roadmap v3.0 (Q4 2026)

Backlog confirmado pós-v2.8.0. Prioridades a refinar conforme tração comercial.

### Distribuição enterprise
- **MSI via WiX** — deploy via Group Policy (SCCM/Intune) para construtoras grandes
- **Co-existência Revit 2025 + 2026** — matrix CI multi-target
- **EV cert** — instant SmartScreen reputation (~R$ 3k/ano)
- **Autodesk App Store** — descoberta automática + legitimidade

### Arquitetura
- **Refactor god services full** — `PfRebar` Strategy, `DiagramaMontagem` 3-split, `CotasService` simplificada
- **TransactionGroup pipelines** — Cotar Treliça (10 etapas), Diagrama Montagem (5 etapas) — Undo compound atômico
- **ConfuserEx obfuscation** — quando base >100 usuários, dificulta bypass HMAC casual

### Licensing & infraestrutura
- **License-as-a-service** — validação online, revogação rápida, observabilidade real
- **Auto-update E2E tests** + telemetria de adoção

### Documentação & produto
- **Manual PDF 40 páginas** (designer/writer freelancer)
- **Video tutorials** (5-10 vídeos por feature)
- **Help in-app** (tooltip + link "Documentação" em cada window)
- **i18n EN/ES** — se decidir mercado LATAM/internacional

### Testes
- **FsCheck property-based testing** — NBR 6118 calculations, geometria Treliça, IFC transforms
- **BenchmarkDotNet** baselines — regressão de performance detectável em CI

---

## Métricas de sucesso v2.8.0 production-grade

| Categoria | Hoje (v2.7.6) | Meta v2.8.0 |
|---|---|---|
| Code-signed releases | ❌ | ✅ Sectigo OV |
| Legal docs (EULA/Privacy/TOS) | ❌ `<TBD>` | ✅ aprovado |
| Auto-update Authenticode verify | ❌ | ✅ |
| Smoke tests reais (não `2+2=4`) | ❌ | ✅ |
| Cobertura `PfRebarService` (2178 LOC) | 0% direta | ≥80% |
| ADR-003 compliance (services mudos) | ~30% | ≥95% |
| ADR-004 compliance (CT em ops >1s) | ~17% | ≥80% |
| `#nullable enable` files | ~18% | 100% |
| Conversor IFC em 6983 elementos | 30-120s sem feedback/cancel | <5s, cancelável |
| ModelCheck | ~10-20s | <2s |
| README versão sync | manual | CI valida |
| Releases com assets baixáveis | ❌ (v2.7.5/v2.7.6) | ✅ |
| Pricing público | ❌ | ✅ |

---

## Contato

Para questões sobre o roadmap, parcerias, ou acesso antecipado:

- **Email:** engenheiroalefvieira@gmail.com
- **Issues GitHub:** https://github.com/Alefvieira233/EMT/issues

Última atualização: 2026-05-25
