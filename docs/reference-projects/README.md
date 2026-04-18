# Galeria de Projetos de Referência — FerramentaEMT

Esta pasta guarda pranchas e projetos executivos reais do escritório EMT,
usados como referência para escrever/lapidar funções do plugin Revit.

## Como funciona

Cada subpasta é um projeto. Contém:

- PDFs-amostra (pranchas representativas).
- `NOTAS.md` — o que a Claude deve aprender com aquele projeto: padrão de
  cotagem, nomenclatura de perfis, estilo de detalhe, convenções do escritório.

Quando a Claude for escrever uma função nova (ex: "Cotar Treliça", "Croqui de
Terça", "Detalhamento de Pilar"), ela **consulta estas NOTAS antes de começar**
pra manter o código alinhado com o padrão EMT em vez de inventar do zero.

## Projetos catalogados

### `igreja-patos/`
**Reforma/Ampliação Diocese de Patos** — prancha EST-05 detalhando treliças.
Foco: cotagem de treliça, identificação de perfis, estrutura de prancha com
elevações + perspectivas 3D + detalhes de conexão + tabelas de perfis e
parafusos.

### `galpao-padrao-emt/`
**Galpão pequeno padrão EMT (Alef, 08/04/2024)** — projeto **mínimo completo em 3 pranchas**:
EST-01 (locação + fundações + chumbadores), EST-02 (plano geral da cobertura), EST-03
(detalhamento das treliças, elevação frontal, detalhes de ligação e topo de pilar).
Grid A-G × 1-2, vãos 5000 mm, treliça 15010 mm vão, pilares metálicos 2U300x100x25x3.00.
Cobre:

1. Planta de locação 1:50 com sapatas 170x210x60, identificação `Pnn` de pilares, sapatas `Snn`, vigas baldrame `Vnn`.
2. Plano geral de cobertura 1:100 com `TRELIÇA 01 - U150x65x4,76`, `TERÇAS 01-09 UE150X60X20X2,00`, contraventamento, linhas de corrente.
3. Elevação da treliça 1:25 com padrão de 5 faixas de cotas + tags de perfil + balões `VER DET 02/03`.
4. Detalhes: mão francesa (1:25), ligação de contraventamento (isométrico), topo do pilar (1:10), perspectiva 3D geral.
5. Tabelas padrão EMT: aço de sapatas/pilares/baldrame, âncoras, formas/concreto, perfis metálicos, telhas, placas, parafusos.
6. Notas gerais CONCRETO (NBR 6118/6120/6122) e METÁLICA (NBR 8800/14762/6123/6120) — texto-modelo replicável em qualquer prancha nova.
7. **Sequência EMT mínima de entrega:** EST-01 → EST-02 → EST-03 (template para "Gerar Projeto Completo").

### `cobertura-samsung/`
**Cobertura metálica Samsung** — galpão com treliças de duas águas sobre pilares.
Cobre:

1. Plano geral da cobertura (EM-01, 1:100) com treliças, terças numeradas,
   contraventamentos em X, linhas de corrente, calha perimetral, 3 perspectivas
   3D de rodapé e tabela geral de perfis metálicos.
2. Elevações das treliças (EM-02) com padrão completo de cotagem:
   - Treliça principal TR01 em 1:50 (vão 38367 mm, banzos W200x26.6,
     diagonais e montantes em cantoneira dupla `2x L ...`).
   - **Cinco faixas de cotas sobrepostas:** painéis do banzo superior, painéis
     do banzo inferior, cota total do vão, cota entre apoios parciais, e altura
     de cada montante (texto vertical).
   - Balões de detalhe em perspectiva 3D: platibanda, ligação banzo superior,
     ligação banzo inferior, ligação de terça, montante central, agulha central,
     mão francesa, apoio em pilar.
3. Cortes auxiliares em 1:100 (mão francesa, agulhas centrais).
4. Tabelas laterais: perfis metálicos geral, placas-resumo, parafusos.

### `ampliacao-vulcaflex/`
**Ampliação Vulcaflex, Uberlândia-MG** — projeto executivo completo (173 PDFs).
Cobre:

1. Detalhamento de pilar isolado (12 pilares em 4 vistas cada, escala 1:40).
2. Planta de locação de pilares com grid de eixos A-I × 1-8.
3. Diagramas de montagem (24 fases).
4. Elevações de contraventamento (barra redonda 10mm em X).
5. Elevações de linhas de corrente flexível da cobertura.
6. Croquis individuais de terça (14 tipos, escala 1:20).
7. Croquis individuais de linha de corrente (47 tipos, escala 1:20).
8. Croquis individuais de chapa (64 tipos, escala 1:2).
9. Tabelas-mestre de chapas, soldas, parafusos e torque A325.
10. Convenções gerais do escritório EMT (prefixos, escalas, nomenclatura).

Arquivo-fonte completo fica em:
`E:\PROJETOS - ENG ALEF VIEIRA\ESTRUTURAL VULCAFLEX\ARQUIVOS OFICIAIS - VULCAFLEX - PROJETO ESTRUTURAL\`

## Para a Claude (próximas sessões)

No início de qualquer sessão onde o usuário pedir uma função nova ou ajuste
em função existente do FerramentaEMT, **ler o `NOTAS.md` de cada projeto**
relevante antes de começar a escrever código. Isso garante que o estilo EMT
seja preservado em cada função do plugin.

## Índice rápido por função do plugin

| Função do plugin | Projetos de referência |
|---|---|
| Cotar Treliça | cobertura-samsung §2, galpao-padrao-emt §3.2, igreja-patos §Cotagem |
| Catálogo de Treliças (várias em 1 prancha) | cobertura-samsung §2B |
| Plano Geral de Cobertura | cobertura-samsung §1, galpao-padrao-emt §2 |
| Locação de Sapatas/Pilares | galpao-padrao-emt §1, ampliacao-vulcaflex §2 |
| Detalhamento de Fundação + Chumbadores | galpao-padrao-emt §1.3, §1.4 |
| Quadros de Platibanda | cobertura-samsung §2B.2 |
| Detalhe de Ligação de Treliça (balão) | cobertura-samsung §2.4, galpao-padrao-emt §3.6 |
| Detalhe de Mão Francesa | cobertura-samsung §2.5, galpao-padrao-emt §3.4 |
| Detalhe de Agulha Central | cobertura-samsung §2.5 |
| Detalhe Topo de Pilar / Chapa | galpao-padrao-emt §3.7, §3.8 |
| Vista de Pilar | ampliacao-vulcaflex §1 |
| Planta de Locação | ampliacao-vulcaflex §2, galpao-padrao-emt §1 |
| Plano de Montagem | ampliacao-vulcaflex §3 |
| Contraventamento | ampliacao-vulcaflex §4, §5, cobertura-samsung §1, galpao-padrao-emt §3.6 |
| Linhas de Corrente (planta) | cobertura-samsung §1, ampliacao-vulcaflex §5, galpao-padrao-emt §2.2 |
| Croqui de Terça | ampliacao-vulcaflex §6 |
| Croqui de LC | ampliacao-vulcaflex §7 |
| Croqui de Chapa | ampliacao-vulcaflex §8 |
| Auto-Vista / Cotar Fabricação | ampliacao-vulcaflex §6-8 |
| Identificar Perfil | ampliacao-vulcaflex §9, cobertura-samsung §3.3, galpao-padrao-emt §4.1 |
| Marcar Peças | ampliacao-vulcaflex §1, §6-8, galpao-padrao-emt §4.1 |
| Tabela de Perfis / Parafusos / Placas | cobertura-samsung §1.5, §2.6, galpao-padrao-emt §2.5 |
| Notas Gerais CONCRETO + METÁLICA (texto-modelo) | galpao-padrao-emt §1.6, §2.6 |
| Gerar Projeto Completo (EST-01+02+03) | galpao-padrao-emt §4.5 |
