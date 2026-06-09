# PLANO V2 — Lapidação do "Gerar Projeto Completo (Pórtico)"

> Refinamentos pós-validação no Revit (galpão saiu correto em 2026-06-03). Quatro ajustes
> cirúrgicos (R1–R4) sobre a feature já mesclável (PR #71). Branch: `claude/great-turing-Vqlig`.
> Execução via `/goal`. Antes de codar, reler a **seção 3 do `docs/PLANO-GERAR-PORTICO.md`**
> (fatos do código) e a **seção 11** (regras de formatação: BOM UTF-8 nos `.cs`, LF, sem
> control-flow de uma linha, `using` System primeiro, `dotnet format` limpo, Release 0 warnings).

---

## Contexto / arquivos que serão tocados

- `SteelBIM/Services/Portico/PorticoGeometriaCalculator.cs` (PURO) — R1, R2, R3
- `SteelBIM/Models/GerarPorticoConfig.cs` — campos novos (R2, R3, R4)
- `SteelBIM/Services/Portico/GerarPorticoService.cs` — `MapearEntrada` (R2,R3) e `MapearTrelicaConfig` (R4)
- `SteelBIM/Models/TrelicaConfig.cs` — campos de rotação (R4)
- `SteelBIM/Services/TrelicaService.cs` — `GerarVao` + `CriarMembro` aplicam rotação por tipo (R4)
- `SteelBIM/Views/GerarPorticoWindow.xaml` + `.xaml.cs` — campos novos (R2, R3, R4)
- `SteelBIM.Tests/Services/Portico/PorticoGeometriaCalculatorTests.cs` — testes novos/atualizados

Sistema de coordenadas (inalterado): **X = comprimento (pórticos), Y = vão/largura, Z = altura**.
`zTopo(y)` já coincide exatamente com o eixo do banzo superior da treliça (verificado:
`zTopo(t·w) = hp + AlturaNaPosicao(t,H,B)`).

---

## R1 — Linha de corrente: sag-rods subindo a água (no meio do vão)

**Hoje** (`PorticoGeometriaCalculator.Calcular`, bloco "LINHA DE CORRENTE"):
```csharp
if (e.LancarLinhaCorrente)
{
    double[] ysLinha = { w / 2.0, w / 4.0, 3.0 * w / 4.0 };
    foreach (double y in ysLinha)
    {
        double z = ZTopo(e, hp, w, y);
        linhasCorrente.Add(new Segmento(new Ponto3D(0.0, y, z), new Ponto3D(comprimento, y, z)));
    }
}
```
→ ERRADO: linhas longitudinais (ao longo de X).

**Novo:** a linha de corrente liga o **meio de uma terça ao meio da terça da cumeeira**, subindo a
água, **no meio de cada vão** (Xmid entre dois pórticos). Um membro por água, por vão:
```csharp
if (e.LancarLinhaCorrente)
{
    double meia = w / 2.0;
    double zElev = e.ElevacaoTercasMm; // sobe junto com as terças (R3)
    for (int b = 0; b < xPorticos.Count - 1; b++)
    {
        double xMid = (xPorticos[b] + xPorticos[b + 1]) / 2.0;
        // agua 1: beiral (y=0) -> cumeeira (y=w/2)
        linhasCorrente.Add(new Segmento(
            new Ponto3D(xMid, 0.0, ZTopo(e, hp, w, 0.0) + zElev),
            new Ponto3D(xMid, meia, ZTopo(e, hp, w, meia) + zElev)));
        // agua 2: cumeeira -> beiral oposto (y=w)
        linhasCorrente.Add(new Segmento(
            new Ponto3D(xMid, meia, ZTopo(e, hp, w, meia) + zElev),
            new Ponto3D(xMid, w, ZTopo(e, hp, w, w) + zElev)));
    }
}
```
- Fica no nível das terças (R3): `ZTopo + ElevacaoTercasMm`.
- `2·(N−1)` membros (um por água por vão), exatamente como o usuário desenhou em azul.

---

## R2 — Nº de contraventamentos (X) configurável (cobertura e pilares)

**Hoje:** `vaosExtremidade = {0, n-2}` fixo. **Novo:** distribuir `K` vãos contraventados.

1. Helper puro novo no calculator:
```csharp
/// <summary>Indices de ate 'quantidade' vaos (0..nVaos-1) distribuidos uniformemente,
/// incluindo extremos quando quantidade >= 2. Vazio se quantidade <= 0.</summary>
private static IReadOnlyList<int> DistribuirVaos(int nVaos, int quantidade)
{
    var ids = new List<int>();
    if (nVaos <= 0 || quantidade <= 0)
        return ids;
    if (quantidade >= nVaos)
    {
        for (int i = 0; i < nVaos; i++)
            ids.Add(i);
        return ids;
    }
    if (quantidade == 1)
    {
        ids.Add(0);
        return ids;
    }
    for (int i = 0; i < quantidade; i++)
    {
        int idx = (int)System.Math.Round((double)i * (nVaos - 1) / (quantidade - 1));
        if (!ids.Contains(idx))
            ids.Add(idx);
    }
    return ids;
}
```
2. Trocar o uso de `vaosExtremidade` por:
```csharp
int nVaos = n - 1;
IReadOnlyList<int> vaosCobertura = DistribuirVaos(nVaos, e.NumeroXCobertura);
IReadOnlyList<int> vaosPilares = DistribuirVaos(nVaos, e.NumeroXPilares);
```
- No bloco de cobertura, iterar `vaosCobertura`; no de pilares, `vaosPilares`.
- Remover a montagem antiga de `vaosExtremidade`.
- As flags `ContravCobertura`/`ContravPilares` continuam gateando (se desligado, não gera; e o
  `NumeroX*` só vale quando a flag está ligada — o serviço/janela garante `NumeroX>=1` quando ligado).

> Interpretação: `NumeroXCobertura` = nº de **vãos** com X na cobertura (cada vão → X nas 2 águas);
> `NumeroXPilares` = nº de **vãos** com X nas paredes (cada vão → X nas 2 paredes). Default 2
> (reproduz o comportamento atual de extremidade para N≥3).

---

## R3 — Terças acima do banzo superior

Adicionar `ElevacaoTercasMm` à entrada pura. No bloco de TERÇAS, somar a elevação ao Z:
```csharp
double z = ZTopo(e, hp, w, y) + e.ElevacaoTercasMm;
```
(e idem para o espelho `zEspelho`). Default `150` mm (usuário ajusta conforme a altura do perfil).
A linha de corrente (R1) usa a mesma elevação, então terças e correntes ficam coplanares.

---

## R4 — Ângulo de rotação do perfil (banzo sup/inf, diagonal, montante)

Rotação é propriedade do `FamilyInstance` (Revit), **não entra no calculator puro**.

1. `TrelicaConfig.cs` — adicionar (todos `double`, default 0):
```csharp
public double RotacaoBanzoSuperiorGraus { get; set; }
public double RotacaoBanzoInferiorGraus { get; set; }
public double RotacaoDiagonalGraus { get; set; }
public double RotacaoMontanteGraus { get; set; }
```
2. `TrelicaService.cs` — `CriarMembro` ganha parâmetro de rotação e aplica só se ≠ 0
   (preserva o caminho interativo, que passará 0):
```csharp
private bool CriarMembro(Document doc, Level nivel, FamilySymbol symbol, XYZ inicio, XYZ fim,
                         int zJustificationValue, double zOffsetFt, double rotacaoRad)
{
    // ... cria fi ...
    RevitUtils.SetZJustification(fi, zJustificationValue);
    RevitUtils.SetYZOffsets(fi, 0.0, zOffsetFt);
    if (System.Math.Abs(rotacaoRad) > RevitUtils.EPS)
        RevitUtils.SetSectionRotation(fi, rotacaoRad);
    RevitUtils.DisallowJoins(fi);
    return true;
}
```
3. `GerarVao` — calcular a rotação por tipo/cordão e passar em cada chamada de `CriarMembro`:
```csharp
double GrausParaRad(double g) => g * System.Math.PI / 180.0;
// banzo: superior vs inferior; montante; diagonal
```
   - Banzo superior → `RotacaoBanzoSuperiorGraus`; inferior → `RotacaoBanzoInferiorGraus`
     (usar `seg.De.Chord == TrussChord.Superior`, igual ao `ResolverBanzo`).
   - Montante → `RotacaoMontanteGraus`; Diagonal → `RotacaoDiagonalGraus`.
   - Atualizar **todas** as chamadas de `CriarMembro` em `GerarVao` (inclui o laço de banzo em pico).
   - Atualizar a chamada de `CriarMembro` no caminho "entre banzos" (`ExecutarEntreBanzos`/`GerarVao`)
     passando `0.0` para rotação (sem regressão).
4. `GerarPorticoConfig.cs` — espelhar os 4 campos de rotação (default 0).
5. `GerarPorticoService.MapearTrelicaConfig` — repassar os 4 campos para o `TrelicaConfig`.

> Nota: o caminho interativo do `TrelicaService` (janela `TrelicaWindow`) não ganha UI de rotação
> agora — os campos default 0 ⇒ comportamento idêntico (nenhum `SetSectionRotation` é chamado).

---

## Campos novos em `GerarPorticoConfig` / `GerarPorticoEntrada`

`GerarPorticoEntrada` (PURO — usado pelo calculator):
```csharp
public double ElevacaoTercasMm { get; set; } = 150.0;
public int NumeroXCobertura { get; set; } = 2;
public int NumeroXPilares { get; set; } = 2;
```
`GerarPorticoConfig` (Revit): os 3 acima **+** os 4 de rotação (R4). `MapearEntrada` copia os 3
numéricos; `MapearTrelicaConfig` copia os 4 de rotação.

---

## Janela `GerarPorticoWindow` (XAML + code-behind)

Novos controles (todos `TextBox` numéricos, parse via `NumberParsing`):
- **Treliça** (no `painelTrelica`): 4 campos de rotação (°): `txtRotBanzoSup`, `txtRotBanzoInf`,
  `txtRotDiagonal`, `txtRotMontante` (default "0"; tooltip ex.: "270 para U na posição correta").
- **Terças**: `txtElevTercas` (mm, default "150") — "Elevação sobre o banzo".
- **Contraventamentos**: ao lado de cada checkbox, `txtNumXCobertura` e `txtNumXPilares`
  (default "2") — "Nº de vãos com X".

`BuildConfig()` preenche os novos campos:
```csharp
ElevacaoTercasMm = NumberParsing.ParseDoubleOrDefault(txtElevTercas.Text, 150.0),
NumeroXCobertura = ParseInt(txtNumXCobertura.Text, 2),
NumeroXPilares = ParseInt(txtNumXPilares.Text, 2),
RotacaoBanzoSuperiorGraus = NumberParsing.ParseDoubleOrDefault(txtRotBanzoSup.Text, 0.0),
RotacaoBanzoInferiorGraus = NumberParsing.ParseDoubleOrDefault(txtRotBanzoInf.Text, 0.0),
RotacaoDiagonalGraus = NumberParsing.ParseDoubleOrDefault(txtRotDiagonal.Text, 0.0),
RotacaoMontanteGraus = NumberParsing.ParseDoubleOrDefault(txtRotMontante.Text, 0.0),
```
Manter o padrão sem `#nullable enable` no code-behind; todos os `x:Name` novos devem existir no XAML
(conferir 1-a-1 antes de commitar — um faltante quebra o build WPF).

---

## Testes (`PorticoGeometriaCalculatorTests`)

Atualizar/adicionar:
1. **R1 — linha de corrente sobe a água:** com `LancarLinhaCorrente=true`, todos os segmentos têm
   `A.XMm == B.XMm` (mesmo X = meio do vão) e `A.YMm != B.YMm` (varia no vão); contagem `2·(N−1)`;
   nenhum segmento longitudinal (`A.XMm == B.XMm`). Cobre a regressão da direção.
2. **R3 — terças elevadas:** terça na cumeeira em `z == hp + B + ElevacaoTercasMm`.
3. **R2 — nº de X:** `NumeroXCobertura=3` ⇒ 3 vãos contraventados ⇒ `ContravCobertura.Count == 3·2·2`
   (3 vãos × 2 águas × 2 diagonais = 12); `NumeroXPilares=1` ⇒ 1 vão ⇒ `4`.
4. Ajustar testes existentes de contraventamento (hoje assumem extremidade) para os defaults
   `NumeroX*=2` (devem continuar dando 8 para N=7) e a `BaseTrelica()` ganha
   `ElevacaoTercasMm=150, NumeroXCobertura=2, NumeroXPilares=2`.
5. Conferir os `HaveCount` das terças (a elevação não muda a contagem; segue 11 no caso base).

---

## Ondas de execução

- **Onda G** (puro + testes): R1, R2, R3 no `PorticoGeometriaCalculator` + `GerarPorticoEntrada` +
  testes. Verde antes de tocar Revit.
- **Onda H** (rotação): R4 — `TrelicaConfig`, `TrelicaService.GerarVao/CriarMembro` (todas as
  chamadas), `GerarPorticoService.MapearTrelicaConfig`. Caminho interativo idêntico.
- **Onda I** (UI): campos novos na `GerarPorticoWindow` (XAML + code-behind) + `GerarPorticoConfig`
  + `MapearEntrada`. Conferir `x:Name` 1-a-1.
- **Onda J** (fechamento): CHANGELOG, scans BOM/format, push, CI verde, atualizar PR #71.

---

## Critérios de aceite

- [ ] Build Release 0 warnings · testes verdes · `dotnet format` limpo · gitleaks limpo.
- [ ] Caminho interativo do `TrelicaService` sem regressão (rotação default 0 ⇒ sem `SetSectionRotation`).
- [ ] Linha de corrente sobe a água no meio do vão (validação visual no Revit pelo Alef).
- [ ] Terças acima do banzo; nº de X respeitado; rotação aplicada aos perfis.

---

## Comando `/goal`

```
/goal Execute o plano docs/PLANO-GERAR-PORTICO-V2.md de ponta a ponta (Ondas G→J), de forma
cirurgica e 100% precisa. Commit por onda no branch claude/great-turing-Vqlig, CI verde
(format, build Release 0 warnings, testes, gitleaks), PR #71 atualizado. Nao toque em outros
branches. Antes de codar releia a secao 3 e 11 do docs/PLANO-GERAR-PORTICO.md. Preserve o
caminho interativo do TrelicaService (rotacao default 0). Confira os x:Name da janela 1-a-1.
```
