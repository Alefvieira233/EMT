# Termos Comerciais (TOS) — FerramentaEMT

> ⚠️ **RASCUNHO — Pendente revisao juridica.** Este documento foi
> redigido pela equipe tecnica como ponto de partida para o advogado.
> NAO publicar como definitivo. Quando revisado, remover este aviso
> e renomear para `TOS.md` (sem `.draft`).

**Versao do rascunho:** 1
**Data do rascunho:** 2026-04-28
**Idioma:** Portugues do Brasil

> Este documento complementa o **EULA** (`EULA.md`) e a **Politica de
> Privacidade** (`PRIVACY.md`), regulando o aspecto **comercial** da
> relacao entre o Licenciante e o Licenciado: valores, prazos, modelo
> de licenciamento, reembolso, suporte e atualizacoes.

---

## 1. Modelos de licenciamento

> ⚠️ **Decisao pendente:** apresentamos as duas opcoes para o
> Licenciante decidir, junto ao advogado, qual modelo adotar. Pode
> oferecer **apenas um** ou **ambos**.

### 1.1. Opcao A — Assinatura anual

| Caracteristica | Detalhe |
|---|---|
| Periodicidade | Cobranca anual antecipada. |
| Renovacao | Automatica salvo cancelamento por escrito 30 dias antes do vencimento. |
| Atualizacoes | Inclusas (todas as versoes lancadas durante a vigencia). |
| Suporte | Inclusos (best-effort, ver §5). |
| Termino | Plug-in deixa de funcionar 30 dias apos a data de vencimento sem renovacao (grace period). |
| Trade-off | **Pro:** receita recorrente previsivel; clientes sempre na versao atual. **Con:** clientes resistentes a "subscription model" — comum em mercado brasileiro de eng. estrutural. |

### 1.2. Opcao B — Licenca perpetua

| Caracteristica | Detalhe |
|---|---|
| Periodicidade | Pagamento unico no ato da compra. |
| Renovacao | Nao aplicavel — Licenca eh permanente para a versao adquirida. |
| Atualizacoes | Inclusas durante 12 meses; apos, exige contratacao adicional ("upgrade plan"). |
| Suporte | Inclusos durante 12 meses; apos, contratacao opcional. |
| Termino | Apenas em caso de violacao do EULA. |
| Trade-off | **Pro:** preferido por clientes brasileiros tradicionais. **Con:** receita pontual; cliente fica "preso" em versao antiga sem upgrade. |

### 1.3. Hibrido (recomendado para v1.7.0)

Oferecer **ambas as opcoes**, com precificacao distinta. Exemplo
(valores indicativos `<TBD>`):

| Plano | Valor `<TBD>` | Inclui |
|---|---|---|
| **Anual** | R$ `<TBD>` /ano | Atualizacoes + suporte recorrentes. Cancela quando quiser. |
| **Perpetuo** | R$ `<TBD>` (fixo) | Versao atual + 12m de atualizacoes/suporte. Renovavel por R$ `<TBD>`/ano apos. |

Reavaliar precos apos 6 meses de mercado.

---

## 2. Numero de maquinas por licenca

2.1. Cada Licenca, por padrao, autoriza ativacao em **1 (uma) Maquina**.

2.2. Pacotes multiusuario podem ser oferecidos sob negociacao direta:

| Pacote | Numero de Maquinas | Desconto sugerido |
|---|---|---|
| Individual | 1 | Preco cheio. |
| Equipe (3-5 maquinas) | 3-5 | 10-15% |
| Empresa (6-15 maquinas) | 6-15 | 20-25% |
| Corporativo (>15) | >15 | Negociado caso a caso. |

2.3. Transferencia entre maquinas eh permitida com **maximo de 2
transferencias por ano por Licenca** (formatacao, troca de hardware,
etc). Excedente requer aprovacao do Licenciante.

---

## 3. Politica de reembolso

### 3.1. Direito de arrependimento (CDC + LGPD)

3.1.1. Conforme o **art. 49 do Codigo de Defesa do Consumidor**, em
compras realizadas fora do estabelecimento comercial (online), o
Licenciado pessoa fisica tem direito a **7 (sete) dias corridos** para
arrependimento, contados da data da compra ou da entrega da chave de
ativacao (o que ocorrer ultimo).

3.1.2. Reembolso integral nesse periodo. Nao se aplica a Licenciado
pessoa juridica.

### 3.2. Janela tecnica adicional

3.2.1. **Primeiros 14 dias** apos a entrega da chave: reembolso integral
caso o plug-in **nao funcione** no ambiente Revit do Licenciado por
motivos imputaveis ao Software.

3.2.2. Casos onde **NAO** ha reembolso na janela tecnica:
- Cliente desistiu por mudanca de planos (nao incompatibilidade).
- Cliente comprou versao errada (Revit 2025 com plug-in para Revit
  2024 — verificar antes de comprar).
- Software funciona normalmente mas cliente acha "nao gostou" da UX.

3.2.3. Reembolso processado em ate **30 dias corridos** apos
solicitacao validada.

### 3.3. Fora dos prazos

3.3.1. Apos os 7 dias do CDC + 14 dias da janela tecnica (15-21 dias
corridos da entrega), nao ha direito a reembolso, exceto em casos de:
- Defeito grave nao-reparavel pelo Licenciante em 60 dias.
- Descontinuidade do produto pelo Licenciante (caso em que reembolso
  proporcional eh oferecido).

---

## 4. Pagamento

4.1. **Formas aceitas:** transferencia bancaria (PIX, TED), cartao de
credito (via gateway de pagamento), boleto bancario.

4.2. **Moeda:** Reais (BRL). Outras moedas mediante negociacao direta.

4.3. **Impostos:** valores anunciados sao **brutos**, ja incluindo
todos os tributos aplicaveis no Brasil (ISS conforme municipio do
Licenciante, eventual ICMS).

4.4. **Atraso:** licencas com pagamento em atraso sao desativadas apos
30 dias de inadimplencia.

4.5. **Aviso previo de cobranca:** pagamentos recorrentes (Opcao A)
serao avisados por email 14 dias antes da cobranca.

---

## 5. Suporte tecnico

### 5.1. Escopo

5.1.1. **Incluido:** atendimento por email para:
- Duvidas sobre uso de funcionalidades documentadas.
- Bugs reproduziveis no plug-in.
- Orientacao sobre instalacao e ativacao.

5.1.2. **NAO incluido:**
- Treinamento em Revit ou em modelagem estrutural.
- Customizacao especifica para o projeto do Licenciado.
- Suporte presencial.
- Garantia de tempo de resposta especifico (SLA — ver §5.2).

### 5.2. Niveis de servico (best-effort)

5.2.1. O Licenciante envida melhores esforcos para:
- Responder novos tickets em **2 dias uteis** (Brasilia).
- Reproduzir bugs reportados em **5 dias uteis**.
- Liberar correcoes via update em **30 dias** apos confirmacao do bug
  (escopo: bugs criticos. Bugs cosmeticos podem ter prazo maior).

5.2.2. **NAO HA SLA contratual.** Os tempos acima sao metas, nao
compromissos. O Licenciante eh um desenvolvedor individual; ferias,
doenca, ou eventos pessoais podem prolongar prazos com aviso previo
quando possivel.

### 5.3. Canais

5.3.1. Email principal: `<TBD — email comercial>`.

5.3.2. Documentacao publica: `<TBD — URL>`.

5.3.3. Pode haver canal Discord/Slack publico para early adopters
(comunidade), sem garantia de resposta — apenas troca livre entre
usuarios.

---

## 6. Atualizacoes do Software

6.1. **Atualizacoes incluidas:**

- **Opcao A (Anual):** todas as versoes lancadas durante a vigencia.
- **Opcao B (Perpetua):** todas as versoes lancadas nos 12 meses
  apos a compra. Apos, exige plano de upgrade adicional.

6.2. **Tipos de atualizacao:**

- **Patch (1.7.0 → 1.7.1):** correcao de bugs. Inclusas em todos os
  planos.
- **Minor (1.7.x → 1.8.0):** novas features. Inclusas conforme §6.1.
- **Major (1.x → 2.0):** mudancas estruturais ou novas modalidades.
  Pode requerer pagamento de upgrade conforme plano.

6.3. **Compatibilidade:** o Licenciante envida melhores esforcos para
manter compatibilidade entre versoes minor (1.7.x → 1.8.0). Major
upgrades (2.0+) podem requerer ajustes no fluxo de trabalho do
Licenciado.

6.4. **Mecanismo:** o plug-in possui sistema de auto-update opt-in
(consentivel via janela de Privacidade). Atualizacoes manuais sempre
disponiveis em `<TBD — URL de releases>`.

---

## 7. Disponibilidade do Software

7.1. **Plug-in eh software desktop**, executando localmente no Revit
do Licenciado. **NAO depende de infraestrutura cloud do Licenciante
para funcionar** apos ativado.

7.2. **Servicos auxiliares opcionais (consentivel):**

- Verificacao de atualizacoes (api.github.com).
- Crash reports (sentry.io).
- Telemetria (eu.posthog.com).

Caso esses servicos estejam temporariamente indisponiveis, o plug-in
**continua funcionando** normalmente — apenas as funcoes auxiliares
ficam off ate retomada.

7.3. **NAO ha SLA de uptime** desses servicos auxiliares por se
tratarem de funcoes nao-criticas.

---

## 8. Mudancas no plano comercial

8.1. **Mudanca de preco:** novos precos aplicam-se apenas a:
- Novas Licencas adquiridas apos a data de vigencia do novo preco.
- Renovacoes (Opcao A) com aviso previo de 30 dias antes do
  vencimento.

8.2. **Migracao de plano:** Licenciado pode migrar de Anual para
Perpetuo (ou vice-versa) com calculo proporcional, sob negociacao
direta.

---

## 9. Termino comercial

9.1. **Pelo Licenciado:** pode cancelar a Licenca a qualquer momento.
Em planos anuais, nao ha reembolso de meses ja pagos (exceto casos
do §3 ou descontinuidade comercial).

9.2. **Pelo Licenciante:** pode descontinuar o produto a seu criterio.
Aviso previo de 90 dias. Reembolso proporcional sera oferecido para
planos em vigencia.

9.3. **Por inadimplencia:** Licenca desativada apos 30 dias de atraso
(§4.4).

9.4. **Por violacao do EULA:** Licenca desativada imediatamente, sem
reembolso (ver EULA §6.5).

---

## 10. Disposicoes finais

10.1. **Lei aplicavel:** este documento eh regido pelas leis da
**Republica Federativa do Brasil**.

10.2. **Foro:** mesmo do EULA — `<TBD — Foro de cidade do Alef>`.

10.3. **Notificacoes:** email `<TBD — email comercial>`.

10.4. **Hierarquia documental:** em caso de conflito entre este
documento e o EULA / Privacy Policy, **prevalece** este documento (TOS)
nas materias comerciais e financeiras; os outros documentos prevalecem
em suas respectivas materias (uso do software / privacidade).

10.5. Este documento entra em vigor na data de aceitacao do EULA pelo
Licenciado.

---

> **Notas para o advogado revisor:**
>
> - **Item 1 (modelo de licenciamento):** decisao estrategica entre
>   Anual / Perpetuo / Hibrido. Recomendamos Hibrido para v1.7.0
>   (maximiza alcance no mercado brasileiro). Validar implicacoes
>   tributarias.
> - **Item 1.3 (precos):** valores `<TBD>` precisam ser definidos pelo
>   Licenciante. Sugerir benchmark com plug-ins concorrentes:
>   Naviate, Revvit, BeamFin (mercado eng. estrutural BR).
> - **Item 3 (reembolso):** validar enquadramento como CDC art. 49 +
>   janela adicional voluntaria. Considerar adicionar clausula de
>   "garantia de adequacao" do art. 18 do CDC.
> - **Item 4.3 (impostos):** validar regime tributario do Licenciante
>   (Simples Nacional? Lucro Presumido? Lucro Real?). Pode requerer
>   ajustes na clausula.
> - **Item 5.2 (best-effort SLA):** linguagem cuidadosa pra evitar
>   interpretacao judicial como obrigacao contratual.
> - **Item 7.3:** validar que clausula "sem SLA de uptime" eh defensavel
>   no CDC para servicos opcionais.
> - **Item 9.2 (descontinuidade):** validar prazo de aviso (90 dias)
>   contra benchmarks de mercado e CDC.
> - **Pontos <TBD>:** preco anual, preco perpetuo, preco upgrade,
>   email comercial, URL de docs, foro, regime tributario.
> - Considerar adicionar **politica de localizacao** se houver intencao
>   de vender no exterior (US/EU sales tax, GDPR ja coberta por
>   PRIVACY).
