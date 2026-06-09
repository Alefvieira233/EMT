# PLANO V3 — Lapidação 2 do "Gerar Projeto Completo (Pórtico)"

> Pós-validação no Revit da V2 (build 2.8.14). Três ajustes (C1–C3). Branch
> `claude/great-turing-Vqlig`. Regras de sempre: BOM UTF-8 nos `.cs`, LF, sem control-flow de
> uma linha, `using` System primeiro, `dotnet format` limpo, Release 0 warnings, `x:Name` 1-a-1.

---

## C1 — Contraventamento de cobertura = "1 X a cada N terças" (corrige o X gigante)

**Problema:** hoje cada vão contraventado recebe **1 X gigante** por água (canto a canto,
beiral→cumeeira). No projeto real põe-se **um X a cada 2–3 terças**.

**Decisão (confirmada pelo usuário):** os X da cobertura são **ancorados nas terças** — um X a
cada `TercasPorXCobertura` espaços de terça, ladrilhando a água, nos **vãos de extremidade**.
Cada X liga nós de terça de pórticos adjacentes, no **plano do banzo** (`ZTopo`, sem elevação).

- Renomear `GerarPorticoEntrada.NumeroXCobertura` → **`TercasPorXCobertura`** (int, default **2**).
  Idem em `GerarPorticoConfig`. `MapearEntrada` repassa.
- No calculator, substituir o bloco de cobertura por: para cada vão de extremidade
  (`VaosExtremidade(n)` = {0, n-2}), em cada água, andar a lista de posições de terça
  (`PosicoesTercasMeiaAgua`) em passos de `TercasPorXCobertura` e criar um X por trecho
  (`AddXsEntrePurlins`). Guard: precisa de `EspacamentoTercasMm > 0` (senão pula).
- **Pilares: inalterado** — `NumeroXPilares` continua = nº de vãos com X vertical
  (`DistribuirVaos`, default 2). Bracing de parede 1 X por vão entre colunas já é o correto.

Helpers novos no calculator (puros):
```csharp
private static IReadOnlyList<int> VaosExtremidade(int n) // {0} e {n-2} quando distintos
private static void AddXsEntrePurlins(List<Segmento> dest, GerarPorticoEntrada e, double hp,
    double w, double xa, double xb, IReadOnlyList<double> purlinsMeia, int passo, bool espelhar)
// anda purlinsMeia em passos de 'passo'; X entre purlin[j] e purlin[min(j+passo, last)];
// espelhar=true => y' = w - y (agua 2). z = ZTopo(y) (plano do banzo).
```

## C2 — Nº de linhas de corrente configurável

**Hoje:** uma linha de corrente no meio de **todos** os vãos. **Novo:** campo
`NumeroLinhasCorrente` (int, default **3**). Distribui N fileiras nos vãos via
`DistribuirVaos(n-1, NumeroLinhasCorrente)`; cada fileira = 2 sag-rods (água1 + água2), no meio do
vão, no nível das terças (`ZTopo + ElevacaoTercasMm`). Adicionar em `GerarPorticoEntrada` +
`GerarPorticoConfig` + `MapearEntrada` + janela + `BuildConfig`.

## C3 — Ligação de terça opcional (reuso do `ConexaoTercasService`)

Investigação confirmou: `ConexaoTercasService.Executar(uidoc, doc, ConexaoTercasConfig, IList<Reference> tercas)`
é **headless** (não usa `uidoc`, acha interseções terça×viga via `ConexaoTercasGeometry.IntersectXY`,
abre **transação própria** e mostra um popup no fim). A família é **face-based** (hospeda na alma da
terça); o filtro de famílias é `Category.Name.Contains("onex")`.

Plano:
- Janela: no painel **Terças**, checkbox **"Inserir ligação de terça"** + combo de família
  (popular com `FamilySymbol` cujo `Category.Name` contém "onex").
- `GerarPorticoConfig`: `bool InserirLigacaoTerca` + `FamilySymbol? SymbolLigacaoTerca`.
- `GerarPorticoService`: rastrear os `ElementId` criados:
  - terças → coletar no laço de terças (CriarBarra devolve o `FamilyInstance`);
  - banzo superior → `GerarTrelicaCompletaNoEixo` ganha um coletor opcional
    `ICollection<ElementId>? banzosSuperiores`; em `GerarVao`, ao criar banzo com
    `Chord==Superior`, adiciona o `Id`. (CriarMembro passa a devolver `FamilySymbol`/`FamilyInstance`
    ou usa coletor — manter caminho interativo intacto.)
  - **Após** commitar a transação principal, montar `List<Reference>` (terças e banzos sup via
    `new Reference(el)`), montar `ConexaoTercasConfig { SymbolSelecionado=..., ColocarExtremidades=true,
    Referencia=Cruzamento, VigasRefs=banzosSupRefs }` e chamar `ConexaoTercasService.Executar(...)`.
    Guardar contagem pro resumo. Tudo atrás do checkbox (default off) e em try/catch.

---

## Ondas

- **K** (puro+testes): C1 (contrav cobertura "a cada N terças" + rename) + C2 (nº linhas de corrente) no calculator + entrada + testes.
- **L** (UI): relabel cobertura ("X a cada N terças"), pilares ("Nº vãos"), campo nº linhas de corrente; `GerarPorticoConfig` + `MapearEntrada`.
- **M** (ligação terça): checkbox + combo "onex"; coleta de IDs (terça + banzo sup) e chamada do `ConexaoTercasService` headless.
- **N** (fechamento): bump 2.8.15, CHANGELOG, CI verde, PR #71.

## Aceite
- [ ] Release 0 warnings, testes verdes, format limpo, gitleaks ok.
- [ ] Contraventamento da cobertura = vários X (1 a cada N terças), não mais 1 gigante.
- [ ] Nº de linhas de corrente respeitado.
- [ ] Ligação de terça inserida nos cruzamentos (atrás de flag), sem quebrar a geração.
- [ ] Caminho interativo do `TrelicaService`/`ConexaoTercasService` sem regressão.
