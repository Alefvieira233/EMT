# FerramentaEMT v1.6.0-rc.1 — Incorporação Victor Wave 2

**Data:** 2026-04-27
**Status:** Release Candidate (validado no Revit 2025 do Alef)
**Tag:** [`v1.6.0-rc.1`](https://github.com/Alefvieira233/EMT/releases/tag/v1.6.0-rc.1)

---

## Highlights

Esta release incorpora a segunda onda de mudanças do Victor (snapshot de 2026-04-24), trazendo features importantes para o fluxo de armadura PF (Pré-Fabricado de Concreto):

- 🎯 **Catálogo de RebarShape do projeto Revit** com preview visual nas janelas de estribos
- 📐 **Cálculo de ancoragem NBR 6118** com lap splice (concreto, aço, eta1/eta2/eta3, gancho)
- 🏗️ **Bloco de duas estacas** — comando dedicado com 14 posições padronizadas
- ✋ **Modo coordenadas manual** para barras de pilar/viga
- 🎨 **Ribbon dividida** em duas abas (`Ferramenta EMT` PF + `Ferramentas ECC` geral)

---

## Para baixar e instalar

### Opção A — Instalador EXE (recomendado)
[`FerramentaEMT.SetupBootstrapper.exe`](attachments) — duplo clique para instalar.

### Opção B — ZIP manual (3.8 MB)
[`FerramentaEMT-Revit2025-Release.zip`](attachments) — extrair, depois clique direito em `Install-FerramentaEMT.ps1` → "Executar com PowerShell".

Após instalação, abrir Revit 2025: o ribbon mostrará **2 abas** novas — `Ferramenta EMT` e `Ferramentas ECC`.

---

## Adicionado

### Catálogo de RebarShape do projeto Revit
- `PfRebarShapeCatalog` varre `RebarShape` do projeto filtrado por `RebarStyle.StirrupTie`
- `PfRebarShapePreviewService` gera preview visual (220 px) usando `BitmapImage`
- `PfBeamStirrupsWindow` e `PfColumnStirrupsWindow` ganharam ComboBox de shape + Image de preview
- Quando o usuário seleciona um shape do projeto: ferramenta tenta aplicar; se incompatível, mantém o automático sem interromper

### Cálculo NBR 6118
- `PfNbr6118AnchorageService.Calculate(diametroMm, PfLapSpliceConfig)` retorna ancoragem básica (`lb`), necessária (`lb,nec`), mínima e traspasse (`l0`)
- Inputs: fck (clamp 12 MPa), fyk (clamp 250 MPa), `BarSurface` (Lisa/Entalhada/Nervurada → eta1), `BondZone` (Boa/Ruim → eta2), `AnchorageType` (Reta=α1.0, Gancho*=α0.7), `SplicePercentage` (tabela 20/25/33/50/>50%)
- `ToDetailText()` gera string `"EMT NBR 6118:2023 | phi 12.5 mm | lb 30 cm | lb,nec 30 cm | traspasse l0 60 cm | fbd 2.846 MPa"` para virar parâmetro Comments do Revit

### Bloco de 2 estacas
- `Commands/PF/CmdPfInserirAcosBlocoDuasEstacas` — novo botão em **PF Armaduras**
- `PfTwoPileCapRebarService.Execute(uidoc, config)` — análogo a `PfRebarService.ExecuteBeamBars`, suporta superior/inferior/lateral
- `PfTwoPileCapBarCatalog.Tipo4` — 14 posições com diâmetros 6.3/8.0/10.0/12.5/16.0 mm e formas (Reta, U, RetanguloFechado, EstriboVertical, CaliceVertical, FormaEspecial)
- `PfTwoPileCapBarPosition.ToComment()` formata padrão `"N1 - POS 1 - diam. 12.5 - C/15 - C=350 - U inferior"` (culture-invariant)

### Modo coordenadas manual
- `PfRebarPlacementMode` (Automatico / Coordenadas)
- Quando `Coordenadas`, o serviço usa `List<PfColumnBarCoordinate>` ou `List<PfBeamBarCoordinate>` em cm local
- `PfColumnBarsConfig.QuantidadeCircular` para seções circulares com N barras igualmente espaçadas

### Lap splice (NBR 6118)
- `PfLapSpliceConfig` — config completa de traspasse
- Integrado em `PfBeamBarsConfig.Traspasse` e `PfColumnBarsConfig.Traspasse`
- Quando `Enabled=true` e barra > `MaxBarLengthCm` (default 1200 cm), serviço insere traspasse calculado

### Ribbon dividida em duas abas
- **`Ferramenta EMT`** — só PF (painéis `PF Construção`, `PF Documentação`, `PF Armaduras`)
- **`Ferramentas ECC`** (nova) — fluxo geral (`Modelagem`, `Estrutura`, `Vigas`, `Vista`, `Documentação`, `Fabricação`, `CNC`, `Verificação`, `Montagem`, `Licença`)

---

## Corrigido

### IsTwoPileCap ausente em PfElementService (CS0117)
- Helper que detecta `FamilyInstance` com `Category.OST_StructuralFoundation` foi adicionado — ele estava sendo chamado mas não tinha sido portado durante a Wave 2

### System.Drawing.Common (CS0012, CS1069)
- Package `System.Drawing.Common` v8.0.10 adicionada ao csproj — `Bitmap` e `ImageFormat` saíram do BCL no .NET 5+ e exigem package explícita

### Culture-invariant em ToComment (descoberto pelos testes em pt-BR)
- `PfTwoPileCapBarPosition.ToComment()` usava interpolação culture-sensitive — em pt-BR gerava `"6,3"` em vez de `"6.3"`
- Forçado `CultureInfo.InvariantCulture` em todos os formatadores numéricos do método
- Crítico porque o `Comment` vai parar no Revit e é consumido por schedules/CSV downstream

---

## Preservado contra snapshot Victor (decisões deliberadas)

- `ModelCheckService` mantém `Result<ModelCheckReport>` (ADR-003) e `IProgress<ProgressReport>` + `CancellationToken` (ADR-004) — Victor tinha regredido essa adoção
- `ModelCheckCollector`, `ModelCheckVisualizationService` e as 9 `ModelCheckRules` mantidos
- `ListaMateriaisExportService`, `AgrupamentoVisualService`, `NumeracaoItensService`, `DstvExportService` mantidos com adoção ADR-003
- `CrashReporter.Initialize()` e `LicenseSecretProvider.GetResolvedSource()` no `App.OnStartup` preservados
- `CmdCortarElementos` (Onda 3 PR-1 anterior) preservado em `Ferramentas ECC > Estrutura`
- UTF-8 com acentos em `App.cs` e UI preservado

---

## Regressões conhecidas (follow-up planejado para v1.7.0)

- **Zoneamento NBR 6118 de estribos dormente** — `PfRebarService` reconciliado consome `EspacamentoCm` único. Os campos granulares (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidade`, `EspacamentoApoio/Central`, `ComprimentoZonaApoio`) estão preservados em `PfRebarConfigs.cs` atrás da flag `UsarEspacamentoUnico=false` (default). A flag existe mas o serviço não a lê. Backup em `Services/PF/PfRebarService.cs.bak-alef-v1.5` (945 linhas).
- **AppDialogService usado em PfTwoPileCapRebarService** (linhas 28, 82) — viola ADR-003 de "service mudo". Consistente com `PfRebarService` (linhas 155, 215) que tem o mesmo padrão. Cleanup deve ser feito nos dois juntos.

---

## Estatísticas

- **Commits:** 3 commits acima de v1.5.0 (`7db8c65` feat Wave 2 + `72669a8` fix build + `3f5dc04` fix culture-invariant)
- **Linhas alteradas:** +4.937 / -270
- **Arquivos novos:** 14 (10 features + 4 testes)
- **Testes:** 460 totais, **460 passando** (+41 vs v1.5.0)
- **Build time:** 7s (Release)
- **Backups preservados:** 13 `.bak-alef-v1.5` (gitignorados, locais para rollback)

---

## Atribuição

Esta release combina:
- **Alef Christian Gomes Vieira** — arquitetura ADR-003/004, infraestrutura, refator pós-merge, validação
- **Victor** — snapshot de 2026-04-24 com features RebarShape, NBR 6118, bloco-2-estacas, ribbon split

Co-autoria 50/50 conforme `LICENSE`.

---

## Documentação relacionada

- `CHANGELOG.md` — histórico completo
- `comparacao-victor/ANALISE-VICTOR-WAVE2.md` — plano de merge em 8 ondas e raciocínio
- `Services/PF/PfRebarService.cs.bak-alef-v1.5` — versão pre-Wave2 com zoneamento vivo (referência para follow-up)
