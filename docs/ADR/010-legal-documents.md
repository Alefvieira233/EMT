# ADR 010: Documentos legais (EULA + Privacy Policy + TOS) com revisao juridica

- Status: aceita (skeleton). Drafts publicaveis pendentes de revisao
  juridica.
- Data: 2026-04-28
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.5),
  ADR-007 (crash reporting — referenciado pela PRIVACY),
  ADR-008 (telemetria — referenciado pela PRIVACY),
  ADR-009 (code signing — paralelo ao trabalho juridico)

## Contexto

A AUDITORIA-MERCADO-2026-04-27.md item P0.5 identificou que o
FerramentaEMT esta prestes a ser comercializado com sistema de
licenciamento + telemetria + crash reports, **sem documentos legais
formalizando a relacao com o usuario**. Isso expoe o Licenciante a:

- **Multa LGPD:** R$ 50M ou 2% do faturamento (LGPD art. 52). Mesmo
  com escala pequena, multa minima eh substancial.
- **Reembolsos indevidos:** sem TOS, qualquer cliente insatisfeito
  pode invocar CDC art. 18 (vicios de adequacao) sem limites
  pre-acordados.
- **Risco de processo por uso indevido de dados:** sem Privacy Policy
  publica antes da primeira execucao, consentimento eh juridicamente
  questionavel.

Esta ADR documenta a estrategia para resolver P0.5 com **dois
caminhos paralelos**: skeleton tecnico (esta PR) + revisao juridica
(Alef contrata advogado).

## Decisao

Implementar P0.5 em **2 fases**:

### Fase 1 (esta PR — skeleton)

1. **Drafts redigidos pela equipe tecnica** com cabecalho `RASCUNHO`
   destacado, em portugues do Brasil, formal mas acessivel:
   - `docs/legal/EULA.draft.md` — Termos de Uso (Contrato de Licenca).
   - `docs/legal/PRIVACY.draft.md` — Politica de Privacidade (LGPD).
   - `docs/legal/TOS.draft.md` — Termos Comerciais (modelo
     comercial, reembolso, suporte).
2. **Notas detalhadas para o advogado** ao final de cada documento:
   pontos `<TBD>`, decisoes estrategicas a tomar, validacoes de
   enquadramento legal, sugestoes de adicoes.
3. **EULA prompt no SetupBootstrapper** com infraestrutura completa
   mas **DESABILITADA** (`ShowEulaPrompt = false`). Quando advogado
   aprovar, basta flip da const + embed do EULA final.

### Fase 2 (PR posterior, apos revisao juridica)

1. Substituir conteudo dos drafts pela versao revisada, **mantendo a
   estrutura testada** (mesma indexacao de secoes, mesmas referencias
   cruzadas).
2. Renomear arquivos: `EULA.draft.md` → `EULA.md`, similar pra outros.
3. Flipar `EulaConfirmation.ShowEulaPrompt = true` +
   `AutoCheckByDefault = false`.
4. Embedar `EULA.md` final como resource em `SetupBootstrapper.csproj`.
5. Atualizar referencias em README + ADRs.

### Por que NAO gerador automatico (iubenda etc.)

| Alternativa | Razao para descarte |
|---|---|
| **iubenda.com / PrivacyPolicies.com** | ~R$ 100/mes recorrente. Documento eh **generico** — gerado por template + dados do produto. Nao cobre nuances especificas de plug-in Revit (ex.: clausula 5.5 do EULA: "NAO substitui responsabilidade tecnica do engenheiro"). Documento generico eh atacavel em juizo por falta de adequacao ao caso concreto. |
| **GitHub OSS templates (MIT, GPL)** | Software comercial — open-source license eh inadequada. |
| **Advogado especializado em tecnologia** (esta ADR) | Adotado. Custo ~R$ 2.000-5.000 fixo (one-shot). Documento personalizado, defensavel em juizo, considera particularidades do plug-in (responsabilidade tecnica de engenharia). |

Trade-off: advogado custa 20-40x mais que iubenda, mas:
- Seguranca juridica > documento generico.
- Investimento amortiza ao longo de centenas/milhares de licencas.
- Revisao posterior (apos 2-3 anos de operacao) muito mais barata
  que comeco do zero.

## EULA prompt skeleton — design

```
┌─────────────────────────────────────────────────────────┐
│ FerramentaEMT Setup                                      │
│   "Deseja instalar o add-in?"                            │
│   [OK] [Cancel]                                          │
└─────────────────────────────────────────────────────────┘
            │ user clica OK
            ▼
┌─────────────────────────────────────────────────────────┐
│ EulaConfirmation.RequestAcceptance(quiet)                │
│   if (!ShowEulaPrompt) return true;  ← BYPASS atual      │
│   if (quiet) return true;                                │
│   show EulaForm;                                         │
└─────────────────────────────────────────────────────────┘
            │ if (ShowEulaPrompt) → mostra dialogo:
            ▼
┌─────────────────────────────────────────────────────────┐
│ Termos de Uso (RASCUNHO)                                 │
│                                                          │
│ [TextBox multiline read-only com EULA]                   │
│                                                          │
│ ☑ Li e aceito os termos          (auto-marcado rascunho) │
│ Termos provisorios — pendente revisao juridica           │
│                                                          │
│                              [Instalar]  [Cancelar]      │
└─────────────────────────────────────────────────────────┘
            │ se OK + checkbox marcado
            ▼
        ExtractPackageToTemp + instalacao normal
```

**Estado por flag:**

| Flag | Atual (rascunho) | Apos revisao juridica |
|---|---|---|
| `ShowEulaPrompt` | `false` | `true` |
| `AutoCheckByDefault` | `true` | `false` |
| Texto exibido | Resumo curto provisorio | EULA.md completo (embedded) |

Mudanca pos-revisao = 4 linhas de codigo + 1 atualizacao de resource.
Skeleton ja esta testado e produtivo.

## Layout dos documentos

```
docs/legal/
├── EULA.draft.md      (254 linhas — Contrato de Licenca, 9 secoes + notas revisor)
├── PRIVACY.draft.md   (266 linhas — Politica de Privacidade LGPD, 10 secoes + notas)
└── TOS.draft.md       (300 linhas — Termos Comerciais, 10 secoes + notas)

FerramentaEMT/installer/SetupBootstrapper/
├── EulaConfirmationForm.cs    (novo — skeleton com gate const)
└── Program.cs                 (modificado — chama RequestAcceptance gate)

docs/ADR/010-legal-documents.md  (este ADR)
```

## O que advogado precisa revisar

### Documento a documento

**EULA.draft.md** (254 linhas):
- Validar limitacao de responsabilidade (clausula 5) — especialmente
  5.5 (nao substitui responsabilidade tecnica do engenheiro estrutural).
- Validar foro de eleicao (9.2) — preencher cidade do Alef.
- Validar email comercial (9.7).
- Avaliar clausula de exportacao (US/EU embargo lists) se houver
  intencao de venda no exterior.
- Considerar clausula DMCA se houver distribuicao via marketplaces.

**PRIVACY.draft.md** (266 linhas):
- Validar enquadramento da telemetria como consentimento (LGPD art.
  7º I) vs legitimo interesse (art. 7º IX).
- Validar transferencia internacional (art. 33 I) para Sentry EU +
  PostHog EU.
- Preencher email do DPO (encarregado de dados).
- Validar prazos legais (resposta em 15 dias, retencao 90/365 dias).
- Validar medidas de seguranca (art. 46).
- Considerar adicionar mapa de fluxo de dados visual.

**TOS.draft.md** (300 linhas):
- **Decisao estrategica:** modelo comercial — Anual, Perpetua, ou
  Hibrido. Recomendamos Hibrido para v1.7.0 (maximiza alcance no
  mercado brasileiro).
- Definir valores em R$ (anual / perpetua / upgrade).
- Validar enquadramento CDC art. 49 (direito de arrependimento) +
  janela tecnica voluntaria de 14 dias.
- Validar regime tributario (Simples? Lucro Presumido? Lucro Real?).
- Validar linguagem "best-effort SLA" pra evitar interpretacao
  judicial como obrigacao contratual.

### Linguagem tecnica especifica

Advogado precisa entender + validar a descricao da telemetria/crash
reporting na PRIVACY:
- O que sao "stack traces", "session_id anonimo", "fingerprint hash".
- Por que distinguimos "execucao de contrato" de "consentimento" para
  cada categoria de dado.
- Por que retencao 90 dias (crash) eh diferente de 365 dias
  (telemetria) — argumento tecnico vs juridico.

Recomendamos sessao de **30-60 minutos** com o advogado pra explicar
a arquitetura tecnica antes da revisao formal.

## Operational Runbook

### Como atualizar documentos pos-revisao sem nova release de plugin

**Cenario:** advogado entrega versao final dos 3 documentos. Como
atualizar **sem** forcar todos os usuarios a baixar nova versao?

1. **URLs publicas (recomendado):** servir os documentos via URL
   estavel (ex.: `ferramentaemt.com/legal/eula`). Plug-in apenas
   exibe link clicavel; documento atualizado sem touch no plug-in.
   - Trade-off: requer hosting publico minimal (GitHub Pages serve
     gratis, mesmo dominio).

2. **Embedded (release-bound):** documento embedado no `setup.exe`.
   Atualizar exige nova release.
   - Trade-off: garantia de versao consistente entre cliente e
     contrato — nao ha "drift" via servidor.

**Recomendacao para v1.7.0:** **hibrido** — embedded no setup.exe pra
garantir aceite no momento da instalacao + URL publica pra consultas
posteriores ("queria reler o EULA, onde ta?"). Plug-in mostra link
clicavel pra URL publica em todas as janelas.

### Como flipar ShowEulaPrompt apos revisao juridica

Sequencia exata:

```bash
# 1. Atualizar drafts com versao revisada
cp <new-EULA.md> docs/legal/EULA.md          # remover ".draft"
cp <new-PRIVACY.md> docs/legal/PRIVACY.md
cp <new-TOS.md> docs/legal/TOS.md

# 2. Atualizar EulaConfirmation
# Em FerramentaEMT/installer/SetupBootstrapper/EulaConfirmationForm.cs:
#   public const bool ShowEulaPrompt = true;        // <-- flip
#   public const bool AutoCheckByDefault = false;   // <-- flip

# 3. Embedar EULA.md como resource:
# Em SetupBootstrapper.csproj, adicionar:
#   <EmbeddedResource Include="..\..\docs\legal\EULA.md"
#       LogicalName="FerramentaEMT.SetupBootstrapper.EULA.md" />

# 4. Atualizar EulaConfirmationForm.LoadEulaText() pra ler do resource:
#   Assembly.GetExecutingAssembly().GetManifestResourceStream(
#       "FerramentaEMT.SetupBootstrapper.EULA.md")
#   .ReadToEnd()

# 5. Validacao
dotnet build FerramentaEMT/installer/SetupBootstrapper/SetupBootstrapper.csproj -c Release
dotnet test
# Smoke test manual: rodar Gerar-Setup.bat + executar setup.exe + verificar
# que EULA prompt aparece, checkbox NAO auto-marcado, OK so habilita
# apos check.

# 6. Commit + tag de release
git add ...
git commit -m "feat(legal): activate EULA prompt with reviewed v1.0"
```

### Como atualizar documentos pos-release (mudanca legal)

Se LGPD muda ou ANPD publica novas diretrizes:

1. Atualizar `docs/legal/PRIVACY.md` com nova versao.
2. Bumpar versao no header do documento.
3. **Aviso ao usuario:**
   - Criar evento `legal.policy_changed` no PostHog telemetry.
   - Plug-in detecta diferenca de versao na inicializacao
     (campo `LastAcceptedPolicyVersion` em privacy.json).
   - Mostra dialogo "Politica de Privacidade atualizada" com link
     pra documento + botao "Aceitar e continuar" / "Sair".
4. Email para todos usuarios ativos (via lista de emails do
   sistema de licenca) com link pra nova versao.

## Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| Drafts publicados sem revisao | Cabecalho `RASCUNHO` em todos os documentos. EulaConfirmation skeleton DESABILITADO por padrao. README aponta "drafts" explicitamente. ADR-010 lista renomeacao de arquivo como gate de release. |
| Advogado demora muito | Skeleton ja esta produtivo — plug-in instala normalmente sem EULA prompt. Sem bloqueio operacional ate revisao concluir. |
| Advogado entrega documento incompativel com arquitetura tecnica | Sessao de explicacao tecnica de 30-60min ANTES da revisao (item §"Linguagem tecnica especifica"). Documentos tecnicos referenciados (ADR-006/007/008) servem de input. |
| LGPD muda durante revisao | Operational Runbook "Como atualizar pos-release" cobre o cenario. Versionamento do documento + aceite re-prompt no plug-in. |
| Cliente recusa EULA na instalacao | Comportamento esperado — instalacao cancelada, plug-in nao usado. Sem dados pessoais coletados. Loga apenas em logs locais (Logger.Info "[Setup] EULA recusado"). |
| Versionamento de aceite dessincronizado entre cliente e Licenciante | TOS sec. 7.2 cobre — versoes anteriores aplicam-se ao periodo ja contratado. Plug-in armazena `LastAcceptedPolicyVersion` em privacy.json. |
| Drafts vazam por commit em main acidental | Sao drafts publicaveis — vazamento nao eh problema (cabecalho RASCUNHO esta visivel). Mas branch separada (esta) + nao mergear ate revisao concluir mitiga. |
| Texto provisorio do EULA prompt vaza pra producao | Skeleton DESABILITADO — texto nao eh exibido em runtime atual. Quando flipar, sera substituido pelo texto revisado. |

## Validacao

- Build local Release + CI=true: 0 erros, 0 avisos (PR-6 nao toca
  codigo C# de Services/Commands; mudanca pontual no
  SetupBootstrapper).
- `dotnet test`: 716/716 verde.
- Build do SetupBootstrapper: 0 erros, 0 avisos (pragma local
  CS0162 pra suprimir o warning intencional do skeleton).
- Comportamento atual: setup.exe NAO mostra EULA prompt
  (ShowEulaPrompt = false). Instalacao flui normalmente.

## Consequencias

### Positivas

- Drafts disponiveis para revisao do advogado em **24h apos approval**
  desta PR.
- Skeleton tecnico do EULA prompt 100% pronto — flipar em ~30min
  apos receber documentos revisados.
- Notas detalhadas pro revisor reduzem tempo de revisao
  (advogado nao precisa adivinhar o contexto tecnico).
- Reducao de risco LGPD desde momento zero (drafts ja documentam o
  que coletamos, mesmo que ainda nao publicado).

### Neutras

- 3 novos arquivos em `docs/legal/` (820 linhas combinadas).
- 2 arquivos novos no SetupBootstrapper (EulaConfirmationForm.cs +
  modificacao em Program.cs).

### Negativas

- Custo financeiro: R$ 2.000-5.000 (advogado especializado).
- Risco de timing: se advogado demora >60 dias, atrasa lancamento
  da v1.7.0 oficial.
- Sem revisao, **drafts NAO PODEM** ser usados como documento
  contratual. Skeleton existe mas eh skeleton.

## Rollback

Se precisarmos abandonar P0.5 (decisao comercial de nao formalizar
juridicamente):

1. Reverter commit do SetupBootstrapper (EulaConfirmationForm.cs +
   trecho em Program.cs — ~8 linhas).
2. Manter ou deletar `docs/legal/*.draft.md` — sao apenas
   documentacao tecnica, nao usadas em runtime.
3. ADR-010 fica como historia.

Cenario implausivel — formalizacao juridica eh requisito legal pra
cobrar (LGPD + CDC). Rollback aqui significa nao cobrar.

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.5 (origem desta decisao).
- ADR-007/008 (referenciados pela PRIVACY — descrevem o que coletamos).
- ADR-009 (paralelo — code signing tambem espera ativacao do Alef).
- [docs/legal/EULA.draft.md](../legal/EULA.draft.md).
- [docs/legal/PRIVACY.draft.md](../legal/PRIVACY.draft.md).
- [docs/legal/TOS.draft.md](../legal/TOS.draft.md).
- [LGPD — Lei 13.709/2018](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm).
- [CDC — Lei 8.078/1990, art. 49 (direito de arrependimento)](https://www.planalto.gov.br/ccivil_03/leis/l8078compilado.htm).
