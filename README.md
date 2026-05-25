# SteelBIM

[![Build & Test](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml/badge.svg)](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml)
![Versão](https://img.shields.io/badge/vers%C3%A3o-v2.7.11-blue)
![Licença](https://img.shields.io/badge/licen%C3%A7a-propriet%C3%A1ria-lightgrey)
![Testes](https://img.shields.io/badge/testes-1110%20passing-brightgreen)
![Plataforma](https://img.shields.io/badge/Revit-2025-orange)

> **Detalhamento estrutural brasileiro direto no Revit.** Treliças, terças,
> conexões, armaduras NBR 6118, Diagrama de Montagem padrão BR e export
> DSTV/NC1 para CNC — em 48 comandos do ribbon. Sem Tekla, sem AutoCAD
> intermediário, sem retrabalho.

---

## Por que SteelBIM existe

Escritórios de estrutura metálica e pré-fabricado no Brasil enfrentam o
mesmo dilema: o Revit nativo entrega o modelo, mas o **detalhamento de
prancha** (vistas, cotas, marcação, lista, fabricação) consome 40-60% do
tempo do projeto e força workflows manuais ou ferramentas estrangeiras
que não conhecem nossa norma. SteelBIM automatiza essa camada de
detalhamento seguindo padrões do executivo brasileiro — convenção EM-08
de prancha, NBR 6118 §9.4.6.1 para estribos, perfis U/UDC/W/HP nos
formatos do mercado local.

---

## Quem usa

- Escritórios de detalhamento metálico (vigas, pilares, terças, conexões)
- Projetistas de pré-fabricado de concreto (armaduras NBR 6118)
- Construtoras com fluxo BIM 4D (Synchro, Navisworks)

---

## O que faz

O SteelBIM cobre o fluxo de detalhamento do projeto ao chão de fábrica
dentro do próprio Revit, sem depender de Tekla ou ferramentas externas.

**Detalhamento metálico.** Geração de treliças, terças, contraventamentos,
travamentos e conexões; corte de perfis por interferência; ajuste de
encontro de vigas; agrupamento de pilares e vigas por tipo. Cotagem para
fabricação e marcação automática de peças seguindo a convenção dos
projetos executivos de referência do escritório.

**Vista de Peça e documentação.** Geração automática de vistas de peça
(shop drawings), cotagem por eixo e por alinhamento, identificação de
perfis, numeração de itens e exportação de lista de materiais.

**Diagrama de Montagem (v2.4, padrão BR completo).** Vista de elevação
automática com eixos, cotas em cinco faixas, cotas verticais por nível,
cota total do conjunto e tags de peça — no padrão de prancha brasileiro
(referência EM-08 do cliente).

**Sequenciamento BIM (4D phasing).** Plano de Montagem com etapas
parametrizáveis e cores customizáveis, exportável para fluxos 4D
(Synchro, Navisworks).

**Pré-Fabricado de concreto (PF).** Lançamento de fundações, armaduras
de pilar, viga, consolo, bloco sobre duas estacas e estaca; estribos de
pilar e viga com ganchos conforme NBR 6118 seção 9.4.6.1; elevações de
forma e nomeação automática de elementos.

**CNC sem Tekla.** Exportação DSTV/NC1 direto do modelo Revit para
máquinas de corte, com extração de furação e mapeamento de perfis.

**Conversor IFC → Nativo Revit.** Importa modelos IFC de coordenação
(arquitetura, instalações, terceiros) e converte perfis genéricos em
famílias Revit nativas — com progresso visível e cancelamento (v2.7.10).

**Verificação de modelo.** Checagem de consistência antes da entrega.

---

## Comparação

| Cenário | Revit puro | Tekla / AutoCAD intermediário | **SteelBIM** |
|---|---|---|---|
| Cotagem de treliça padrão BR (5 faixas) | manual, 30-90min por treliça | export + retrabalho em 2D | 1 clique, ~10s |
| Estribo com gancho NBR 6118 §9.4.6.1 | annotation manual | template proprietário | gerado automaticamente |
| Diagrama de Montagem EM-08 | montagem manual de prancha | não tem | 1 comando, vista pronta |
| Marcação de peças (P01, V01, T01) | parâmetro manual por elemento | numeração não-BR | 1 comando, prefixo configurável |
| DSTV/NC1 para CNC | requer Tekla ou converter externo | export Tekla → CAM | direto do Revit |
| Cobertura NBR | só geometria | configurável manual | nativa |
| Custo de licença | incluso no Revit | licença adicional (anual ou perpétua) | a definir (v2.8.0) |

Não substitui Tekla para projetos de aço pesado com **conexões soldadas
complexas** — SteelBIM cobre o que é detalhamento brasileiro típico
(estrutura leve, treliça padrão BR, pré-fabricado de concreto). Para
projetos pesados, é complementar.

---

## Instalação

### Opção 1 — Setup pronto (recomendado)

1. Baixe a release mais recente em
   [github.com/Alefvieira233/EMT/releases](https://github.com/Alefvieira233/EMT/releases)
2. Feche o Revit 2025 se estiver aberto
3. Execute `SteelBIM-Revit2025-Setup.exe`. O SmartScreen vai avisar — o
   plugin ainda não é assinado digitalmente (cert Sectigo OV em
   aquisição); clique em "Mais informações" e depois em "Executar assim
   mesmo"
4. Abra o Revit — duas abas aparecem no ribbon:
   "SteelBIM | Modelagem" e "SteelBIM | Detalhamento"

### Opção 2 — Build a partir do source

Requisitos: .NET 8 SDK e Revit 2025 instalado em
`C:\Program Files\Autodesk\Revit 2025\`.

```
git clone https://github.com/Alefvieira233/EMT.git
cd EMT
SteelBIM\Compilar-e-Instalar.bat
```

---

## Licenciamento

Plugin proprietário. Modelo:

- **Trial** de 7 dias com 100% das funcionalidades habilitadas (sem
  cadastro, ativação automática na primeira execução)
- **Chave perpétua por máquina** após o trial — gerada manualmente após
  contato (modelo de pricing em definição para v2.8.0)
- **Validação offline** via HMAC (não exige conexão de internet pra rodar)

Para licenças ou para participar do **programa beta** de validação,
contate `engenheiroalefvieira@gmail.com`.

---

## Estrutura do projeto

```
SteelBIM/
├── Commands/          # 48 comandos do ribbon (entry points)
│   ├── PF/            # 13 comandos do fluxo Pré-Fabricado
│   └── *.cs           # 35 comandos do fluxo geral
├── Services/          # Lógica de negócio (ADR-003 — services mudos)
├── Views/             # Janelas WPF (configuração + interação)
├── Models/            # DTOs e configs
├── Infrastructure/    # Cross-cutting (Logger, Crash, Telemetry, License, Update)
└── Resources/         # Ícones do ribbon (conjunto lucide_blue)
```

Dos 48 comandos do ribbon, **46 são comandos de feature** de
detalhamento (33 do fluxo geral + 13 do fluxo Pré-Fabricado) e **2 são
utilitários** do painel Licença — "Ativar Licença" e "Sobre".

---

## Arquitetura

Plugin estruturado em camadas, com decisões registradas em `docs/ADR/`:

- **ADR-003** — Services mudos: retornam `Result<T>`, não chamam diálogo
- **ADR-004** — `IProgress` + `CancellationToken` em operações longas
- **ADR-005** — CI usa Revit reference stubs (Nice3point) sem instalar Revit
- **ADR-006** — Auto-update com fallback de 3 tentativas
- **ADR-007** — Crash reporting via Sentry com `PiiScrubber` (LGPD-friendly)
- **ADR-008** — Telemetria PostHog HTTP-direct (não SDK)
- **ADR-009** — Code signing parametrizado via signtool + GitHub secrets
- **ADR-010** — Documentos legais (EULA/Privacy/TOS — drafts em revisão jurídica)

São **1080 testes automatizados** em `SteelBIM.Tests` cobrindo lógica
pura: zoneamento de armadura NBR, formatadores culture-invariant,
validação de configuração, regras de domínio, scrubbing de PII e
verificação Authenticode.

---

## Versão atual

**v2.7.11** (2026-05-25) — histórico completo em [CHANGELOG.md](CHANGELOG.md).

Releases recentes:

- **v2.7.11** — Wave 2 da auditoria 2026-05-25 (3 PRs estruturais): PfRebar Strangler Fig completion (F5 — original delega ao Pure, 7 métodos + 1 const), ADR-003 template em 4 services (F6 — Tercas/PipeRack/Escada/GuardaCorpo migrados pra IUIDecisionService), extrações NBR adicionais no Pure (F10 — 5 novos métodos cobrindo cobrimento + espaçamento + distribuição). +30 testes (1080 → 1110).
- **v2.7.10** — Wave 1 da auditoria 2026-05-25 (6 PRs cirúrgicos): Conversor IFC com progresso/cancelamento (F1), build reproduzível packages.lock + Dependabot (F2), breadcrumbs Sentry sem PII pra LGPD (F3), Authenticode verify pós-extract flag-gated no auto-update (F4), README marketing-ready (F8), SECURITY.md + SUPPORT.md + fix de templates quebrados (F9). +32 testes (1048 → 1080).
- **v2.7.9** — Sprint 2/3 estrutural: Nullable annotations projeto-wide + AutoVistaService template ADR-003 (IUIDecisionService injetada) + PfRebarServicePure extraction (resolve auditoria #1 bloqueador crítico, 58 testes novos)
- **v2.7.8** — Sprint 2/3 quick wins: dead code removal (-219 LOC) + NumberParsing dedup (-43 LOC) + IfcMaterialParser cache (perf hot path Conversor IFC, -240ms)
- **v2.7.7** — Sprint 0 do roadmap v2.8.0: CI hardening + release publish workflow + smoke tests reais + IFC Progress/Cancel API + ROADMAP+README sync (6 PRs em 1 dia)
- **v2.7.6** — Canonicalização de ícones do ribbon (5 swaps em [App.cs](SteelBIM/App.cs); 84% → 88% conformidade canônica)
- **v2.7.5** — Hotfix visual Conversor IFC (botões e combos com texto cortado por `Height` override)
- **v2.7.4** — Conversor IFC fixes do Victor: rotação preservada U/L/T + colunas inclinadas (`StructuralType.Brace`) + topo correto
- **v2.7.3** — HOTFIX CRITICAL crash `ToggleButton.IsChecked` ao abrir Conversor IFC
- **v2.7.2** — Vista de Peça modernizada (cotagem longitudinal reformulada + tag)
- **v2.7.1** — Conversor IFC UX: dialog modeless + click-to-highlight + filtro estrutural
- **v2.7.0** — FEATURE MAIOR: Conversor IFC → Perfis Nativos Revit (co-autoria 50/50 Victor)
- **v2.6.4** — Hotfix UX (5 Windows padronizadas)
- **v2.6.3** — Hotfix UX (17 ícones lucide_blue + cleanup Resources/)
- **v2.6.2** — Hotfix UX (DiagramaMontagemWindow)
- **v2.6.1** — Hotfix CRITICAL P0 (NBR-1 + NBR-2 + MARCA + security)
- **v2.6.0** — Ribbon split (Modelagem + Detalhamento)
- **v2.5.0** — Pre-market polish
- **v2.4.0** — Diagrama de Montagem completo (padrão BR EM-08)
- **v2.0.0** — Rebranding FerramentaEMT → SteelBIM

---

## Suporte e contato

- **Email comercial / suporte**: engenheiroalefvieira@gmail.com
- **Bugs e melhorias**: [github.com/Alefvieira233/EMT/issues](https://github.com/Alefvieira233/EMT/issues)
- **Beta program**: contato por email com nome do escritório
- **Local**: Uberlândia/MG, Brasil

---

## Roadmap & Pricing

**Pricing público sai na v2.8.0** (em definição). Modelo proposto a ser
confirmado: trial 7 dias, tiers Professional / Studio / Perpétua. Para
acesso antecipado ao programa beta, contate via email.

**Roadmap próximo (v2.8.0, ~6-8 semanas):**

- ✅ Authenticode verification pós-extract no auto-update (v2.7.10 — flag-gated)
- ✅ Conversor IFC com `IProgress` + `CancellationToken` (v2.7.10)
- ✅ Sentry breadcrumbs LGPD-compliant (v2.7.10)
- ✅ Build reproduzível: packages.lock + Dependabot (v2.7.10)
- ⏳ Code signing efetivo (cert Sectigo OV em aquisição)
- ⏳ EULA/Privacy/TOS revisados e ativados (revisão jurídica em curso)
- ⏳ MSI installer assinado (WiX)
- ⏳ Refactor template ADR-003 (Wave 2: Tercas/PipeRack/Escada/GuardaCorpo/Contraventamento)
- ⏳ Migração de 3 windows pra MVVM (Wave 3)

Detalhes em [docs/ROADMAP.md](docs/ROADMAP.md).

---

## Status

Plugin em **soft launch funcional** com beta selecionado. Auditoria
técnica completa de 2026-05-25 mapeou 22 achados críticos/altos e
forneceu roadmap consolidado de 6-8 semanas até v2.8.0 production-grade
(distribuição comercial, code-signed, legal-cover, manual). Wave 1
fechada em v2.7.10. Artefatos da auditoria em `.audit/` (gitignored —
workspace local).

Itens prioritários em andamento:

- ✅ Hardening de CI aplicado em v2.7.7 (PR #20: cache NuGet, timeouts, dorny test-reporter, job EmtKeyGen)
- ✅ Wave 1 da auditoria (4 PRs em v2.7.10 — IFC UX, packages.lock + Dependabot, Sentry LGPD, Authenticode)
- ⏳ Code signing cert (Sectigo OV — aquisição planejada)
- ⏳ Revisão jurídica dos drafts em `docs/legal/` (contratação de advogado TI planejada)
- ⏳ Wave 2 (Strangler Fig PfRebar + ADR-003 em 5 services + MSI WiX)

---

## Licença

Licença proprietária. Copyright (c) 2026 Victor Luis de Oliveira e Alef
Christian Gomes Vieira. Ver [LICENSE](LICENSE) para os termos completos.
