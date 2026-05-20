# SteelBIM

[![Build & Test](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml/badge.svg)](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml)
![Versão](https://img.shields.io/badge/vers%C3%A3o-v2.6.1-blue)
![Licença](https://img.shields.io/badge/licen%C3%A7a-propriet%C3%A1ria-lightgrey)
![Testes](https://img.shields.io/badge/testes-851%20passing-brightgreen)

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

São 851 testes automatizados em `SteelBIM.Tests` cobrindo lógica pura:
zoneamento de armadura NBR, formatadores culture-invariant, validação
de configuração e regras de domínio.

## Versão atual

**v2.6.1** — histórico completo de releases em
[CHANGELOG.md](CHANGELOG.md).

Releases recentes:

- **v2.6.1** — Hotfix CRITICAL P0 (NBR-1 + NBR-2 + MARCA + security)
- **v2.6.0** — Ribbon split (Modelagem + Detalhamento)
- **v2.5.0** — Pre-market polish (README rewrite + 9 guards defensivos de doc.ActiveView)
- **v2.4.1** — Hotfix gancho de estribo NBR 6118 9.4.6.1
- **v2.4.0** — Diagrama de Montagem completo (100% padrão BR EM-08)
- **v2.3.0** — MVP Diagrama de Montagem
- **v2.2.0** — Sequenciamento BIM (4D phasing)
- **v2.1.0** — Ribbon unificada + ícones lucide_blue
- **v2.0.0** — Rebranding FerramentaEMT → SteelBIM

## Suporte e contato

- Email: engenheiroalefvieira@gmail.com
- Issues: [github.com/Alefvieira233/EMT/issues](https://github.com/Alefvieira233/EMT/issues)
- Local: Uberlândia/MG, Brasil

## Status

Plugin em fase comercial. Soft launch para alunos selecionados em
andamento. Aprovado em auditoria sênior (8 PASS / 5 WARN / 0 CRITICAL
nos 14 eixos auditados).

Pré-requisitos pós-MVP em backlog confirmado:

- Code signing cert (Sectigo OV — em processo de aquisição)
- Revisão jurídica dos drafts em `docs/legal/` (em processo)
- Migração ADR-003 dos services legados restantes (planejada para v2.6.0)

## Licença

Licença proprietária. Copyright (c) 2026 Victor Luis de Oliveira e Alef
Christian Gomes Vieira. Ver [LICENSE](LICENSE) para os termos completos.
