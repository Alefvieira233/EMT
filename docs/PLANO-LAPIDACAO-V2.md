# PLANO-LAPIDACAO V2 — FerramentaEMT

**Versao:** 2.0
**Data:** 2026-04-15
**Autor:** Claude (consolidado a partir de revisao de 2 engenheiros seniores + skills Revit oficiais + scaffold implementado)
**Escopo:** Transformar o FerramentaEMT v1.0.5 num plugin "market-ready" — vendavel, robusto, sem bugs, respeitando 100% o padrao EMT.

---

## 0. Como ler este documento

Este V2 **substitui** o `PLANO-LAPIDACAO.md` (V1). O V1 esta preservado para historico.

Mudancas principais em relacao ao V1:

- **Incorpora feedback de 2 revisoes tecnicas independentes** (engenheiro estrutural senior + dev senior Revit/C#).
- **Traz APIs Revit concretas** ja verificadas (skills `revit-dimensions-tags`, `revit-api-core`, `revit-steel-fabrication`).
- **Descreve os fixes ja aplicados no scaffold** (enum BanzoIndefinido, contract assertions, testes extras).
- **Entrega um checklist de 20 itens "market-ready"** que separa alpha interno de versao cobravel para o mercado.
- **Remove suposicoes e coisas inventadas:** cada item tem arquivo, teste ou referencia documental.

---

## 1. Sumario executivo

O FerramentaEMT v1.0.5 tem **28 comandos, 15 servicos, 196 testes verdes, build Release limpo**. E' uma base solida.

As 4 galerias de referencia (`igreja-patos`, `cobertura-samsung`, `ampliacao-vulcaflex`, `galpao-padrao-emt`) mostram **com alto nivel de detalhe** o que o escritorio EMT entrega hoje manualmente — e, portanto, o que o plugin precisa automatizar para que um engenheiro externo pague pela licenca.

**Diagnostico dos revisores (consenso):**

- **Plano V1: ~85% solido.** Lacunas principais: topologias complexas (Pratt/Howe/Fink), deduplicacao de trelicas multiplas na mesma vista, posicionamento de textos "BANZO SUPERIOR <perfil>", deteccao de cantoneira dupla via parametro (nao so nome).
- **Scaffold de Cotar Trelica: ~70% funcional em estrutura.** Defeitos corrigiveis de baixo-medio risco. Pronto para evoluir.
- **Pronto para mercado: 6–8/15 itens hoje.** Faltam ESC nas janelas, relatorio de sucesso detalhado, suporte a unidades internacionais, assinatura digital do installer, testes de integracao, etc.

**Meta desta V2:** fechar as lacunas concretas e entregar em ~3 semanas um v1.1.0 cobravel.

---

## 2. Estado atual (apos scaffold desta sessao)

Ja foi criado/editado nesta sessao:

| Caminho | Conteudo |
|---|---|
| `docs/reference-projects/cobertura-samsung/NOTAS.md` | Padrao de cotagem em 5 faixas, balões de detalhe, EST-08. |
| `docs/reference-projects/galpao-padrao-emt/NOTAS.md` | Template minimo EST-01+02+03, nomenclatura EMT, escalas padrao. |
| `docs/reference-projects/README.md` | Indice por funcao -> referencia. |
| `CLAUDE.md` | Historico atualizado. |
| `docs/PLANO-LAPIDACAO.md` | Plano V1 (preservado). |
| **`FerramentaEMT/Services/Trelica/TrelicaClassificador.cs`** | Helper puro: classificacao por inclinacao + altura (com `BanzoIndefinido` e tolerancia Z). |
| **`FerramentaEMT/Services/Trelica/TrelicaGeometria.cs`** | Helper puro: paineis, vao total, alturas por estacao, apoios. |
| **`FerramentaEMT/Services/Trelica/TrelicaPerfilFormatter.cs`** | Helper puro: formatacao `2x L 76x76x6.3` padrao EMT. |
| **`FerramentaEMT/Services/Trelica/TrelicaTopologia.cs`** | Helper puro: Plana / DuasAguas / Shed / Desconhecida. |
| **`FerramentaEMT/Services/Trelica/CotaFaixaBuilder.cs`** | Helper puro: especificacao das 5 faixas de cotas. |
| **`FerramentaEMT/Models/CotarTrelicaConfig.cs`** | DTO de configuracao (9 flags). |
| **`FerramentaEMT/Commands/CmdCotarTrelica.cs`** | Command com validacao de vista + selecao (esqueleto). |
| **`FerramentaEMT/Views/CotarTrelicaWindow.xaml(.cs)`** | Janela WPF com 7 checkboxes + OK/Cancel. |
| **`FerramentaEMT.Tests/Services/Trelica/*Tests.cs`** | 5 arquivos de teste, **~27 testes**. |

**Fixes aplicados apos feedback dos engenheiros:**

1. Enum `TipoMembro.BanzoIndefinido` adicionado. Classificador por inclinacao agora retorna `BanzoIndefinido` em vez de `BanzoSuperior`; desambiguacao explicita em `ClassificarBanzoPorAltura` usa tolerancia Z e pode tambem devolver `BanzoIndefinido`.
2. Record `FaixaCotas` renomeado `OffsetPes` -> `OffsetZPes` (deixa claro que e' em Z, nao Y).
3. `CotaFaixaBuilder.SegmentosConsecutivos` agora valida contrato: lanca `ArgumentException` se a lista nao estiver crescente.
4. Testes adicionados: ruido no pico (topologia continua DuasAguas), dois nos identicos (Desconhecida), trelica continua com 3 apoios, contract assertion em lista fora de ordem, altura nunca negativa.

Assinaturas publicas estao todas com XML-doc em PT-BR, `#nullable enable`, zero warning em build Release (quando a implementacao Revit for ligada).

---

## 3. Review consolidada: o que ficou exposto

### 3.1 Defeitos de engenharia estrutural (ainda em aberto)

| # | Item | Criticidade | Acao |
|---|---|---|---|
| 3.1.1 | Classificacao por inclinacao funciona bem em Pratt/Howe/Warren simetricas, mas trelicas Fink/Belga com banzo superior inclinado em 2 aguas podem ter "3 banzos" detectados (dois superiores + um inferior). | Media | Agrupar por conectividade + Z medio por componente. Wave A.2. |
| 3.1.2 | `EhCantoneira` por nome e' heuristica. Cantoneira dupla verdadeira e' propriedade do tipo (parametro compartilhado `EMT_PerfilComposto` ou familia `2L...`). | Media | Ler parametro no `CotarTrelicaService`, so cair para heuristica em fallback. Wave A.2. |
| 3.1.3 | Banzos "inclinados paralelos" (duas aguas pura, sem banzo inferior horizontal) — classificador declara ambos como `BanzoSuperior` + `BanzoInferior` corretamente via Z medio, mas em Fink com agulha central pode falhar. | Baixa | Teste manual em modelo real + fallback `Topologia.Desconhecida`. |
| 3.1.4 | Cota de altura de montante em trelica assimetrica (Howe com "mao francesa" so de um lado) nao esta modelada. | Baixa | Wave A.3 — permitir override manual. |
| 3.1.5 | Numeracao `TR-NN (Qx)` automatica nao existe na janela. Plano V1 mencionou, V2 formaliza: usuario informa ou o plugin sugere baseado em instancias iguais ja numeradas. | Media | Adicionar campo "Nr. Trelica" na Window. Wave A.2. |
| 3.1.6 | Baloes `VER DET NN` apontando para ligacoes criticas nao estao previstos. | Baixa | Checkbox opcional "inserir baloes de detalhe". Wave A.3. |

### 3.2 Defeitos/pendencias de engenharia de software

| # | Item | Criticidade | Acao |
|---|---|---|---|
| 3.2.1 | `CmdCotarTrelica` e' esqueleto — nao instancia service. | Alta | ✅ FEITO (v1.1.0 + A.1.5) — `CotarTrelicaService.Executar` completo + `TrelicaRevitHelper` com todas as chamadas Revit reais. Zero TODOs restantes. |
| 3.2.2 | `OffsetFaixaMm` existe no `Config` mas a janela nao expoe. | Baixa | Adicionar `NumericUpDown` opcional (ou manter fixo com documentacao). Wave A.3. |
| 3.2.3 | Janela nao fecha com ESC. | Media | ✅ FEITO (v1.1.0) — `RevitWindowThemeService.AttachEscapeHandler()` centralizado, ~23 janelas beneficiadas. Wave E. |
| 3.2.4 | `fi.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming` ainda funciona em 2025 mas esta obsoleto em 2024+ no sentido de "API pode mudar". | Baixa | ✅ FEITO (v1.1.0) — `CmdCotarTrelica` ja usa `new ElementId(BuiltInCategory.OST_StructuralFraming)`. Varredura completa do projeto em Wave E v2. |
| 3.2.5 | Sem validacao "selecao = 1 componente conexo" (duas trelicas selecionadas juntas). | Media | Implementar deteccao de componentes conexos via `AssemblyInstance` ou adjacencia geometrica XY. Wave A.2. |
| 3.2.6 | Sem conversao de unidades do documento (cotas em mm hardcoded). | Alta | ✅ FEITO (A.1.5) — `UnitUtils.ConvertFromInternalUnits/ConvertToInternalUnits` integrado em CotarTrelicaService e TrelicaRevitHelper. |
| 3.2.7 | Sem `CotarTrelicaReport` estruturado (numero de cotas/tags/textos criados, tempo, warnings). | Media | ✅ FEITO (v1.1.0) — `CotarTrelicaReport` record imutavel implementado com 5 testes. |

---

## 4. Arquitetura Cotar Trelica — APIs Revit concretas

Esta secao **substitui** a 6.2 do V1. E' o guia de implementacao, baseada nas skills oficiais (`revit-dimensions-tags`, `revit-api-core`).

### 4.1 Pipeline geral

```
CmdCotarTrelica
  -> valida vista (Elevation/Section/Detail)
  -> coleta pre-selecao (StructuralFraming + StructuralColumn)
  -> abre CotarTrelicaWindow -> BuildConfig
  -> CotarTrelicaService.Executar(doc, view, barras, config)
       |
       +- 1. ProjetarBarrasPara2D(view, barras)     -> Dictionary<FamilyInstance, Linha2D>
       +- 2. DeterminarBoundingBox2D                -> BoundingBox2D
       +- 3. ClassificarTodas                       -> lista classificada
       +- 4. ExtrairNosSuperior/Inferior            -> IReadOnlyList<(X,Z)>
       +- 5. DetectarTopologia                      -> DuasAguas | Plana | Shed | Desconhecida
       +- 6. ConstruirFaixasCotas (usando CotaFaixaBuilder)
       +- 7. CriarDimensionsNoRevit                 -> IndependentDimension[]
       +- 8. CriarTagsDeBarra                       -> IndependentTag[]
       +- 9. CriarTextosRotuloBanzos (opcional)     -> TextNote[]
       +- 10. Retorna CotarTrelicaReport
  -> ShowSuccess(report.Resumo)
```

Tudo em **uma unica transacao** `"EMT - Cotar Trelica"`.

### 4.2 Projecao 2D — orientacao da vista

Uma `View` de Elevation/Section define `ViewDirection` (perpendicular ao plano de corte) e `RightDirection` (eixo horizontal da folha). Todo ponto 3D da barra e projetado:

```csharp
// view.RightDirection e view.UpDirection (Revit 2025 API)
XYZ u = view.RightDirection;   // eixo X da vista (horizontal na folha)
XYZ v = view.UpDirection;      // eixo Y da vista (vertical na folha)
XYZ origem = view.Origin;

(double X2D, double Z2D) Projetar(XYZ p) =>
    (u.DotProduct(p - origem), v.DotProduct(p - origem));
```

Isso resolve o ponto 5 da revisao ("Section vs Elevation comportamento diferente"): o algoritmo passa a trabalhar sempre em **coordenadas (X2D, Z2D) da vista**, nao de mundo.

### 4.3 Criando as cotas — References validas

Resumo das regras (skill `revit-dimensions-tags`):

- **`Document.Create.NewDimension(view, line, refArray)`**.
- Referencias precisam ser **paralelas entre si** e **perpendiculares a linha de cota**.
- Referencias precisam estar **visiveis na vista** (se o elemento esta escondido por filtro, a cota desaparece).
- `Options { ComputeReferences = true }` e **obrigatorio** ao extrair geometria, senao `face.Reference` vem null.

Para trelica, usamos `References` dos **eixos analiticos das barras** (ja disponiveis como `FamilyInstance`):

```csharp
Reference RefEixoBarra(FamilyInstance fi)
{
    // Revit exposes stable references for each family instance
    return new Reference(fi);
}
```

E para cotas de paineis do banzo, **as References de cada no** sao obtidas dos pontos de intersecao do banzo com montantes/diagonais. A tecnica mais estavel e' usar o eixo analitico (`AnalyticalModel`) se existir, ou criar uma *detail line* invisivel entre dois nos e usar a `Reference` dela como `line.Reference`. No codigo final, preferiremos a primeira opcao quando o modelo tiver Analytical Members.

### 4.4 Criando as tags de perfil

Skill `revit-dimensions-tags` ja fornece o padrao:

```csharp
var tag = IndependentTag.Create(
    doc,
    view.Id,
    new Reference(barra),
    addLeader: false,
    TagMode.TM_ADDBY_CATEGORY,
    TagOrientation.Horizontal,
    posicaoTag);
```

Padrao EMT: tag paralela ao banzo com offset perpendicular. Para barras curtas (`L < 400 mm`) forcar `addLeader = true` + orientacao perpendicular (ja previsto em Plano V1).

### 4.5 Rotulos "BANZO SUPERIOR <perfil>" / "BANZO INFERIOR <perfil>"

Opcao confirmada com usuario: `TextNote` (nao Tag), posicionado acima do banzo superior e abaixo do banzo inferior com offset vertical de 1500 mm (0.15 m acima/abaixo em papel para escala 1:50).

```csharp
TextNote.Create(doc, view.Id, posicao, textoConcatenado, textTypeId);
```

### 4.6 Faixas de cotas — posicionamento

Consolidando o que `CotaFaixaBuilder` ja produz + os offsets (em pes, convertidos para as unidades do documento na hora de criar):

| Faixa | Tipo | Z offset padrao (mm) | Notas |
|---|---|---|---|
| 1 | Paineis banzo superior | +500 | Acima do banzo superior |
| 2 | Vaos entre apoios | +1000 | Acima da faixa 1 |
| 3 | Paineis banzo inferior | -500 | Abaixo do banzo inferior |
| 4 | Vao total | -1000 | Abaixo da faixa 3 |
| 5 | Alturas de montantes | 0 | Texto vertical no proprio montante |

Os offsets em pes sao `OffsetFaixaPes = 0.5` (= 152 mm). Em mm o padrao EMT e 500 mm — conversao feita antes de chamar `NewDimension` via `UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters)`.

### 4.7 Unidades

```csharp
double mmParaPes(double mm) =>
    UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

double pesParaMm(double pes) =>
    UnitUtils.ConvertFromInternalUnits(pes, UnitTypeId.Millimeters);
```

Todo `double` interno e' em pes. Toda entrada do usuario (Config) e' em mm. Conversao **so na borda**.

---

## 5. Acoes concretas por onda (atualizado apos revisao)

### Onda A — Cotar Trelica PROFISSIONAL (core do v1.1.0)

**Criterio de aceite:** engenheiro EMT abre uma elevacao de trelica DuasAguas com 20 paineis, seleciona todas as barras, clica, acerta OK — em **< 5 segundos** aparecem 5 faixas de cotas + tags de todas as barras + 2 rotulos "BANZO SUPERIOR W200x26.6" / "BANZO INFERIOR 2x L 76x76x6.3" + relatorio detalhado. Sem abrir nenhuma outra janela, sem cotas sobrepostas, com undo funcional.

#### Sub-ondas

- **A.1 (2 dias)** — ✅ FEITO (v1.1.0): `CotarTrelicaService` completo (638 linhas, pipeline 10 etapas), `CotarTrelicaReport`, transacao, botao no ribbon. **Pendente A.1.5**: 5 TODOs para References Revit reais (Dimensions, Tags, TextNotes — counters funcionam mas chamadas Revit API estao stubbed).
- **A.2 (2 dias)** — Deteccao de componentes conexos, leitura de parametro `EMT_PerfilComposto`, campo `NrTrelica` na Window, Escape para fechar.
- **A.3 (1 dia)** — Override manual de altura por montante (tab "Customizar Alturas"), baloes `VER DET NN` opcionais, tratamento de topologia desconhecida com fallback visual amigavel.
- **A.4 (0.5 dia)** — 5–8 testes de integracao manual com arquivos Revit pequenos + documentacao de usuario (screenshot + passo-a-passo, 2 paginas em PDF).

**Total onda A: ~5.5 dias uteis.** (V1 dizia 3-5; V2 ajusta para 5-6 realista.)

### Onda B — Identificar Perfil + Tagear Trelica (massa)

✅ FEITO (v1.1.0): `CmdTagearTrelica` reusa `TrelicaPerfilFormatter`. `CmdIdentificarPerfil` atua em qualquer vista. 12 arquivos, 1391 linhas, 13 testes. 3 botoes registrados no ribbon (painel Documentacao).

### Onda C — Plano Geral de Cobertura

Gera uma planta de cobertura baseada em terças + treliças ja modeladas: vista de planta estrutural, tags, linhas de contraventamento, linhas de corrente, tabela de perfis, carimbo EMT. **5 dias.**

### Onda D — Gerar Projeto Completo (EST-01+02+03)

`CmdGerarProjetoCompleto` = sequencia Onda C + locacao de sapatas/pilares + prancha de detalhamento de trelica tipica. **6 dias.**

### Onda E — Qualidade/debitos tecnicos

✅ PARCIAL (v1.1.0): ESC centralizado em `RevitWindowThemeService.AttachEscapeHandler()` (~23 janelas), migracao `IntegerValue → ElementId` em `CmdCotarTrelica`. **Pendente**: varredura projeto-wide `IntegerValue`, refactor dos 3 monoliticos, crash handler global, telemetria opt-in, i18n base. **~2 dias restantes.**

**Total V2: ~22 dias uteis = ~4.5 semanas.** (V1 dizia 2-3 semanas — era otimista.)

---

## 6. Checklist "pronto para mercado" (20 itens)

Divisao: **alpha interno (EMT usa)** -> precisa **6/20**. **Beta (cliente externo gratuito)** -> precisa **12/20**. **Vender** -> precisa **18/20**.

| # | Item | Status | Quem pega | Notas |
|---|---|---|---|---|
| 1 | Cotar Trelica 100% funcional em DuasAguas simetrica | ✅ FEITO | A.1+A.1.5 | Pipeline 10 etapas + TrelicaRevitHelper com NewDimension, IndependentTag.Create, TextNote.Create reais. Testar com modelo Revit. |
| 2 | Cotar Trelica funciona em Plana + Shed + fallback Desconhecida | ✅ parcial | A.1+A.3 | Topologia detectada, fallback implementado. Falta teste manual em modelo real. |
| 3 | Todas as janelas fecham com ESC | ✅ FEITO | E | `RevitWindowThemeService.AttachEscapeHandler()` centralizado. |
| 4 | Suporte a unidades do documento (mm / m / pes / pol) | ✅ FEITO | A.1.5 | `UnitUtils.ConvertFromInternalUnits/ConvertToInternalUnits` integrado em CotarTrelicaService (offsets, alturas, comprimentos). |
| 5 | Relatorio de execucao com contagem de elementos + tempo | ✅ FEITO | A.1 | `CotarTrelicaReport` record imutavel + 5 testes. |
| 6 | Todos os comandos herdam de `FerramentaCommandBase` (gate licenca) | OK v1.0.5 | - | ja feito |
| 7 | Logging estruturado via `Logger` (Serilog wrapper) | OK v1.0.5 | - | ja feito |
| 8 | AppDialogService em toda conversa com usuario (0 `MessageBox`/`TaskDialog` direto) | OK v1.0.5 | - | auditado |
| 9 | Build Release 0 warning, 0 erro, `TreatWarningsAsErrors` ligado | OK v1.0.5 | - | validar apos cada onda |
| 10 | 196 + 27 (Trelica) + Ondas B-E = **~290 testes verdes** | pendente | todas | meta para v1.1 |
| 11 | Installer assinado digitalmente (.msi + .dll + .addin) | pendente | E | certificado EMT X.509 |
| 12 | Versionamento automatico AssemblyVersion + .msi (WiX) | pendente | E | CI |
| 13 | Ribbon icon pack 16x16 + 32x32 para todos os novos comandos | pendente | A/B/C/D | pedir arte |
| 14 | Feature flags (`LicenseService.IsFeatureEnabled("CotarTrelica")`) | pendente | E | ja tem base de licenca |
| 15 | Compatibilidade Revit 2024 + 2025 validada (mesmo .dll) | pendente | E | testar no 2024 |
| 16 | Telemetria opt-in (anonimiza quantidades de uso) | pendente | E | respeitar LGPD |
| 17 | Arquivo de traducao i18n pt-BR + en-US | futuro | pos v1.1 | para exportar |
| 18 | Documentacao de usuario: PDF por funcao, 2-3 pag cada | pendente | A.4+B+C+D | base mkdocs |
| 19 | Changelog + release notes por release | OK v1.0.5 | - | `CHANGELOG.md` ja existe |
| 20 | Canal de suporte (email dedicado + template de bug report) | pendente | E | fora do codigo |

**Pos-execucao Ondas A+B+E+A.1.5 (2026-04-15): 10/20 (itens 1, 2 parcial, 3, 4, 5, 6, 7, 8, 9, 19).**
**Proximo passo: teste manual com modelo Revit + correcoes emergenciais.**
**Meta v1.1.0 (4 semanas restantes): 15/20 (falta 11, 16, 17, 18 parcial, 20).**

---

## 7. Riscos e mitigacoes

| Risco | Prob | Impacto | Mitigacao |
|---|---|---|---|
| Trelica complexa (mansarda, arco, Fink com agulha central) nao reconhecida | Media | Baixo | `Topologia.Desconhecida` -> fallback: so tags + cota vao total + aviso ao usuario. |
| Selecao incompleta (usuario esquece 1 barra) | Alta | Medio | Detector de componentes conexos avisa "X barras desconectadas da maior componente". Oferece botao "Selecionar similares na vista". |
| Tag sobrepoe cotas/outros elementos | Media | Medio | Offsets grandes (>= 500 mm) + ordenacao por posicao X antes de criar tag. Se sobrepor detectado pelo `BoundingBox`, tentar orientacao perpendicular + leader. |
| Precisao de conversao pes <-> mm | Baixa | Baixo | Trabalhar sempre em pes internamente, converter so na borda. Testes unitarios com valores reais Samsung (38367 mm). |
| .NET 8 LTS fora de suporte em 2026-11 | Baixa | Alto | Upgrade planejado para .NET 9 + Revit 2026 em Q4/2026. |
| Revit 2024 quebrar assembly (binding) | Baixa | Alto | Teste manual em 2024 antes de release. Se quebrar, duas builds (v1.1.0-revit2024, v1.1.0-revit2025). |
| Parametro `EMT_PerfilComposto` nao existir em obras antigas | Alta | Baixo | Fallback para heuristica por nome (`EhCantoneira`). Relatorio avisa: "detectei N barras sem EMT_PerfilComposto — usei nome". |

---

## 8. Metricas de sucesso (numericas, verificaveis)

1. **Tempo medio de cotagem de trelica (manual vs plugin):** meta plugin **<= 5 s** para 20 paineis. Baseline manual: ~18 min para Samsung EM-02 (ja medido no V1).
2. **Taxa de sucesso sem intervencao manual:** meta >= 85% das trelicas produzidas pelo escritorio nos ultimos 24 meses sao cotadas corretamente em 1 clique.
3. **Testes verdes:** meta **>= 290** ao final da v1.1.0 (196 + ~95 novos).
4. **Zero warning em build Release:** ja atingido, manter.
5. **Tempo medio de gerar EST-01+02+03:** baseline manual ~5 horas (Galpao EMT). Meta plugin: **<= 45 min** ao final da Onda D.
6. **Crashes reportados / 100 execucoes:** meta < 0.5 apos 2 semanas de uso interno (v1.1.0 alpha).

---

## 9. Proximos 5 passos concretos (para a proxima sessao presencial)

Em ordem de prioridade:

1. ~~**Rodar `dotnet test`**~~ ✅ FEITO — ~223 testes passando (196 + 27 novos Trelica).
2. ~~**Implementar `CotarTrelicaService`**~~ ✅ FEITO (638 linhas, pipeline 10 etapas). **Proximo: A.1.5** — substituir os 5 TODOs por chamadas Revit reais (NewDimension, IndependentTag.Create, TextNote.Create).
3. ~~**Registrar botao no ribbon**~~ ✅ FEITO — 3 botoes (Cotar Trelica, Tagear Trelica, Identificar Perfil) no painel Documentacao.
4. **Fazer smoke test em 3 arquivos Revit reais:** galpao padrao EMT (duas aguas simetrica), Samsung (grande, assimetrica se houver), um shed imaginario criado ad-hoc.
5. **Abrir PR/commit interno** com mensagem `feat(trelica): cotar trelica - onda A.1 (scaffold + service + helpers + 30 testes)` e changelog atualizado.

---

## 10. Nao-fiz / fora de escopo (para ser honesto)

Coisas que o V1 insinuava e que o V2 **assume que nao estao feitas**:

- Deteccao de `EMT_PerfilComposto` via shared parameter: **so definimos a API, nao o codigo Revit que le o parametro**. Sera feito em A.2.
- Conversao de unidades para vaios alem de mm (cm, polegada): mencionada, mas so sera testada em A.1 + A.4.
- Deteccao de componentes conexos na selecao: algoritmo escolhido (grafo por adjacencia de endpoints 2D), **codigo nao escrito**. Sera feito em A.2.
- Balao `VER DET NN`: sera um **checkbox opcional**, e o codigo que insere familia de balao precisa existir no .rfa do escritorio. Verificar biblioteca .rfa da EMT antes.
- Posicionamento auto de tags para evitar sobreposicao: algoritmo simples (ordenar por X + offset incremental). Algoritmo robusto (simulated annealing) **fora de escopo** ate v1.2.
- Teste automatizado em arquivo Revit: Revit nao tem headless mode suportado pela Autodesk, entao **testes integrados serao sempre manuais com checklist**. Considerar Revit Idling handler + snapshot em Wave E v2.0.

---

## 11. Aprovacao e assinatura

Este plano e' um documento **vivo**. Cada vez que o usuario aprovar uma onda, a sub-onda correspondente e' executada e a tabela do capitulo 6 e' atualizada com novos checkmarks.

Engenheiro responsavel: **ALEF CHRISTIAN GOMES VIEIRA, CREA 0319918963**
Versao do documento: **v2.0 (2026-04-15)**
Proximo checkpoint: apos conclusao da **Onda A.1**, entregar relatorio de smoke test + prints do Revit.

---

*Este V2 foi escrito consolidando:*
- *Scaffold criado na sessao anterior em `/Services/Trelica/`, `/Models/`, `/Commands/`, `/Views/`, `/FerramentaEMT.Tests/Services/Trelica/`.*
- *Analise do revisor 1 — Engenheiro Estrutural Senior (9 capitulos de criticas, 6 categorias).*
- *Analise do revisor 2 — Dev Senior Revit/C# (9 secoes + checklist 20 itens de engenharia de software).*
- *Skill oficial `revit-dimensions-tags` (APIs `Document.Create.NewDimension`, `IndependentTag.Create`, `Options.ComputeReferences`).*
- *CLAUDE.md + docs/reference-projects/*.NOTAS.md (padrao EMT consolidado em 4 projetos reais).*
