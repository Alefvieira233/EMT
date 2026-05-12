# Plano — Ferramenta de Lançamento de Alvenaria Estrutural

**Módulo:** ECC (Engenharia Civil e Construção)
**Status:** Ideia / Planejamento
**Data:** 2026-05-02

---

## Contexto

Blocos de alvenaria estrutural são modelados no projeto Revit como **quadros estruturais**
(`OST_StructuralFraming`), um por bloco, colocados horizontalmente ao longo do eixo da parede.
O lançamento manual é trabalhoso porque exige modulação precisa — encaixar os blocos dentro do
vão disponível com junta entre 8 mm e 12 mm, usando blocos especiais nos cantos e nas
extremidades quando necessário.

A ferramenta automatiza esse processo: recebe o eixo da parede, os tipos de bloco e os parâmetros
de modulação, e coloca os blocos calculados já posicionados e espaçados corretamente.

---

## Catálogo de Blocos

| Código | Descrição | Comprimento nominal | Uso |
|---|---|---|---|
| B14 | Bloco padrão | 140 mm | Preenchimento geral |
| BC34 | Bloco de canto (L) | 340 mm | Quinas em L |
| BT | Bloco de encontro em T | variável | Junção em T |
| B19 | Meio bloco | 190 mm | Ajuste de extremidade |
| B09 | Bloco de 9 cm | 90 mm | Ajuste fino |
| B04 | Bloco de 4 cm / pastilha | 40 mm | Último recurso de ajuste |
| B20 | Bloco 20 cm (acústico) | 200 mm (largura) | Paredes entre unidades |

> Largura padrão: 14 cm. Paredes entre apartamentos: 20 cm.
> Altura do bloco: 19 cm. Junta horizontal: 1 cm → fiada = 20 cm.

---

## Regras de Modulação

### Junta vertical
- Nominal: **10 mm**
- Faixa permitida: **8 mm – 12 mm**
- O Revit distribui o ajuste uniformemente entre todas as juntas da parede

### Hierarquia de ajuste (quando bloco inteiro não cabe)
1. Ajustar a junta dentro da faixa permitida (8–12 mm)
2. Usar meio bloco (B19) na extremidade
3. Usar bloco de 9 cm (B09)
4. Usar pastilha de 4 cm (B04)
5. **Nunca** usar dois meios blocos juntos — substituir por um bloco inteiro

### Cantos e encontros
- **Quina em L:** bloco BC34 nas duas faces, septo quadrado voltado para a quina
  (o furo quadrado é por onde passa o graute + armadura vertical)
- **Encontro em T:** bloco BT no ponto de junção
- **Nunca** deixar junta a prumo (descontinuidade) nos cantos — é a região de maior
  concentração de tensão; se inevitável, mover a descontinuidade para o meio da parede,
  preferencialmente sob janela (região não calculada como estrutural)

### Aberturas
- **Janelas:** blocos passam normalmente por baixo da janela na 1ª e 2ª fiadas
- **Portas:** não inserir bloco de canto onde há porta; reservar folga de ≈ 2,5 cm de cada
  lado para o marco + 1 cm de argamassa de fixação

---

## Algoritmo de Modulação (primeira fiada, parede reta)

```
entrada:
  L          = comprimento do eixo da parede (mm)
  bLen       = comprimento do bloco padrão (ex: 140 mm)
  jNominal   = junta nominal (10 mm)
  jMin       = 8 mm
  jMax       = 12 mm

1. Estimar quantidade de blocos inteiros
   n = floor((L + jNominal) / (bLen + jNominal))

2. Calcular junta ajustada
   jAdj = (L - n * bLen) / (n - 1)

3. Se jAdj está em [jMin, jMax] → usar n blocos com junta jAdj

4. Se jAdj < jMin → remover 1 bloco (n -= 1) e recalcular
   Se jAdj > jMax → adicionar 1 bloco (n += 1) e recalcular
   (iterar até convergir; se não convergir → usar bloco parcial na extremidade)

5. Se ainda sobrar espaço na extremidade → inserir bloco menor conforme hierarquia
```

---

## Lançamento da Segunda Fiada

1. Copiar todos os blocos da primeira fiada
2. Elevar **200 mm** (altura da fiada = bloco 190 mm + junta 10 mm)
3. Deslocar **200 mm** longitudinalmente para garantir amarração (junta vertical no meio do bloco inferior)
4. Ajustar extremidades: bloco de canto que estava em posição A → posição B (rotação 180°)
5. Rever e substituir blocos de ponta que sobrem ou faltem após o deslocamento

---

## Arquitetura do Código

```
Commands/
  CmdLancarAlvenaria.cs          ← entrada, lê config, chama service

Models/
  Alvenaria/
    AlvenariaConfig.cs           ← parâmetros da parede
    AlvBloco.cs                  ← representa um bloco calculado (posição, tipo, rotação)
    AlvResultado.cs              ← resultado do lançamento (qtd por tipo, erros)

Services/
  Alvenaria/
    AlvenariaModulacaoService.cs ← algoritmo puro (sem API Revit; testável)
    AlvenariaRevitService.cs     ← coloca as FamilyInstances no documento

Views/
  AlvenariaWindow.xaml(.cs)      ← configuração da ferramenta
```

### AlvenariaConfig (propriedades principais)

| Propriedade | Tipo | Default | Descrição |
|---|---|---|---|
| `EixoParede` | `Line` | — | Linha do eixo da parede (selecionada pelo usuário) |
| `Nivel` | `Level` | vista ativa | Nível de referência |
| `SymbolPadrao` | `FamilySymbol` | — | Bloco padrão (B14) |
| `SymbolCanto` | `FamilySymbol?` | null | Bloco de canto (BC34) |
| `SymbolBT` | `FamilySymbol?` | null | Bloco BT |
| `SymbolMeio` | `FamilySymbol?` | null | Meio bloco (B19) |
| `JuntaNominalMm` | `double` | 10 | Junta nominal em mm |
| `JuntaMinMm` | `double` | 8 | Junta mínima em mm |
| `JuntaMaxMm` | `double` | 12 | Junta máxima em mm |
| `GerarSegundaFiada` | `bool` | false | Gera automaticamente a 2ª fiada |
| `NumFiadas` | `int` | 1 | Quantidade de fiadas a lançar |
| `CantoInicial` | `TipoExtremidade` | Padrao | Tipo de bloco na extremidade inicial |
| `CantoFinal` | `TipoExtremidade` | Padrao | Tipo de bloco na extremidade final |

### Separação de responsabilidades

- **`AlvenariaModulacaoService`** — lógica pura:
  recebe `double wallLength`, `AlvenariaConfig` e retorna `List<AlvBloco>` com
  posição relativa, tipo e rotação de cada bloco. Sem API Revit — 100% testável via xUnit.

- **`AlvenariaRevitService`** — consome `List<AlvBloco>` e chama
  `doc.Create.NewFamilyInstance(pt, symbol, nivel, StructuralType.NonStructural)`
  ou a sobrecarga com `Line` conforme o tipo do bloco.

---

## Fases de Implementação

### v1 — Parede reta, primeira fiada
- [ ] Selecionar eixo (linha de modelo ou dois pontos clicados)
- [ ] Escolher bloco padrão e bloco de extremidade no dropdown
- [ ] Calcular modulação com junta variável 8–12 mm
- [ ] Colocar blocos no Revit
- [ ] Relatório: quantidade por tipo, junta utilizada, sobra/falta

### v2 — Cantos e encontros
- [ ] Detecção automática de quinas em L a partir de linhas conectadas
- [ ] Detecção de encontros em T
- [ ] Inserção automática de BC34 e BT nos pontos corretos
- [ ] Verificação de alinhamento do septo quadrado para graute

### v3 — Múltiplas fiadas
- [ ] Loop de n fiadas com elevação acumulada de 200 mm/fiada
- [ ] Alternância do padrão de amarração (deslocamento 200 mm a cada fiada)
- [ ] Ajuste automático de extremidades em cada fiada

### v4 — Aberturas
- [ ] Reconhecimento de janelas e portas no eixo da parede (interseção com `OST_Doors`, `OST_Windows`)
- [ ] Interrupção do lançamento nos vãos; folga automática de 2,5 cm + 1 cm para marcos
- [ ] Tratamento especial: blocos passando por baixo de janelas normalmente

### v5 — Parede acústica e geração de projeto
- [ ] Flag `ParedeAcustica` → troca B14 por B20 automaticamente
- [ ] Exportar QDM (quantidade de materiais) por parede e totalizador global
- [ ] Integração com a ferramenta Marcar Peças (marca blocos por fiada e tipo)

---

## Questões em Aberto (a definir antes da v1)

1. Os blocos são inseridos com `NewFamilyInstance(Line, ...)` (bloco como viga) ou
   `NewFamilyInstance(XYZ, FamilySymbol, StructuralType)` (bloco como ponto)?
2. Os blocos de canto precisam de parâmetro de rotação ou são famílias espelhadas separadas?
3. O eixo da parede é sempre o centro do bloco ou a face interna/externa?
4. A ferramenta deve detectar paredes já existentes para evitar duplicatas?
5. A seleção de famílias deve ser persistida em `AppSettings` entre sessões?

---

## Referências

- Padrão de lançamento: vídeo de modelagem manual de alvenaria estrutural (acervo interno)
- Blocos padrão ABNT NBR 15270 (bloco cerâmico) / NBR 6136 (bloco de concreto)
- Módulo de altura: 20 cm (bloco 19 cm + junta 1 cm)
- Documento técnico do escritório EMT sobre alvenaria estrutural
