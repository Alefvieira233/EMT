# Politica de Privacidade — FerramentaEMT

> ⚠️ **RASCUNHO — Pendente revisao juridica.** Este documento foi
> redigido pela equipe tecnica como ponto de partida para o advogado.
> NAO publicar como definitivo. Quando revisado, remover este aviso
> e renomear para `PRIVACY.md` (sem `.draft`).

**Versao do rascunho:** 1
**Data do rascunho:** 2026-04-28
**Idioma:** Portugues do Brasil
**Conformidade legal:** LGPD (Lei 13.709/2018)

---

## Sumario executivo

A FerramentaEMT eh um plug-in para o software Revit (Autodesk). Para
funcionar, **coletamos dados minimamente necessarios** ao licenciamento,
diagnostico de erros e melhoria do produto. Esta politica detalha
**exatamente o que coletamos**, **como protegemos** e **quais sao
seus direitos**.

**Em resumo:**
- ✅ Coletamos: email da licenca, identificador anonimo de instalacao,
  stack traces de erros, eventos agregados de uso de comandos, versao
  do Revit/sistema operacional/idioma.
- ❌ NAO coletamos: machine ID, MAC address, paths absolutos do seu
  computador, conteudo de modelos do Revit, nomes de arquivos do
  projeto, identificadores pessoais alem do email da licenca.
- 🛡️ Voce pode desativar a maioria das coletas a qualquer momento na
  janela de Privacidade do plug-in.
- 🇪🇺 Crash reports e telemetria ficam em servidores na **Uniao
  Europeia** (Sentry EU + PostHog EU) — jurisdicao mais alinhada com
  os direitos LGPD.

---

## 1. Quem somos (controlador de dados)

**ALEF CHRISTIAN GOMES VIEIRA**
CREA: 0319918963
Email do encarregado de dados (DPO): `<TBD — Alef define>`
Endereco: `<TBD>`

Este eh o **controlador de dados** nos termos do art. 5º, VI da LGPD.

---

## 2. O que coletamos

### 2.1. Dados sempre coletados (necessarios para o servico)

| Dado | Finalidade | Base legal LGPD |
|---|---|---|
| **Email da licenca** | Vincular chave de ativacao ao Licenciado, suporte tecnico, comunicacao sobre renovacao. | Execucao de contrato (art. 7º, V). |
| **Fingerprint da maquina** (hash unidirecional, NAO eh machine ID legivel) | Vincular a Licenca a 1 dispositivo conforme contratado. | Execucao de contrato (art. 7º, V). |
| **Versao do plug-in** | Suporte tecnico contextual. | Execucao de contrato (art. 7º, V). |

### 2.2. Dados coletados com consentimento (opt-in explicito)

**Voce escolhe individualmente quais dessas tres categorias autorizar**,
na primeira execucao do plug-in (janela de Privacidade) e a qualquer
momento na janela de Licenca.

| Dado | Finalidade | Base legal LGPD |
|---|---|---|
| **Verificacao de atualizacoes** (HTTP GET `api.github.com`) | Avisar sobre nova versao disponivel; baixar com seu consentimento. | Consentimento (art. 7º, I). |
| **Crash reports** (stack traces de erros) | Diagnosticar e corrigir falhas que afetam usuarios. | Consentimento (art. 7º, I) + legitimo interesse limitado (art. 7º, IX). |
| **Telemetria de uso** (eventos agregados de comandos) | Entender quais funcionalidades sao usadas, priorizar correcoes e melhorias. | Consentimento (art. 7º, I). |

### 2.3. Detalhe dos dados nas tres coletas opcionais

#### Verificacao de atualizacoes
- **Para onde vai:** `api.github.com` (servidor publico do GitHub).
- **O que enviamos:** apenas user agent padrao do .NET HttpClient.
NAO enviamos email, fingerprint, ou identificador.
- **Frequencia:** maximo 1x a cada 24h.
- **Retencao:** GitHub trata logs conforme suas proprias politicas.

#### Crash reports (Sentry)
- **Para onde vai:** Sentry (region EU — `o*.ingest.sentry.io`).
- **O que enviamos:**
  - Stack trace do erro (nomes de classes, metodos, numeros de linha).
  - Tipo da excecao + mensagem (com **scrubbing automatico** de email
    e paths Windows com username — substituidos por `<EMAIL>` e
    `<USER>\` antes do envio).
  - Versao do plug-in, versao do Revit, versao do Windows, idioma
    do sistema.
  - Estado da licenca (`Trial`/`Valid`/`Expired`) — sem incluir o
    email associado.
- **NAO enviamos:**
  - Email do usuario.
  - Fingerprint ou machine ID.
  - Conteudo do modelo Revit.
  - Nomes de arquivo do projeto.
  - Paths absolutos com username.
- **Identificador anonimo:** mesmo session_id usado pela telemetria
  (UUID v4 sem relacao com hardware ou usuario).
- **Retencao:** **90 dias** apos o registro do crash. Apos esse periodo,
  os dados sao deletados pelo Sentry.

#### Telemetria de uso (PostHog)
- **Para onde vai:** PostHog (region EU — `eu.posthog.com`).
- **O que enviamos (por evento):**
  - Nome do comando executado (ex.: `CmdGerarTrelica`) ou nome do
    evento de sistema (`license.state_checked`, `update.detected`).
  - Duracao em milissegundos.
  - Sucesso ou falha (boolean) e tipo da excecao caso falha.
  - Versao do plug-in / Revit / sistema operacional / idioma.
  - Estado da licenca.
  - session_id anonimo (UUID v4 sem relacao com hardware ou usuario).
- **NAO enviamos:**
  - Email do usuario.
  - ElementId.Value ou conteudo do modelo Revit.
  - Nomes de elementos especificos do projeto.
  - Conteudo de paths absolutos.
- **Sample rate:** comandos bem-sucedidos sao amostrados em 10% para
  reduzir volume; falhas e crashes sao 100%.
- **Retencao:** **365 dias** apos o registro. Apos esse periodo, os
  dados sao agregados (anonimizados sem possibilidade de reverter ao
  individuo) ou deletados.

---

## 3. O que NAO coletamos (compromisso explicito)

Para deixar claro o que **NAO** entra em nenhuma rota de coleta:

- ❌ **Machine ID legivel** (nem como hash retro-engenheiravel).
- ❌ **MAC address** ou identificadores de hardware.
- ❌ **Paths absolutos** com nome de usuario do Windows (sao scrubbados
  antes do envio).
- ❌ **Conteudo de modelos Revit** (geometria, parametros, texto livre).
- ❌ **Nomes de arquivo** ou paths de projetos abertos pelo usuario.
- ❌ **Username do Revit ou do Windows.**
- ❌ **Cookies de rastreamento** (nao somos web).
- ❌ **Endereco IP** persistido junto com identificador (apenas trafego
  TLS, comum a qualquer requisicao HTTP).

---

## 4. Onde os dados ficam (residencia + cross-border)

### 4.1. Servidores

| Categoria | Provedor | Regiao | Justificativa |
|---|---|---|---|
| Email da licenca | Servidor proprio do Licenciante | Brasil | Dados contratuais brasileiros sob jurisdicao BR. |
| Crash reports | Sentry Inc. | Uniao Europeia (Frankfurt, Alemanha) | LGPD-compatibilidade. |
| Telemetria de uso | PostHog Inc. | Uniao Europeia (eu.posthog.com) | LGPD-compatibilidade. |

### 4.2. Transferencia internacional

Crash reports e telemetria envolvem transferencia de dados anonimos para
servidores na **Uniao Europeia**. A LGPD permite tal transferencia
(art. 33) considerando que:

- Os paises europeus oferecem **nivel de protecao adequado** (art. 33, I).
- Os dados transferidos sao anonimos (sem identificadores pessoais
  diretos como email).

---

## 5. Seus direitos (LGPD)

Voce tem os seguintes direitos sobre seus dados pessoais (LGPD art. 18):

| Direito | Como exercer |
|---|---|
| **Confirmar a existencia de tratamento** | Email para o DPO `<TBD>`. Resposta em ate 15 dias. |
| **Acessar os dados** | Email para o DPO. Receber copia dos dados associados ao seu email de licenca. |
| **Corrigir dados incompletos, inexatos ou desatualizados** | Email para o DPO. |
| **Anonimizar, bloquear ou eliminar dados desnecessarios** | Email para o DPO. |
| **Portabilidade dos dados** | Email para o DPO. Dados serao entregues em formato JSON estruturado. |
| **Eliminar dados tratados com consentimento** | Desativar a respectiva coleta no plug-in (Privacidade) **+** email para o DPO solicitando eliminacao retroativa. |
| **Revogar consentimento** | Desativar a respectiva coleta na janela de Privacidade do plug-in (efeito imediato em coletas futuras). |
| **Informacao sobre compartilhamento** | Esta Politica ja lista os 3 destinos: Sentry, PostHog, GitHub (apenas atualizacoes). |
| **Oposicao** | Email para o DPO em caso de tratamento sem consentimento expresso (raro neste plug-in). |

Resposta dentro do prazo legal de **15 dias corridos** (LGPD art. 19).

---

## 6. Seguranca

Adotamos as seguintes medidas tecnicas de seguranca:

- **Email da licenca:** transmissao via TLS 1.2+; armazenamento em
  servidor com acesso restrito.
- **Chave de ativacao:** assinada via HMAC-SHA256 antes do envio; nao
  contem dados pessoais legiveis.
- **Crash reports e telemetria:** transmissao via TLS 1.2+; **scrubbing
  automatico** de PII (email, paths) antes do envio para Sentry/PostHog.
- **Certificados:** Sentry e PostHog usam certificados validos emitidos
  por CAs publicas; falhas de TLS abortam o envio sem retry.
- **Codigo aberto auditavel:** o codigo de coleta esta em arquivos puros
  (sem ofuscacao) auditados por testes automatizados que verificam o
  scrubbing.

---

## 7. Crianca e adolescente

Este plug-in **nao se destina ao uso por menores de 18 anos**. Nao
coletamos intencionalmente dados de menores. Se voce eh responsavel
legal por um menor que tenha utilizado o plug-in, contate o DPO para
remocao dos dados.

---

## 8. Cookies

O plug-in **nao usa cookies**. Cookies sao tecnologia de navegacao web,
e o plug-in eh um software desktop.

---

## 9. Atualizacoes desta Politica

9.1. O Licenciante pode atualizar esta Politica de Privacidade
periodicamente. A versao atual fica disponivel em
`<TBD — URL publica do documento>`.

9.2. Mudancas materiais serao comunicadas:
- Por **aviso na janela de Privacidade** do plug-in (na proxima execucao).
- Por **email** ao endereco da Licenca, com 30 dias de antecedencia.

9.3. Se voce nao concordar com a nova versao, pode revogar consentimentos
ou cessar uso do plug-in. Continuar usando apos vigencia da nova versao
implica aceitacao.

---

## 10. Contato e reclamacao

10.1. **Encarregado de dados (DPO):** `<TBD — Alef define>`.

10.2. **ANPD (Autoridade Nacional de Protecao de Dados):**
em caso de reclamacao nao-resolvida pelo Licenciante, voce pode
reportar a ANPD em https://www.gov.br/anpd/pt-br.

---

> **Notas para o advogado revisor:**
>
> - Verificar enquadramento da telemetria como "consentimento" vs
>   "legitimo interesse" (LGPD art. 7º, IX). Argumento pra legitimo
>   interesse: dados sao anonimos, agregados, e necessarios para
>   manutencao do produto. Argumento pra consentimento: melhor pratica
>   + clareza pro usuario. Recomendamos manter consentimento explicito.
> - Pontos `<TBD>`: DPO email, endereco, URL publica do documento.
>   Alef precisa preencher antes de publicar.
> - Item 4.2: validar se a transferencia para EU eh realmente "nivel
>   adequado" sob LGPD (art. 33, I). ANPD ainda nao publicou lista
>   oficial de paises adequados — depende de avaliacao caso-a-caso.
> - Item 5: revisar prazos legais. LGPD usa "15 dias" mas algumas
>   jurisprudencias estendem para reclamacoes complexas.
> - Item 6: validar se as medidas tecnicas atendem ao art. 46 da LGPD
>   (medidas de seguranca, tecnicas e administrativas).
> - Item 7: avaliar se o plug-in deve ter clausula explicita de
>   recusa de menores no EULA tambem.
> - Considerar adicionar mapa de fluxo de dados visual (data flow
>   diagram) como anexo, para facilitar auditorias.
> - Esta politica deve estar disponivel ANTES da primeira execucao do
>   plug-in (consentimento informado eh apenas valido se usuario teve
>   acesso ao texto). URL publica obrigatoria.
