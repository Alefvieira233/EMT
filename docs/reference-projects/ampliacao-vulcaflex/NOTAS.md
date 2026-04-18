# Ampliação Vulcaflex — Uberlândia-MG

Projeto executivo completo. Autor: ALEF CHRISTIAN GOMES VIEIRA (CREA 031991896-3 AP). Cliente: VULCAFLEX. Data: 11/2024.

Arquivo-fonte completo (173 PDFs, ~47 MB) fica em:
`E:\PROJETOS - ENG ALEF VIEIRA\ESTRUTURAL VULCAFLEX\ARQUIVOS OFICIAIS - VULCAFLEX - PROJETO ESTRUTURAL\`

Nesta pasta guardamos só as pranchas-amostra (CO-01 e CO-02) e as notas de padrão por categoria.

Obra: galpão industrial com eixos A-I × 1-8, 29 pilares W530X72, contraventamentos em X por cabo/barra redonda, cobertura em treliça, fechamento lateral com terças em UE.

---

## 1. Detalhamento de pilar isolado (COLUNAS/)

Pranchas `CO-01` a `CO-12`, 12 pilares detalhados individualmente.

- Cada pilar é apresentado em **4 elevações**: A-A (mesa frontal, completa), B-B (alma lateral, essencial), C-C (vista vazada), D-D (mesa oposta, limpa).
- Escala padrão **1:40**.
- Cotas verticais acumuladas do térreo ao topo, com cotas parciais de cada nível de viga, chapa e emenda.
- Níveis identificados com balão circular + texto (`TÉRREO 0`, `COBERTURA 03 19027`).
- Balão de eixo da planta no topo (1, A, B, C, D, E, F).
- **Callouts de chapa**: `CH 08`, `CH 17`, `CH 09`, `CH 33`, etc. Numeração sequencial da biblioteca de chapas.
- **Callouts de tipo de conexão**: `TIPO 01` (topo), `TIPO 02` (emenda), `TIPO 03` (base), `TIPO 18` (viga secundária), `TIPO 34/35` (enrijecedores/apoio de terça).
- **Soldas**: `E70XX / SMAW` + número do filete (5, 7, 9 mm).
- **Emenda** quando pilar > 12 m (limite comercial): geralmente em torno de 11,64 m, unindo parte inferior (12 m) com parte superior.
- **Base do pilar**: chapa de 900 mm com chumbadores visíveis, callout `TIPO 03`.
- Tabela de perfis por prancha: `W530X72` × comprimento × peso × ID do pilar × contagem.
- Tabelas-mestre globais aparecem só na CO-02 (válidas para todas as pranchas da série): Chapas, Soldas, Torque A325, Parafusos/Porcas/Arruelas.

## 2. Planta de locação de pilares (DIAGRAMAS DE MONTAGEM/EM-01)

Planta em escala **1:75** mostrando onde cada pilar fica no terreno.

- **Grid de eixos**: letras A-I na horizontal (9 colunas), números 1-8 na vertical (8 linhas).
- Eixos em **linha traço-ponto** atravessando toda a prancha.
- **Distância entre eixos** cotada no topo/laterais (5381, 5625, 5000 mm).
- Cada pilar é representado como um retângulo (planta da chapa de base) com círculos (chumbadores).
- **Tag P1 a P29** ao lado de cada pilar, em caixa retangular.
- **Cotas locais** quando o pilar está fora do eixo exato (ex: 398, 402, 425, 900, 650 mm).
- Balões de eixo nos cantos e no topo/lateral.
- Título embaixo: `LOCAÇÃO DOS PILARES / PLACAS DE BASE`.

## 3. Diagramas de montagem (DIAGRAMAS DE MONTAGEM/EM-01 a EM-24)

24 pranchas — sequência visual de como a obra é montada em campo, passo a passo.

- Série numerada EM-01 (planta de locação), EM-02 em diante (elevações/fases).
- Cada prancha isola um sistema ou fase: pilares → travamentos → tesouras → contraventamentos → fechamentos → telhas.
- Escala típica **1:75** para planta/elevação de galpão inteiro.
- Notação inline do perfil ao longo dos elementos.

## 4. Contraventamentos (CONTRAVENTAMENTOS/EM-25 a EM-28)

4 pranchas — elevações dos planos contraventados.

- **Elevação de eixo inteiro** do galpão (escala 1:75).
- Balões de eixo da planta no topo (A-I ou I-A dependendo da orientação).
- Contraventamento em **X** com **barra redonda 10 mm**, marcado inline ao longo da diagonal.
- **Tags CP01, CP02, ..., CP06** identificando cada peça de contraventamento.
- Tabela de perfis no canto: BARRA REDONDA 10mm × comprimento de corte × peso × ID × contagem.
- Peso total do eixo na última linha.
- Título: `ELEVAÇÃO EIXO NN - CONTRAVENTAMENTO` e `CONTRAVENTAMENTO - EIXO NN` no carimbo.

## 5. Linhas de corrente flexível (CORRENTE FLEXÍVEL/EM-29 a EM-36)

8 pranchas — corrente flexível (linhas de travamento horizontal da cobertura entre terças).

- **Elevação do eixo** em escala 1:75.
- Correntes representadas como linhas diagonais finas entre terças.
- **Tags LC 01, LC 02, LC 03** identificando cada tipo de linha.
- Esquema de contagem inline: `(9x) LC 01`, `(7x) LC 02`, `(32x) LC 03` — útil pra lista de material.
- Pequenas cotas entre pontos de ancoragem (1657, 1664, 1999, 1654, etc.).
- Título: `ELEVAÇÃO EIXO NN - LINHAS DE CORRENTE FLEXIVEL EIXO NN`.

## 6. Croqui individual de terça (TERÇAS E FECHAMENTO - CROQUIS/T01 a T14)

Uma prancha por tipo de terça. Formato **vertical**, escala **1:20**.

- Mostra a **peça deitada** (duas vistas — superior plana + lateral).
- Notação do perfil inline: `T01 - U150x60x20x2.0`.
- Cotas:
  - Comprimento total (`5448`).
  - Distâncias entre furos (`1652, 1775, 1775, 1664`).
  - Offsets de borda (`25, 25`).
  - Posição vertical do furo dentro da mesa (`60, 90, 50, 100`).
- **Furos**: `Ø 14 (TIP.)` — diâmetro 14 mm típico.
- Tabela no canto inferior: Posição (T01) × Tipo (`U150x60x20x2.0`) × Comprimento do corte × Peso × Quantidade.
- Carimbo simplificado: `CROQUI TERÇAS`, data, engenheiro, cliente, escala.

## 7. Croqui individual de linha de corrente (LINHAS DE CORRENTE - CROQUIS/LC01 a LC15+)

Uma prancha por tipo de LC. Formato **vertical**, escala **1:20**.

- Mostra peça única deitada (cantoneira simples fina).
- Notação inline do perfil.
- Cota do comprimento total (`1003, 1002`).
- Cota da altura/largura (`25`).
- Tabela: Posição (LC01) × Tipo (`L 1 x 1 x 1/8"`) × Comprimento × Peso × Quantidade.
- Carimbo: `CROQUI LINHA DE CORRENTE`.

## 8. Croqui individual de chapa (CHAPAS - CROQUIS/CH 03 a CH 15+)

Uma prancha por tipo de chapa. Formato **vertical**, escala **1:2**.

- Desenho da chapa em planta, em tamanho próximo do real (escala 1:2).
- **Vista lateral fina** mostrando a espessura da chapa (ex: `3.20`).
- Dimensões em mm: largura (`50`), comprimento (`106`), offsets de furo (`23, 60, 23`), espaçamentos (`25, 25`).
- **Furos** com tag `Ø 14 (TIP.)`.
- Tabela: Contagem × Espessura × Largura × Comprimento × Peso × Comentários (`CH 03`).
- Linha de soma no rodapé da tabela com peso total do item (ex: `117.96 kg`).
- Carimbo: `CHAPAS`, data, engenheiro, escala.

## 9. Convenções gerais do escritório EMT (extraídas destas pranchas)

- **Carimbo padrão** no canto inferior direito com 7 campos: OBRA/SERVIÇO, ENDEREÇO, CLIENTE, ENG. CIVIL + CREA + ASSINATURA, DESENHISTA, ASSUNTO, ESCALA + DATA + PRANCHA Nº.
- **Nomenclatura de prefixos**:
  - `CO` = coluna (pilar).
  - `EM` = estrutura metálica (diagrama de montagem, contraventamento, LC global).
  - `T` = terça (croqui individual).
  - `LC` = linha de corrente (croqui individual).
  - `CH` = chapa (croqui individual).
  - `CP` = contraventamento individual (no layout EM-25+).
  - `P` = pilar (na planta de locação EM-01).
- **Escalas padronizadas por tipo de prancha**:
  - Planta de locação: 1:75.
  - Elevação de galpão (contraventamento, corrente): 1:75.
  - Elevação de pilar isolado: 1:40.
  - Croqui de peça individual (terça, LC): 1:20.
  - Croqui de chapa: 1:2.
  - Detalhe de conexão: 1:10 ou 1:25.
  - Elevação de treliça: 1:50 (vide Igreja Patos).
- **Convenção de perfis**:
  - Pilares laminados: `W150x18`, `W200x19.3`, `W200x26.6`, `W530X72`.
  - Terças (perfil U enrijecido formado a frio): `UE 200x75x20x2.25`, `U150x60x20x2.0`.
  - Banzos de treliça (dupla cantoneira): `2L 2 1/2 x 1/4 x 3/16`.
  - Diagonais/montantes/LC (cantoneira simples): `L 1 1/4 x 1/8`, `L 1 x 1 x 1/8"`.
  - Chapa: identificada por ID sequencial (`CH NN`) com dimensões tabeladas separadamente.
  - Barra redonda: `BARRA REDONDA 10mm`.
- **Parafusos**: ASTM A325M (estruturais), ASTM A307 (chumbadores, galvanizados). Torque tabelado por diâmetro.
- **Solda**: eletrodo AWS E70XX (E7018 estrutural, E6013 chapa fina). Processo SMAW (eletrodo revestido, manual).

---

## Como usar isto no código

Ao escrever/lapidar funções do FerramentaEMT, consultar a seção correspondente:

| Função do plugin | Seção desta NOTAS |
|---|---|
| Gerar Vista de Peça (pilar) | §1 |
| Gerar Vista de Peça (treliça) | Ver `igreja-patos/NOTAS.md` |
| Planta de Locação | §2 |
| Plano de Montagem (faseamento) | §3 |
| Detalhamento de Contraventamento | §4, §5 |
| Croqui de Terça individual | §6 |
| Croqui de LC individual | §7 |
| Croqui de Chapa individual | §8 |
| Auto-Vista (qualquer peça) | §6, §7, §8 |
| Cotar Fabricação | §6, §7, §8 |
| Identificar Perfil (tag) | §9 |
| Exportar DSTV | §6 (comprimentos e furações) |
| Marcar Peças | §1 para CO, §6 para T, §7 para LC, §8 para CH |

## Arquivos nesta pasta

- `CO-01-colunas-01-a-03.pdf` — amostra de detalhamento de pilar.
- `CO-02-colunas-04-a-06.pdf` — amostra com tabelas-mestre de chapas/soldas/parafusos.
- `NOTAS.md` — este arquivo.
