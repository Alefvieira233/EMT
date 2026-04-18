# CLAUDE.md — Contexto do projeto FerramentaEMT

Este arquivo é lido automaticamente pela Claude no início de cada sessão.
Contém o contexto essencial do projeto e apontadores para o resto da documentação.

## Sobre o projeto

**FerramentaEMT** é um plugin Revit 2025 para automatizar trabalho de estrutura metálica
(detalhamento, fabricação, documentação, CNC, montagem). Desenvolvido para uso do
escritório EMT (ALEF CHRISTIAN GOMES VIEIRA, CREA 0319918963).

Alvo: .NET 8.0-windows / Revit 2025 API / WPF.

## Onde estão as coisas

- `FerramentaEMT/` — código-fonte do plugin (Commands, Services, Views, Models, etc.).
- `FerramentaEMT.Tests/` — testes unitários (196 testes passando na última verificação).
- `FerramentaEMT.Distribuicao/` — projeto de instalador/distribuição.
- `docs/reference-projects/` — **projetos executivos reais do escritório usados como referência de estilo**. Sempre consultar antes de escrever funções novas relacionadas a cotagem, tags, vistas, detalhes.
- `CHANGELOG.md` — histórico de versões (v1.0.5 é o estado atual estável).

## Padrões a seguir

- Idioma: código e comentários em português (PT-BR sem acentos em identificadores).
- Logging via `FerramentaEMT.Infrastructure.Logger` (nunca `Console` ou `TaskDialog` direto).
- Diálogos via `FerramentaEMT.Utils.AppDialogService` (nunca `MessageBox` direto).
- Transações sempre com `using (Transaction t = ...)` e nome descritivo.
- Commands herdam de `FerramentaCommandBase` (que já trata licenciamento e erros).
- Nullable enable nos arquivos modificados (`#nullable enable` no topo se não for projeto-wide).
- TreatWarningsAsErrors em Release — zero warnings.

## Antes de escrever função nova

1. Ler `CLAUDE.md` (este arquivo).
2. Ler `docs/reference-projects/README.md` e percorrer as subpastas — cada `NOTAS.md` descreve o padrão do escritório para aquele tipo de desenho.
3. Consultar as skills carregadas (`ferramenta-emt-architecture`, `revit-api-core`, `revit-dimensions-tags`, `revit-views-sections`, `revit-steel-fabrication`).
4. Seguir o padrão Command → Window (WPF) → Service → Models do código existente.

## Histórico recente

- **v1.2.0 (2026-04-17):** Integração do fork do Victor (módulo **PF — Pré-Fabricado de Concreto**) sobre o tronco oficial. Adiciona 10 comandos (Commands/PF/), 6 services (Services/PF/, com `PfRebarService` de 946 linhas como ponto de entrada de armaduras), 9 configs (Models/PF/), 7 janelas WPF (Views/Pf*Window) e 3 painéis novos no ribbon (`PF Construção`, `PF Documentação`, `PF Armaduras`). Também traz 4 refinamentos de núcleo metálico do Victor: `GerarVistaPecaConfig.FiltroCategoria` (novo enum + prop), `AutoVistaService` filtra por categoria, `TagearTrelicaService` implementa rótulos de banzo via TextNote (substitui TODO do v1.1.0), `AppSettings` persiste 9 propriedades `LastPfNaming*`. Novo helper puro `Services/PF/PfNamingFormatter` (culture-invariant) com suite xUnit. Suite de testes sobe de 223 → **279** (todos verdes em 52 ms). Build Release com 0 erros, 2 avisos MSB3277 não-impeditivos. Pasta do Victor preservada em `backup-victor-pre-merge.zip` antes da remoção.
- **v1.0.5 (2026-04-14):** Correções Ondas 1-4 + hardening + extração de 3 helpers puros + 196 testes + build Release limpo (0 erros). Plugin instalado e rodando no Revit 2025.
- A pasta `docs/reference-projects/igreja-patos/` foi adicionada como primeira referência de estilo do escritório (cotagem de treliça, identificação de perfis, layout de prancha).
- A pasta `docs/reference-projects/ampliacao-vulcaflex/` foi adicionada com padrões completos do escritório para: detalhamento de pilar isolado (4 vistas 1:40), planta de locação, diagramas de montagem, contraventamento, linhas de corrente, croquis individuais de terça/LC/chapa, tabelas-mestre e convenções gerais (prefixos, escalas, nomenclatura de perfis).
- A pasta `docs/reference-projects/cobertura-samsung/` foi adicionada com padrões completos para **cobertura metálica em treliça de duas águas**: plano geral da cobertura 1:100 (EM-01), elevações de treliça 1:50 (EM-02) e catálogo de treliças + platibandas (EST-08, prancha 08/14 do executivo Samsung/CRC). Foco principal: o **padrão de cotagem de treliça em cinco faixas** (painéis banzo superior, painéis banzo inferior, vão total, vãos parciais entre apoios, altura de cada montante) que será usado pela função `Cotar Treliça`. Também documenta convenções de balão de detalhe (platibanda, ligação de banzo, ligação de terça, agulha central, mão francesa, apoio em pilar), nomenclatura `TR - NN (Qx)` e tabelas-resumo (perfis, placas, parafusos).
- A pasta `docs/reference-projects/galpao-padrao-emt/` foi adicionada com o **template mínimo de entrega EMT em 3 pranchas** (EST-01 locação + fundações, EST-02 plano geral da cobertura, EST-03 detalhamento das treliças). É a referência de "projeto completo menor" do escritório: grid A-G × 1-2, pilares 2U300x100x25x3.00, treliça 15010 mm vão em U150x65x4,76. Consolida nomenclatura padrão (Pnn/Snn/Vnn/Tnn/TRELIÇA NN), escalas padrão por tipo de desenho (1:50 fundação, 1:100 cobertura, 1:25 treliça pequena, 1:10 chapas), texto-modelo das notas gerais CONCRETO e METÁLICA, e o conjunto completo de tabelas padrão que cada prancha deve ter. Base para futura função `Gerar Projeto Completo` (EST-01→02→03 automático).
- **2026-04-15** — Scaffold da funcao `Cotar Treliça` em `FerramentaEMT/Services/Trelica/` (5 helpers puros: `TrelicaClassificador`, `TrelicaGeometria`, `TrelicaPerfilFormatter`, `TrelicaTopologia`, `CotaFaixaBuilder`) + `Models/CotarTrelicaConfig.cs` + `Commands/CmdCotarTrelica.cs` + `Views/CotarTrelicaWindow.xaml(.cs)` + ~27 testes unitarios em `FerramentaEMT.Tests/Services/Trelica/`. Plano detalhado em `docs/PLANO-LAPIDACAO.md` (V1) e **`docs/PLANO-LAPIDACAO-V2.md` (consolidado com revisao de 2 engenheiros seniores + APIs Revit oficiais + checklist de 20 itens "market-ready")**. Fixes aplicados apos revisao: enum `BanzoIndefinido`, `OffsetZPes` (claridade), `SegmentosConsecutivos` com contract assertion, testes extras (ruido no pico, altura nunca negativa, 3 apoios, dois nos identicos).
- **v1.1.0 (2026-04-15):** Execucao das Ondas A+B+E em paralelo:
  - **Onda A**: `CotarTrelicaService` (638 linhas, pipeline 10 etapas) + `CotarTrelicaReport` + integracao com `CmdCotarTrelica`. 5 TODOs para Wave A.1.5 (Revit References reais).
  - **Onda B**: 2 novos comandos — `CmdTagearTrelica` (tagear perfis na trelica) e `CmdIdentificarPerfil` (identificacao generica de perfis). 12 arquivos, 1391 linhas, 13 testes.
  - **Onda E**: ESC handler centralizado em `RevitWindowThemeService.AttachEscapeHandler()` (~23 janelas beneficiadas). Migracao `IntegerValue → ElementId`.
  - 3 novos botoes registrados no ribbon (painel Documentacao): Cotar Trelica, Tagear Trelica, Identificar Perfil.
  - Total de testes unitarios: **~223** (196 anteriores + 27 novos da Trelica).
- **Wave A.1.5 (2026-04-15):** Implementacao real das APIs Revit no `CotarTrelicaService`:
  - Novo `TrelicaRevitHelper.cs` (~310 linhas) — wrapper para `NewDimension`, `IndependentTag.Create`, `TextNote.Create`, Reference extraction, coord projection.
  - 5 TODOs substituidos por chamadas reais: running dimensions por faixa, tags com offset 150mm + leader, TextNotes "BANZO SUPERIOR/INFERIOR <perfil>", alturas de montante em mm.
  - `TrelicaRevitHelperTests.cs` (~50 testes documentados, Skip="Requer Revit") + 3 novos testes em `CotarTrelicaReportTests`.
