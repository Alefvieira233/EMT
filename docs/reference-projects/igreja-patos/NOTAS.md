# Igreja Patos — Reforma/Ampliação da Diocese de Patos

Prancha fonte: `EST-05-trelicas.pdf` (EST - 05, data 03/06/2024, escala indicada)
Autor: ALEF CHRISTIAN GOMES VIEIRA (CREA 0319918963)
Cliente: Diocese de Patos (CNPJ 09.084.385/0026-45)
Endereço: Paróquia de São Pedro, Rua Manoel Meira 296, Jabobá, Patos-PB

## O que esta prancha ensina sobre o padrão EMT

### Cotagem de treliça (referência para função "Cotar Treliça")

- **Cotas principais correm junto ao banzo superior**, acompanhando a inclinação da água da cobertura. Não há uma linha de cota horizontal única em cima — a cota segue a geometria do banzo.
- **Uma cota por painel** (distância de nó a nó ao longo do banzo superior).
- **Cota total do vão** aparece separadamente, geralmente na linha do banzo inferior ou abaixo do apoio.
- **Não cota explicitamente cada diagonal/montante** — o comprimento sai da geometria por consequência das cotas do banzo. Essa foi uma decisão de clareza gráfica.
- **Não cota ângulos** das diagonais.
- Cotas em **milímetros** (padrão ABNT para estrutura metálica).
- Escala de trabalho **1:50** para elevações de treliça.

### Identificação de perfis (tagging inline)

Os perfis aparecem escritos direto ao lado do elemento, com convenções:

- **Banzos**: perfil duplo em cantoneira, notação `2L 2 1/2 x 1/4 x 3/16` (duas cantoneiras costas-com-costas, dimensões em polegadas com espessura da chapa que as separa).
- **Diagonais e montantes**: cantoneira simples, notação `L 1 1/4 x 1/8`, `L 1 x 1 x 1/8`, `L 2 1/2 x 3/16`.
- **Terças e correntes**: `TERÇAS T1 à T2H - UE 200x75x20x2.25` (perfil U enrijecido formado a frio).
- **Corrente**: `CORRENTE T1 1/2 x 1/8`.
- **Pilares**: `W150x18`, `W200x19.3`, `W200x26.6` (perfis laminados I).
- **Travamentos**: `UE 200x75x20x2.25`.
- **Barra redonda**: `BARRA REDONDA 10mm`.
- Solda indicada com símbolo **EXX 70** (eletrodo AWS E70XX).

### Referência a detalhes de conexão

- Balões circulares numerados: `DET. 06`, `DET. 07`, etc.
- Texto inline do tipo `VER DET 03`, `VER DET 06` apontando para o detalhe de conexão no canto da prancha.
- Detalhes tridimensionais coloridos (renders) acompanham cada balão — não são apenas vistas 2D. Facilita o montador na obra.

### Estrutura da prancha (layout)

- **Elevações das treliças** em escala 1:50, cada uma com título `Corte N - TRELIÇA NN (Xx)` indicando quantas unidades iguais existem no projeto.
  - Ex: `Corte 1 - TRELIÇA 01 (8x)`, `Corte 3 - TRELIÇA 04 (2x)`.
- **Detalhes de chapas** em escala 1:10 ou 1:25 (`DET. 06`, `DET. CHAPAS TRELIÇA 01/02`, `Corte 6 - MÃO FRANCESA`).
- **Perspectivas coloridas 3D** destacando as peças (cores diferentes para banzo, diagonais, terças).
- **Fotos da obra** (IMAGEM 01, 02, 03) quando já executada — útil como prova de cabimento do projeto.
- **Detalhes de base de pilar** (Tipo 1 e Tipo 2) com planta, corte e vista lateral.
- **Tabelas** no canto inferior:
  - Tabela de parafusos (diâmetro × número total) — ex: 12.0mm: 1152, 16.0mm: 352, total 1504.
  - Tabela de perfis metálicos geral (tipo × comprimento de corte × peso/m × peso total).
- **Notas gerais** listadas à esquerda do carimbo:
  - Dimensões em mm salvo indicação contrária.
  - Eletrodo E7018 para estruturais, E6013 para chapa fina.
  - Solda altura do filete igual à espessura da chapa.
  - Perfis laminados para banzo em aço ASTM A36, Fy 250 MPa.
  - Perfis formados a frio ASTM A572, 290 MPa (terças e banzos de UE).
  - Chumbadores tipo CAL (rosca galvanizada).
  - Normas: NBR 8800 (2008), NBR 14762 (2001), NBR 6120 (2019), NBR 6123 (1988), NBR 8681 (2003).
  - Pintura: fundo EPOXI 100 micra + esmalte 100 micra + poliuretano alifático 40 micra.
  - Parafusos de alta resistência ASTM A307 galvanizados.

### Quantidades do projeto (ordem de grandeza)

- **Peso total de aço:** 20.305,56 kg (vinte toneladas).
- **Comprimento total de material:** 4.508,62 metros lineares.
- **Parafusos:** 1.504 unidades.
- **Mix de perfis usados:**
  - Cantoneiras: L25x25x3.0, L 1 1/2 x 1/8, L 1 1/4 x 1/4, L 1 1/4 x 1/8, L 1 3/4 x 3/16, L 1 x 1 x 1/8, L 2 1/2 x 3/16.
  - Perfis U enrijecidos: U100x50x1x22.28, U100x50x3x42, U150x50x3.42.
  - Perfil UE 200x75x20x2.25.
  - Laminados: W150x18, W200x19.3, W200x26.6.
  - Barra redonda 10mm.

## Como usar isto no código

Quando escrever a função **Cotar Treliça**, seguir:

1. Selecionar vigas estruturais que compõem a treliça (via Assembly, grupo, ou seleção manual).
2. Classificar cada barra como banzo superior / banzo inferior / diagonal / montante (por inclinação relativa).
3. Projetar cotas alinhadas **sobre o banzo superior**, uma por painel.
4. Adicionar cota total no banzo inferior.
5. Colocar tags inline com o nome do perfil (pegar do parâmetro de tipo).
6. Não cotar ângulos nem comprimentos de diagonais individualmente.
7. Se houver detalhes de conexão já modelados, adicionar referência ("VER DET XX").

## Arquivos nesta pasta

- `EST-05-trelicas.pdf` — prancha original da Diocese de Patos.
- `NOTAS.md` — este arquivo.
