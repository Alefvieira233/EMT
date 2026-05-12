# Politica de Privacidade — FerramentaEMT

**Versao:** 1.7.0
**Data de vigencia:** 2026-05-XX
**Controlador dos dados:** Alef Christian Gomes Vieira (CPF/CNPJ: <TBD>)
**Encarregado pelo Tratamento de Dados (DPO):** Alef Christian Gomes Vieira
**Contato DPO:** alefchristiangomesvieira@gmail.com

Esta Politica de Privacidade esta em conformidade com a **Lei Geral de Protecao de Dados Pessoais (LGPD — Lei 13.709/2018)**.

---

## 1. Dados coletados

O Plugin FerramentaEMT coleta os seguintes dados, mediante consentimento previo:

### 1.1 Dados de licenciamento (obrigatorios)

- **Email do titular da licenca** — utilizado exclusivamente para identificar a licenca e enviar comunicacoes relacionadas (renovacao, atualizacoes criticas)
- **Status da licenca** (Trial / Valid / Expired)

### 1.2 Relatorios de erro — *Sentry* (opcional, opt-in)

Caso o usuario consinta:

- Stack trace anonimizado de excecoes nao tratadas
- Versao do Plugin (`AssemblyInformationalVersion`)
- Versao do Revit (sempre "2025")
- Versao do sistema operacional (ex.: "Microsoft Windows 10.0.19045")
- Idioma do sistema (ex.: "pt-BR")

**Dados explicitamente NAO enviados:** email do usuario, nome de maquina, ID de maquina, endereco MAC, caminhos absolutos de arquivos, conteudo de modelos do Revit.

Os dados passam por **scrubbing automatico** (`PiiScrubber`) antes do envio para remover quaisquer informacoes pessoais que possam aparecer em mensagens de erro.

### 1.3 Telemetria de uso — *PostHog* (opcional, opt-in)

Caso o usuario consinta:

- Identificador anonimo de sessao (UUID v4 gerado localmente, NAO derivado de dados de maquina)
- Eventos: `command.executed`, `command.failed`, `license.state_checked`, `update.detected`, `update.applied`
- Propriedades dos eventos: nome do comando, duracao de execucao, contagem de elementos processados, status de sucesso

**Dados explicitamente NAO enviados:** email, nome, identificadores de maquina, caminhos, conteudo de projetos.

## 2. Finalidades

Os dados sao tratados para as seguintes finalidades:

- **Execucao de contrato** (Art. 7º, V da LGPD): validacao de licenca, entrega de atualizacoes
- **Legitimo interesse do controlador** (Art. 7º, IX da LGPD): melhoria continua do produto via analise agregada de uso e identificacao de bugs

## 3. Compartilhamento de dados

Os dados sao compartilhados exclusivamente com:

| Operador | Dados | Finalidade | Localizacao |
|----------|-------|------------|-------------|
| Sentry GmbH | Crash reports anonimizados | Analise de erros | Uniao Europeia (eu.sentry.io) |
| PostHog Inc. | Telemetria anonimizada | Analise de uso | Uniao Europeia (eu.posthog.com) |
| GitHub Inc. | Verificacao de atualizacoes | Auto-update | Estados Unidos (api.github.com) |

Ambos os operadores (Sentry e PostHog) estao configurados para regioes da Uniao Europeia, em alinhamento com praticas de protecao de dados.

## 4. Tempo de retencao

| Tipo de dado | Retencao |
|--------------|----------|
| Email de licenciamento | Enquanto a licenca estiver ativa + 24 meses |
| Crash reports (Sentry) | 90 dias |
| Telemetria de uso (PostHog) | 12 meses |
| Logs locais no computador do usuario | 30 dias rotativos (`%LocalAppData%\FerramentaEMT\logs\`) |

## 5. Direitos do titular (Art. 18 LGPD)

O titular dos dados pode, a qualquer momento, solicitar:

- **Acesso** aos dados pessoais tratados
- **Correcao** de dados incompletos, inexatos ou desatualizados
- **Anonimizacao, bloqueio ou eliminacao** de dados desnecessarios ou tratados em desconformidade com a LGPD
- **Portabilidade** dos dados
- **Eliminacao** dos dados pessoais tratados com consentimento
- **Revogacao do consentimento**, a qualquer momento, pela propria interface do Plugin (janela "Privacidade")

Solicitacoes devem ser enviadas para `alefchristiangomesvieira@gmail.com`. Resposta em ate 15 dias uteis.

## 6. Medidas de seguranca

- Comunicacao com servicos de terceiros sempre via **TLS 1.2+**.
- **Scrubbing automatico de PII** (emails, paths absolutos com username do Windows) antes de qualquer envio para Sentry e PostHog.
- Consent ledger local versionado (`PrivacyConsentWindow` v3): cada toggle exige acao explicita do usuario.
- Codigo de coleta auditavel (sem ofuscacao), coberto por testes automatizados.

## 7. Atualizacoes desta Politica

Esta Politica pode ser atualizada periodicamente. Mudancas materiais serao comunicadas:

- Por aviso na janela de Privacidade do Plugin (na proxima execucao apos atualizacao).
- Por email ao endereco da licenca, com 30 dias de antecedencia para mudancas materiais.

Versoes anteriores ficam disponiveis no repositorio publico do produto. O uso continuado do Plugin apos uma atualizacao desta Politica constitui aceitacao da nova versao.

## 8. Contato e reclamacao

- **Encarregado de dados (DPO):** alefchristiangomesvieira@gmail.com.
- **ANPD (Autoridade Nacional de Protecao de Dados):** em caso de reclamacao nao-resolvida pelo Licenciante, o titular pode reportar a ANPD em https://www.gov.br/anpd/pt-br.
