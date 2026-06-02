# Politica de Seguranca

## Versoes suportadas

Apenas a release **mais recente** recebe correcoes de seguranca.
Usuarios em versoes antigas devem fazer upgrade antes de reportar.

| Versao | Suporte |
|---|---|
| v2.8.x (atual) | ✅ Patches de seguranca |
| v2.7.x | ⚠️ So bugs criticos comprovados |
| < v2.7.0 | ❌ Sem suporte |

## Como reportar uma vulnerabilidade

**NAO abra uma issue publica no GitHub** para vulnerabilidades. Issues
publicas expoem o problema antes do fix estar disponivel, colocando
todos os usuarios em risco.

Use um dos canais privados abaixo:

1. **Email** (preferencial): `engenheiroalefvieira@gmail.com`
   - Assunto: `[SECURITY] <descricao curta>`
   - Conteudo sugerido na secao "Conteudo do report" abaixo
2. **GitHub Security Advisories** (privado): use o botao
   "Report a vulnerability" em
   [github.com/Alefvieira233/EMT/security/advisories/new](https://github.com/Alefvieira233/EMT/security/advisories/new)

Voce vai receber confirmacao de recebimento em **ate 48 horas uteis**.

## Conteudo do report

Inclua tudo que conseguir, mas nao deixe de reportar so porque falta
algum item:

- Descricao do impacto (o que um atacante consegue fazer?)
- Versao do plugin afetada
- Versao do Revit + Windows
- Passos pra reproduzir
- Prova de conceito (POC) — codigo, modelo Revit reduzido, screenshot
- Sugestao de fix, se tiver

## Processo de resposta

1. **0-48h**: confirmacao de recebimento, triagem inicial
2. **48h-7d**: validacao tecnica + classificacao de severidade
3. **7-30d**: desenvolvimento do fix (severidade decide o prazo:
   critica = horas, alta = dias, media = semanas, baixa = proximo ciclo)
4. **Release**: patch publicado em release oficial; reporter creditado
   no CHANGELOG (a menos que prefira anonimato)
5. **Disclosure publico**: ~7-14 dias apos release pra dar tempo dos
   usuarios atualizarem; mais cedo se reporter pedir.

## Bug bounty

Atualmente **nao temos programa de bug bounty** (single-developer team,
sem orcamento dedicado pra recompensas em dinheiro). Reporters serios
recebem:

- Reconhecimento publico no CHANGELOG + Hall of Fame em
  `docs/security/HALL-OF-FAME.md` (a ser criado)
- Licenca perpetua gratuita do plugin
- Comunicacao direta com o desenvolvedor

## Escopo

**In scope:**
- Plugin SteelBIM (DLL principal + dependencias custom)
- Sistema de licenca (HMAC, validacao offline, gerador de chaves)
- Auto-update (download, validacao SHA256, Authenticode, extract)
- Crash reporting (Sentry — scrubbing de PII)
- Telemetria (PostHog — consent, opt-out)
- Documentos legais (EULA/Privacy/TOS quando publicados)

**Out of scope:**
- Vulnerabilidades no Revit em si (reportar pra Autodesk)
- Vulnerabilidades em dependencias NuGet third-party — abrir issue
  upstream e nos avisar; vamos bumpar a versao
- Engenharia social, phishing, ataques fisicos
- Bugs de UX que nao tem impacto de seguranca

## Threat model resumido

O plugin roda **client-side em maquinas Windows isoladas**. Vetores
considerados na arquitetura:

| Vetor | Mitigacao |
|---|---|
| Supply chain (release comprometido) | SHA256 manifest + Authenticode verify pos-extract (v2.7.10, ADR §5.3) + cert Sectigo OV (em aquisicao) |
| MITM no auto-update | HTTPS pinning (futuro), validacao SHA256, Authenticode |
| Vazamento de PII em crash report | PiiScrubber em SentryEvent + breadcrumbs (v2.7.10, LGPD) |
| Brute force de chave de licenca | HMAC SHA256 com secret nao-distribuido + DPAPI per-user pra storage |
| Path traversal em extract de ZIP | ZipSlipValidator antes de aplicar |
| Code injection via modelo Revit malicioso | Plugin nao executa codigo embutido em .rvt; APIs Revit retornam tipos fortemente tipados |

ADRs relacionadas:
- [ADR-006](docs/ADR/006-auto-update.md) — Auto-update
- [ADR-007](docs/ADR/007-crash-reporting-sentry.md) — Crash reporting LGPD-friendly
- [ADR-008](docs/ADR/008-telemetry-posthog.md) — Telemetria PostHog HTTP-direct
- [ADR-009](docs/ADR/009-code-signing.md) — Code signing

## Contato

`engenheiroalefvieira@gmail.com` — resposta em ate 48h uteis.
