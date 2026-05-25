# SteelBIM

[![Build & Test](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml/badge.svg)](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml)
![Versão](https://img.shields.io/badge/vers%C3%A3o-v2.7.9-blue)
![Licença](https://img.shields.io/badge/licen%C3%A7a-propriet%C3%A1ria-lightgrey)
![Testes](https://img.shields.io/badge/testes-1048%20passing-brightgreen)

Plugin Revit 2025 para detalhamento estrutural brasileiro. NBR 6118
nativo, export DSTV/NC1 para CNC, Diagrama de Montagem completo no
padrão BR e 48 comandos especializados para escritórios de
detalhamento metálico e pré-fabricado de concreto.

## Quem usa

- Escritórios de detalhamento metálico (vigas, pilares, terças, conexões)
- Projetistas de pré-fabricado de concreto (armaduras NBR 6118)
- Construtoras com fluxo BIM 4D (Synchro, Navisworks)

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

**Verificação de modelo.** Checagem de consistência antes da entrega.

## Instalação

### Opção 1 — Setup pronto (recomendado)

1. Baixe a release mais recente em
   [github.com/Alefvieira233/EMT/releases](https://github.com/Alefvieira233/EMT/releases)
2. Feche o Revit 2025 se estiver aberto
3. Execute `SteelBIM-Revit2025-Setup.exe`. O SmartScreen vai avisar — o
   plugin ainda não é assinado digitalmente; clique em "Mais informações"
   e depois em "Executar assim mesmo"
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

## Estrutura do projeto

```
SteelBIM/
├── Commands/          # 48 comandos do ribbon (entry points)
│   ├── PF/            # 13 comandos do fluxo Pré-Fabricado
│   └── *.cs           # 35 comandos do fluxo geral
├── Services/          # Lógica de negócio (ADR-003 — services mudos)
├── Views/             # Janelas WPF (configuração + interação)
├── Models/            # DTOs e configs
├── Infrastructure/    # Cross-cutting (Logger, Crash, Telemetry, License)
└── Resources/         # Ícones do ribbon (conjunto lucide_blue)
```

Dos 48 comandos do ribbon, **46 são comandos de feature** de
detalhamento (33 do fluxo geral + 13 do fluxo Pré-Fabricado) e **2 são
utilitários** do painel Licença — "Ativar Licença" e "Sobre".

## Arquitetura

Plugin estruturado em camadas, com decisões registradas em `docs/ADR/`:

- **ADR-003** — Services mudos: retornam `Result<T>`, não chamam diálogo
- **ADR-004** — `IProgress` + `CancellationToken` em operações longas
- **ADR-006** — Auto-update com fallback de 3 tentativas
- **ADR-007** — Crash reporting via Sentry com `PiiScrubber`
- **ADR-008** — Telemetria PostHog HTTP-direct (não SDK)

São 1048 testes automatizados em `SteelBIM.Tests` cobrindo lógica pura:
zoneamento de armadura NBR, formatadores culture-invariant, validação
de configuração e regras de domínio.

## Versão atual

**v2.7.9** (2026-05-25) — histórico completo de releases em
[CHANGELOG.md](CHANGELOG.md).

Releases recentes:

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

## Suporte e contato

- Email: engenheiroalefvieira@gmail.com
- Issues: [github.com/Alefvieira233/EMT/issues](https://github.com/Alefvieira233/EMT/issues)
- Local: Uberlândia/MG, Brasil

## Roadmap & Pricing

**Pricing público sai na v2.8.0** (em definição). Modelo proposto a ser
confirmado: trial 7 dias, tiers Professional / Studio / Perpétua. Para
acesso antecipado ao programa beta, contatar via email (abaixo).

**Roadmap próximo (v2.8.0, ~10 semanas):**

- Code signing efetivo (cert Sectigo OV em aquisição)
- EULA/Privacy/TOS revisados e ativados (revisão jurídica em curso)
- Authenticode verification pós-extract no auto-update
- Testes de unidade do `PfRebarService` (módulo PF core)
- Conversor IFC com `IProgress` + `CancellationToken` (resolve trava em modelos > 5000 elementos)
- Refactor template ADR-003 (AutoVistaService como modelo)

Detalhes em [docs/ROADMAP.md](docs/ROADMAP.md).

## Status

Plugin em **soft launch funcional** com beta selecionado. Auditoria
técnica completa de 2026-05-25 mapeou 22 achados críticos/altos e
forneceu roadmap consolidado de 10 semanas até v2.8.0 production-grade
(distribuição comercial, code-signed, legal-cover, manual). Artefatos
da auditoria em `.audit/` (gitignored — workspace local).

Itens prioritários (Sprint 0/1 do roadmap v2.8.0):

- ✅ Hardening de CI aplicado em v2.7.7 (PR #20: cache NuGet, timeouts, dorny test-reporter, job EmtKeyGen)
- ⏳ Code signing cert (Sectigo OV — aquisição planejada)
- ⏳ Revisão jurídica dos drafts em `docs/legal/` (contratação de advogado TI planejada)
- ⏳ Authenticode verification pós-extract no auto-update

## Licença

Licença proprietária. Copyright (c) 2026 Victor Luis de Oliveira e Alef
Christian Gomes Vieira. Ver [LICENSE](LICENSE) para os termos completos.
