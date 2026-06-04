# PLANO — Ajustes do "Gerar Pórtico" V5 (terças, linha de corrente, chapas) — 2026-06-04

> Pedido do usuário (com imagens da treliça). 4 itens. Cada um: análise + spec executável.
> Branch `claude/great-turing-Vqlig`. Estado atual: v2.8.27, CI verde.
> REGRA: cirúrgico, CI-verde por onda, **nunca quebrar** fluxo existente.

---

## Item 1 — Terças: inclinação do banzo superior + INVERTER abertura
**Pedido:** a terça (perfil U/"C") deve acompanhar a inclinação do banzo superior, **por água**; e o
usuário precisa de uma **opção para inverter** a abertura do "C" (ele pode querer a abertura
acompanhando a descida da água, ou o contrário).

**Estado atual:** `PorticoGeometriaCalculator.InclinacaoTercaRad` (v2.8.25) já gira água 1 +β e água 2
−β, onde β = atan2(rise, meia-largura). A magnitude (inclinação) está certa.

**O que falta:**
1. Novo campo `bool InverterAberturaTerca` em `GerarPorticoConfig` **e** `GerarPorticoEntrada` (default false).
2. `InclinacaoTercaRad` recebe/usa esse flag: quando true, **nega o sinal** do ângulo retornado
   (abertura vira para o outro lado), preservando a magnitude por água.
3. `GerarPorticoService.MapearEntrada` repassa o flag.
4. Janela `GerarPorticoWindow`: checkbox **"Inverter abertura das terças"** na seção Terças
   (sem campo de ângulo — continua automático). Wire em BuildConfig.
5. Testes puros: InclinacaoTercaRad com InverterAbertura=true → sinais trocados; magnitude igual.

**Aceite:** no Revit, a terça "C" assenta inclinada no banzo de cada água; o checkbox troca o lado
da abertura.

---

## Item 2 — Linha de corrente: "coloquei 2 e só sai 1"
**Diagnóstico:** o NÚCLEO PURO já está correto e testado — `Calcular_LinhaCorrente_Duas_NosTercos
DoComprimento` confirma que N=2 gera 2 linhas (em L/3 e 2L/3), 4 segmentos. Logo, se o usuário ainda
vê 1, a causa é (a) **versão instalada antiga** (anterior à v2.8.24) ou (b) **bug no lado Revit**
(serviço ou leitura da janela), NÃO no cálculo.

**O que fazer (próxima sessão):**
1. Confirmar com o usuário a versão em *Sobre* (precisa ser ≥ v2.8.24; ideal v2.8.27+).
2. Reler `GerarPorticoService` o laço `foreach (Segmento s in layout.LinhasCorrente)` —
   garantir que cria 1 barra por segmento com `SymbolLinhaCorrente` e que não há dedupe/guard
   que descarte segmentos coincidentes (os 2 segmentos de cada linha compartilham o ponto da
   cumeeira, mas têm Y diferentes → não coincidem).
3. Reler a janela: `txtNumLinhasCorrente` é lido por `ParseInt(..., 3)`? O checkbox
   `chkLinha`/`LancarLinhaCorrente` está ligado? AtualizarHabilitacao não está zerando?
4. Acrescentar no resumo do comando a contagem real de linhas de corrente criadas (honestidade
   → o usuário vê "Linha de corrente: N" e sabemos se é cálculo ou criação).

**Aceite:** N=2 cria 2 linhas visíveis (L/3 e 2L/3); resumo bate.

---

## Item 3 — Chapa na cabeça do pilar (ligação pilar ↔ treliça) — NOVO
**Pedido:** sequência **pilar → chapa (topo do pilar) → treliça**. Campo para o usuário marcar essa
ligação; de preferência uma **chapa com dimensões editáveis (espessura, largura, comprimento)**.
Vale para pilar metálico OU de concreto.

**Spec:**
1. Reusar o padrão de `PlacaBaseLancamentoService`, mas no **TOPO** do pilar (z = hp), não na base.
   Provável novo `Services/Conexoes/ChapaTopoPilarService` (headless, transação própria), ou
   estender o de placa de base com parâmetro `NoTopo`.
2. `GerarPorticoConfig`: `bool InserirChapaTopoPilar`, `FamilySymbol? SymbolChapaTopo`,
   `double ChapaTopoEspessuraMm/LarguraMm/ComprimentoMm`. (Se usar família carregável face-based,
   as dimensões viram parâmetros de instância setados após inserir; se não houver família, criar
   chapa via extrusão/DirectShape com as 3 dimensões.)
3. Geometria: para cada `pilarId` recém-criado, achar o topo (LocationCurve/BoundingBox max Z) e
   inserir a chapa centrada, normal +Z, no nível. Aplicar dimensões.
4. Janela: seção **"Ligação pilar ↔ treliça"** → checkbox "Inserir chapa no topo do pilar" +
   combo de família (opcional) + 3 campos de dimensão (espessura/largura/comprimento) com defaults.
5. Best-effort + try/catch + contagem no resumo. Não bloqueia o resto.

**DECISÃO PENDENTE DO USUÁRIO (perguntar antes de codar):** a chapa deve ser
(A) uma **família de chapa carregável** que o usuário escolhe (como a placa de base), ou
(B) uma **chapa genérica criada pela ferramenta** a partir das 3 dimensões (sem precisar de família)?
Recomendado (B) para "só marcar e funcionar", com (A) como alternativa se já houver família padrão.

**Aceite:** com o checkbox ligado, cada pilar ganha uma chapa no topo, sob a treliça, com as
dimensões informadas.

---

## Item 4 — "Lançar placa de base nos pilares não funcionou"
**Diagnóstico (causa raiz provável):** `PlacaBaseLancamentoService` só insere a placa **onde há
concreto/fundação ABAIXO do pilar** (`FindConcreteBelow`). Num galpão recém-gerado **sem fundações
modeladas**, não há concreto embaixo → **0 placas** (comportamento atual, silencioso).

**O que fazer:**
1. Confirmar a hipótese relendo `FindConcreteBelow` e o fluxo no pórtico.
2. Correções possíveis (escolher a segura):
   - (a) Se "Lançar fundações" estiver ligado, lançar as fundações **antes** das placas de base
     (ordem), para haver concreto embaixo. **Recomendado** — resolve o caso comum.
   - (b) Adicionar modo "apoiar no nível/base do pilar" quando não houver concreto (fallback), para
     a placa sair mesmo sem fundação.
   - (c) No mínimo: **mensagem honesta** no resumo ("0 placas: nenhum pilar tem concreto/fundação
     abaixo — ligue 'Lançar fundações' ou modele os apoios").
3. Aceite: com fundações ligadas (ou apoio existente), as placas de base saem; sem isso, o usuário
   recebe a explicação clara.

---

## Ordem de execução sugerida (próxima sessão, com contexto cheio)
1. **Item 2** (diagnóstico linha de corrente — barato, alto valor) + **Item 4** (placa de base —
   provável fix de ordem/mensagem). Commit 1.
2. **Item 1** (inverter abertura da terça + garantir inclinação). Commit 2 (com testes puros).
3. **Item 3** (chapa no topo do pilar) — após responder a decisão A vs B. Commit 3.
Cada commit: build Release 0 warnings, testes, format, gitleaks — CI verde. Bump de versão por onda.
