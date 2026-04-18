# Projeto de referência — Galpão padrão EMT (3 pranchas)

**Responsável técnico:** Eng. Alef Christian Gomes Vieira (EMT).
**Data da prancha:** 08/04/2024.
**Tipo de obra:** galpão em estrutura metálica sobre fundação em concreto armado,
pilares metálicos em U duplo, cobertura em treliça de duas águas.
**Grid:** A–G × 1–2 (7 linhas × 2 linhas) com vãos típicos **5000 mm** horizontais.
**Vão transversal da treliça:** 15010 mm (pilar a pilar).

**Pranchas arquivadas aqui:**

- `EST-01-locacao-fundacoes.pdf` — Planta de locação das sapatas/pilares, armação das fundações, perspectivas, lista de materiais.
- `EST-02-plano-geral-cobertura.pdf` — Plano geral da cobertura com treliças, terças, contraventamentos, linhas de corrente, tabelas (perfis, telhas, placas), notas gerais metálica, 2 perspectivas 3D.
- `EST-03-detalhamento-trelicas.pdf` — Elevação das treliças, elevação frontal/posterior, detalhes (mão francesa, ligação de contraventamento, topo do pilar), perspectivas, lista de materiais.

**Importância:** este é o **exemplo mínimo mais completo** do escritório — mostra que
**três pranchas bastam** para um galpão pequeno bem documentado. A estrutura
EST-01 → EST-02 → EST-03 é o template que o FerramentaEMT deve ser capaz de
reproduzir automaticamente.

---

## 1. EST-01 — Locação e fundações

### 1.1. Escala e conteúdo

- **Escala principal:** 1:50 (`LOCAÇÃO DAS SAPATAS / PILARES`).
- **Escalas secundárias:** 1:50 (`DETALHAMENTO DAS FUNDAÇÕES`), 1:50 (`DET. VIGA BALDRAME`).
- Vista em planta com grid A-G × 1-2, malha 5000 mm × 6683/8327 mm.

### 1.2. Elementos representados

- **Sapatas** em planta: retângulos tracejados mostrando a projeção da sapata sob cada pilar, com texto lateral no formato `170x210x60` (dimensões em cm, L × B × h da sapata).
- **Pilares metálicos** em planta: hachura preta sólida + identificador:
  `P11 2U300x100x25x3.00`, `P13 2U300x100x25x3.00`, `P15 2U300x100x25x3.00`, `P16 2U300x100x25x3.00`.
  **Padrão de tag:** `Pnn <perfil>` onde `P` = pilar, `nn` = número sequencial, `2U...` = perfil duplo em U formado a frio.
- **Sapatas pelos pares de eixos:** numeradas `S1`, `S3`, `S11`, `S13`, `S15`, `S16` etc.
- **Vigas baldrame** entre sapatas: linhas tracejadas identificadas `V3`, `V4`, `V12`, `V14`, `V16`.

### 1.3. Detalhamento em corte (quadrante inferior esquerdo)

- `DETALHAMENTO DAS FUNDAÇÕES` (1:50): corte mostrando sapata 170X210X60,
  com armação inferior (12 N2 Ø10.0 C=143), armação vertical, chumbador.
- `DET. VIGA BALDRAME` (1:50): corte da viga baldrame com armação longitudinal e estribos.
- Perspectiva 3D da sapata com pilar metálico e cálice de concreto (label `170x210x60`).

### 1.4. Detalhe de ancoragem (lateral direita)

- Detalhe do **cálice/pedestal de concreto** com:
  - Dimensões da placa: `350x450x16 mm (A-36)`.
  - Parafusos: `8 Ø20 mm, ISO 898.C4.6`.
- Desenho da ancoragem em 3 vistas (planta + elevação + isométrica).
- **Nota crítica:** `Orientar ancoragem ao centro da placa - 90 mm`.
- Simbologia das ancoragens com legenda.

### 1.5. Tabelas laterais (direita)

- `TABELA RESUMO DE AÇO DAS VIGAS BALDRAME` (Número do vergalhão / Diâmetro / Comprimento / Peso por metro / Peso total).
- `TABELA RESUMO DE AÇO DAS SAPATAS`.
- `TABELA RESUMO DE AÇO DOS PILARES`.
- `TABELA DE ÂNCORAS` (Diâmetro / Comprimento / Número). Exemplo desta obra: `20.00 mm / 250 / 128`.
- `FORMAS / CONCRETO` (Tipo / Volume / Área). Exemplo: `170X210X60 → 28.09 m³ / 57 m²`.

### 1.6. Notas gerais - CONCRETO (caixa de texto na direita)

Formato padrão EMT, normas citadas:

- NBR 6118:2014 — projeto de estruturas de concreto.
- NBR 6120:2019 — cargas para cálculo de estruturas de edificações.
- NBR 6122:2019 — projeto e execução de fundações.
- Medidas em **centímetros** (fundações).
- Carregamentos considerados (ex.: alvenaria de bloco cerâmico 1.300 kgf/m³).
- Especificação de concreto: C20, C25 etc.
- Cobrimento mínimo, características de resistência do solo, chumbadores, escoramento etc.

### 1.7. Carimbo

`PLANTA DE LOCAÇÃO DAS SAPATAS / PILARES / ARMAÇÃO DAS FUNDAÇÕES / PILARES / PERSPECTIVAS / LISTA DE MATERIAIS`.
Escala: `INDICADO`. Data: `08/04/2024`. Prancha: `EST-01`.

---

## 2. EST-02 — Plano geral da cobertura

### 2.1. Escala e grid

- **Escala principal:** 1:100 (`PLANO GERAL DA COBERTURA`).
- Grid A-G × 1-2 (igual ao da EST-01) com vãos 5000 mm.
- Balões de eixo circulares nos bordos externos.

### 2.2. Elementos representados em planta

- **Treliças** (linha dupla entre eixos): `TRELIÇA 01 - U150x65x4,76` — uma em cada linha de grid perpendicular ao vão (7 treliças no total).
- **Terças** (linhas contínuas longitudinais): `TERÇAS 01 - UE150X60X20X2,00`, `TERÇAS 02 - UE150X60X20X2,00`, ..., `TERÇAS 09 - UE150X60X20X2,00`. **Padrão:** `TERÇAS NN - <perfil>` (plural porque refere ao tipo T01 repetido em todos os vãos), texto paralelo à terça.
- **Contraventamentos em X:** linhas cruzadas em cada vão entre treliças, com legenda:
  `CONTRAVENTAMENTO BARRA 10mm TIP`.
  Usam `L38x38x3,2` (enrijecedores/mão-francesa) + `BARRA REDONDA 10mm`.
- **Linhas de corrente:** linhas transversais entre terças, legenda repetida em cada vão:
  `LINHAS DE CORRENTE`.
- **Chamadas de detalhe:** `VER DET 02`, `VER DET 03` apontando para elementos na EST-03.
- Perímetro externo: calha metálica implícita (trapezoidal).

### 2.3. Cotas

- Cotas de vão entre eixos externos: `5000 | 5000 | 5000 | 5000 | 5000 | 5000` (total 30000 mm).
- Cotas transversais no eixo 1 e 2: 6683 / 8327 mm (igual à EST-01).
- Algumas cotas internas locais para contraventamento (2520, 2480, 2570, 2430).

### 2.4. Perspectivas de rodapé

- `PERSPECTIVA - 01` — galpão completo com fechamento lateral e cobertura.
- `PERSPECTIVA - 02` — galpão só estrutura (sem telha), para ver treliças e terças.

### 2.5. Tabelas laterais (canto inferior direito)

- **TABELA DE PERFIS METÁLICOS GERAL** (Tipo / Comprimento do corte / PESO POR METRO / Peso Total Kg). Lista completa dos perfis da obra inteira. Exemplos desta obra:
  `14x30`, `BARRA REDONDA 10mm`, `L38x38x3,2`, `L100X100X3,0`, `L100X100X6.3`, `u75x38x4,47`, `U75x40x15x1.90`, `U100x50x3.04`, `U150x60x20x2.0`, `U150x65x4.76`, `U200x75x20x2.00`, `UE 300x100x25x3.04`.
- **TABELA DE TELHA METÁLICA** (Tipo / Material / Área).
  Ex.: `PERFIL METÁLICO 186 m²`, `Telha Metálica - Trapezoidal 1356 m²`, total 1542 m².
- **TABELA DE PLACAS** (Contagem / Espessura / Largura / Comprimento / Peso).
  Tabela densa com TODAS as chapas utilizadas no projeto, ordenadas por espessura.
  Exemplos da obra: `439 × 3mm (50×125) = 64.62 kg`, `62 × 3mm (66×133) = 6.84 kg`, ...

### 2.6. Notas gerais - METÁLICA (caixa de texto)

Formato padrão EMT, 10 itens numerados. Cobrem:

1. Dimensões em milímetros (exceto fundações em cm).
2. Eletrodos E60XX / E70XX para chanfrados, eletrodos E60XX para soldas com chapas de base.
3. Soldas em aço carbono: compatibilidade do eletrodo com o material.
4. Fazer pré-montagem em todas peças da estrutura metálica.
5. Ligações:
   - Furos ovais para controle de tolerâncias.
   - Solda sulfica retificadora usar amperagem 180A.
   - Especificação de soldas. Ver detalhes.
6. Perfis laminados tipo cantoneira e chapas planas grossas: aço ASTM A-36 (fy=250 MPa).
7. Perfis em chapa dobrada: tipo ASTM A-36, MR250 (fy = 250 MPa).
8. Chumbadores do tipo galv. de aço ASM (Rosca galvanizada).
9. Normas utilizadas neste projeto:
   - NBR 8800 (2008): projeto e execução de estruturas de aço de edifícios.
   - NBR 14.762 (2010): dimensionamento de estruturas de aço constituídas por perfis formados a frio.
   - NBR 6120 (2019): cargas para cálculo de estruturas de edificações.
   - NBR 6123 (1988): forças devidas ao vento em edificações.
   - NBR 14.631 (2016): ações de segurança nas edificações.
10. Pintura: a especificação de pinturas da estrutura metálica terá sua superfície preparada e aplicada até atingir metal quase branco, a pintura terá:
    - Uma primeira demão de fundo epóxi mástic com 100 micras.
    - Uma segunda demão com fundo epóxi mástic com 100 micras.
    - Uma terceira demão com tinta de acabamento poliuretano alifático 40 micras.
10 Parafusos de alta resistência ASTM A307 galvanizados.

### 2.7. Carimbo

`PLANO GERAL DA COBERTURA / PERSPECTIVAS / LISTA DE MATERIAIS`.
Escala: `INDICADO`. Data: `08/04/2024`. Prancha: `EST-02`.

---

## 3. EST-03 — Detalhamento das treliças

Essa é a **prancha-mãe** do detalhamento — é basicamente o que a função
`Cotar Treliça` + `Detalhar Treliça` precisa gerar automaticamente.

### 3.1. Layout geral da prancha

Quadrante central alto: elevação da treliça principal + elevação da treliça tipo 2.
Quadrante esquerdo alto: balão `ELEVAÇÃO MÃO FRANCESA` (1:25).
Quadrante central: balão `PERSPECTIVA - 03` (ligação intermediária).
Quadrante direito alto: balão `DET. LIGAÇÃO CONTRAVENTAMENTO` (sem escala, isométrico).
Quadrante direito meio: balão `PERSPECTIVA - 05 - TOPO PILAR (16X)`, `CHAPA TOPO PILAR` (1:10).
Quadrante inferior esquerdo: `ELEVAÇÃO FRONTAL / POSTERIOR` (1:50).
Quadrante inferior central: `PERSPECTIVA - 04` (vista isométrica geral).
Quadrante direito inferior: tabelas de perfis + parafusos + notas + carimbo.

### 3.2. ELEVAÇÃO DA TRELIÇA (7X) — escala 1:25

**Treliça principal, 7 unidades iguais em toda a obra.**

- Vão: **15010 mm** pilar a pilar.
- Duas águas simétricas com cumeeira central.
- Banzo superior + inferior: `U150x65x4.76` (perfil U laminado).
- Diagonais e montantes: cantoneiras (tamanho varia — ver perfis na elevação).
- Terças apoiadas no banzo superior a cada painel, numeradas T8, T9, T10, T11, T12...

**Cotagem em 5 faixas** (igual Samsung + Igreja Patos, mas escala 1:25 porque é treliça menor):

1. Painéis banzo superior: `1130 | 815 | 815 | 1081 | 1103 | 820 | 815 | ...` (mm, nó a nó ao longo do banzo superior).
2. Painéis banzo inferior: cotas na linha inferior.
3. Vão total: `15010` (cota grande externa).
4. Posição das terças (`T8, T10, T12, T11, T9`) anotada no banzo superior.
5. Altura das montantes: cotas verticais nos montantes (valores menores porque vão é menor).

**Identificação de perfis** — cada membro recebe tag com perfil:

- `U150x65x4.76` no banzo.
- `L38x38x3,2 2` (o sufixo `2` indica cantoneira dupla — equivalente ao `2x` do Samsung).
- Perfil escrito paralelo ao membro, meio do elemento.

**Balões apontando para:**

- `VER DET 02` e `VER DET 03` nos banzos (ligações parafusadas).
- Detalhe da mão francesa na ponta.

### 3.3. TRELIÇA TIPO 2 (12X) — escala 1:25

**Treliça secundária, 12 unidades.**

- Vão menor (5000 mm), treliça paralela plana (não duas águas).
- Mesmo padrão de cotagem em 5 faixas.
- Mesmos tipos de tag.

### 3.4. ELEVAÇÃO MÃO FRANCESA — escala 1:25

Balão à esquerda mostrando o nó de apoio da treliça no topo do pilar.
- Perfil: `1033 | 1033` (cotas do painel).
- Perfusos 10mm A307 + cantoneira.

### 3.5. PERSPECTIVA - 03 (ligação intermediária)

- Ilustração isométrica 3D colorida da ligação de um nó intermediário do banzo.
- Componentes: `CHAPA 3 mm` (gusset), `CHAPA 3 mm` (reforço).
- Sem cotas — só identificação por texto colorido.

### 3.6. DET. LIGAÇÃO CONTRAVENTAMENTO

Isométrico 3D colorido mostrando como o contraventamento se liga ao banzo:

- `L101.6X101.6 X6.30` (cantoneira de ligação, 3 unidades no ponto).
- `BARRA REDONDA 10mm`.
- `RUELA + PORCA E CONTRA PORCA`.
- `CHAPA 4.7mm` (gusset).
- `ENRIJECEDOR = 3mm`.
- `3 E60XX` (solda 3 mm, eletrodo E60XX).
- Nota: `LIGAÇÃO DO CONTRAVENTAMENTO CHAPA #4.8 - EXECUTAR O MAIS PRÓXIMO DA TERÇA`.

### 3.7. PERSPECTIVA - 05 - TOPO PILAR (16X)

Isométrico do topo de um pilar metálico com placa de cabeça e enrijecedores,
mostrando onde a treliça apoia.

### 3.8. CHAPA TOPO PILAR — escala 1:10

Vista ortogonal da chapa de topo de pilar:

- Dimensões: `220 × 320` mm.
- Espessura: `10 mm` (nas 4 bordas).
- Enrijecedor interno: `CHAPA 16 mm`.
- Nota lateral: `ENRIJECEDOR CHAPA 16 mm` e `CHAPA 3.2mm` (complementares).

### 3.9. ELEVAÇÃO FRONTAL / POSTERIOR — escala 1:50

Vista frontal do galpão mostrando:

- Pilares metálicos com placa de base no concreto.
- Treliça apoiada no topo.
- Cotas externas: `8324` + `6686` = vão total (14010 + margem).
- Altura total: `5077` mm do nível da base até o topo do banzo inferior na cumeeira.
- Platibanda implícita na borda superior.

### 3.10. PERSPECTIVA - 04 (rodapé)

Vista isométrica geral do galpão com todas as treliças montadas em cor verde,
pilares em verde escuro, sapatas de concreto em cinza.

### 3.11. Tabelas laterais (direita)

- `TABELA DE PERFIS METÁLICOS GERAL` — mesma da EST-02, mas com pesos preenchidos
  (EST-02 tinha colunas em branco para alguns perfis; EST-03 consolida).
- `TABELA DE PARAFUSOS` (Diâmetro / Número). Ex.: `12.00 mm → 136`, `+ 336 = 472` total.
- `NOTAS GERAIS - METÁLICA` — cópia do mesmo texto da EST-02.

### 3.12. Carimbo

`DETALHAMENTO DAS TRELIÇAS / ELEVAÇÕES / PERSPECTIVAS / LISTA DE MATERIAIS`.
Escala: `INDICADO`. Data: `08/04/2024`. Prancha: `EST-03`.

---

## 4. Convenções consolidadas (o que o plugin deve reproduzir)

### 4.1. Nomenclatura e prefixos (padrão completo EMT)

| Elemento | Prefixo/Padrão | Exemplo |
|---|---|---|
| Pilar (metálico) | `P` + número + perfil | `P11 2U300x100x25x3.00` |
| Sapata | `S` + número | `S11` |
| Viga baldrame | `V` + número | `V12` |
| Treliça principal | `TRELIÇA 01 - <perfil>` | `TRELIÇA 01 - U150x65x4,76` |
| Treliça secundária | `TRELIÇA TIPO 2` ou `TR02` | |
| Terça | `TERÇAS NN - <perfil>` (plural) | `TERÇAS 01 - UE150X60X20X2,00` |
| Contraventamento | `CONTRAVENTAMENTO BARRA <Ø>mm TIP` | `CONTRAVENTAMENTO BARRA 10mm TIP` |
| Linha de corrente | `LINHAS DE CORRENTE` | |
| Cantoneira dupla | `<perfil> 2` (sufixo 2) ou `2x <perfil>` | `L38x38x3,2 2` |
| Chumbador | `Ø<d> mm, ISO 898.C4.6` | `Ø20 mm, ISO 898.C4.6` |

### 4.2. Escalas padrão EMT por tipo de desenho

| Desenho | Escala |
|---|---|
| Planta de locação de fundações | 1:50 |
| Detalhamento de fundação (corte) | 1:50 |
| Detalhe de viga baldrame | 1:50 |
| Plano geral de cobertura | 1:100 |
| Elevação de treliça pequena (≤ 20m) | 1:25 |
| Elevação de treliça grande (> 20m) | 1:50 |
| Elevação frontal/posterior do galpão | 1:50 |
| Detalhe de ligação (chapa topo pilar) | 1:10 |
| Detalhe de ligação em balão isométrico | sem escala / `{3D}` |

### 4.3. Padrão de 5 faixas de cotas na elevação de treliça

Confirmado em **3 projetos de referência agora** (Igreja Patos, Samsung, Galpão padrão EMT):

1. Painéis banzo superior (acima do banzo superior).
2. Painéis banzo inferior (abaixo do banzo inferior).
3. Cota de vão total (externa).
4. Cotas parciais entre apoios / cumeeira.
5. Altura de cada montante (texto vertical no eixo da montante).

### 4.4. Tabelas-padrão que devem acompanhar cada prancha

- **EST-01 (fundação):** aço sapatas + aço pilares + aço vigas baldrame + âncoras + formas/concreto + notas gerais CONCRETO.
- **EST-02 (cobertura):** perfis metálicos geral + telha metálica + placas + notas gerais METÁLICA + 2 perspectivas 3D.
- **EST-03 (detalhamento):** perfis metálicos geral (completo com pesos) + parafusos + notas gerais METÁLICA + perspectiva isométrica geral.

### 4.5. Sequência EMT mínima de entrega (3 pranchas)

- **EST-01:** Locação das sapatas + armação + perspectivas + materiais.
- **EST-02:** Plano geral da cobertura + perspectivas + materiais.
- **EST-03:** Detalhamento das treliças + elevações + perspectivas + materiais.

Se o plugin conseguir gerar essas 3 pranchas (**botão único "Gerar Projeto Completo"**),
o escritório tem a entrega mínima pronta automaticamente.

---

## 5. Como usar este NOTAS.md

Antes de implementar/ajustar funções relacionadas a:

- Locação de fundações, sapatas, chumbadores, viga baldrame → seção 1.
- Plano geral de cobertura (planta com treliças/terças/contraventamento/LC + tabelas + perspectivas) → seção 2.
- Elevação e detalhamento de treliça (cotagem 5 faixas + tags + mão francesa + topo pilar) → seção 3.
- Padronização de tabelas (perfis, telhas, placas, parafusos, âncoras, concreto) → seção 4.4.

Cruzar com:

- `docs/reference-projects/igreja-patos/NOTAS.md` — treliça plana simples, cotagem no banzo superior.
- `docs/reference-projects/cobertura-samsung/NOTAS.md` — treliça grande de duas águas (38367 mm), 5 faixas de cotas detalhadas, balões de detalhe + EST-08 (platibandas).
- `docs/reference-projects/ampliacao-vulcaflex/NOTAS.md` — pilar, planta locação, croquis individuais de terça/LC/chapa, tabelas-mestre.
