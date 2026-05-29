# Plano de Fix — Diagrama de Montagem (cotas com referências inválidas)

**Data:** 2026-05-29
**Sintoma reportado pelo Alef:**
> "Apliquei e ela colocou apenas as tags. As cotas deram algum erro... abriu uma caixa de diálogo e só tinha a opção de excluir as cotas. Precisei excluir."

**Versão atual:** v2.8.7

---

## 1. Diagnóstico — por que o Revit abre o dialog "Excluir cotas"

Esse dialog do Revit (`"Constraints not satisfied"` / `"Dimension references not valid"`) aparece quando ele tenta commitar uma `Dimension` que tem **pelo menos um `Reference`** apontando pra geometria que:

1. **Não existe na vista** (Reference fora do crop ou em categoria escondida), OU
2. **Tem geometria coincidente** (linha de cota degenerada — comprimento zero), OU
3. **Foi criada com `Reference` de Grid em vista que não enxerga o Grid**, OU
4. **A Line da cota não toca/projetar nas Refs** (Revit não consegue alinhar a cota com as refs)

A `IndependentTag.Create` **não usa Dimension** — por isso as **tags funcionaram, mas as cotas falharam**. Confirma que o problema é específico de `NewDimension`/`NewSpotElevation`.

---

## 2. Análise técnica dos 4 caminhos de cota

A função `DiagramaMontagemService.Executar` cria 4 tipos diferentes de cota, cada um num bloco `try-catch` em transação separada. Vou analisar cada um:

### 2.1 `CriarCotasEntreEixos` ([DiagramaMontagemService.cs:431-485](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L431-L485))

**Estratégia atual:**
- Coleta todos os Grids do doc (sem filtro de visibilidade).
- Ordena pela projeção do ponto base do Grid na `RightDirection` da Section View.
- Pra cada par consecutivo cria `Dimension` com `Reference(g1)` e `Reference(g2)`.
- Linha de cota: usa `cropBox.Max.Y` da Section View como altura (`topo`).

**❌ BUGS IDENTIFICADOS:**

#### BUG #1 — Linha de cota usando coordenadas erradas (CRÍTICO)

```csharp
BoundingBoxXYZ cropBox = vista.CropBox;
XYZ topo = vista.Origin + vista.UpDirection * (cropBox.Max.Y + UnitUtils.ConvertToInternalUnits(500.0, UnitTypeId.Millimeters));
```

`vista.CropBox.Max.Y` é em **coordenadas LOCAIS da vista** (UV do plano), mas está sendo multiplicado em **espaço MODELO** (`vista.UpDirection`). Não faz sentido somar uma coordenada Y local da Section View (que pode ser, por exemplo, 5 ft) à origem da vista no espaço modelo escalado.

**Resultado prático:** `topo` cai num lugar absurdo do espaço modelo. A `Line.CreateBound(p1, p2)` provavelmente fica com comprimento ~zero ou totalmente fora dos eixos → Revit não consegue alinhar a dimension com os Refs dos Grids → abre o dialog "excluir cotas".

#### BUG #2 — `ProjetarParaTopo` matematicamente incorreto

```csharp
private XYZ ProjetarParaTopo(XYZ ponto, XYZ topo, XYZ upDir)
{
    double yTopo = topo.DotProduct(upDir);
    XYZ pontoNoUp = upDir * yTopo;
    XYZ horizontal = ponto - upDir * ponto.DotProduct(upDir);
    return horizontal + pontoNoUp;
}
```

Isso quase faz sentido conceitualmente, MAS:
- `topo.DotProduct(upDir)` projeta `topo` em `upDir` (ok).
- `horizontal` extrai a componente do ponto fora de `upDir` (ok).
- Retorna `horizontal + upDir * yTopo`.

**Problema:** quando o `Grid` é uma `Line` vertical (Grid clássico no plano XY), `GridPosicaoBase(g)` retorna `lin.GetEndPoint(0)` que tem um Z arbitrário (depende de onde o Grid foi modelado). Combinado com o `topo` errado do BUG #1, a `Line` da cota fica completamente desalinhada com os Grids reais.

#### BUG #3 — Não filtra Grids visíveis na vista

```csharp
var gridsComOrdem = new FilteredElementCollector(doc).OfClass(typeof(Grid))...
```

Pega **todos** os Grids do projeto. Em projetos com múltiplas plantas (G1, G2, G3) ou Grids escondidos, isso vai criar `Reference` pra Grid que **a vista não enxerga**. Revit rejeita.

#### BUG #4 — Não verifica se o Grid intersecta o crop da vista

Mesmo Grids visíveis precisam **cruzar o plano da Section View** pra serem cotáveis. Grids paralelos ao plano da seção (improvável mas possível) não geram Reference utilizável.

---

### 2.2 `CriarCotaTotalConjunto` ([DiagramaMontagemService.cs:670-714](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L670-L714))

**Tem o mesmo padrão de bugs:**
- Usa `vista.CropBox.Max.Y` da mesma forma errada (BUG #1)
- Pega todos os Grids sem filtrar visibilidade (BUG #3)
- Usa `ProjetarParaTopo` com mesmo cálculo torto (BUG #2)

---

### 2.3 `CriarCotasVerticais` ([DiagramaMontagemService.cs:563-661](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L563-L661))

**Estratégia:** `SpotElevation` em clusters de pontos Z.

**❌ BUGS IDENTIFICADOS:**

#### BUG #5 — `new Reference(refElem)` em FamilyInstance é proibido

```csharp
Element refElem = ... // encontrou um elemento que tem face nesse nivel Z
Reference refE = new Reference(refElem);
SpotDimension sd = doc.Create.NewSpotElevation(vista, refE, ...);
```

`new Reference(Element)` **só funciona pra Grids, Levels e ReferencePlanes**. Pra `FamilyInstance` precisa usar:
- `elem.GetReferenceByName("Top")` / `"Bottom"` / `"Center (Front/Back)"` etc, OU
- `elem.GetReferences(FamilyInstanceReferenceType.Top)` (Revit 2019+)

Resultado: ou throws `InvalidOperationException`, ou cria uma Reference inválida que o Revit aceita silenciosamente mas depois rejeita ao commitar.

#### BUG #6 — Coordenadas world-space erradas no SpotElevation

```csharp
double xDireita = bbVista.Max.X + ...;
XYZ pontoElbow = new XYZ(xDireita, 0, zCluster);
```

`bbVista.Max.X` vem do `BoundingBox` calculado com `vista.get_BoundingBox(null)` que retorna em coordenadas world (não-rotacionadas pra vista). Para Section Views rotacionadas (que é o caso quando você seleciona "Paralela ao eixo Y"), `xDireita` vai pra qualquer lugar.

**Por que isso quebra o SpotElevation:** o Revit valida que o `pontoCota` esteja na geometria da Reference. Como o ponto está num lugar absurdo, a validação falha.

#### BUG #7 — `Y=0` hardcoded é erro grave

```csharp
XYZ pontoElbow = new XYZ(xDireita, 0, zCluster);
```

Y=0 só funciona se o projeto inteiro está na origem 0,0. Em qualquer projeto real (galpão posicionado em coordenada cartográfica ou simplesmente longe da origem), o `SpotElevation` cai em local errado.

---

### 2.4 `CriarComprimentosIndividuais` ([DiagramaMontagemService.cs:787-881](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L787-L881))

**Esse provavelmente FUNCIONA** — usa o padrão validado `FamilyInstance.GetReferences(Left/Right)` + helper puro `DimensionPlanCalculator` (que tem 15 testes unitários verdes). Note que está **desligado por padrão** (`AdicionarComprimentosIndividuais = false`), então não foi essa parte que falhou no seu teste.

**Único cuidado:** ele faz `continue` silencioso quando a peça não tem refs Left/Right (perfis customizados podem não ter). Já tem Logger.Warn então não é blocker.

---

## 3. Resumo das causas raiz

| # | Caminho | Bug | Impacto |
|---|---------|-----|---------|
| #1 | CotasEntreEixos | Linha de cota usa `cropBox.Max.Y` (UV local) como coord world | Dimension com Line absurda → Revit rejeita |
| #2 | CotasEntreEixos | `ProjetarParaTopo` mistura coords local+world | Posicionamento errado |
| #3 | CotasEntreEixos | Não filtra Grids visíveis na vista | Reference rejeitada |
| #4 | CotasEntreEixos | Não verifica se Grid cruza o plano da seção | Reference inválida |
| #1' | CotaTotalConjunto | Mesmos #1 #2 #3 | Mesma falha |
| #5 | CotasVerticais | `new Reference(FamilyInstance)` é proibido | SpotElevation rejeitado |
| #6 | CotasVerticais | `bbVista.Max.X` em world-space pra vista rotacionada | Ponto fora da geometria |
| #7 | CotasVerticais | `Y=0` hardcoded | Falha em qualquer projeto fora da origem |

**Conclusão técnica:** as 3 funções de cota (`CotasEntreEixos`, `CotaTotalConjunto`, `CotasVerticais`) têm o mesmo padrão estrutural de erro — **misturam coordenadas world-space e view-space sem conversão**. O Revit detecta a inconsistência e abre o dialog "Excluir cotas" pra cada uma que ele não consegue commitar.

**Por que o try-catch não evitou o dialog:** os catches estão **dentro do `for` que cria cada cota individualmente**. Cada cota que falha gera UMA exception. Mas se o `NewDimension` consegue criar o objeto Dimension (sem throwar), e SÓ NO COMMIT da transaction o Revit detecta o constraint inválido, aí o dialog aparece — fora do try-catch do service.

---

## 4. Plano de Fix v2.8.8 — 5 ondas

### Onda 1 (CRÍTICO — 2h): Reescrever `CriarCotasEntreEixos` corretamente

**Approach correto:**

```csharp
// 1) Filtrar Grids visíveis na vista
var gridsNaVista = new FilteredElementCollector(doc, vista.Id)
    .OfClass(typeof(Grid))
    .Cast<Grid>()
    .Where(g => g.Curve != null)
    .ToList();

// 2) Pra cada Grid, calcular ponto de interseção com o plano da Section View
//    (usar vista.Origin + vista.ViewDirection como plano)
var gridsComProjecao = gridsNaVista
    .Select(g => new {
        Grid = g,
        PontoNaVista = ProjetarGridNoPlanoDaVista(g, vista)
    })
    .Where(x => x.PontoNaVista != null)
    .OrderBy(x => x.PontoNaVista.DotProduct(vista.RightDirection))
    .ToList();

// 3) Pra linha de cota: usar bbox dos próprios Grids (no plano da vista) + offset acima
double topYNaVista = gridsComProjecao.Max(x => x.PontoNaVista.DotProduct(vista.UpDirection));
double offsetMm = 1000.0; // 1m acima do topo dos Grids
double yLinhaCota = topYNaVista + UnitUtils.ConvertToInternalUnits(offsetMm, UnitTypeId.Millimeters);

// 4) Pra cada par consecutivo:
for (int i = 0; i < gridsComProjecao.Count - 1; i++) {
    XYZ p1Vista = gridsComProjecao[i].PontoNaVista;
    XYZ p2Vista = gridsComProjecao[i+1].PontoNaVista;

    // Substituir Y dos pontos pelo yLinhaCota (mantém X projetado)
    double x1 = p1Vista.DotProduct(vista.RightDirection);
    double x2 = p2Vista.DotProduct(vista.RightDirection);

    // Construir pontos em world-space:
    // origem (do plano da vista) + RightDirection * x + UpDirection * yLinhaCota
    XYZ p1 = vista.Origin + vista.RightDirection * x1 + vista.UpDirection * yLinhaCota;
    XYZ p2 = vista.Origin + vista.RightDirection * x2 + vista.UpDirection * yLinhaCota;

    Line linhaCota = Line.CreateBound(p1, p2);

    ReferenceArray refs = new ReferenceArray();
    refs.Append(new Reference(gridsComProjecao[i].Grid));
    refs.Append(new Reference(gridsComProjecao[i+1].Grid));

    Dimension dim = doc.Create.NewDimension(vista, linhaCota, refs);
}
```

**Helper novo a extrair em `DimensionPlanCalculator` (puro, testável):**

```csharp
/// <summary>
/// Projeta o ponto base de um Grid no plano de uma Section View.
/// Retorna null se o Grid for paralelo ao plano (não cruza).
/// </summary>
public static Vec3? ProjetarPontoNoPlano(Vec3 pontoGrid, Vec3 origemPlano, Vec3 normalPlano)
{
    // Distância signed do ponto ao plano
    double d = (pontoGrid - origemPlano).DotProduct(normalPlano);
    // Subtrai a componente normal pra projetar
    return pontoGrid - normalPlano * d;
}
```

### Onda 2 (CRÍTICO — 1h): Reescrever `CriarCotaTotalConjunto` reusando Onda 1

Mesma lógica de Onda 1 mas pegando apenas `gridsComProjecao.First()` e `gridsComProjecao.Last()`. Linha de cota fica 1m acima da linha das cotas entre eixos consecutivos (`yLinhaCota + 1000mm`).

### Onda 3 (CRÍTICO — 2h): Reescrever `CriarCotasVerticais` corretamente

**Approach correto:** usar `Reference` válida do elemento via `FamilyInstance.GetReferences(FamilyInstanceReferenceType.Top/Bottom)`.

```csharp
foreach (double zCluster in clusters) {
    // 1) Achar um FamilyInstance que tem topo/base nesse Z
    FamilyInstance refFI = elementos.OfType<FamilyInstance>()
        .Where(fi => {
            BoundingBoxXYZ bb = fi.get_BoundingBox(null);
            return bb != null && (
                Math.Abs(bb.Min.Z - zCluster) < tolFt ||
                Math.Abs(bb.Max.Z - zCluster) < tolFt);
        })
        .FirstOrDefault();

    if (refFI == null) continue;

    // 2) Escolher Top/Bottom baseado em qual extremo bate com zCluster
    BoundingBoxXYZ bbRef = refFI.get_BoundingBox(null);
    FamilyInstanceReferenceType tipoRef =
        Math.Abs(bbRef.Max.Z - zCluster) < tolFt
            ? FamilyInstanceReferenceType.Top
            : FamilyInstanceReferenceType.Bottom;

    IList<Reference> refs = refFI.GetReferences(tipoRef);
    if (refs == null || refs.Count == 0) {
        Logger.Debug("[DiagramaMontagem] SpotElevation: peca {Id} sem refs {Tipo}", refFI.Id, tipoRef);
        continue;
    }

    // 3) Calcular coordenadas em world-space corretas (usando bbox do CONJUNTO de elementos
    //    em world-space, não da vista)
    double xDireitaWorld = elementos.Max(e => {
        var bb = e.get_BoundingBox(null);
        return bb != null ? bb.Max.X : double.MinValue;
    }) + UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters);

    // Y do SpotElevation: usar o Y médio dos elementos
    double yMedio = elementos.Average(e => {
        var bb = e.get_BoundingBox(null);
        return bb != null ? (bb.Min.Y + bb.Max.Y) / 2.0 : 0.0;
    });

    XYZ pontoNaFace = new XYZ(xDireitaWorld - UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters),
                              yMedio, zCluster);
    XYZ pontoElbow = new XYZ(xDireitaWorld, yMedio, zCluster);
    XYZ pontoTexto = new XYZ(xDireitaWorld + UnitUtils.ConvertToInternalUnits(200, UnitTypeId.Millimeters),
                             yMedio, zCluster);

    SpotDimension sd = doc.Create.NewSpotElevation(
        vista, refs[0],
        pontoNaFace, pontoElbow, pontoTexto, pontoElbow,
        true /* hasLeader */);
}
```

### Onda 4 (DEFENSIVO — 1h): Guard rails + telemetria

Adicionar antes do `tx.Commit()` em cada transaction de cota:

```csharp
// Pre-commit validation: detectar se há dimensions com Reference inválida
//   na transaction. Se sim, faz Rollback silencioso em vez de Commit
//   (evita o dialog "Excluir cotas").
var failureHandler = new SuppressInvalidDimensionsHandler();
var failureOpts = tx4.GetFailureHandlingOptions();
failureOpts.SetFailuresPreprocessor(failureHandler);
failureOpts.SetForcedModalHandling(false);
failureOpts.SetClearAfterRollback(true);
tx4.SetFailureHandlingOptions(failureOpts);
tx4.Commit();
```

Onde `SuppressInvalidDimensionsHandler` implementa `IFailuresPreprocessor`:

```csharp
public FailureProcessingResult PreprocessFailures(FailuresAccessor a) {
    var fails = a.GetFailureMessages();
    foreach (var f in fails) {
        // BuiltInFailures.AnnotationFailures.DimensionsInconsistent é o
        // failure que abre o dialog "Excluir cotas". Suprimir + deletar
        // as dimensions ofensoras automaticamente.
        if (f.GetFailureDefinitionId() == BuiltInFailures.AnnotationFailures.DimensionsInconsistent) {
            var ids = f.GetFailingElementIds();
            Logger.Warn("[DiagramaMontagem] Suprimindo {N} cotas com refs inválidas (auto-cleanup)", ids.Count);
            foreach (var id in ids)
                a.DeleteElement(id);
            a.DeleteWarning(f);
            return FailureProcessingResult.ProceedWithCommit;
        }
    }
    return FailureProcessingResult.Continue;
}
```

Esse handler garante que **nunca mais** apareça o dialog modal pro usuário. Cotas inválidas que escaparam dos fixes das Ondas 1-3 são silenciosamente removidas + logadas.

### Onda 5 (TESTES — 1.5h): Cobertura nova em `DimensionPlanCalculator`

- Testes pra `ProjetarPontoNoPlano` (helper novo da Onda 1)
- Testes pra cenários de Grids ortogonais, oblíquos, paralelos à vista
- Testes pra cluster Z com elementos em níveis exatamente iguais

---

## 5. Estimativa total

| Onda | Esforço | Risco | Impacto |
|------|---------|-------|---------|
| 1 — Cotas entre eixos | 2h | Médio | Fix do bug principal |
| 2 — Cota total | 1h | Baixo | Reusa Onda 1 |
| 3 — Cotas verticais | 2h | Médio | Fix do SpotElevation |
| 4 — Failure handler | 1h | Baixo | Garantia "nunca mais aparecer dialog" |
| 5 — Testes | 1.5h | Baixo | Confiança + previne regressão |
| **TOTAL** | **7.5h** | | |

## 6. Como validar manualmente (após fix)

1. Selecionar 3-5 vigas/pilares no Revit
2. Executar Diagrama de Montagem com TODAS as opções marcadas:
   - ✅ Mostrar eixos
   - ✅ Cotas entre eixos
   - ✅ Tags com marca
   - ✅ Cotas verticais
   - ✅ Cota total
   - ✅ Mostrar Levels
   - ☐ Comprimentos individuais (já funciona)
3. **Critério de sucesso:**
   - Nenhum dialog "Excluir cotas" aparece
   - Vista gerada tem: eixos visíveis + cotas entre eixos consecutivos + cota total acima + spot elevations à direita + tags por peça
   - Log mostra `Cotas entre eixos: N`, `Cotas verticais: N`, `Cota total: SIM`, sem warnings

## 7. Workaround temporário (até v2.8.8 sair)

**Por enquanto, no Window:**
- ☐ **Desmarque** "Cotas entre eixos consecutivos"
- ☐ **Desmarque** "Cota total do conjunto"
- ☐ **Desmarque** "Cotas verticais"
- ✅ Mantenha "Mostrar eixos", "Tags com marca", "Mostrar Levels"

Vai ficar sem cotas mas pelo menos não trava com o dialog. As cotas você adiciona manualmente na vista gerada.

---

## 8. Next steps recomendados

1. **Ler este plano** e confirmar que faz sentido
2. **"OK, segue"** + escolher onda(s) — sugerido executar todas as 5 ondas como sprint v2.8.8 numa branch isolada
3. Após v2.8.8 mergeado: validar no Revit com seleção real (mesmo galpão do teste falho)
4. Se Onda 4 (failure handler) provar valor, replicar padrão em outros services que criam Dimension (Cotar Treliça, Auto-Vista, Cotar Peça Fabricação)
