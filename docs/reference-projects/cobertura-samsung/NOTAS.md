# Projeto de referência — Cobertura Samsung

**Responsável técnico:** Eng. Alef Christian Gomes Vieira (EMT).
**Tipo de obra:** cobertura metálica em treliça de duas águas sobre pilares (galpão),
com platibanda perimetral, mãos francesas, agulhas centrais e ligações parafusadas.
**Pranchas arquivadas aqui:**

- `EM-01-plano-geral-cobertura.pdf` — Plano geral da cobertura (planta + 3 perspectivas + tabelas).
- `EM-02-elevacoes-trelicas.pdf` — Elevações das treliças TR01/TR02 e cortes dos sub-elementos.
- `EST-08-elevacao-trelicas-platibanda.pdf` — Prancha 08/14 do executivo completo Samsung (CRC LTDA / Resp. Ricardo Corrêa, CREA-AM 18926). Apresenta **6 tipos de treliça** (TR-01 a TR-06) com quantidades (6x, 6x, 6x, 2x, 2x, 1x) e **4 quadros de platibanda** (QUADRO PLATIBANDA 01 a 04), todos em 1:50 / 1:75. Serve como referência de nomenclatura `TR-NN (Nx)` e de detalhamento de platibanda treliçada.

Serve como referência para as funções do FerramentaEMT que precisem gerar:

- plano geral / planta de cobertura com treliças, terças, contraventamento e LC,
- elevação de treliça de duas águas (com banzos, diagonais, montantes, mãos francesas, agulha central),
- detalhes de ligação em balão (chapas de emenda, ligação de terça, ligação em pilar).

---

## 1. Plano geral da cobertura (EM-01)

### 1.1. Escala e layout

- **Escala principal:** 1:100 (PLANO GERAL DA COBERTURA).
- **Orientação:** grid com eixos alfabéticos verticais (L, M, N, O, P, Q, R) e numéricos horizontais (1, 2, 3, 4).
- **Malha de pilares típica:** espaçamento **5800 mm** entre a maioria dos eixos, com um vão final de **4464 mm** no bordo.
- **Balões de eixo:** círculos com letra/número nas extremidades de cada linha de grid (padrão ABNT/EMT).
- **Setas de norte / sul nos cantos:** pequenos marcadores triangulares apontando a orientação em cada canto da prancha.

### 1.2. O que aparece representado na planta

- **Treliças** (vistas em planta como linha dupla/contorno do banzo) identificadas uma a uma:
  `TRELIÇA 02 - U150x50x3.04` repetida a cada linha de grid perpendicular ao vão.
- **Terças** (linhas horizontais contínuas entre as treliças), numeradas e com perfil ao lado:
  `TERÇA 26 - UE 200x75x25x3,0`, `TERÇA 27 - UE 200x75x25x3,0`, ..., `TERÇA 32 - UE 200x75x25x3,0`.
  **Convenção:** numeração corrida (T01…Tn), texto na forma `TERÇA NN - <perfil>`, acompanhando a linha da terça.
- **Contraventamento em X** (barras cruzadas entre vãos de treliça), com legenda:
  `CONTRAVENTAMENTO BARRA 10mm TIP`.
- **Linhas de corrente** (entre terças, barras redondas transversais), com legenda:
  `LINHAS DE CORRENTE` + `BARRA REDONDA 10mm`.
- **Calha metálica** perimetral (contorno externo da cobertura), com legenda `CALHA METÁLICA`.
- **Chamadas de detalhe** tipo balão (`VER DET. 01`, `17`, `06`) apontando para detalhes na prancha de elevação.

### 1.3. Cotagem da planta

- Cotas de vão **entre eixos de pilar**, em mm, colocadas **externamente** ao perímetro (lados direito e esquerdo).
  Exemplo lido nesta planta: `5800 | 5800 | 5800 | 5800 | 4464`.
- Não há cotas internas redundantes — a malha de eixos já define posição das terças.

### 1.4. Perspectivas de rodapé (sempre na prancha EM-01)

Na parte inferior da prancha aparecem **três perspectivas isométricas** da cobertura inteira, em cores:

- `PERSPECTIVA - 01` — vista geral do conjunto.
- `PERSPECTIVA - 02` — vista por ângulo diferente (ex.: mostrando terças/LC em azul).
- `PERSPECTIVA - 03` — vista com as treliças em destaque e pilares.

Essas perspectivas são renderizações 3D do modelo (sem cotas), servindo de referência visual para quem monta.

### 1.5. Conteúdo complementar da EM-01 (lateral direita)

A EM-01 concentra boa parte das **informações normativas e tabelas gerais** da obra:

- **Ligações soldadas em estrutura metálica** (caixa de texto lateral):
  - Normas citadas: **ABNT NBR 8800:2008** (projeto), **AWS A2.4 / BS EN 22553** (simbologia de solda).
  - Materiais: perfis definidos pela ABNT NBR 8800 → A-36 / A-572; chapas conforme A-36.
  - Método de representação das soldas: "Conforme a figura 2 da NBR 8800 item 4.8 e os tipos de solda utilizados...".
- **Tabela "Soldas"** (gabarito de representação):
  - Colunas: *Designação / Ilustração / Símbolo*.
  - Itens típicos: solda de topo, solda de topo em V simples (com chanfro), solda de topo simples com chanfro duplo, solda combinada de topo em bisel simples com ângulo, solda de topo em bisel simples com todo contorno, etc.
- **Tabela de "Placas de base"** (material):
  - Material A-36 (25 MPa) e parafusos ISO 898 H10 (base).
  - Colunas: *Placa base / Tipo / Quantidades / Dimensões (mm) / Peso (kg)*.
- **TABELA DE PERFIS METÁLICOS GERAL** (caixa vermelha à direita):
  - Colunas: **Tipo | Comprimento do corte | Peso por metro | Peso total kg**.
  - Entradas típicas desta obra (ordem real da tabela):
    `BARRA REDONDA 10mm`, `LS0X50X4.76`, `L 1 1/2 x 1/8"`, `L 1 1/4 x 1/8"`, `L 1 3/4 x 3/16`,
    `L 1 x 1 x 1/8"`, `L 2 1/2 X 1/4"`, `L 2 1/2 X 3/16`, `L 2 x 1/8"`, `L 2 x 3/16" 2`,
    `U150x50x3.04`, `U150x60x20x2.0`, `UE 150x60x20x2.28`, `UE 200x75x25x3,0`, `W200X26.6`.
  - Totais no rodapé da tabela (linha `Total`).

### 1.6. Carimbo (canto inferior direito)

Formato EMT: retângulo com barra vermelha "ESTRUTURA METÁLICA" no topo, seguido dos campos
ENDEREÇO / CLIENTE / RESPONSÁVEL TÉCNICO (`ALEF CHRISTIAN GOMES VIEIRA`) / DESENHISTA /
número da prancha / data / revisão / e-mail (`engenheiroalefvieira@gmail.com`).
**Mesmo carimbo usado em Vulcaflex e Igreja-Patos** — padrão do escritório.

---

## 2. Elevações de treliça (EM-02)

### 2.1. Treliça principal (TR01 / 1:50) — duas águas simétricas

Essa é a **peça central da prancha** e mostra o padrão EMT completo de cotagem/identificação
que o FerramentaEMT precisa reproduzir automaticamente.

**Geometria lida:**

- Vão total: **38367 mm** (eixo a eixo de pilar).
- Subdivisão de apoio: **11056 + 13656 + 13656 = 38368** (duas metades + ponta, confere com 38367).
- Inclinação de duas águas simétricas com cumeeira central (altura cresce do apoio até o meio).
- **Banzo superior:** `W200X26.6`, contínuo.
- **Banzo inferior:** `W200X26.6`, contínuo.
- **Diagonais / montantes em cantoneira dupla** (indicadas com `2x` ao lado do perfil):
  - `L 1 3/4 x 3/16` — diagonal principal repetida ao longo de toda a treliça.
  - `L 1 1/4 x 1/8"` — diagonal secundária.
  - `L 2 1/2 X 1/4"` — montantes próximos aos apoios.
  - `L 2 1/2 x 3/16"` — montantes intermediários.
  - `L 2 x 3/16" 2` — montantes de extremidade/apoio.
  - Prefixo `2x` = par de cantoneiras costas a costas (perfil composto).

### 2.2. Padrão de cotagem da treliça (PADRÃO EMT — APLICAR EM `Cotar Treliça`)

A prancha mostra **cinco linhas de cotas sobrepostas** à elevação, cada uma num nível,
e é exatamente esse o padrão que a função `Cotar Treliça` deve gerar automaticamente:

1. **Cotas dos painéis do banzo SUPERIOR** — colocadas *acima* do banzo superior.
   Uma cota por painel (nó a nó), em mm. Exemplo da prancha:
   `1474 | 1500 | 1476 | 1464 | 1353 | 1353 | 1464 | 1476 | 1488 | 1486 | 1502 | 1495 | 1490 | 1486 | 1493 | 1475 | 1497 | 1471 | 1481 | 1463 | 1489 | 1489 | 778 | 1241`.

2. **Cotas dos painéis do banzo INFERIOR** — colocadas *abaixo* do banzo inferior.
   Uma cota por painel (nó a nó):
   `1374 | 1468 | 1497 | 1481 | 1471 | 1497 | 1475 | 1463 | 2971 | 2962 | 2962 | 2970 | 2971 | 1481 | 1471 | 1497 | 1475 | 1463`.

3. **Cota longa do banzo superior total** (entre apoios), uma só cota acima das anteriores, em mm:
   `13655 | 11056 | 13656` e, acima de tudo, **vão total externo** (extensão linear entre
   projeções dos apoios) — `7248` e `7257` em cada metade e `38367` ao longo do rodapé.

4. **Cota longa do banzo inferior** também com a soma dos painéis (confirmação redundante).

5. **Cotas de altura das montantes** — texto vertical no eixo de cada montante, com o valor
   da *altura da montante naquela posição*. Lidos na prancha (metade da treliça):
   `3432 | 991 | 1955 | 1145 | 1228 | 1686 | 1299 | 1850 | 1288 | 3753 | 2315 | 2020 | 1834 |
   1729 | 2131 | 1583 | 2020 | 1438 | 1291 | 1804 | 1760`.
   Essas cotas crescem do apoio (menor) até próximo à cumeeira (maior) e depois decrescem simetricamente.

**Regras práticas:**

- Todas as cotas em **milímetros** (inteiros, sem casa decimal).
- Sem cotagem de ângulos (como no Igreja-Patos).
- Sem cotagem individual de cada diagonal (apenas painéis de banzo + altura de montante).
- Sobreposição ordenada de pelo menos 2 faixas em cima e 2 em baixo (para não cruzar números).

### 2.3. Identificação de perfis (tags)

Cada membro da treliça recebe **uma etiqueta textual com o perfil**, posicionada:

- **Banzos:** texto paralelo ao membro, próximo à linha do banzo (ex.: `BANZO SUPERIOR W200X26.6`, `BANZO INFERIOR W200X26.6`).
- **Diagonais/montantes:** texto curto no meio do elemento, paralelo ao elemento, com o perfil (`L 1 3/4 x 3/16`).
- **Quantidade composta:** `2x` à esquerda do perfil quando for par de cantoneiras.
- **Sem prefixos "TR01-"** nos membros individuais (o nome da treliça vai só no título da vista/corte).

### 2.4. Sub-vistas e detalhes ao redor da elevação principal

Ao redor da treliça principal, são espalhados **detalhes em balão** (círculos pontilhados)
ligados por linha de chamada até o ponto correspondente da treliça. Cada detalhe tem
seu próprio **título com "PERSPECTIVA - XXX"** e pode ser uma vista isométrica pequena:

Os balões presentes nesta prancha (replicáveis na função futura `Detalhar Treliça`):

- `PERSPECTIVA - PLATIBANDA` — detalhe da platibanda.
- `PERSPECTIVA - LIGAÇÃO BANZO SUPERIOR - TR01` — ligação parafusada na emenda do banzo superior.
- `PERSPECTIVA - LIG. TERÇA - TR01` — fixação de terça sobre o banzo superior (chapa + 2 parafusos).
- `PERSPECTIVA Montante Central - TR01` — detalhe da montante central (cumeeira).
- `PERSPECTIVA 05 - LIG PILAR C.A 01` — apoio da treliça sobre pilar de concreto.
- `PERSPECTIVA - LIGAÇÃO BANZO INFERIOR - TR01` — ligação parafusada na emenda do banzo inferior.
- `PERSPECTIVA - AGULHA CENTRAL` — vista isolada das agulhas centrais (contraventamento superior).
- `PERSPECTIVA = MÃO FRANCESA - TRELIÇA 01` — mão francesa entre pilar e banzo inferior.
- `PERSPECTIVA - LIG TERÇA TR02` / `PERSPECTIVA - MÃO FRANCESA TR02` / `PERSPECTIVA 05 - LIG PILAR C.A` / `PERSPECTIVA - TRELIÇA 02` — equivalentes para a treliça menor (TR02).
- `PERSPECTIVA - 04` — panorama isométrico 3D do conjunto completo (canto direito da prancha).

**Padrão de numeração de chapas/parafusos dentro dos balões:**

- Chapa: `CHAPA 5mm` (espessura em mm).
- Parafusos: `4x ASTM A307 12mm` (quantidade × norma × diâmetro em mm).
- Barra: `BARRA REDONDA 10mm`.

### 2.5. Cortes secundários na mesma prancha

Abaixo da elevação principal, a prancha traz uma série de **cortes longitudinais 1:100** de
elementos auxiliares (usados como referência cruzada com o plano geral):

- `Corte 1 - TRELIÇA 01 (1:50)` — a elevação principal descrita acima.
- `Corte 2 - TRELIÇA 02 (1:50)` — treliça secundária menor (mesmo padrão de cotagem).
- `Corte 3 - MÃO FRANCESA TR01 (1:100)` — desenvolvimento da mão francesa em planta (painéis
  cotados `1463 | 1544 | 1458 | 1463 | 2880 | 1458 | 2880 | 1458 | 2880 | 1454 | 2889 | 1458 | 2856 | 1463`).
- `Corte 4 - MÃO FRANCESA TR01 (1:100)` — vista complementar da mesma mão francesa.
- `Corte 5 - MÃO FRANCESA TR02 (1:100)` — mão francesa da treliça menor.
- `Corte 39 - AGULHAS CENTRAIS (1:100)` — agulhas centrais de contraventamento superior,
  painéis `1450 | 1580 | 1434 | 1446 | 2920 | 1434 | 1455 | 2920 | 1425 | 2902 | 1443 | 1455 | 2920 | 1425`
  e entre-painéis `4248 | 216 | 5584 | 216 | 5584 | 216 | 5584 | 216 | 5584 | 216 | 5584`.

**Convenção de escalas nesta prancha:**

- Elevação principal da treliça: **1:50**.
- Cortes longitudinais auxiliares (mão francesa, agulhas): **1:100**.
- Balões de detalhe de ligação: **isométricos 3D, sem escala** (só identificação).

### 2.6. Tabelas laterais (canto inferior direito)

Três tabelas empilhadas:

- **TABELA DE PERFIS METÁLICOS GERAL** — cópia da mesma tabela da EM-01
  (Tipo / Comprimento do corte / Peso por metro / Peso total kg).
- **TABELA DE PLACAS - RESUMO** — Contagem / Espessura / Peso.
  Exemplo desta obra: `233 × 3mm = 24.80 kg`, `992 × 5mm = 717.62 kg`, `10 × 10mm`,
  `77 × 18mm`, `7 × 19mm`, `15 × 20mm`, **total 1755.28 kg**.
- **TABELA DE PARAFUSOS** — Diâmetro / Número.
  Exemplo desta obra: `12.00 mm → 1131`, `16.00 mm → 699`, **total 1830**.

---

## 2B. EST-08 — Elevação das treliças e platibandas (prancha 08/14)

Mesma obra Samsung, nível detalhado complementar. Arquivada aqui:
`EST-08-elevacao-trelicas-platibanda.pdf`.

### 2B.1. Catálogo de treliças

A prancha consolida **6 tipos de treliça** da obra, cada um com a quantidade
anotada no título do corte:

- `TR - 01 (6x)` em 1:50 — **treliça principal duas águas**, vão 38367 mm,
  banzo W200X26.6, diagonais e montantes em cantoneira dupla `2x`. Igual à EM-02.
- `TR 02 (6x)` em 1:50 — treliça intermediária menor, inclinada.
- `TR03 (6x)` em 1:50 — treliça paralela (sem duas águas), banzos `U150x50x3.04` / `UE150x60x20x2.28`.
- `TR - 04 (2x)` em 1:50 — treliça com vão 6193 mm, banzos `U150x50x3.04 + UE150x60x20x2.28`, diagonais `2x L 2 1/2 X 1/4"`.
- `TR - 05 (2x)` em 1:50 — treliça com vão 6193 mm, similar à TR-04.
- `TR - 6 (1x)` em 1:?? — treliça com painéis 1239 / 3020 mm.

**Padrão de nomenclatura:** `TR - NN (Qx)` onde `NN` = número sequencial e `Qx` = quantidade da obra toda (essa anotação é única nesta prancha e deve ser reproduzida no título da vista quando o plugin gerar um catálogo de treliças).

### 2B.2. Quadros de platibanda

A prancha traz **4 quadros de platibanda** em 1:75, que são estruturas treliçadas
horizontais (vigas-treliça) que fecham a platibanda no entorno da cobertura:

- `QUADRO PLATIBANDA 01 (1x)` em 1:75 — perfil treliçado triangulado com painéis
  `1490 | 1490 | 1490 | 1490 | ... | 2843 | 2843 | 3092 | ... | 1066 | 666`.
- `QUADRO PLATIBANDA 02 (1x)` em 1:75 — banda mais longa, painéis 1280 mm típicos.
- `QUADRO PLATIBANDA 03` em 1:75 — muito longa, vãos 9000 mm entre apoios,
  painéis internos 2197 mm repetidos.
- `QUADRO PLATIBANDA 04` em 1:75 — trecho de platibanda com variação de altura.

**Componentes típicos das platibandas:**

- Banzo superior e inferior em perfil U ou UE.
- Diagonais em cantoneira simples ou dupla.
- Montantes verticais equidistantes (1280 mm típico).

**Padrão de identificação das terças na platibanda:**
Dentro dos quadros de platibanda, as terças aparecem como montantes curtos e
recebem tag `T15, T16, T17, ... T29` ao longo do banzo superior,
confirmando que cada terça da cobertura tem um identificador único mesmo quando
em contexto de vista de platibanda.

### 2B.3. Tag global de terças

Ao lado das elevações, uma nota coletiva:
`TERÇAS T1 A T29 = UE 200X75X25X3,0`.

**Convenção:** quando todas as terças da obra são do mesmo perfil, colocar uma
**nota-resumo** indicando o intervalo completo (`T1 A Tnn = <perfil>`) em vez de
anotar cada terça individualmente.

### 2B.4. Observações do carimbo (para contexto)

- **Obra:** `SEDA-HEAT-SAMSUNG`.
- **Etapa:** `EXECUTIVO`.
- **Conteúdo:** `ELEVAÇÃO DAS TRELIÇAS E PLATIBANDAS`.
- **Responsável técnico:** Ricardo Corrêa (CREA-AM 18926), escritório `CRC LTDA`.
- Não é prancha do Alef/EMT, mas é usada como **referência externa de qualidade** —
  confirma que o padrão de cotagem em 5 faixas e os balões de detalhe em
  perspectiva são convenção de mercado, não particular do escritório.

### 2B.5. Padrão "catálogo de treliças" numa prancha só

**Layout replicável pelo plugin:**

Se o usuário tiver N tipos de treliça no modelo, gerar **uma prancha de catálogo**
onde cada tipo aparece uma vez, lado a lado, com:

- Título do corte: `TR - NN (Qx)` onde Q = quantidade de instâncias dessa treliça no modelo.
- Cota total do vão na base de cada corte.
- Cotagem de 5 faixas padrão.
- Tags de perfil em cada membro.
- Tabela geral de perfis metálicos consolidada no canto inferior direito.

Isso economiza pranchas quando a obra tem muitas treliças iguais.

---

## 3. Convenções consolidadas que o FerramentaEMT deve reproduzir

### 3.1. Plano geral de cobertura (auto-gerar a partir do modelo Revit)

- Plantas em **1:100** com grid alfanumérico.
- Terças numeradas (`T26 … T32` neste caso), texto `TERÇA NN - <perfil>` ao lado da linha.
- Treliças identificadas uma a uma (`TRELIÇA 02 - U150x50x3.04`).
- Contraventamentos marcados com **linha cruzada + legenda** `CONTRAVENTAMENTO BARRA 10mm TIP`.
- Linhas de corrente marcadas com **linha transversal + legenda** `LINHAS DE CORRENTE` + `BARRA REDONDA 10mm`.
- Calha perimetral contornada com legenda `CALHA METÁLICA`.
- Cotas de vão entre eixos (mm, fora do perímetro).
- Três perspectivas 3D do modelo no rodapé.

### 3.2. Elevação de treliça (auto-gerar a partir de um assembly/grupo de treliça)

Quando o usuário rodar `Cotar Treliça` selecionando uma treliça, a ferramenta deve gerar:

1. Elevação 1:50 da treliça com **banzos, diagonais e montantes** visíveis.
2. **Cota de painel** em mm em cada um dos banzos (superior e inferior), em linhas separadas.
3. **Cota total** do vão (soma das panéis).
4. **Cota de altura de montante** em cada montante (texto vertical no eixo da montante).
5. **Tag de perfil** em cada membro (paralela ao membro, com prefixo `2x` se perfil composto).
6. **Títulos de banzo** distintos (`BANZO SUPERIOR <perfil>`, `BANZO INFERIOR <perfil>`).
7. Balões de detalhe em posições-chave: emenda de banzo, ligação de terça, apoio no pilar,
   mão francesa e montante central (opcionalmente gerados como views isométricas separadas).

### 3.3. Nomenclatura de perfis (padrão EMT nesta obra, a aceitar/gerar pelos helpers)

- Cantoneira (L) simples: `L 1 3/4 x 3/16` (fracionada em polegadas).
- Cantoneira dupla: `2x` prefixado + perfil L (ex.: `2x L 1 3/4 x 3/16`) — renderizado na tag como `L 1 3/4 x 3/16` com marca `2x` à esquerda.
- U formado a frio sem enrijecedor: `U150x50x3.04`.
- U formado a frio com enrijecedor: `UE 150x60x20x2.28`, `UE 200x75x25x3,0`.
- W laminado: `W200X26.6`.
- Barra redonda: `BARRA REDONDA 10mm` ou `BARRA REDONDA Ø10mm`.

### 3.4. Parafusos e soldas (padrão nos balões de detalhe)

- Texto de parafusagem: `<quantidade>x ASTM A307 <diâmetro>mm` (ou `A325M`).
- Texto de chapa: `CHAPA <espessura>mm`.
- Representação de solda conforme NBR 8800 item 4.8 + AWS A2.4 / BS EN 22553.

---

## 4. Como usar este NOTAS.md ao escrever código novo

Antes de implementar ou alterar qualquer função relacionada a:

- **plano geral de cobertura** (gerar planta com treliças/terças/contraventamentos/LC anotados),
- **elevação de treliça** (cortes 1:50 com cotagem de painéis + altura de montantes + tags),
- **detalhes de ligação em balão** (chapa, parafuso, mão francesa, agulha central, ligação em pilar),

abrir os PDFs desta pasta e seguir o padrão descrito acima.
Cruzar com:

- `docs/reference-projects/igreja-patos/NOTAS.md` (treliça plana simples com cotagem no banzo superior)
- `docs/reference-projects/ampliacao-vulcaflex/NOTAS.md` (pilar, planta de locação, terças, LC, chapas)

para manter a mesma família visual em todos os desenhos que o plugin gerar.
