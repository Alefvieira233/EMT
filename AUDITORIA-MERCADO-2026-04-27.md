# Auditoria de mercado — FerramentaEMT v1.6.0-rc.1

**Data:** 2026-04-27
**Auditor:** Claude (simulando equipe sênior 10 devs)
**Escopo:** arquitetura, código, testes, build/deploy, distribuição, segurança, performance, UX, legal
**Veredito geral:** **produto sólido, demo-ready hoje, NÃO market-ready ainda.** Tem 6 itens P0 (críticos) que precisam ser resolvidos antes de vender em escala. Roadmap de 4 fases (~6-8 semanas) leva de RC a produto comercial maduro.

---

## Resumo executivo (TL;DR)

### O que está bem (já fizemos):
- ✅ Arquitetura definida com 4 ADRs (Result Pattern, IRevitContext, Progress/Cancel)
- ✅ 460 testes automatizados (cobertura saudável de lógica pura)
- ✅ CI/CD básico (GitHub Actions roda testes em cada push)
- ✅ Sistema de licenciamento com HMAC
- ✅ Logging estruturado (Serilog) + crash reporter local
- ✅ Tema escuro/claro consistente (33/36 janelas usam `AppTheme.Base.xaml`)
- ✅ Documentação de processo (CHANGELOG, CONTRIBUTING, ADRs, RUNBOOK, ROADMAP)
- ✅ Versão semântica + tags + release notes

### O que falta (essencial pra mercado):
- ❌ **Code signing** — Windows SmartScreen vai bloquear o `.exe` em PCs novos
- ❌ **Auto-update** — não tem como entregar correção de bug pros usuários
- ❌ **Crash reporting remoto** — bugs em campo ficam invisíveis pra você
- ❌ **CI valida só testes** — o projeto principal nunca é compilado no CI
- ❌ **Privacy policy + EULA** — exigidos por lei pra cobrar
- ❌ **Telemetria de uso** — você não vai saber quem está usando, com qual frequência

### O que está OK mas pode evoluir:
- ⚠️ ADR-003 inconsistente (8 services ainda mostram diálogos direto)
- ⚠️ Arquivos gigantes (3 services com 1.4k–2.2k linhas — "god objects")
- ⚠️ Sem injeção de dependência (cada command faz `new Service()`)
- ⚠️ Dívidas declaradas (6 TODOs no código)
- ⚠️ Sem internacionalização (português hardcoded — fecha portas pra exterior)

---

## Estatísticas do código (snapshot atual)

| Métrica | Valor | Avaliação |
|---|---|---|
| Linhas de código (.cs) | 41.131 | Médio porte — sustentável por 1-2 devs |
| Arquivos .cs | 253 | OK |
| Arquivos de teste | 48 | Bom |
| Testes automatizados | 460 (Facts+Theories) | Bom (~1 teste por 90 LOC) |
| Botões no ribbon | 43 | Significativo — produto rico |
| Painéis no ribbon | 13 (10 ECC + 3 EMT) | OK |
| Janelas WPF | 36 | OK |
| Janelas com tema consistente | 33 (92%) | Quase 100% — bom |
| ADRs (decisões arquiteturais) | 4 | Bom — projeto pensado |
| Comandos `IExternalCommand` | ~30 | Médio porte |
| Maior arquivo | ListaMateriaisExportService 2.224 linhas | ⚠️ Refatorar |

---

# Achados por severidade

## P0 — CRÍTICO (corrigir antes de vender)

### P0.1 — Code signing ausente

**O QUE É:** O instalador `.exe` não tem assinatura digital. Quando uma pessoa baixa e dá duplo-clique, o **Windows SmartScreen** mostra um aviso vermelho "Aplicativo não reconhecido" e bloqueia. Pra prosseguir, a pessoa precisa clicar em "Mais informações" → "Executar mesmo assim".

**POR QUE IMPORTA:** Em vendas, **30-50% dos usuários desistem** quando vêem esse aviso. Quem é menos técnico interpreta como vírus.

**SOLUÇÃO:**
- Comprar certificado de code signing (~R$ 1.000–2.000/ano):
  - Sectigo (mais barato)
  - DigiCert (mais reconhecido)
  - SSL.com (boa relação custo-benefício)
- Configurar `signtool.exe` no `Gerar-Setup.bat` ou no CI
- Re-gerar instalador assinado

**ESFORÇO:** 1 dia (compra + setup) + ~R$ 1.500/ano.

---

### P0.2 — Auto-update inexistente

**O QUE É:** Cada usuário fica preso na versão que instalou. Se você descobrir um bug grave amanhã, **todos os clientes precisam baixar e reinstalar manualmente**.

**POR QUE IMPORTA:**
- Bugs em campo ficam ativos por meses
- Usuários esquecem de atualizar
- Você não consegue forçar correção de segurança
- Usuários com versões diferentes geram bugs incompatíveis quando trabalham juntos

**SOLUÇÃO:** Implementar mecanismo "phone home" no `App.OnStartup`:
1. No boot, fazer `HttpGet` num endpoint (`https://api.ferramentaemt.com/version-check`)
2. Comparar versão local vs remota
3. Se houver nova: mostrar dialog "Versão X disponível. Baixar agora?"
4. Baixar `.zip`, extrair, copiar pro `%AppData%\Autodesk\Revit\Addins\2025\` no próximo restart

Alternativas mais simples (no curto prazo):
- Squirrel.Windows (open-source, funciona)
- Velopack (mais moderno, fork do Squirrel)
- ClickOnce (built-in mas limitado)

**ESFORÇO:** 1 semana (Velopack mais simples) ou 3 semanas (custom).

---

### P0.3 — Crash reporting só local

**O QUE É:** Quando o plugin crasha, o `CrashReporter` salva log num arquivo local do usuário. Você **só vê o erro se o cliente lembrar de mandar o arquivo**.

**POR QUE IMPORTA:**
- Crashes silenciam o usuário (ele desinstala em vez de reportar)
- Você não sabe se 1 cliente ou 100 clientes sofrem o mesmo crash
- Não consegue priorizar correções

**SOLUÇÃO:** Integrar serviço de crash reporting:
- **Sentry.io** (~U$ 26/mês plano team, free pra <5k erros/mês)
- **AppCenter** (Microsoft, free)
- **Bugsnag** (mais caro)

No `CrashReporter.Initialize()`, adicionar handler que faz POST do stack trace + metadata pro endpoint do Sentry.

⚠️ **Privacy:** precisa permissão do usuário (LGPD/GDPR). Adicionar opt-in na primeira execução: "Pode mandar erros anônimos pra ajudar a melhorar o plugin?"

**ESFORÇO:** 2 dias (Sentry SDK NuGet) + privacy dialog.

---

### P0.4 — CI só roda testes, não compila o projeto principal

**O QUE É:** O workflow `.github/workflows/build.yml` é honesto sobre isso:
> "We don't actually compile against shims - we just skip the main project on CI and only build the test project"

Significa: **se você quebrar o `FerramentaEMT.csproj` (projeto principal), o CI não vai pegar**. Só descobre quando rodar `Compilar-e-Instalar.bat` localmente.

**POR QUE IMPORTA:**
- Hoje aconteceu: build falhou com 5 erros (CS0117, CS0012, CS1069). CI não pegou porque não compila o projeto principal.
- Em PRs do Victor, vai ser pior — ele não tem Revit pra testar localmente também.
- Releases podem sair quebradas.

**SOLUÇÃO:** Configurar Revit API DLLs na máquina de CI:
- **Self-hosted runner** com Revit 2025 instalado (R$ 0 mas trabalho)
- **Mock DLLs** (gerar shims via reflection do Revit) — open source: [RevitAPI.NET](https://github.com/revitapi)
- **Cloud VM com licença Revit** (~U$ 100/mês AWS)

Ou caminho pragmático: mantém o CI atual + roda `Compilar-e-Instalar.bat` localmente antes de cada push (já é o que você faz).

**ESFORÇO:** 1 dia (mock DLLs) ou 1 semana (self-hosted runner).

---

### P0.5 — Privacy Policy + EULA + Termos de Uso ausentes

**O QUE É:** Você está prestes a vender software com sistema de licenciamento, telemetria potencial, crash reports. Sem documentos legais, está **vulnerável a:**
- Multa LGPD (R$ 50M ou 2% do faturamento)
- Cliente pedindo reembolso indevido (sem TOS, fica lado dele)
- Usuário processando por uso indevido de dados

**POR QUE IMPORTA:** É a diferença entre "produto experimental" e "produto comercial".

**SOLUÇÃO:** Mínimo viável:
1. **Termos de Uso** (1-2 páginas) — quem pode usar, limites, garantias, jurisdição
2. **Política de Privacidade** (LGPD) — quais dados coleta (email, machine ID, telemetria), pra que, por quanto tempo
3. **EULA** dentro do instalador — checkbox "Li e aceito" antes de instalar

Templates prontos:
- [iubenda.com](https://www.iubenda.com/pt) (~R$ 100/mês, gera automático)
- [PrivacyPolicies.com](https://www.privacypolicies.com)
- Advogado especializado em tecnologia (R$ 2.000–5.000 fixo, mais robusto)

**ESFORÇO:** 1 semana (advogado) ou 2 horas (gerador automático mas menos sólido).

---

### P0.6 — Telemetria de uso ausente

**O QUE É:** Você não tem nenhuma forma de saber:
- Quantos usuários ativos têm hoje
- Quais botões eles mais usam
- Quais comandos crasham com mais frequência
- Quais features ninguém toca

**POR QUE IMPORTA:**
- Sem dados, decisões de produto viram chute
- Não consegue defender investimento de tempo nas features que ninguém usa
- Vai gastar 2 semanas refatorando algo que 3% dos usuários tocam

**SOLUÇÃO:** Adicionar telemetria opt-in:
- **PostHog** (open-source, free <1M events/mês)
- **Mixpanel** (caro mas robusto)
- **Application Insights** (Azure, baseado em uso)

Eventos a registrar:
```csharp
Telemetry.Track("CommandExecuted", new {
  Command = "PfInserirAcosBlocoDuasEstacas",
  DurationMs = 1234,
  ElementCount = 50,
  Success = true
});
```

⚠️ Mesmo opt-in da P0.3.

**ESFORÇO:** 3 dias (SDK + integração nos commands).

---

## P1 — ALTA PRIORIDADE (qualidade arquitetural)

### P1.1 — ADR-003 inconsistente: 59 chamadas a `AppDialogService` em 8 services

Services que mostram diálogo direto (anti-padrão):
- `AutoVistaService`, `AgrupamentoVisualService`, `AjustarEncontroService`
- `ListaMateriaisExportService`, `CotarPecaFabricacaoService`, `MarcarPecasService`
- `PfRebarService`, `PfTwoPileCapRebarService`

**O QUE É:** ADR-003 do projeto diz que services devem ser "mudos" (só retornam `Result<T>`, caller mostra dialog). Mas 8 services ainda gritam "ShowWarning" direto.

**POR QUE IMPORTA:**
- Service não é unit-testável (precisa mockar UI)
- Lógica de UX espalhada em 8 lugares
- Mensagens em português hardcoded → impossível internacionalizar

**SOLUÇÃO:** Migração service-por-service:
1. Substituir `AppDialogService.Show*` por `Result.Fail("mensagem")`
2. Caller (`Cmd*.cs`) recebe Result e chama `ShowWarning(result.Error)` na hora certa
3. Cada migração = 1 PR pequeno + testes

**ESFORÇO:** ~1 dia por service = 8 dias total.

---

### P1.2 — Arquivos gigantes (god objects)

| Arquivo | Linhas | Risco |
|---|---|---|
| `ListaMateriaisExportService.cs` | 2.224 | Alto — coleta + agrupa + grava Excel + UI dialog |
| `PfRebarService.cs` | 1.790 | Alto — 5 features distintas misturadas |
| `CotasService.cs` | 1.428 | Médio |

**O QUE É:** Classes que fazem coisa demais. Difícil entender, testar, evoluir, dar handoff.

**SOLUÇÃO:** Extrair sub-classes:
- `ListaMateriaisExportService` → `MaterialCollector` + `MaterialAggregator` + `XlsxWriter` + `BomDialog`
- `PfRebarService` → `ColumnStirrupService` + `BeamStirrupService` + `ColumnBarsService` + `BeamBarsService` + `ConsoloService`
- `CotasService` → `EixoCotaService` + `AlinhamentoCotaService`

**ESFORÇO:** 1 semana por refator (3 semanas total).

---

### P1.3 — Sem injeção de dependência

53 ocorrências de `new Service()` direto nos commands. Cada command instancia seus services:

```csharp
// CmdGerarConexao.cs
var service = new ConexaoGeneratorService();
```

**O QUE É:** Acoplamento tight. Pra trocar a impl de `ConexaoGeneratorService` (ex.: criar versão mock pra teste), precisa editar todos os commands.

**POR QUE IMPORTA:**
- Testes de integração impossíveis (hoje só testes unitários puros)
- Difícil adicionar logging cross-cutting (decorators)
- Difícil feature-flag (não dá pra trocar service condicionalmente)

**SOLUÇÃO:** Container DI minimal:
- `Microsoft.Extensions.DependencyInjection` (BCL, gratuito)
- Composition root no `App.OnStartup`
- Commands recebem services via construtor (Revit não suporta nativamente, mas dá pra fazer com factory)

**ESFORÇO:** 2 semanas (refator de toda Composition Root).

⚠️ Trade-off: Revit API é single-threaded e o ciclo de vida de IExternalCommand é não-controlável. DI puro fica esquisito. Pode ser que ADR-005 sobre isso esteja certo em manter `new` direto. **Avaliar custo vs benefício antes de adotar.**

---

### P1.4 — Zero XML doc comments em arquivos críticos

Arquivos que entregam contrato público mas não têm `/// <summary>`:
- `App.cs` — entry point, IExternalApplication
- `PfRebarService.cs` — coração do PF Wave 2

**SOLUÇÃO:** Adicionar XML docs em:
- Todos os métodos `public` de services
- Todas as classes `public` em `Models/`
- Todos os enums

Configurar `<GenerateDocumentationFile>true</GenerateDocumentationFile>` no csproj e tratar warnings de docs ausentes como erros (já tem `TreatWarningsAsErrors=true` em Release).

**ESFORÇO:** 3 dias (passada metódica).

---

### P1.5 — 10 linhas com formatadores numéricos sem `CultureInfo.InvariantCulture`

Risco: bug recorrente em pt-BR (vírgula em decimal).

**SOLUÇÃO:** Audit + fix das 10 ocorrências. Adicionar regra de lint customizada (ou template pre-commit) que falha em `$"{x:F"` sem `CultureInfo.InvariantCulture`.

**ESFORÇO:** 1 dia.

---

### P1.6 — Strings hardcoded sem internacionalização (i18n)

**O QUE É:** Todas mensagens estão em pt-BR hardcoded no código C#. Pra rodar em espanhol/inglês, precisa duplicar tudo.

**POR QUE IMPORTA:** Mercado brasileiro tem teto. Plugins de Revit com versão em inglês têm 10-20x mais usuários.

**SOLUÇÃO:** Adotar `System.Resources` (.resx):
- `Strings.pt-BR.resx`, `Strings.en-US.resx`, `Strings.es-MX.resx`
- Substituir `"Nenhum elemento selecionado"` por `Strings.NoElementsSelected`

**ESFORÇO:** 2 semanas (refator de ~500 strings).

⚠️ Defer pra depois de ter market validation no Brasil.

---

## P2 — MÉDIA PRIORIDADE (robustez)

### P2.1 — 6 TODOs declarados

Ver `grep TODO`. Cada um é uma promessa não cumprida. Listar + atribuir + agendar.

### P2.2 — 79 `FilteredElementCollector` sem cache evidente

Em modelo grande (>10k elementos), cada collector itera tudo. Performance pode degradar.

**Soluções:**
- Cachear resultados em fields da classe (invalidar em `DocumentChanged`)
- Usar `ElementCategoryFilter` early
- Cachear `BuiltInCategory` lookups

### P2.3 — Sem testes de integração com Revit

CI não tem Revit. Impossível testar fluxo completo automaticamente. Mitigação:
- Smoke tests manuais documentados (já tem RUNBOOK.md)
- VM com Revit + script de smoke que abre, executa, captura screenshot, compara

### P2.4 — License sem invalidação online

Hoje a licença é validada via HMAC local. Se você revogar uma chave (cliente devolveu, fraude), **continua funcionando indefinidamente**.

**Solução:** validação online periódica (1x por semana). Em offline, dá grace period de 30 dias.

### P2.5 — Sem rate limiting em comandos pesados

Usuário pode clicar 10x em "Gerar Lista de Materiais" e travar Revit. Adicionar mutex.

### P2.6 — Transactions sem `using` em 36 lugares (vs 39 com `using`)

Padrão dotnet idiomático é `using (Transaction t = new Transaction(doc, "..."))`. 36 lugares estão usando `try/finally` (provavelmente correto, mas inconsistente).

---

## P3 — NICE-TO-HAVE

### P3.1 — `dotnet format` não-blocking no CI
Habilitar como check obrigatório.

### P3.2 — Code coverage threshold no CI
Ex.: cair se cobertura <70%.

### P3.3 — `semantic-release` ou `release-please`
Geração automática de CHANGELOG + tags via conventional commits.

### P3.4 — Dependabot / Renovate
Atualização automática de dependências NuGet.

### P3.5 — Discord/Slack pra suporte
Canal de feedback rápido com early adopters.

### P3.6 — Knowledge base pública
Site simples com tutorial de cada comando (Notion, GitBook, mkdocs).

---

# Plano de fases (rumo a v2.0.0 market-ready)

## 🔴 Fase 0 — Demo de amanhã (2026-04-28)

**O que falta agora:** Re-build pra DLL deployada ter o fix culture-invariant + push pro GitHub.

**Esforço:** 5 minutos.

**Por que:** se você apresentar amanhã com a DLL stale, qualquer demo do bloco-2-estacas em pt-BR vai mostrar "diam. 6,3" no Comment — visualmente feio.

---

## 🟠 Fase 1 — "Pronto pra vender" (v1.7.0, ~2 semanas)

Resolve os P0 mais urgentes. Depois desta fase, você pode COMEÇAR a vender pequenas quantidades:

| Item | Esforço | Por quê |
|---|---|---|
| **Code signing** (P0.1) | 1 dia + R$ 1.500/ano | Senão SmartScreen mata a venda |
| **Privacy Policy + EULA** (P0.5) | 2-7 dias | Senão é multa LGPD |
| **Crash reporting Sentry** (P0.3) | 2 dias | Senão você vira cego em produção |
| **CI compilando o csproj principal** (P0.4) | 1 dia | Senão releases saem quebradas |
| **Re-port zoneamento NBR 6118** (Wave 2 followup) | 2-3 dias | Feature anunciada nos changelogs antigos |

**Total:** ~10 dias úteis (2 semanas com tempo pra testar).

**Marco:** v1.7.0 é a primeira release "comercial" — pode cobrar com tranquilidade legal.

---

## 🟡 Fase 2 — "Pronto pra escalar" (v1.8.0, ~3 semanas)

Adiciona infraestrutura pra crescer sem você quebrar:

| Item | Esforço | Por quê |
|---|---|---|
| **Auto-update** (P0.2) | 1 semana | Senão usuários ficam presos em versão antiga |
| **Telemetria opt-in** (P0.6) | 3 dias | Senão você vai chutar prioridades |
| **License validação online** (P2.4) | 2 dias | Senão chave revogada continua valendo |
| **ADR-003 cleanup** dos 8 services (P1.1) | 8 dias | Pré-requisito pra DI/i18n no futuro |

**Total:** ~17 dias úteis (3-4 semanas).

**Marco:** v1.8.0 escala — você consegue suportar 100+ clientes sem perder noites.

---

## 🟢 Fase 3 — "Pronto pra exportar" (v2.0.0, ~6-8 semanas)

Refatorações grandes de qualidade:

| Item | Esforço | Por quê |
|---|---|---|
| **Refator god objects** (P1.2) | 3 semanas | Manutenção sustentável a longo prazo |
| **DI container** (P1.3) | 2 semanas | Testes de integração + decorators |
| **i18n / l10n** (P1.6) | 2 semanas | Mercado internacional |
| **XML docs em arquivos críticos** (P1.4) | 3 dias | Onboarding de novos devs |
| **Resolver TODOs + culture-invariant audit** (P2.1, P1.5) | 2 dias | Higiene |
| **Performance: cache FilteredElementCollector** (P2.2) | 1 semana | Modelos grandes (>10k elementos) |

**Total:** ~9 semanas úteis com folga pra testes.

**Marco:** v2.0.0 é o produto maduro — pode entrar em mercados internacionais (USA, Europa).

---

## 🔵 Fase 4 — "Operação madura" (contínuo)

| Item | Quando |
|---|---|
| Dependabot (P3.4) | hoje |
| Discord de suporte (P3.5) | semana 1 da Fase 1 |
| Knowledge base pública (P3.6) | semana 2 da Fase 1 |
| Semantic-release (P3.3) | depois de v2.0.0 |
| Code coverage threshold (P3.2) | semana 3 da Fase 1 |

---

# Investimento estimado

| Fase | Tempo (1 dev) | $ extras | Output |
|---|---|---|---|
| Fase 0 | 5 minutos | R$ 0 | Demo de amanhã sem bug visível |
| Fase 1 | 2 semanas | R$ 1.500/ano (cert) + R$ 100/mês (Sentry) | **Pode vender** |
| Fase 2 | 3-4 semanas | R$ 100-500/mês (PostHog + infra update) | Pode crescer pra 100+ clientes |
| Fase 3 | 6-8 semanas | R$ 0 (tudo open source) | Pode internacionalizar |

**Total para market-ready completo:** 12-14 semanas (~3 meses) de dev focado + ~R$ 3.000/ano de infra.

---

# Recomendação imediata (esta noite)

Execute APENAS a Fase 0 antes da apresentação:

1. Fechar Revit
2. Re-rodar `Compilar-e-Instalar.bat` (DLL local atualizada)
3. Re-rodar `Gerar-Setup.bat` (instalador atualizado)
4. `git push origin main && git push origin v1.6.0-rc.1` (quando tiver token)

Tudo o resto: faça depois da apresentação. Não introduzir mudanças grandes nas últimas 12h antes de demo é regra de ouro de qualquer engenheiro sênior.

---

**Documentos relacionados:**
- `CHANGELOG.md` — histórico de releases
- `docs/ROADMAP.md` — roadmap atual (anterior a esta auditoria)
- `docs/PLANO-100-100.md` — plano anterior de hardening
- `RELEASE-NOTES-v1.6.0-rc.1.md` — release atual
- `HANDOFF-v1.6.0-rc.1.md` — instruções da apresentação de amanhã

**Próxima auditoria recomendada:** após Fase 1 (em ~2 semanas) pra validar que P0s foram resolvidos antes de escalar.
