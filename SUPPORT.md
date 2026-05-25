# Suporte e Contato

## Canais por tipo de problema

| Voce precisa de... | Use... |
|---|---|
| Reportar bug | [Issue: Bug Report](https://github.com/Alefvieira233/EMT/issues/new?template=bug_report.yml) |
| Sugerir feature | [Issue: Feature Request](https://github.com/Alefvieira233/EMT/issues/new?template=feature_request.yml) |
| Apontar erro de documentacao | [Issue: Documentacao](https://github.com/Alefvieira233/EMT/issues/new?template=docs.yml) |
| Reportar vulnerabilidade | Ver [SECURITY.md](SECURITY.md) (NAO use issue publica) |
| Suporte comercial / licenca / pricing | Email: `engenheiroalefvieira@gmail.com` |
| Entrar no beta program | Email com nome do escritorio + tipo de projeto principal |
| Pergunta tecnica geral | [Issues](https://github.com/Alefvieira233/EMT/issues) (label `question`) |

## Antes de abrir uma issue

Verifique nessa ordem (5 minutos podem evitar duplicacao):

1. **Voce esta na ultima versao?** Versao atual em
   [CHANGELOG.md](CHANGELOG.md). Upgrade pode ja ter o fix —
   o plugin auto-atualiza, mas pode ter ficado pendente; reinicie o
   Revit pra forcar.
2. **Issue ja existe?** Procure em
   [github.com/Alefvieira233/EMT/issues](https://github.com/Alefvieira233/EMT/issues)
   por palavras-chave do erro.
3. **CHANGELOG menciona algo recente?** Se o bug surgiu apos uma update,
   o CHANGELOG da pista do que mudou na area afetada.
4. **O log diz alguma coisa util?** Em `%LOCALAPPDATA%\SteelBIM\logs\`
   — anexe na issue.

## SLA por tipo de cliente

| Tier | Resposta inicial | Fix critico | Fix nao-critico |
|---|---|---|---|
| Trial / beta | 7 dias uteis | 14 dias | proximo ciclo |
| Cliente Professional (futuro) | 3 dias uteis | 7 dias | proximo ciclo |
| Cliente Studio (futuro) | 1 dia util | 3 dias uteis | 14 dias |
| Cliente Perpetua + manutencao (futuro) | 1 dia util | 48h | 14 dias |
| Security (todos os tiers) | 48h uteis | conforme severidade | — |

Os tiers comerciais ficam ativos a partir da **v2.8.0** (pricing em
definicao). Ate la, todos os usuarios sao "trial / beta" e usam o canal
publico (GitHub Issues + email).

## Beta program

O beta program valida features novas antes do release publico. Quem
participa:

- **Recebe primeiro** features experimentais (gated por flag em
  AppSettings)
- **Influencia o roadmap** — feedback direto vai pra ROADMAP.md
- **Reportes prioritarios** (resposta em 3 dias uteis mesmo no tier
  trial)
- **Reconhecimento publico** no CHANGELOG quando reporta bug que
  impacta o release

Pra entrar: email pra `engenheiroalefvieira@gmail.com` com:
- Nome do escritorio
- Tipo de projeto principal (metalica leve / pesada / pre-fabricado /
  outros)
- Versao do Revit
- Quantidade de seats em uso

## FAQ rapido

**O SmartScreen avisa "Aplicativo nao reconhecido" ao instalar. Eh
seguro?**
Sim. O instalador ainda nao eh assinado por certificado de codigo
(Sectigo OV em aquisicao — ver [ADR-009](docs/ADR/009-code-signing.md)).
Clique em "Mais informacoes" -> "Executar assim mesmo". Apos cert
ativar (v2.7.11+), o aviso desaparece automaticamente.

**O plugin manda dados meus pra algum lugar?**
Por default sim, com opt-out por consent dialog na primeira execucao:
- **Crash reports** via Sentry (com [PII scrubbing](docs/ADR/007-crash-reporting-sentry.md))
- **Telemetria de uso** via PostHog (eventos agregados, sem identidade —
  ver [ADR-008](docs/ADR/008-telemetry-posthog.md))
Voce pode desativar ambos em qualquer momento via dialogo Privacidade
no ribbon.

**Como faco upgrade?**
Auto-update roda no startup do Revit. Se o update foi baixado mas nao
aplicou (DLL em uso), reinicie o Revit — vai aplicar no proximo boot.
Para forcar download manual: baixar release nova em
[github.com/Alefvieira233/EMT/releases](https://github.com/Alefvieira233/EMT/releases)
e rodar o setup por cima.

**Posso usar em mais de uma maquina?**
Cada chave de licenca eh **per-machine** (binding por hash de hardware).
Para multiplas maquinas, multiplas chaves. Tier Studio (futuro)
suportara floating license em LAN.

**Funciona em Revit 2024 ou 2026?**
Hoje so 2025. Suporte a 2026 entra no roadmap apos a Autodesk congelar
a API (geralmente Q1 do ano da release). Suporte a 2024 nao esta
planejado.

## Local

Uberlandia/MG, Brasil. Fuso UTC-3 (BRT).

Resposta media historica:
- Issues GitHub: ~24-48h em dias uteis
- Email: ~24h em dias uteis
- Security: ate 48h uteis garantidos (ver SECURITY.md)
