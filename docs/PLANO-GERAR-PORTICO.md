# PLANO DE EXECUÇÃO — "Gerar Projeto Completo (Pórtico)"

> Documento-fonte para execução ponta-a-ponta via `/goal`.
> Função-chave do SteelBIM: a pessoa vai para uma planta de piso ou ambiente 3D,
> ativa a ferramenta, preenche UMA janela e, com **1 clique**, tem um galpão metálico
> modelado (pilares + treliça/viga + terças + contraventamentos + linha de corrente),
> seguindo o padrão do escritório EMT.

**Branch:** `claude/great-turing-Vqlig` · **Alvo:** .NET 8 / Revit 2025 API / WPF
**Status do plano:** APROVADO pelo usuário (Alef). Decisões D1–D7 fechadas (seção 2).

---

## 0. Princípio arquitetural (LEIA PRIMEIRO)

O levantamento do código provou que **não dá para "costurar" os serviços existentes**:

- ❌ Não existe serviço atômico de *Lançar Pilar/Viga* — o único criador de `StructuralType.Column`
  é `PipeRackService.CriarMembro` (privado).
- ❌ `TercasService`, `ContraventamentoPlanoService`, `TravamentoService` e `TrelicaService`
  são **interativos** — chamam `PickPoint`/`PickObjects`/`PickObject` **dentro** do `Executar`.
  Não podem ser orquestrados "no escuro" (headless).
- ✅ Só `PlacaBaseLancamentoService.Lancar(doc, config)` é headless de verdade.

**Decisão:** criar **um serviço novo, autocontido e headless** (`GerarPorticoService`) que
calcula TODA a geometria a partir do diálogo (núcleo puro) e cria tudo numa **única transação**
via `doc.Create.NewFamilyInstance(...)`, reusando o **padrão limpo** já validado em
`TrelicaService.CriarMembro` / `PipeRackService.CriarMembro`. Para a treliça (coração de cada
pórtico), aplicamos **um corte mínimo** no `TrelicaService` expondo a geração sem-pick — assim
reusamos 100% da lógica de duas-águas/cumeeira/padrões que já está validada.

**Toca-se em apenas 4 pontos do código existente** (seção 7). Todo o resto é arquivo novo.

---

## 1. Fluxo do usuário (resultado esperado)

1. Usuário abre uma planta ou vista 3D.
2. Clica no botão **"Projeto Completo"** (aba *SteelBIM | Modelagem*, painel *Estrutura Metálica*).
3. Abre a janela `GerarPorticoWindow` com todos os campos da seção 5.
4. Preenche e clica **"Gerar Pórtico Completo"**.
5. Em 1 transação, o galpão inteiro é modelado na origem `(0,0)` do nível ativo.
6. Diálogo de resumo informa quantos elementos foram criados.

---

## 2. Decisões fechadas (D1–D7)

| # | Decisão | Valor |
|---|---------|-------|
| D1 | "Tipos de treliça do template" | = enum **`TrussPattern`** (Warren, Pratt, Howe, Alternada, DiagonalDireita, DiagonalEsquerda, EmX, SoMontantes). Já existe em `Models/TrussPattern.cs`. |
| D2 | Banzo superior ≠ banzo inferior | **Estender `TrelicaConfig`** com `SymbolBanzoSuperior?`/`SymbolBanzoInferior?` (se nulos → fallback no `SymbolBanzo` atual). |
| D3 | Origem do galpão | **`(0,0,0)` no nível ativo**, eixo +X = comprimento (pórticos), +Y = vão (largura), +Z = altura. Sem pick. |
| D4 | Altura | `AlturaPilarMm` (beiral) + `AlturaExtremidadeMm` (H) + `AlturaCentralMm` (B) da treliça (duas águas quando B>H). |
| D5 | Eixos (grid) | Criar opcional via checkbox "Criar eixos" (`Grid.Create`). Letras A,B,C… nos X; números 1,2 nos Y. |
| D6 | Placa de base | Opcional (Onda F). Pré-requisitos: pilar com material "aço/steel", apoio de concreto, família work-plane-based. |
| D7 | Posição no ribbon | Aba *SteelBIM \| Modelagem*, painel **`panelEstruturaMetalica`**, botão grande logo após `btnGerarTrelica`. |

---

## 3. Inventário de REÚSO (fatos do código — não re-descobrir)

### 3.1 Helpers utilitários — `SteelBIM/Utils/RevitUtils.cs`
```
public const double FT_PER_MM = 0.00328083989501312;   // mm -> pés
public const double EPS       = 1e-9;
public static Level GetElementLevel(Document doc, Element el);
public static void  SetZJustification(FamilyInstance fi, int zJustificationValue);
public static void  SetYJustification(FamilyInstance fi, int yJustificationValue);
public static void  SetYZOffsets(FamilyInstance fi, double y, double z);
public static void  SetSectionRotation(FamilyInstance fi, double angleRad);
public static void  DisallowJoins(FamilyInstance fi);
```

### 3.2 Padrão "membro reto entre 2 pontos" (Beam) — `TrelicaService.CriarMembro` (privado, ~linha 351)
```
Line line = Line.CreateBound(inicio, fim);                       // coords em PÉS
FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
// pós: SetZJustification + SetYZOffsets + DisallowJoins; guard distancia < EPS
```

### 3.3 Padrão de PILAR (Column) — `PipeRackService.CriarMembro` / `ConfigurarExtremosColuna` (~linhas 293–368)
```
bool ehColuna = symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
StructuralType tipo = ehColuna ? StructuralType.Column : StructuralType.Beam;
FamilyInstance fi = doc.Create.NewFamilyInstance(Line.CreateBound(p0,p1), symbol, level, tipo);
// ConfigurarExtremosColuna: seta FAMILY_BASE_LEVEL_PARAM, FAMILY_TOP_LEVEL_PARAM,
// offsets de base/topo e INSTANCE_LENGTH_PARAM. Ativar símbolo antes (if (!IsActive) Activate()).
```

### 3.4 Treliça — `SteelBIM/Services/TrelicaService.cs`
- `public void Executar(UIDocument uidoc, Document doc, TrelicaConfig config)` (linha 26) — **interativo**.
- Bifurca em `ExecutarTrelicaCompleta` (quando `config.TrelicaCompleta`) ou `ExecutarEntreBanzos`.
- `ExecutarTrelicaCompleta`: `PickObject` de 1 linha → `RevitUtils.GetElementCurve` → abre transação
  `"Criar Treliça completa"` → calcula banzo superior por `TrelicaPatternBuilder.AlturaNaPosicao(t, H, B)`
  (interpolação H→B→H) → chama `GerarVao(...)`.
- **`private int GerarVao(Document doc, Level nivel, Curve? cSup, Curve cInf, bool incluirBanzos,
  TrelicaConfig config, double zOffsetFt)`** (linha 184) — gera banzos+montantes+diagonais. Hoje usa
  `config.SymbolBanzo` para AMBOS os banzos.
- Helper puro de espaçamento de estações: `Views/Helpers/TercasSpacingCalculator.cs` (`FtPerCm = 1.0/30.48`).
- Helper puro de topologia/altura: `Services/Trelica/TrelicaPatternBuilder.cs` (`AlturaNaPosicao(t,H,B)`).

### 3.5 `TrelicaConfig` — `SteelBIM/Models/TrelicaConfig.cs` (propriedades relevantes)
```
FamilySymbol? SymbolMontante, SymbolDiagonal, SymbolBanzo
bool   LancarMontante, LancarDiagonal
int    Quantidade                 // subdivisões intermediárias (modo Uniforme)
TrussSpacingMode ModoEspacamento  // default Uniforme
List<double> EspacamentosCm
TrussPattern Padrao               // default Warren
bool   MontantesIntermediarios, MontantesExtremidade(=true), DiagonaisExtremidade(=true)
bool   TrelicaCompleta            // true = gera banzos+montantes+diagonais de 1 linha-base
double AlturaExtremidadeMm        // H (apoio)
double AlturaCentralMm            // B (cumeeira); B>H => duas águas
int    ZJustificationValue; double ZOffsetMm; bool InverterSentido
```

### 3.6 Placa de base — `SteelBIM/Services/Conexoes/PlacaBaseLancamentoService.cs`
- `public PlacaBaseLancamentoResultado Lancar(Document doc, PlacaBaseConfig config)` — **headless**, abre
  a própria transação, descobre pilares de aço + apoios de concreto sozinho.
- `public static IList<FamilySymbol> CollectCompatibleSymbols(Document doc)`.
- `PlacaBaseConfig`: `FamilyName, TypeName, FamilySymbolId, ComprimentoMm(300), LarguraMm(300),
  EspessuraMm(20), FuroDiametroMm(22), FuroOffsetXmm(80), FuroOffsetYmm(80), ChumbadorDiametroMm(19),
  GrauteEspessuraMm(30), Solda("6 mm"), SobrescreverParametros(true)`.
- Exige família **work-plane-based** com parâmetros PT-BR.

### 3.7 Seleção de perfil na UI (padrão cascata) — `SteelBIM/Forms/UiItems.cs`
```
public class SymbolItem { public FamilySymbol Symbol; public string Text /* "Familia : Tipo" */; }
```
Padrão (ex.: `TrelicaWindow`, `TercasWindow`): `cmbFamilia` (nomes de família distintos) → `cmbPerfil`
(`SymbolItem` dos `FamilySymbol` daquela família). O Command coleta `List<FamilySymbol>` por categoria
ANTES de abrir a janela e passa no construtor. A config guarda o **objeto `FamilySymbol`** (não nome/Id).
```
new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_StructuralColumns /* pilar */ | OST_StructuralFraming /* resto */)
    .Cast<FamilySymbol>().OrderBy(x => x.FamilyName).ThenBy(x => x.Name).ToList();
```

### 3.8 Ribbon — `SteelBIM/App.cs` (painel já existe, linha 248)
```
RibbonPanel panelEstruturaMetalica = GetOrCreatePanel(application, tabName, "Estrutura Metálica");
AddButton(panel, internalName, buttonText, assemblyPath, className, tooltip, largeImg, smallImg);
// Exemplo real (btnGerarTrelica, linha 320): icones "trelica_large.png"/"trelica_small.png".
```

### 3.9 Projeto de testes — `SteelBIM.Tests/SteelBIM.Tests.csproj`
Fonte PURA é **linkada** (não há ProjectReference): `<Compile Include="..\SteelBIM\Services\...\X.cs" />`.
→ O calculator puro novo PRECISA ser adicionado lá para ser testado.

### 3.10 Padrão EMT (template `docs/reference-projects/galpao-padrao-emt/`)
- Grid **A–G × 1–2**; **N pórticos × 5000 mm = comprimento**; **vão transversal 15010 mm = largura**.
- Perfis: pilar `2U300x100x25x3.00`; banzo `U150x65x4,76`; diagonal/montante `L38x38x3,2` (dupla);
  terça `UE150X60X20X2,00`; contrav. `BARRA REDONDA 10mm`.
- Nomenclatura: `Pnn`, `TRELIÇA NN - <perfil>`, `TERÇAS NN - <perfil>`, `CONTRAVENTAMENTO BARRA 10mm TIP`,
  `LINHAS DE CORRENTE`. Altura total ~`5077 mm` (beiral + B).

---

## 4. Arquivos NOVOS

| Arquivo | Papel | Testável |
|---------|-------|----------|
| `SteelBIM/Models/GerarPorticoConfig.cs` | DTO Revit (números + `FamilySymbol`s + flags) | — |
| `SteelBIM/Services/Portico/PorticoGeometriaCalculator.cs` | **PURO**: tipos `Ponto3D`/`Segmento`/`PorticoLayout` + `GerarPorticoEntrada` (números) + `Calcular(...)`. Zero tipos Revit. | ✅ xUnit |
| `SteelBIM/Services/Portico/GerarPorticoService.cs` | Revit-bound: 1 transação, cria tudo | parcial |
| `SteelBIM/Views/GerarPorticoWindow.xaml` + `.xaml.cs` | A janela da seção 5 | — |
| `SteelBIM/Commands/CmdGerarPorticoCompleto.cs` | Carrega símbolos (2 categorias), abre janela, chama serviço | — |
| `SteelBIM.Tests/Services/Portico/PorticoGeometriaCalculatorTests.cs` | Testa o calculator | ✅ |

---

## 5. A janela — `GerarPorticoWindow` (campo a campo)

`DockPanel` + `ScrollViewer` (muitos campos). `GroupBox`es. Cada seletor de perfil = cascata
**Família → Perfil** (wrapper `SymbolItem`). Pilar usa `OST_StructuralColumns`; o resto `OST_StructuralFraming`.

**▸ Geometria do galpão**
- Nº de pórticos **(N)** — default **7**
- Espaçamento entre pórticos (mm) — default **5000** → readout: `Comprimento = (N−1)×esp`
- Vão do galpão / largura (mm) — default **15010**
- Altura do pilar / beiral (mm) — default **4000**

**▸ Pilares**
- Perfil do pilar — cascata (`OST_StructuralColumns`). Tentar pré-selecionar família contendo "2U300".

**▸ Cobertura** — `( • ) Treliça   ( ) Viga metálica`
- **Treliça:** Tipo (`ComboBox` = `TrussPattern`); H = Altura extremidade (mm, default 600);
  B = Altura central (mm, default 1600); Nº de divisões/montantes (default 8);
  4 cascatas: **banzo superior**, **banzo inferior**, **diagonal**, **montante**.
- **Viga:** Perfil da viga (cascata) + Altura da cumeeira (mm, default 1500).

**▸ Terças** — ☑ Lançar terças · Perfil (cascata) · Espaçamento entre terças (mm, default 1500)

**▸ Contraventamentos** — ☑ Plano da cobertura → Perfil · ☑ Plano dos pilares → Perfil

**▸ Linha de corrente** — ☑ Lançar → Perfil

**▸ Extras** — ☑ Criar eixos (grid) · ☑ Lançar placas de base

**Rodapé:** `Cancelar` · `Gerar Pórtico Completo`.
**Validação no OK:** N ≥ 2, esp > 0, vão > 0, pilar selecionado; se treliça, os 4 perfis selecionados;
se viga, perfil da viga selecionado; cada seção opcional marcada exige seu perfil.
Parsing numérico via `NumberParsing.ParseDoubleOrDefault` (já existe no projeto).
Persistir últimas escolhas em `AppSettings` é desejável mas **opcional** (não bloquear a Onda E).

---

## 6. Núcleo puro — `PorticoGeometriaCalculator`

Sistema de coordenadas: **X** longitudinal (comprimento), **Y** transversal (vão/largura), **Z** vertical.
Tudo em **mm**. Conversão para pés só no serviço (`RevitUtils.FT_PER_MM`).

### 6.1 Tipos (records puros — sem Revit)
```csharp
public readonly record struct Ponto3D(double XMm, double YMm, double ZMm);
public readonly record struct Segmento(Ponto3D A, Ponto3D B);

public sealed class GerarPorticoEntrada   // só números/flags (sem FamilySymbol)
{
    public int    NumeroPorticos;          // N
    public double EspacamentoPorticosMm;   // S
    public double VaoGalpaoMm;             // W (largura/vão)
    public double AlturaPilarMm;           // Hp (beiral)
    public bool   UsarTrelica;
    public double AlturaExtremidadeMm;     // H (treliça)
    public double AlturaCentralMm;         // B (treliça)
    public double AlturaCumeeiraMm;        // Cum (viga)
    public bool   LancarTercas;   public double EspacamentoTercasMm;
    public bool   ContravCobertura, ContravPilares, LancarLinhaCorrente;
}

public sealed record PorticoLayout(
    IReadOnlyList<Segmento> Pilares,
    IReadOnlyList<Segmento> EixosInferioresTrelica, // 1 por pórtico (banzo inferior, modo treliça)
    IReadOnlyList<Segmento> Vigas,                  // 2 águas por pórtico (modo viga)
    IReadOnlyList<Segmento> Tercas,
    IReadOnlyList<Segmento> ContravCobertura,
    IReadOnlyList<Segmento> ContravPilares,
    IReadOnlyList<Segmento> LinhasCorrente,
    IReadOnlyList<double> XPorticosMm,              // estações dos pórticos
    IReadOnlyList<double> YEixosMm);                // {0, W}
```

### 6.2 Fórmulas (entrada → `PorticoLayout`)
Sejam `x_i = i*S` (i=0..N-1), `L = (N-1)*S`. Altura do topo da água em função de Y:
```
zTopo(y): se treliça -> Hp + H + (B-H) * (yEspelhado / (W/2))   // yEspelhado = min(y, W-y)
          se viga    -> Hp + Cum * (yEspelhado / (W/2))
// no apoio (y=0 ou y=W): treliça=Hp+H, viga=Hp ; na cumeeira (y=W/2): treliça=Hp+B, viga=Hp+Cum
```

- **Pilares** (2 por pórtico): `(x_i,0,0)→(x_i,0,Hp)` e `(x_i,W,0)→(x_i,W,Hp)`.
- **EixosInferioresTrelica** (modo treliça): `(x_i,0,Hp)→(x_i,W,Hp)`.
- **Vigas** (modo viga): `(x_i,0,Hp)→R` e `R→(x_i,W,Hp)`, com `R=(x_i, W/2, Hp+Cum)`.
- **Terças** (distribuídas por comprimento de água `Et=EspacamentoTercasMm`):
  - Água 1 (y:0→W/2): `rise = zTopo(W/2)-zTopo(0)`; `Ls = sqrt((W/2)^2 + rise^2)`;
    `n = max(1, round(Ls/Et))`; para `j=0..n`: `f=j/n`, `y=f*(W/2)`, `z=zTopo(y)`;
    terça longitudinal `(0,y,z)→(L,y,z)`.
  - Água 2 (espelho): `y'=W - y`; gerar para `j=1..n` (pula `j=0` para não duplicar a cumeeira).
- **ContravCobertura** (X nos vãos de extremidade, no plano das águas): vãos `{0, N-2}` (se N≥3;
  se N=2 só o vão 0). Para cada vão (a=i, b=i+1) e cada água: nó de apoio
  `Ne=(0|W, zTopo(0|W))`, nó de cumeeira `Nc=(W/2, zTopo(W/2))`; X =
  `(x_a,Ne)→(x_b,Nc)` e `(x_b,Ne)→(x_a,Nc)`.
- **ContravPilares** (X vertical nas paredes y∈{0,W}, vãos de extremidade): para cada vão (a,b) e parede y:
  `(x_a,y,0)→(x_b,y,Hp)` e `(x_b,y,0)→(x_a,y,Hp)`.
- **LinhasCorrente** (tirantes longitudinais): na cumeeira `(0,W/2,zTopo(W/2))→(L,W/2,zTopo(W/2))`
  e meia-água `y=W/4` e `y=3W/4` (z=`zTopo(y)`), cada um `(0,y,z)→(L,y,z)`.
  *(Interpretação default; ajustável após ver no Revit.)*

Geração condicionada às flags (`LancarTercas`, `ContravCobertura`, etc.). N=2 ⇒ 1 vão (todo de extremidade).

---

## 7. Cortes cirúrgicos no código EXISTENTE (apenas 4)

### C1 — `TrelicaService.cs`: expor geração sem-pick/sem-transação
Extrair o corpo pós-pick e dentro-da-transação de `ExecutarTrelicaCompleta` para:
```csharp
public int GerarTrelicaCompletaNoEixo(Document doc, Level nivel, Curve eixoInferior, TrelicaConfig config)
```
- Ativa os símbolos (se já não ativados pelo chamador), calcula o banzo superior por `AlturaNaPosicao(t,H,B)`,
  chama `GerarVao(doc, nivel, cSup, eixoInferior, incluirBanzos:true, config, zOffsetFt)`, retorna nº de membros.
- **NÃO abre transação** (assume transação do chamador aberta) e **não faz pick**.
- `ExecutarTrelicaCompleta` passa a: `PickObject` → curva → `using (Transaction…)` → `GerarTrelicaCompletaNoEixo` → commit → diálogo.
- **Obrigatório:** ler o arquivo e manter o caminho interativo atual **idêntico** (refator de extração, não reescrita).

### C2 — `TrelicaConfig.cs`: banzo superior/inferior distintos
Adicionar `public FamilySymbol? SymbolBanzoSuperior { get; set; }` e `public FamilySymbol? SymbolBanzoInferior { get; set; }`.
Em `GerarVao`, onde os banzos são criados: topo → `config.SymbolBanzoSuperior ?? config.SymbolBanzo`;
inferior → `config.SymbolBanzoInferior ?? config.SymbolBanzo`. Retrocompatível (nulos caem no comportamento atual).

### C3 — `App.cs`: registrar o botão
Após o bloco `btnGerarTrelica` (linha ~329), adicionar:
```csharp
AddButton(
    panelEstruturaMetalica,
    "btnGerarPorticoCompleto",
    "Projeto\nCompleto",
    assemblyPath,
    "SteelBIM.Commands.CmdGerarPorticoCompleto",
    "Gera um galpão completo (pilares, treliça/viga, terças, contraventamentos e linha de corrente) a partir de uma janela, com 1 clique.",
    "trelica_large.png",   // reusar ícone existente (evita asset faltando no build)
    "trelica_small.png"
);
```

### C4 — `SteelBIM.Tests.csproj`: linkar o calculator puro
Adicionar (mesmo formato dos itens vizinhos):
```xml
<Compile Include="..\SteelBIM\Services\Portico\PorticoGeometriaCalculator.cs" />
```

---

## 8. `GerarPorticoService` — ordem de orquestração

`public void Executar(UIDocument uidoc, GerarPorticoConfig config)`:
1. `Document doc = uidoc.Document;` Resolver nível: `Level nivel = doc.ActiveView.GenLevel ?? <primeiro Level por elevação>`; se nenhum, `AppDialogService.ShowError` e sai.
2. Mapear `config` → `GerarPorticoEntrada` (só números). `var layout = PorticoGeometriaCalculator.Calcular(entrada);`
3. Ativar todos os símbolos usados (`if (!s.IsActive) s.Activate();`) e `doc.Regenerate()` uma vez.
4. `using (Transaction t = new Transaction(doc, "Gerar Pórtico Completo")) { t.Start();`
   - **Pilares:** cada `Segmento` → `CriarPilar(doc, config.SymbolPilar, nivel, A, B)`.
   - **Cobertura:** se `UsarTrelica`, para cada `EixoInferiorTrelica` → `Line` (pés) →
     `tc = MapearTrelicaConfig(config)` (popula `TrelicaCompleta=true`, H/B, `Padrao`, banzos sup/inf, diagonal, montante,
     `LancarMontante/Diagonal=true`, `Quantidade`) → `_trelicaService.GerarTrelicaCompletaNoEixo(doc, nivel, line, tc)`.
     Senão (viga), cada `Viga` → `CriarBarra(Beam, config.SymbolViga)`.
   - **Terças / ContravCobertura / ContravPilares / LinhasCorrente:** cada `Segmento` → `CriarBarra(Beam, símbolo correspondente)`.
   - **Eixos (opcional):** se `CriarEixos`, `Grid.Create(doc, Line)` por `x_i` (letras) e `y∈{0,W}` (números).
   - `t.Commit(); }`
5. **Placa de base (opcional, Onda F):** após o commit, `new PlacaBaseLancamentoService().Lancar(doc, placaConfig)`
   só se `config.LancarPlacasBase`. Guardar resultado e incluir no resumo. Avisar pré-requisitos se 0 inseridas.
6. `AppDialogService.ShowInfo("Gerar Pórtico Completo", resumo, ...)` com as contagens.

**Conversão mm→XYZ:** `XYZ P(Ponto3D p) => new XYZ(p.XMm*FT, p.YMm*FT, nivel.Elevation + p.ZMm*FT);` com `FT=RevitUtils.FT_PER_MM`.

**`CriarBarra` (Beam)** — espelho de `TrelicaService.CriarMembro`:
```csharp
private static void CriarBarra(Document doc, FamilySymbol? symbol, Level nivel, XYZ a, XYZ b, List<ElementId> criados)
{
    if (symbol == null || a.DistanceTo(b) < RevitUtils.EPS)
    {
        return;
    }
    Line line = Line.CreateBound(a, b);
    FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
    RevitUtils.DisallowJoins(fi);
    criados.Add(fi.Id);
}
```

**`CriarPilar` (Column)** — espelho de `PipeRackService.CriarMembro`/`ConfigurarExtremosColuna`:
```csharp
private static void CriarPilar(Document doc, FamilySymbol? symbol, Level nivel, XYZ baseP, XYZ topo, List<ElementId> criados)
{
    if (symbol == null || baseP.DistanceTo(topo) < RevitUtils.EPS)
    {
        return;
    }
    bool ehColuna = symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
    StructuralType tipo = ehColuna ? StructuralType.Column : StructuralType.Beam;
    FamilyInstance fi = doc.Create.NewFamilyInstance(Line.CreateBound(baseP, topo), symbol, nivel, tipo);
    // mirror ConfigurarExtremosColuna: FAMILY_BASE_LEVEL_PARAM/FAMILY_TOP_LEVEL_PARAM = nivel,
    // base offset = 0, top offset = (topo.Z - baseP.Z). Ler PipeRackService para fidelidade.
    criados.Add(fi.Id);
}
```

---

## 9. Ondas de execução (commit por onda)

> Regras git: desenvolver/push só em `claude/great-turing-Vqlig`; `git push -u origin <branch>` com retry
> exponencial (2s,4s,8s,16s) só em erro de rede; após push, garantir **PR draft**; NÃO colocar
> identificador de modelo em commits/PR/código.

| Onda | Escopo | Commit (sugestão) |
|------|--------|-------------------|
| **A** | `GerarPorticoConfig` + `PorticoGeometriaCalculator` (puro) + `GerarPorticoEntrada`/records + **testes xUnit** + link no `.csproj`. Verde antes de tocar Revit. | `feat(portico): nucleo puro PorticoGeometriaCalculator + testes (Onda A)` |
| **B** | C1 (refator `TrelicaService` → `GerarTrelicaCompletaNoEixo`) + C2 (`TrelicaConfig` banzo sup/inf). Caminho interativo idêntico. | `refactor(trelica): entry sem-pick + banzo sup/inf distintos (Onda B)` |
| **C** | `GerarPorticoService`: pilares + treliça/viga em 1 transação (MVP visível). | `feat(portico): GerarPorticoService cria pilares + cobertura (Onda C)` |
| **D** | Terças + contraventamentos (cobertura/pilares) + linha de corrente (headless). | `feat(portico): tercas, contraventamentos e linha de corrente (Onda D)` |
| **E** | `GerarPorticoWindow` (.xaml/.cs) + `CmdGerarPorticoCompleto` + C3 (ribbon) + eixos opcionais. | `feat(portico): janela + comando + botao no ribbon (Onda E)` |
| **F** | (opcional) Placa de base via `PlacaBaseLancamentoService.Lancar` + nomenclatura Mark (P01…, TRELIÇA 01…). | `feat(portico): placas de base e marcacao opcionais (Onda F)` |

---

## 10. Testes (`PorticoGeometriaCalculatorTests`)

- N=7, S=5000 ⇒ `XPorticosMm = {0,5000,…,30000}` e `L=30000`.
- Pilares: `2*N` segmentos; cada um de z=0 a z=Hp; pés em `(x_i,0)` e `(x_i,W)`.
- EixosInferioresTrelica: `N` segmentos horizontais em z=Hp (modo treliça).
- Vigas (modo viga): `2*N`; ápice em `(x_i, W/2, Hp+Cum)`.
- `zTopo`: na cumeeira = Hp+B (treliça) / Hp+Cum (viga); no apoio = Hp+H / Hp; simetria `zTopo(y)==zTopo(W-y)`.
- Terças: simétricas em torno de W/2; cumeeira contada **uma** vez; todas longitudinais (`A.X=0`, `B.X=L`, `A.Y==B.Y`, `A.Z==B.Z`).
- ContravCobertura/Pilares: presentes só nos vãos de extremidade; vazio se N=2 e flag off; 2 diagonais por X.
- Flags off ⇒ listas correspondentes vazias.
- Borda: N=2 (1 vão), W ímpar (15010), B==H (águas paralelas — sem pico), divisões=1.

---

## 11. Regras de formatação / CI (OBRIGATÓRIO)

- Todos os `.cs` com **BOM UTF-8** e `#nullable enable` no topo.
- **Sem control-flow de uma linha**: `if (x) return;` é proibido — usar bloco com chaves.
- Evitar `case X: {` (regra de indentação) — usar `case X:` com statements ou if-blocks.
- Sem trailing whitespace; quebras **LF**; newline final.
- `dotnet format --verify-no-changes` deve passar (validar por scan/bash; não há `dotnet` local).
- **TreatWarningsAsErrors** no Release ⇒ **0 warnings**.
- O calculator puro **não pode** referenciar tipos Revit (senão quebra o build do projeto de testes).
- Identificadores PT-BR sem acentos; logging via `SteelBIM.Infrastructure.Logger`; diálogos via `AppDialogService`.

---

## 12. Riscos & mitigação

| Risco | Mitigação |
|-------|-----------|
| Refator C1 quebrar a treliça que funciona | Extração (mover corpo), não reescrita; manter `Executar` como casca; revisar diff linha a linha |
| Pilar line-based não setar topo certo | Espelhar fielmente `ConfigurarExtremosColuna` do `PipeRackService` |
| Placa de base não inserir (sem apoio/família) | Onda F opcional + aviso de pré-requisitos; não bloquear o galpão |
| Serviços Revit não entram nos testes | Núcleo crítico é o calculator puro (testado); validar serviços pelo build Release no CI |
| Família de pilar incoerente | Usar o `FamilySymbol` escolhido pelo usuário; não inventar perfil |

---

## 13. Definition of Done

- [ ] Build Release **0 warnings** (TreatWarningsAsErrors).
- [ ] `SteelBIM.Tests` verde (incl. `PorticoGeometriaCalculatorTests`).
- [ ] `dotnet format` limpo · gitleaks/Secret Guard limpos.
- [ ] Botão "Projeto Completo" aparece no painel *Estrutura Metálica*.
- [ ] Caminho interativo do `TrelicaService` continua idêntico (sem regressão).
- [ ] PR draft aberto/atualizado no branch; CHANGELOG anotado (v2.8.x).
- [ ] Validação manual no Revit (pelo usuário): planta/3D → janela → 1 clique → galpão.

---

## 14. Como executar com `/goal`

```
/goal Execute o plano docs/PLANO-GERAR-PORTICO.md de ponta a ponta, Ondas A→F, de forma
cirurgica e 100% precisa. Faça commit por onda no branch claude/great-turing-Vqlig, mantenha
o CI verde (format, build Release 0 warnings, testes, gitleaks) e mantenha o PR draft atualizado.
Não toque em outros branches. Antes de codar, releia a secao 3 (fatos do codigo) e a secao 11
(regras de formatacao). A Onda F (placa de base) e opcional — se houver risco, deixe atras de flag.
```
