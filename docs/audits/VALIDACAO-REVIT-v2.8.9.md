# Roteiro de Validação no Revit — v2.8.9

**Para:** Alef + Victor · **Onde:** Revit 2025 com o SteelBIM v2.8.9 instalado.
**Por quê:** o ambiente de CI/IA não tem Revit — ele garante que o código **compila e
passa nos testes puros**, mas o comportamento dentro do modelo (geometria, cotas,
armaduras) só pode ser confirmado por vocês. Este roteiro cobre **cada função alterada
na v2.8.9** (regressão) + os itens que a auditoria marcou como **PRECISA-REVIT**.

Para cada teste: faça o **Setup**, siga os **Passos**, confira o **Esperado**. Se falhar,
anote o **Reportar** (print + arquivo de log em `%LOCALAPPDATA%\SteelBIM\logs\`).

---

## PARTE A — Regressão das correções da v2.8.9 (devem funcionar agora)

### A1. ⭐ Cotar Treliça (era 100% inoperante — correção principal)
- **Setup:** uma elevação/corte de uma treliça plana (banzos paralelos) com banzo
  superior, inferior, montantes e diagonais modelados como vigas estruturais. Repita
  depois com uma treliça de **duas águas** (banzo superior inclinado).
- **Passos:** selecione todas as barras da treliça → Ribbon **SteelBIM | Detalhamento →
  Cotar Treliça** → marque todas as faixas → OK.
- **Esperado:**
  - **NÃO** aparece mais o aviso "Não foi possível detectar banzos válidos" (era o bug).
  - São criadas as faixas de cota (painéis do banzo superior, do inferior, vão total,
    vãos entre apoios) + textos "BANZO SUPERIOR/INFERIOR `<perfil>`" + tags de perfil.
  - As alturas de montante saem com valores plausíveis (em mm), não "0" nem absurdos.
- **Reportar se falhar:** quantas cotas/tags/textos saíram (o diálogo final informa);
  quais faixas vieram erradas/ausentes; se alguma cota ligou pontos de banzos diferentes
  (cota "torta"). *Obs.: o posicionamento fino das cotas (faixa de painéis) é o ponto que
  mais precisa do seu olho — ver B2 abaixo.*

### A2. Verificar Modelo — falsos positivos de sobreposição
- **Setup:** um modelo com várias ligações/encontros de perfis que apenas se **tocam**
  (mesa com mesa, chapa com perfil) sem sobreposição real significativa.
- **Passos:** Ribbon → **Verificar Modelo** → rode a checagem de sobreposição.
- **Esperado:** **bem menos** "sobreposições" reportadas que antes (o limiar estava ~35×
  sensível demais — pequenos toques viravam issue). Só sobreposições reais (volume
  relevante) devem aparecer.
- **Reportar:** quantidade de sobreposições antes vs. agora, se possível.

### A3. Marcar Peças — não apagar marca em parâmetro numérico
- **Setup:** alguns elementos com uma marca já preenchida num parâmetro **numérico**
  (Integer/Double) customizado.
- **Passos:** Marcar Peças → destino = esse parâmetro → **desmarque** "Sobrescrever
  existentes" → execute.
- **Esperado:** as marcas existentes **permanecem** (antes eram sobrescritas/apagadas).

### A4. Exportar Lista de Materiais — arquivo aberto no Excel
- **Setup:** gere a lista uma vez, **deixe o .xlsx aberto no Excel**, gere de novo no
  mesmo caminho.
- **Esperado:** mensagem amigável "O arquivo pode estar ABERTO no Excel… Feche o arquivo
  e tente novamente" (antes vinha a mensagem técnica do .NET).

### A5. Travamento — cancelar com ESC
- **Passos:** Gerar Travamentos → aperte **ESC** durante a seleção das terças.
- **Esperado:** cancela limpo, sem erro. (Se ocorrer um erro real de API agora, ele
  aparece com mensagem — antes era engolido em silêncio.)

### A6. Guarda-Corpo — config incompleta
- **Passos:** abra Guarda-Corpo; se a janela permitir confirmar **sem** escolher
  família ou nível, tente.
- **Esperado:** aviso "Selecione a família… e o nível… antes de continuar" (antes:
  crash/erro genérico).

### A7. Janelas de armadura PF — separador decimal
- **Passos:** em **PF - Estribos Pilar** e **PF - Aços Estaca**, digite o cobrimento
  como **`1.5`** e depois como **`1,5`**.
- **Esperado:** ambos viram 1,5 cm. (Antes, em PC pt-BR, "1.5" podia virar 15.)

### A8. Branding PF (cosmético)
- **Esperado:** os títulos dos diálogos de validação do PF agora mostram **"PF -"**
  (ex.: "PF - Aços Pilar", "PF - Estribos Viga", "PF - Lançar Fundação"), não mais
  "PM -" nem "ECC -".

---

## PARTE B — Itens PRECISA-REVIT (validar e, se preciso, eu ajusto pelo que vocês virem)

> Estes dependem de geometria real. Façam o teste e me digam o resultado; com isso eu
> fecho cada um com segurança (não dá pra acertar às cegas).

### B1. Cotas verticais do Diagrama de Montagem em galpão ao longo de Y
- **Setup:** um galpão cujo **comprimento está ao longo do eixo Y** do projeto (vistas
  de seção com RightDirection = +Y).
- **Passos:** Gerar Diagrama de Montagem com as cotas verticais ligadas.
- **Esperado / Verificar:** as cotas verticais (SpotElevation) aparecem **à direita,
  visíveis e alinhadas** na vista. **Suspeita:** em vistas ao longo de Y elas podem sair
  fora de lugar (o código posiciona em world-space). Se isso ocorrer, me avise — a
  correção é posicionar via `RightDirection/UpDirection` da vista.

### B2. Cotar Treliça — cota de painel ligando o banzo certo
- **Verificar (sobre A1):** as faixas de **painéis do banzo superior** ligam só nós do
  banzo **superior** (e idem inferior)? Em treliça alta/duas águas, confira se nenhuma
  cota encadeada mistura nó de cima com nó de baixo. Se misturar, me diga em qual faixa —
  a correção é casar a barra por banzo (hoje `EncontrarBarraNoNo` casa só por X).

### B3. Estribos PF — estribo duplicado na junção de zonas
- **Setup:** gere estribos de uma viga (ou pilar) no modo de **zoneamento NBR**
  (apoio/central/apoio).
- **Verificar:** existe **estribo duplicado/coincidente** exatamente na fronteira entre
  a zona de apoio e a central? Conte os estribos vs. o esperado. Se houver duplicata, me
  avise — ajusto o include-first/last das zonas.

### B4. Bloco sobre 2 estacas — armadura pela base, não pelo bounding box
- **Setup:** um bloco de 2 estacas que tenha **pedestal/cálice acima da base** (bbox
  maior que a base).
- **Verificar:** as barras de fundo/topo são posicionadas pela **dimensão da base**
  (cobrimento medido a partir da base), não pela altura total do bbox? **Suspeita:** o
  código lê os parâmetros da família com nomes possivelmente corrompidos
  ("DimensÃ£o…") e cai no fallback do bbox. Me diga o **nome exato** dos parâmetros de
  dimensão na sua família de bloco — corrijo o lookup para bater certinho.

### B5. Conexão Terça — modo Completo em viga de apoio profunda
- **Setup:** uma terça apoiada numa viga **mais alta que ~200 mm** de mesa/altura.
- **Verificar:** no modo "Completo", a placa inferior é ajustada até o topo da viga?
  **Suspeita:** o raycast tem alcance fixo de 200 mm e pode não achar a face em vigas
  profundas (vira no-op silencioso). Se acontecer, me avise — derivo o alcance da altura
  da viga.

---

## Como me reportar
Para cada item que falhar, mande: (1) qual teste (ex.: "B3"), (2) o que esperava vs. o
que aconteceu, (3) print da vista/diálogo, (4) o `emt-*.log` do dia. Com isso eu corrijo
e empurro pro PR #63 — você revalida só o item.

> **Prioridade sugerida:** A1 (Cotar Treliça) e B2 primeiro — é a função mais reescrita e
> a de maior impacto pra entrega de detalhamento.
