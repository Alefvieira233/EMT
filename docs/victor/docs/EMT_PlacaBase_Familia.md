# EMT Placa Base

Especificacao da familia hospedeira para lancamento automatico de placas de base no EMT.

## Objetivo

Criar uma familia unica, parametricamente controlada, preparada para ser inserida pelo comando `Lancar Placas de Base` sobre a face superior de fundacoes ou outros apoios de concreto.

O plugin deve resolver:

- coleta de pilares metalicos
- deteccao do apoio de concreto abaixo
- projecao do ponto base do pilar na face superior
- insercao e orientacao da familia
- preenchimento de parametros

A familia deve resolver:

- geometria da placa
- composicao com itens aninhados
- visibilidade simplificada ou completa
- offsets, furos e chumbadores

## Template recomendado

Usar `Generic Model face based.rft`.

Motivo:

- o comando atual usa `NewFamilyInstance(Face, XYZ, XYZ, FamilySymbol)`
- esse fluxo exige `FamilyPlacementType.WorkPlaneBased`
- a familia precisa nascer alinhada a uma face de concreto e aceitar rotacao pelo eixo local do pilar

Nao comecar por familia `Structural Connection` nesta fase.

Motivo:

- aumenta a complexidade do comportamento
- restringe a reutilizacao
- nao agrega ganho imediato para a primeira versao do EMT

## Nome recomendado

- Familia: `EMT_PlacaBase`
- Tipo inicial: `Padrao`

## Estrutura da familia

Familia hospedeira:

- `EMT_PlacaBase.rfa`

Familias aninhadas sugeridas:

- `Conexao estrutural - Placa retangular de aco.rfa`
- `Conexao estrutural - Chumbador J.rfa`
- `Conexao estrutural - Arruela quadrada.rfa` ou `Conexao estrutural - Arruela redonda.rfa`
- `Conexao estrutural - Porca sextavada.rfa`

Item opcional futuro:

- familia de volume de graute

## Regra de compartilhamento

Padrao inicial:

- placa: `Nao Shared`
- chumbador: `Nao Shared`
- arruela: `Nao Shared`
- porca: `Nao Shared`

Usar `Shared = true` apenas se houver necessidade real de:

- taguear separadamente
- agendar separadamente
- selecionar separadamente no projeto

Para o fluxo de automacao inicial, o melhor e manter tudo como um conjunto unico.

## Planos de referencia

Criar e nomear no minimo os seguintes planos:

- `Centro_LR`
- `Centro_FB`
- `Topo_Hospedeiro`
- `Base_Placa`
- `Topo_Placa`
- `Eixo_Chumbador_X_Pos`
- `Eixo_Chumbador_X_Neg`
- `Eixo_Chumbador_Y_Pos`
- `Eixo_Chumbador_Y_Neg`

Regras:

- origem da familia no centro da placa
- plano de insercao encostado na face do concreto
- espessura da placa sempre para fora da face hospedeira
- chumbadores distribuidos simetricamente a partir do centro

## Parametros da familia hospedeira

Esses nomes devem ser mantidos exatamente assim para bater com o plugin atual:

### Dimensoes principais

- `Comprimento` - Length - instancia
- `Largura` - Length - instancia
- `Espessura` - Length - instancia

### Furos

- `Furo_Diametro` - Length - instancia
- `Furo_Offset_X` - Length - instancia
- `Furo_Offset_Y` - Length - instancia

### Chumbadores

- `Chumbador_Diametro` - Length - instancia
- `Chumbador_Comprimento` - Length - instancia
- `Chumbador_Qtde_X` - Integer - tipo ou instancia
- `Chumbador_Qtde_Y` - Integer - tipo ou instancia

### Graute

- `Graute_Espessura` - Length - instancia

### Solda e metadados

- `Solda` - Text - instancia
- `EMT_TipoLigacao` - Text - tipo
- `EMT_VersaoFamilia` - Text - tipo

### Visibilidade

- `Mostrar_Placa` - Yes/No - instancia
- `Mostrar_Chumbadores` - Yes/No - instancia
- `Mostrar_Arruelas` - Yes/No - instancia
- `Mostrar_Porcas` - Yes/No - instancia
- `Mostrar_Graute` - Yes/No - instancia
- `LigacaoCompleta` - Yes/No - instancia

## Parametros derivados recomendados

Esses parametros podem ficar como formulas internas da familia:

- `Meio_Comprimento = Comprimento / 2`
- `Meio_Largura = Largura / 2`
- `Raio_Furo = Furo_Diametro / 2`
- `Offset_Topo_Graute = Graute_Espessura`

Regras de consistencia recomendadas:

- `Furo_Offset_X < Comprimento / 2`
- `Furo_Offset_Y < Largura / 2`
- `Espessura > 0`
- `Graute_Espessura >= 0`

## Comportamento visual recomendado

### Modo simplificado

Quando `LigacaoCompleta = false`:

- mostrar apenas a placa
- opcionalmente mostrar volume de graute
- esconder chumbadores, arruelas e porcas

### Modo completo

Quando `LigacaoCompleta = true`:

- mostrar placa
- mostrar 2 ou 4 chumbadores
- mostrar arruelas e porcas
- manter simetria por offsets

## Modelagem recomendada

### Placa

Se a familia `Conexao estrutural - Placa retangular de aco.rfa` ja for boa geometricamente:

- aninhar essa familia
- associar seus parametros a `Comprimento`, `Largura` e `Espessura`

Se ela nao responder bem:

- modelar a placa diretamente na hospedeira
- deixar a familia aninhada para uma segunda fase

### Chumbadores

Criar um chumbador e espelhar/array conforme necessidade.

Distribuicao inicial recomendada:

- 4 chumbadores
- offsets controlados por `Furo_Offset_X` e `Furo_Offset_Y`

### Arruelas e porcas

Devem ser aninhadas no topo da placa, alinhadas ao eixo de cada chumbador.

## Mapeamento esperado pelo plugin

O comando atual ja tenta preencher estes parametros por `LookupParameter`:

- `Comprimento`
- `Largura`
- `Espessura`
- `Furo_Diametro`
- `Furo_Offset_X`
- `Furo_Offset_Y`
- `Chumbador_Diametro`
- `Graute_Espessura`
- `Solda`

Se a familia usar nomes diferentes, o plugin nao vai preencher automaticamente.

## Fluxo recomendado de criacao

1. Criar `EMT_PlacaBase` com template face-based.
2. Criar planos de referencia e travar a origem no centro.
3. Adicionar parametros principais.
4. Inserir a placa retangular e associar dimensoes.
5. Inserir 1 chumbador, 1 arruela e 1 porca.
6. Travar aos planos de referencia.
7. Espelhar para formar o conjunto de 4 chumbadores.
8. Adicionar visibilidades por checkbox.
9. Testar manualmente em uma face horizontal de concreto.
10. Carregar no projeto e testar com o comando do EMT.

## Tipos iniciais sugeridos

- `Padrao_250x250x16`
- `Padrao_300x300x20`
- `Padrao_350x350x25`
- `Pesada_400x400x32`

## Escopo da primeira versao

Primeira versao da familia deve garantir:

- insercao correta sobre face de concreto
- rotacao correta pelo eixo do pilar
- controle de comprimento, largura e espessura
- controle basico de furos
- opcao de modo simplificado ou completo

Nao precisa resolver na primeira versao:

- listas detalhadas de ferragens
- conexoes especiais por perfil
- furos oblongos
- chapas com recortes complexos
- layouts variaveis por padrao normativo

## Validacao minima no Revit

Antes de liberar a familia para uso no EMT, testar:

1. Hospedagem em face horizontal de bloco ou sapata.
2. Rotacao a 0, 90 e 180 graus.
3. Alteracao de `Comprimento`, `Largura` e `Espessura`.
4. Alteracao de `Furo_Offset_X` e `Furo_Offset_Y`.
5. Alternancia entre `LigacaoCompleta` ligado e desligado.
6. Insercao automatica em pelo menos 3 pilares com orientacoes diferentes.

## Proxima etapa no EMT

Depois que a familia existir e estiver validada:

1. testar o comando com essa familia como padrao
2. adicionar prevencao de duplicatas por pilar
3. opcionalmente detectar tipos de pilar e atribuir tipos de placa
4. opcionalmente registrar `EMT_Pilar_Base` e `EMT_Apoio_Base` para rastreabilidade
