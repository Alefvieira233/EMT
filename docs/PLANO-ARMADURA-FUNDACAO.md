# PLANO — Módulo de Armadura de Fundação (Blocos de Coroamento + Vigas Baldrames + Arranque)

> Pedido do engenheiro (áudio + diagnóstico do EST-01): gerar a armadura de **blocos de
> coroamento** como **gaiola fechada contínua** (não "U soltos"), com **arranque do pilar**
> ancorado e **vigas baldrames** ancoradas no bloco (lb), corrigindo o detalhamento que hoje sai
> fragmentado. Branch de trabalho: `claude/great-turing-Vqlig`. Plugin Revit 2025 / .NET 8.

## Princípio-chave (a causa raiz)
As 3 queixas do diagnóstico (gaiola fragmentada, arranque desconexo, baldrame omitida) **NÃO são
problema de "motor 2D"** — o plugin nem tem motor 2D. São consequência da **geração 3D fragmentada**
(malhas soltas + estribos isolados, arranque inexistente, barras que terminam dentro do próprio
host). **Corrigir a geração 3D para uma gaiola real + arranque ancorado + baldrame ancorada faz os
cortes/elevações nativos do Revit já mostrarem a continuidade.** É onde está 80% do valor.

---

## Inventário de reúso (fatos do código — não re-descobrir)

| Capacidade | Onde | Reúso |
|---|---|---|
| Ancoragem NBR 6118 (lb, lb,nec, l0, η1/η2/η3, fbd, α) | `Services/PF/PfNbr6118AnchorageService.Calculate` | **Direto** p/ lb do arranque e da baldrame |
| Cobrimento efetivo (cobr.+Ø estribo+raio) | `Services/PF/PfRebarServicePure.EffectiveCoverCm` (puro/testado) | **Direto** (corrige bug de desconto) |
| Estribo fechado `StirrupTie` | `Services/Bloco/RebarCreationService.CreateClosedStirrup` + `VerticalStirrupService`/`HorizontalStirrupService` | Base da gaiola fechada |
| Barra aberta / com dobras | `RebarCreationService.CreateOpenBar` / `BuildBarWithBends` | Malhas + patas de arranque |
| Longitudinais + transpasse | `PfRebarService.CreateLongitudinalBarsWithLap` / `CreateVerticalBarsWithLap` + `PfRebarServicePure.BuildLapRanges` | Barras contínuas/emendadas do baldrame |
| Hook/pata NBR | `RebarCreationService.GetHookTypeByAngle` + `PfStirrupHookRules.IsCompliantWithNbr` | Patas de ancoragem |
| Frame do host (bbox) | `Services/Bloco/BlockGeometryService.BuildFrame` | Geometria do bloco/baldrame |
| Estribos de viga com zoneamento NBR | `PfRebarService.InsertBeamStirrups` (3 zonas) | Armadura do baldrame |
| Estaca: cobrimento efetivo correto | `PfRebarService.InsertEstacaBars:1041` | Referência do padrão correto |
| Config de armadura de bloco | `Models/Bloco/BlocoFundacaoRebarConfig.cs` | Estender |

**Não existe (criar):** gaiola fechada integrada; desconto do topo da estaca; arranque do pilar;
ancoragem cruzando elementos (barra entra no vizinho); criação da viga baldrame; qualquer
representação 2D (tags/tabela de ferro/visibilidade).

---

## Fases (cada uma shippável, CI-verde, com bump de versão)

### FASE 1 — Gaiola fechada do bloco de coroamento (núcleo; conserta o 2D na origem)
**Objetivo:** trocar "malhas soltas + estribos isolados" por uma **armação integrada**.
- Novo `Services/Bloco/BlocoCoroamentoCageBuilder` (orquestra a gaiola):
  1. **Cobrimento efetivo** em todas as faces via `PfRebarServicePure.EffectiveCoverCm` (cobr. do
     bloco, ex.: 4–5 cm, **descontando o Ø do estribo** — corrige o bug atual).
  2. **Malha de fundo (tração)** X+Y por espaçamento, com **gancho para cima** nas pontas
     (`BuildBarWithBends` + `GetHookTypeByAngle`) formando o "U" inferior.
  3. **Estribos fechados perimetrais** (`CreateClosedStirrup`) ligando fundo→topo (o que fecha a
     gaiola e dá a sensação de continuidade no corte).
  4. **Malha de topo** + **pele lateral** opcionais (`TopRebarService`/`SideSkinRebarService`).
  5. **Desconto do topo da estaca**: novo parâmetro `TopoEstacaEmbutidoCm` (default 5 cm); a malha
     de fundo posiciona-se em `topoEstaca + cobrimento` (acima das estacas), não no `bbox.Min.Z`.
- Estender `BlocoFundacaoRebarConfig`: `CobrimentoBlocoCm`, `TopoEstacaEmbutidoCm`,
  `FecharGaiola` (liga estribos perimetrais + ganchos das malhas).
- **Aceite:** no corte do Revit, fundo+topo aparecem ligados pelos estribos (gaiola), sem "U soltos".

### FASE 2 — Arranque do pilar (ferro de espera) — NOVO
**Objetivo:** barras verticais do pilar **descendo no bloco** até a malha inferior, com **pata**.
- Novo `Services/PF/PfArranqueService`:
  - Detecta o pilar sobre o bloco (mesma técnica do `PfFoundationPlacementService.FindConcreteBelow`).
  - Para cada barra longitudinal do pilar, cria a barra de arranque que **desce até a malha de
    fundo** e termina com **pata horizontal** (gancho 90°/dobra) sobre a malha — `BuildBarWithBends`.
  - **Comprimento de ancoragem** pelo `PfNbr6118AnchorageService` (lb,nec) + transpasse com a barra
    do pilar acima (`CreateVerticalBarsWithLap`).
- **Aceite:** no corte vertical do bloco, o arranque cruza o bloco até o fundo com a pata visível.

### FASE 3 — Viga baldrame: elemento + armadura ancorada — NOVO/REÚSO
**Objetivo:** lançar a viga baldrame e armá-la **ancorando no bloco (lb)**.
- (3a) **Criação da viga baldrame** (opcional): `NewFamilyInstance(Line, perfil de viga de concreto,
  nível, Beam)` entre blocos/pilares — análogo ao `GerarPorticoService.CriarBarra`. (Se o usuário
  preferir, atua sobre vigas já lançadas.)
- (3b) **Armadura**: reusa `PfRebarService.InsertBeamBars` + `InsertBeamStirrups` (já tem zoneamento
  NBR). **Novidade:** estender as longitudinais para **dentro do bloco** pelo `lb`
  (`PfNbr6118AnchorageService`) — ver Fase 4.
- **Aceite:** longitudinais do baldrame entram no bloco no comprimento lb calculado.

### FASE 4 — Ancoragem cruzando elementos (lb para dentro do vizinho) — NOVO
**Objetivo:** o que falta para arranque (Fase 2) e baldrame (Fase 3): **estender a barra
geometricamente para dentro do elemento vizinho** (pilar→bloco, baldrame→bloco) no comprimento lb.
- Helper puro `Services/PF/AncoragemGeometria` (testável): dado o ponto/direção de entrada e o lb
  (em mm), devolve a curva estendida + ponto da pata. Reúso do `lb` do `PfNbr6118AnchorageService`.
- **Aceite:** barra ultrapassa a face do host e entra no vizinho no lb; pata no fim.

### FASE 5 — Representação 2D + notas (SEPARADA, maior risco; honestidade)
**Objetivo:** o "polimento" 2D que o engenheiro pediu. Realista no Revit, mas trabalhoso.
- **Visibilidade da armadura por vista** (`Rebar.SetSolidInView`/`SetUnobscuredInView`) nos cortes do
  bloco → arranque/baldrame aparecem cruzando "transparente".
- **Tabela de ferro (BBS)**: `ViewSchedule` nativo de `Structural Rebar` alimentado pelos parâmetros
  `EMT_*` que os serviços já gravam (lb, transpasse, critério NBR) + Mark/posição.
- **Tags de ferro**: `IndependentTag.Create` (padrão já dominado no projeto para perfis) nas barras.
- **Nota automática** na prancha: `TextNote.Create` "Montar em forma de gaiola fechada conforme
  perspectiva 3D" + linha de chamada associando posições X/Y da tabela à malha.
- **Honestidade:** detalhamento 2D de **fabricação** nível TQS/Eberick/CYPE (desenho linear do ferro
  dobrado, cada trecho cotado, transpasses cotados) é domínio de software dedicado; o Revit
  aproxima via Rebar Shape + tags + schedule, mas a automação 100% "prancha de aço pronta" fica
  **fora deste plano** (proposta futura).

---

## Correção transversal (vale para tudo)
Padronizar o **desconto do Ø do estribo** no cobrimento das longitudinais (bug: bloco e pilar PF
não descontam; estaca desconta). Aplicar `PfRebarServicePure.EffectiveCoverCm` em todos.

## Ondas de execução (sugestão)
- Onda 1: Fase 1 (gaiola fechada + desconto estaca + cobrimento efetivo) + testes puros do frame/cover.
- Onda 2: Fase 4 (helper puro de ancoragem) + Fase 2 (arranque), reusando lb NBR.
- Onda 3: Fase 3 (baldrame elemento + armadura ancorada).
- Onda 4: Fase 5 parcial (visibilidade nos cortes + schedule de ferro + nota automática).
- Cada onda: UI no comando/janela apropriada, CI verde, bump de versão.

## Critérios de aceite (DoD por fase)
- [ ] Build Release 0 warnings, testes verdes (lógica pura: cover efetivo, lb, frame, ancoragem).
- [ ] Caminhos interativos existentes (Armaduras Bloco, Acos/Estribos Viga/Pilar) sem regressão.
- [ ] Validação no Revit pelo engenheiro: gaiola fechada no corte, arranque cruzando, baldrame
      ancorada com lb visível.

## Fora de escopo (explícito)
- Dimensionamento estrutural (a ferramenta DETALHA conforme premissas, não calcula esforços) —
  manter o disclaimer técnico existente.
- Detalhamento 2D de fabricação automático nível software dedicado (Fase futura).

## Pendência de entrada
Não consegui renderizar o PDF EST-01 neste ambiente. As premissas do texto são suficientes para o
plano; se houver bitolas/espaçamentos/cotas específicas do projeto-referência, o engenheiro deve
confirmá-las para calibrar os defaults da Fase 1.
