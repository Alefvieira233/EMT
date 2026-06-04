# PLANO V4 — Fundações + Armaduras no "Gerar Projeto Completo (Pórtico)"

> Adiciona, na janela da função, **Fundações** (sapata sob cada pilar) e **Armadura de
> fundação** (opt-in, best-effort). Branch `claude/great-turing-Vqlig`. Regras de sempre:
> BOM UTF-8 nos `.cs`, LF, sem control-flow de uma linha, `using` System primeiro,
> `dotnet format` limpo, Release 0 warnings, `x:Name` 1-a-1, caminho interativo sem regressão.

## Veredito de viabilidade (do levantamento)
- **Fundação: 100% viável, baixo risco.** `PfFoundationPlacementService.Execute` é headless
  (sem pick, transação própria, posiciona sob cada pilar). Reuso direto.
- **Armadura: viável, porém best-effort/opt-in.** `BlocoFundacaoRebarOrchestrator.Execute`
  é headless (hosts por parâmetro, `Rebar.CreateFromCurves`), MAS o sucesso depende de a
  **família de fundação aceitar armadura** (`RebarHostData != null`) e de haver `RebarBarType`.
  Famílias genéricas frequentemente NÃO são rebar-host → sai 0 sem erro. Logo: opt-in, com
  checagem por fundação e relatório honesto ("X armadas, Y puladas").

---

## Fatos do código (não re-descobrir)

**Fundação** — `SteelBIM/Services/PF/PfFoundationPlacementService.cs`
- `public ResultSummary Execute(UIDocument uidoc, PfFoundationPlacementConfig config)` — headless;
  coleta pilares de `uidoc.Selection` (Escopo `SelecaoAtual`) ou da vista (`VistaAtiva`);
  cria via `doc.Create.NewFamilyInstance(insertionPoint, symbol, level, StructuralType.Footing)`
  sob o ponto-base de cada pilar; grava `Comments = "EMT_FUNDACAO_PILAR|Pilar:{id}"`; transação própria.
- `PfFoundationPlacementConfig` (`SteelBIM/Models/PF/`): `ElementId SymbolId`, `PfFoundationPlacementScope Escopo`,
  `bool OrientarPeloPilar=true`, `bool IgnorarSeJaExisteFundacao=true`, `double ToleranciaCentroMm=150`.
- `PfFoundationPlacementScope`: `SelecaoAtual=0`, `VistaAtiva=1`.
- `IsSupportedFoundationSymbol`: categoria `OST_StructuralFoundation` + `FamilyPlacementType`
  `OneLevelBased`/`OneLevelBasedHosted`.

**Armadura** — `SteelBIM/Services/Bloco/BlocoFundacaoRebarOrchestrator.cs`
- `public Result Execute(UIDocument uidoc, IReadOnlyList<Element> hosts, BlocoFundacaoRebarConfig config)`
  — hosts por parâmetro (sem pick); usa `RebarCreationService` (`Rebar.CreateFromCurves`, precisa de
  `RebarBarType`); geometria por bounding-box; **mostra `AppDialogService.ShowInfo` no fim e mexe em
  `uidoc.Selection`** (efeitos de UI a silenciar).
- `BlockGeometryService.CanHostRebar(Element host)` → `RebarHostData.GetRebarHostData(host) != null`.
- `BlocoFundacaoRebarConfig` (`SteelBIM/Models/Bloco/`): default `LancarArmaduraInferior=true`,
  `BarTypeName` vazio (fallback "primeiro RebarBarType"). **CONFIRMAR nomes exatos lendo o arquivo.**

Namespaces (de `SteelBIM.Services.Portico` precisam de `using`): `SteelBIM.Services.PF`,
`SteelBIM.Services.Bloco`, `SteelBIM.Models.PF`, `SteelBIM.Models.Bloco`.

---

## Fase 1 — Fundação (Onda Q) — GARANTIDA

1. **`GerarPorticoConfig`**: `public bool LancarFundacoes { get; set; }` + `public FamilySymbol? SymbolFundacao { get; set; }`.
2. **`GerarPorticoService`**:
   - Coletar os `ElementId` dos pilares criados: mudar `CriarPilar` para devolver `FamilyInstance?`
     (igual `CriarBarra`); no laço de pilares, `var fp = CriarPilar(...); if (fp != null) { pilares++; pilarIds.Add(fp.Id); }`.
     (O fallback de `CriarPilar` já chama `CriarBarra`, que devolve `FamilyInstance?`.)
   - **Após** o `t.Commit()` da transação principal (junto com placa/ligação), se
     `config.LancarFundacoes && config.SymbolFundacao != null && pilarIds.Count > 0`:
     ```csharp
     var antes = ColetarFundacoes(doc); // HashSet<ElementId> de OST_StructuralFoundation
     uidoc.Selection.SetElementIds(pilarIds);
     var fcfg = new PfFoundationPlacementConfig
     {
         SymbolId = config.SymbolFundacao.Id,
         Escopo = PfFoundationPlacementScope.SelecaoAtual,
         OrientarPeloPilar = true,
         IgnorarSeJaExisteFundacao = true
     };
     new PfFoundationPlacementService().Execute(uidoc, fcfg);
     fundacoesCriadas = ColetarFundacoes(doc).Where(id => !antes.Contains(id)).ToList();
     fundacoes = fundacoesCriadas.Count;
     ```
   - Helper `ColetarFundacoes(doc)` = `FilteredElementCollector(doc).OfCategory(OST_StructuralFoundation).WhereElementIsNotElementType().ToElementIds()`.
   - Tudo em `try/catch` (não quebra o pórtico) + linha no resumo "Fundações: {fundacoes}".
3. **Janela**: nova `GroupBox "Fundações"` com `chkFundacoes` + `cmbFundacao` (cascata simples
   `SymbolItem`). O `CmdGerarPorticoCompleto` coleta as famílias de fundação
   (`OfCategory(OST_StructuralFoundation)` + `FamilyPlacementType` OneLevelBased/Hosted) e passa
   como 4º argumento ao construtor da janela. `BuildConfig`: `LancarFundacoes`/`SymbolFundacao`.

## Fase 2 — Armadura (Onda R) — OPT-IN, BEST-EFFORT

1. **`GerarPorticoConfig`**: `public bool LancarArmaduraFundacao { get; set; }`.
2. **`BlocoFundacaoRebarOrchestrator`**: adicionar overload/param **silencioso**
   `Execute(uidoc, hosts, config, bool mostrarResumo)` que pula `AppDialogService.ShowInfo` e a
   manipulação de `uidoc.Selection` quando `mostrarResumo == false`. Default `true` (caminho
   interativo "Armaduras Bloco" **idêntico** — sem regressão).
3. **`GerarPorticoService`** (depois da Fase 1, só se `LancarArmaduraFundacao` e houver
   `fundacoesCriadas`):
   - Filtrar `fundacoesCriadas` por `BlockGeometryService.CanHostRebar(host)`.
   - Montar `BlocoFundacaoRebarConfig` mínimo (armadura inferior; bar type default/fallback).
   - `new BlocoFundacaoRebarOrchestrator().Execute(uidoc, hostsArmaveis, cfg, mostrarResumo: false)`
     em `try/catch`. Resumo: "Armadura de fundação: {armadas} armada(s), {puladas} pulada(s)
     (família não suporta armadura)".
4. **Janela**: na `GroupBox "Fundações"`, checkbox `chkArmaduraFundacao` ("Armar fundações —
   requer família que aceite armadura"); habilitado só quando `chkFundacoes` marcado. `BuildConfig`.

## Onda S — fechamento
Bump de versão (2.8.17 → **2.8.18**), CHANGELOG, scans, push, CI verde, PR #71 atualizado.

## Testes
- Núcleo puro `PorticoGeometriaCalculator` **não muda** (fundação/armadura são Revit-bound, fora do
  cálculo de geometria). Sem novos testes de cálculo. Validação: build Release + smoke test no Revit.
- Garantir que `GerarPorticoConfig` novo compila e que a janela tem todos os `x:Name`.

## Aceite
- [ ] Release 0 warnings · format · gitleaks · testes existentes verdes.
- [ ] Caminho interativo de "Armaduras Bloco" sem regressão (overload silencioso default true).
- [ ] Fundação: sapata sob cada pilar (validação no Revit pelo Alef).
- [ ] Armadura: opt-in; relatório honesto de armadas/puladas; nunca quebra a geração.

## Comando `/goal`
```
/goal Execute o plano docs/PLANO-GERAR-PORTICO-V4.md de ponta a ponta (Ondas Q, R, S), de forma
cirurgica e 100% precisa. Antes de codar, releia os arquivos citados em "Fatos do codigo" para
confirmar assinaturas/propriedades exatas (PfFoundationPlacementService/Config,
BlocoFundacaoRebarOrchestrator, BlockGeometryService.CanHostRebar, BlocoFundacaoRebarConfig,
ResultSummary) e a secao 3 e 11 do docs/PLANO-GERAR-PORTICO.md. Fundacao e GARANTIDA; armadura e
OPT-IN best-effort (cheque CanHostRebar por fundacao, sem popup via overload silencioso, e nunca
quebre a geracao). Commit por onda no branch claude/great-turing-Vqlig, mantenha CI verde
(format, build Release 0 warnings, testes, gitleaks), bump de versao no fim e atualize o PR #71.
Preserve o caminho interativo do comando "Armaduras Bloco" (overload silencioso default true).
Confira os x:Name da janela 1-a-1. Nao toque em outros branches.
```
