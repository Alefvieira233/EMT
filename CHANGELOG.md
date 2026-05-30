# Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
versionamento [SemVer](https://semver.org/lang/pt-BR/).

---

## [Unreleased]

**Auditoria 2026-05-25 CONCLUÍDA** com v2.8.0 (3 waves, 13 PRs estruturais).
**Incorporação Victor** em v2.8.1 (2 PRs).
**Conexão Terça v2** em v2.8.2 (algoritmo face-based + spec da família).
**Hotfix Conexão Terça v3** em v2.8.3 (centramento automático + 3 fixes campo).
**Hotfix IFC falso-cancel** em v2.8.6 (6 fixes + 1 enhancement).
**Sprint Hardening Dia 1** em v2.8.7 (7 melhorias defensivas pós-auditoria 5-agents).
**Hotfix Diagrama de Montagem — cotas inválidas** em v2.8.8 (5 ondas, reescrita das 3 funções de cota + failure handler).

Próximas atividades dependem de eventos externos:

- **External-dependent:** code signing efetivo (cert Sectigo OV — aguarda compra), bump Authenticode flag default → TRUE (após primeira release assinada), EULA/Privacy/TOS revisados (aguarda advogado TI)
- **Strategic-dependent:** i18n EN/ES (F13 deferido — depende de decisão de expansão LATAM; infra não criada pra evitar dead code)
- **Manual no Revit (Alef + Victor):** validar v2.8.3 no mesmo galpão do teste anterior — confirmar que conexão fica na altura da terça, viga do meio recebe conexão, face externa selecionada (ou marca "Inverter face" se preciso)
- **Manual no Revit (Alef):** validar v2.8.6 conversão IFC no mesmo arquivo que apresentou "Cancelado" falso — confirmar que conversão completa sem dialog de cancelamento, perfis inclinados são preservados, log lista ignorados com motivo

---

## [2.8.9] - 2026-05-30

Auditoria profunda do codebase (6 agentes sênior em paralelo: bootstrap/infra,
licenciamento, metálico-geometria, documentação/CNC, PF pré-fabricado, build/CI)
seguida de correções de alta confiança (lógica pura + hardening + docs). Relatório
completo e backlog priorizado em `docs/audits/AUDITORIA-PROFUNDA-2026-05-30.md`.

### Fixed

#### Cotar Treliça — função estava 100% inoperante (P0)

- `CotarTrelicaService.ExtrairNosBanzo` reclassificava cada barra com
  `TrelicaClassificador.ClassificarPorInclinacao`, que **nunca** retorna
  `BanzoSuperior`/`BanzoInferior` (só `BanzoIndefinido`/`Montante`/`Diagonal`). A
  comparação `tipo == tipoBanzo` era **sempre falsa** → `nosSuperior`/`nosInferior`
  saíam vazios → o pipeline abortava em "Não foi possível detectar banzos válidos"
  para **toda** treliça. Agora reutiliza a classificação do passo 3 (que desambigua
  por altura via `ClassificarBanzoPorAltura`) e delega a coleta de nós ao novo
  helper puro `TrelicaGeometria.ColetarNosDoBanzo`.
- `TrelicaRevitHelper.ObterReferenciaExtremo` retornava `new Reference(FamilyInstance)`
  como fallback — **proibido** em `doc.Create.NewDimension` (lança no commit da
  transação, derrubando a faixa de cota inteira). Agora retorna `null` e o caller
  pula o segmento.
- Sentinela `noSup.X == 0 || noInf.X == 0` na faixa de alturas descartava nós
  legitimamente em X≈0 e aceitava "não encontrado" (tuple default) como válido com
  Z=0 → alturas de montante ausentes/absurdas. Substituído por busca por índice com
  tolerância (`TrelicaGeometria.IndiceNoMaisProximo`).
- Altura do montante calculada via round-trip 3D (`DesprojetarPonto` + leitura do
  `.Z` mundial) — só correto quando `UpDirection == +Z` mundial. Agora usa a
  separação vertical 2D direta (`|noSup.Z − noInf.Z|`).
- Gate duplicado (`EncontrarBarraNoNo` chamado 2× com os mesmos argumentos) removido.
- "Cotas criadas" no relatório somava `faixa.Segmentos.Count` em vez de contar 1 por
  `Dimension` encadeada (inflava a contagem).
- Vãos entre apoios passam a derivar dos extremos do **banzo inferior** (onde a
  treliça apoia), não do superior (que pode ter balanço em duas águas).
- +6 testes de regressão em `TrelicaGeometriaTests` (`ColetarNosDoBanzo`,
  `IndiceNoMaisProximo`).

#### Outras correções de campo

- **Verificar Modelo** (`OverlappingElementsRule`): `intersection.Volume` (pés³,
  unidade interna do Revit) era comparado contra um limiar documentado como m³
  (`0.0001`). Sem conversão, o limiar efetivo era ~35× mais sensível (~2,8 cm³ em
  vez de 100 cm³) → enxurrada de falsos positivos de sobreposição. Agora converte
  para m³ antes de comparar.
- **Janelas PF** (`PfEstacaRebarWindow`, `PfColumnStirrupsWindow`): parsing decimal
  padronizado em `SteelBIM.Utils.NumberParsing`. A estaca tentava `CurrentCulture`
  primeiro (viola a regra de ouro do projeto; "1.5" quebrava em PC pt-BR) e dava
  `NullReferenceException` se o texto fosse null — cobrimento/espaçamento podiam ser
  lidos errado, posicionando armadura indevidamente.
- **Marcar Peças** (`GravarMarca`): a proteção "não sobrescrever" usava só
  `param.AsString()`, que retorna null em parâmetro numérico → marca existente era
  sobrescrita mesmo com `SobrescreverExistentes=false` (perda de dado). Agora usa
  `AsString() ?? AsValueString()`.
- **Travamento** (`TravamentoService`): `catch {}` genérico no `PickObjects`
  mascarava todas as exceções; agora trata só `OperationCanceledException` e deixa o
  resto subir para o handler de `FerramentaCommandBase`.
- **Boot** (`App.OnStartup`): construção do ribbon protegida por try/catch raiz — uma
  exceção ali (internalName duplicado em reload, PNG corrompido) escapava do
  `OnStartup` e o Revit **desabilitava o add-in inteiro**. Agora loga e segue (ribbon
  parcial em vez de plugin que não carrega).
- **Privacidade/LGPD** (`CrashReporter`): `Environment.UserName` (PII) não é mais
  gravado no crash dump local — arquivo que o usuário envia ao suporte. Alinha com a
  decisão da v2.8.7 que removeu o UserName do Logger.

### Changed

- Bump de versão 2.8.8 → 2.8.9 (`AssemblyInfo.cs`).
- Documentação sincronizada com o estado real: `CLAUDE.md` (dizia v2.0.3/777 testes
  e apontava `SteelBIM.Distribuicao/` inexistente), `README.md` (badge de versão +
  contagem de testes contraditória 1223 vs 1241 + contagem de comandos 48→50),
  `docs/ROADMAP.md` e `SECURITY.md` (versão estável defasada).
- Arquivos órfãos da era v1.6.0 movidos para `docs/historico/`.

### Backlog aberto (detalhado em `docs/audits/AUDITORIA-PROFUNDA-2026-05-30.md`)

- **Licenciamento (P0 arquitetural — requer decisão):** esquema HMAC simétrico — o
  segredo que verifica a chave é o mesmo que a assina e é distribuído com o plugin;
  um aluno técnico pode extraí-lo do próprio disco e forjar chaves ilimitadas.
  Recomendação: migrar para assinatura assimétrica (Ed25519/RSA) com a chave privada
  só no EmtKeyGen e a pública embarcada no plugin.
- **DSTV/NC1 (P1 — requer Revit + referência real):** o formato emitido diverge da
  spec NC1 (ordem de campos do bloco ST, blocos AK/BO/SC), provável causa de arquivos
  recusados por leitores/máquinas CNC.
- Itens que exigem validação no Revit real (cotas verticais do Diagrama de Montagem
  em vistas ao longo de Y; nomes de parâmetro com mojibake no bloco de 2 estacas;
  sobreposição de estribos PF na junção de zonas) estão catalogados no relatório.

---

## [2.8.8] - 2026-05-29

### Hotfix Diagrama de Montagem — cotas inválidas (5 ondas)

Bug reportado pelo Alef em teste real: ao executar "Diagrama de Montagem"
com todas as opções marcadas, o Revit abria dialog modal "Excluir cotas"
oferecendo só essa opção. As tags funcionavam, mas as 3 funções de cota
falhavam silenciosamente — usuário precisava excluir tudo pra prosseguir.

#### Root cause analysis

Análise técnica em `docs/audits/DIAGRAMA-MONTAGEM-FIX-PLAN-2026-05-29.md`.
Resumo: **8 bugs estruturais** nas funções `CriarCotasEntreEixos`,
`CriarCotaTotalConjunto` e `CriarCotasVerticais` — todos convergindo no
mesmo padrão de erro: **coordenadas world-space vs view-space misturadas
sem conversão**.

- `CriarCotasEntreEixos`/`CriarCotaTotalConjunto`: linha de cota construída
  com `cropBox.Max.Y` (UV LOCAL da vista) somada em `vista.UpDirection`
  (world). Resultado: `Line.CreateBound` em ponto absurdo no espaço
  modelo → Refs dos Grids não alinhavam → Revit rejeitava no commit.
- Não filtrava Grids visíveis na vista — pegava todos do projeto.
- `CriarCotasVerticais`: usava `new Reference(FamilyInstance)` que **é
  proibido pela API** (só funciona pra Grids/Levels/ReferencePlanes).
- `bbVista.Max.X` em world-space pra vista rotacionada — coords absurdas.
- `Y=0` hardcoded — quebra em qualquer projeto fora da origem 0,0.
- O `try-catch` dentro do `for` não evita o dialog: `NewDimension` aceita
  criar o objeto, Revit só detecta inconsistência no `tx.Commit()`.

#### Fixed (5 ondas)

##### Onda 1 — `CriarCotasEntreEixos` reescrito

[Services/DiagramaMontagem/DiagramaMontagemService.cs:431-555](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L431-L555)

- `FilteredElementCollector(doc, vista.Id)` — filtra Grids visíveis na vista.
- Projeta cada Grid no plano da vista via `DimensionPlanCalculator.ProjetarPontoNoPlano`.
- Ordena Grids pelo U (direção RightDirection) no espaço 2D da vista.
- Calcula `vLinhaCota` em coordenada V (UP), 1m acima do topo dos Grids reais.
- Reconstroi pontos 3D world-space via `ReconstruirPonto3DDaVista` — garante
  Line.CreateBound alinhada com Refs dos Grids.
- Sanidade: pula par com `p1.DistanceTo(p2) < 1mm`.

##### Onda 2 — `CriarCotaTotalConjunto` reescrito

[Services/DiagramaMontagem/DiagramaMontagemService.cs:752-820](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L752-L820)

Mesma estratégia da Onda 1, usando `primeiro/ultimo` Grid e `vLinhaCota =
vMaxDosGrids + 2*OffsetCotaAcimaGridsMm` (linha de cota empilhada 1m
acima da linha das cotas entre eixos).

##### Onda 3 — `CriarCotasVerticais` reescrito

[Services/DiagramaMontagem/DiagramaMontagemService.cs:618-744](SteelBIM/Services/DiagramaMontagem/DiagramaMontagemService.cs#L618-L744)

- `FamilyInstance.GetReferences(FamilyInstanceReferenceType.Top/Bottom)`
  para obter Reference válida (substitui `new Reference(FamilyInstance)`
  proibido).
- `xMaxWorld` calculado a partir do bbox dos elementos selecionados em
  world-space (não da vista).
- `yMedio` calculado dos elementos (substitui `Y=0` hardcoded).
- Pula clusters sem FamilyInstance com Refs Top/Bottom válidas via
  `Logger.Debug`.

##### Onda 4 — `SuppressInvalidDimensionsHandler` (safety net)

[Services/DiagramaMontagem/SuppressInvalidDimensionsHandler.cs](SteelBIM/Services/DiagramaMontagem/SuppressInvalidDimensionsHandler.cs)

`IFailuresPreprocessor` instalado nas 4 transações de cota
(`CotasEntreEixos`, `CotaTotal`, `CotasVerticais`, `Comprimentos`).
Identifica failures `Error` cujos failing elements são `Dimension` ou
`SpotDimension` e deleta silenciosamente — **garante que nunca mais
apareça o dialog modal pro usuário**. Contador `CotasSuprimidas`
exposto pra resumo final via `resultado.Avisos`.

Defesa em profundidade: mesmo se um edge case escapar dos fixes das
Ondas 1-3, o handler captura e remove a cota inválida sem interromper
o fluxo.

##### Onda 5 — Testes

[SteelBIM.Tests/Services/DiagramaMontagem/DimensionPlanCalculatorTests.cs](SteelBIM.Tests/Services/DiagramaMontagem/DimensionPlanCalculatorTests.cs)

**+13 testes novos** cobrindo:
- `ProjetarPontoNoPlano` (4 cenários: ponto no plano, acima, oblíquo, origem deslocada)
- `ProjetarPontoEm2DDaVista` (3 cenários)
- `ReconstruirPonto3DDaVista` (2 cenários incluindo round-trip)
- `Vec3.Dot` (3 cenários: ortogonal, paralelo, antiparalelo)
- Cenário integrado de ordenação de Grids num galpão simples (3 Grids X)

#### Métricas

- Build Release: 0 erros, 0 warnings
- Testes: **1241/1241** verdes (+13 novos)
- Diff: ~7 arquivos
- Plano completo em [docs/audits/DIAGRAMA-MONTAGEM-FIX-PLAN-2026-05-29.md](docs/audits/DIAGRAMA-MONTAGEM-FIX-PLAN-2026-05-29.md)

#### Validação manual pendente (Alef)

- [ ] Selecionar 3-5 vigas/pilares
- [ ] Executar Diagrama de Montagem com TODAS opções marcadas
- [ ] Confirmar: nenhum dialog modal "Excluir cotas"
- [ ] Vista gerada tem eixos + cotas entre eixos + cota total + spot elevations + tags
- [ ] Log mostra contadores corretos

---

## [2.8.7] - 2026-05-29

### Sprint Hardening Dia 1 — pós-auditoria 5 agents (Arquitetura, Performance, UX, Security, Code Quality)

Análise paralela de 5 sub-agents executada após o release v2.8.6 identificou
3 problemas convergentes (Logger sync, padrão Progress/Cancel duplicado,
adoção parcial do ADR-003) e ~30 oportunidades menores. Esta release aplica
os **7 itens "zero-risco aditivo" do Dia 1** do sprint recomendado.

Relatório completo em [docs/audits/RELATORIO-CONSOLIDADO-2026-05-29-v2.8.6.md](docs/audits/RELATORIO-CONSOLIDADO-2026-05-29-v2.8.6.md).

#### Performance

- **`Serilog.Sinks.Async 2.1.0` adicionado** + File sink envolvido em `WriteTo.Async`
  (`Infrastructure/Logger.cs:55-78`). Antes era sync com `shared: true` — em galpão
  6000 elementos com 30% ignorados pelo Conversor IFC, custava 270ms-9s só de
  logging na thread Revit API. Buffer 10000 entries, `blockWhenFull: true` para
  evitar perda silenciosa.
- **Agregação de logs no `ConverterPerfilIfcService`** (`Services/Ifc/ConverterPerfilIfcService.cs:50-205`).
  Antes v2.8.6 emitia 1 `Logger.Warn` por elemento ignorado (até 1800+ entries
  individuais). Agora 4 caminhos esperados (sem origem, sem linha, sem nível) viram
  `Logger.Debug` (filtrado em Information default); um único `Logger.Warn` agregado
  ao fim mostra o breakdown por categoria. `Warn(ex)` mantido apenas no
  caminho de exceção na criação do `FamilyInstance` (rota mais rara e
  diagnóstica).

#### Security

- **`PiiScrubber.MaskEmail`** novo helper (`Infrastructure/PiiScrubber.cs:120-149`) que
  mascara parte local do email mantendo domínio (`"alefchristian@gmail.com"` →
  `"ale***@gmail.com"`). Aplicado em `LicenseService.cs:113` no log de licença
  ativada. Antes o email vazava em cleartext em `emt-*.log` (P2-3 do audit).
  `UserName` removido dos enrichers globais do Serilog (`Logger.cs:60`) — também
  vazava em cada evento. **+5 testes** cobrindo edge cases.
- **Whitelist `https` em `Process.Start(ReleaseUrl)`** (`Views/LicenseActivationWindow.xaml.cs:170-187`).
  Antes `ShellExecute` resolvia qualquer URI registrado no Windows; agora
  rejeita `file://`, `javascript:`, etc. (P2-4 do audit).
- **`Guid.NewGuid()` no nome do SharedParams temp** (`Services/Montagem/PlanoMontagemService.cs:62`).
  Antes usava `DateTime.Now:yyyyMMddHHmmss` (previsível, permitia TOCTOU em
  `%TEMP%`). Paridade com `UpdateDownloader.cs:311`. (P2-5 do audit).

#### UX

- **`ex.Message` em vez de `ex.ToString()`** nas 3 Windows que jogavam stack trace
  completo na cara do usuário: `TercasWindow.xaml.cs:36`, `TrelicaWindow.xaml.cs:30`,
  `TravamentoWindow.xaml.cs:31`. Stack trace agora vai para `Logger.Error(ex, ...)`
  (rastreabilidade preservada).

#### Code Quality

- **Catches silenciosos documentados ou trocados por `Logger.Debug`**:
  `PfRebarService.cs:1121,1132` (tentativa-em-cadeia de criação de estribo —
  Logger.Debug nas tentativas intermediárias), `PlanoMontagemService.cs:107`
  (cleanup do temp — Debug, falha aceitável), `DiagramaMontagemService.cs:367`
  (`section.Scale = 75` — Debug, ViewTemplate pode bloquear). Catches genuínos
  de fallback geométrico em `ConexaoTercasService.cs:236,254,275,293` mantidos
  silent por design (rotina O(N) em hot loop com retorno sentinela documentado).

#### Métricas

- Build Release: 0 erros, 0 warnings
- Testes: **1228/1228** verdes (+5 testes novos do `PiiScrubber.MaskEmail`)
- Diff: ~13 arquivos, +180 / -20 LOC
- **Zero mudanças funcionais** — todas as alterações são aditivas ou substituições semanticamente equivalentes

---

## [2.8.6] - 2026-05-29

### Hotfix Conversor IFC — "Cancelado" falso e perfis sumidos

Bug crítico encontrado pelo Alef em galpão real: ao rodar o Converter Perfis IFC,
a operação aparentava sucesso na ProgressWindow mas terminava com dialog
"Operação cancelada — rollback automático aplicado" mesmo quando o usuário
não clicou em Cancelar. Resultado: TODOS os perfis convertidos eram perdidos
(rollback da Transaction). Em arquivos com perfis inclinados, alguns
sumiam, outros mudavam de posição.

#### Root cause analysis

Três bugs em camadas distintas se combinavam:

1. **`ConverterPerfilIfcWindow.OnClosed` cancelava CTS incondicionalmente** —
   se a janela fechasse por qualquer motivo (ESC global do `RevitWindowThemeService`,
   X acidental, qualquer redirecionamento de foco que disparasse Close), o handler
   chamava `_cts.Cancel()`. O serviço, ainda rodando ou recém-terminado, capturava
   `OperationCanceledException` → rollback da Transaction → perfis criados eram
   deletados em massa.

2. **`ProgressWindow.Closing` interpretava fechamento programático como cancel** —
   quando `OnConversionFinished` chamava `_progressWindow.Close()` após sucesso,
   o handler disparava o evento `Cancelled` → CTS cancelado → `wasCancelled=true` →
   dialog falso de cancelamento.

3. **3 `catch` silenciosos + 1 fluxo de null-check sem log** no service mascaravam
   por que elementos eram ignorados. Em geometria inclinada complexa, o
   `SectionAxisExtractor` falhava no fallback de bbox e o elemento era ignorado
   sem deixar rastro.

#### Fixed

- **Fix #1 (CRÍTICO) — `Views/ConverterPerfilIfcWindow.OnClosing`:** bloqueia
  fechamento da janela enquanto a conversão está em andamento. Se há CTS ativo
  e ProgressWindow visível, `e.Cancel = true` + aviso ao usuário ("Aguarde a
  conversão terminar. Se quiser abortar, clique em Cancelar na janela de
  progresso").

- **Fix #2 — `Views/ProgressWindow.IsProgrammaticClose`:** flag pública nova
  que, quando setada antes de `Close()`, faz o handler `ProgressWindow_Closing`
  ignorar o evento (não dispara `Cancelled`). `OnConversionFinished` agora
  marca `IsProgrammaticClose=true` antes de fechar.

- **Fix #3 — `Services/Ifc/ConverterPerfilIfcService.cs`:** todos os 4 caminhos
  que faziam `ignorados++; continue;` agora têm `Logger.Warn` detalhado com
  index, total, ElementId, categoria e motivo. Inclui o `catch (Exception)`
  silencioso da criação do `FamilyInstance` (agora captura `ex` e loga
  exception completa + perfil destino).

- **Fix #4 — `Views/ConverterPerfilIfcWindow.OnClosed`:** quando CTS é cancelado
  no cleanup (caso defensivo onde `OnClosing` foi bypassed), agora loga
  `Logger.Info` pra investigação caso o bug "perfis sumiram" volte a aparecer.

- **Fix #5 — `Services/Ifc/ConverterPerfilIfcService.cs`:** após `t.Commit()`,
  resumo final via `Logger.Info` com `convertidos`, `ignorados`, `total`.
  Permite ao usuário abrir o log e ver o resumo da conversão sem contar
  manualmente entries IGNORADO.

- **Fix #6 — `Utils/RevitWindowThemeService.cs`:** novo opt-out do ESC global
  via `Tag="no-escape"`. `ConverterPerfilIfcWindow.xaml` recebe a tag → ESC
  acidental não fecha mais a janela enquanto conversão roda. Defesa em
  profundidade junto com Fix #1.

#### Enhanced

- **Mensagem final com path do log** — quando há ignorados, o dialog "Conversão
  concluída" mostra o caminho do diretório de log (`Logger.LogDirectory`) pra
  o usuário investigar quais elementos foram pulados e por quê.

---

## [2.8.5] - 2026-05-29

### Hotfix Conexão Terça — heurística de face + janela cortada

Bugs encontrados pelo Alef em teste real no galpão da EMT após v2.8.4:

1. **Chapa saía deitada sobre a terça** em vez de em pé encostada na alma.
   Causa: heurística `DotProduct(BasisZ_global)` em v2.8.3 preferia faces com
   normal apontando pra cima. Em U/C com mesas pra baixo, a face SUPERIOR
   da terça (horizontal, normal +Z) vencia a face LATERAL da alma (vertical,
   normal ~0 em Z). Resultado: hospedagem face-based usando a face da MESA
   em vez da face da ALMA → chapa deitada.

2. **Janela "Conexão de Terça" cortada na parte de baixo** em DPI alto, com
   botões "Inserir" e "Cancelar" fora da viewport. Causa: `SizeToContent=Height`
   + `MaxHeight=700` + `ResizeMode=NoResize` insuficientes para conter o
   conteúdo (família + tipo + parâmetros populados + expander de ajuste fino).

#### Fixed

- **`Services/ConexaoTercasService.cs` — heurística da face em 2 passos:**
  1. **Filtra candidatas que são faces da ALMA**:
     - `|FaceNormal · tercaDir| < 0.3` — descarta faces das extremidades
       (cuja normal é paralela ao eixo da terça)
     - `|FaceNormal · BasisZ_global| < 0.7` — descarta mesas horizontais
       (cuja normal aponta vertical). Tolerância 0.7 tolera terça inclinada
       até ~45° (telhado típico)
  2. **Escolhe entre as candidatas pela proximidade ao `pt.Base`** (ponto na
     curva da viga). Em U/C, ambas as faces da alma têm Z similar mas Y
     diferente; face do MESMO lado da viga tem distância menor. Checkbox
     `InverterFace` força a face oposta. Helpers novos: `SafeNormalizeFaceNormal`
     + `DistanceFaceCenterToPoint`.

- **`Views/ConexaoTercasWindow.xaml` — reescrito pra DockPanel + ScrollViewer:**
  - Botões "Cancelar/Inserir" agora ficam fixos no rodapé (`DockPanel.Dock=Bottom`)
  - Conteúdo principal envolto em `ScrollViewer` → scrolla automaticamente se
    janela for menor que conteúdo
  - `ResizeMode=CanResize` + `Height=780` + `MaxHeight=920` + `MinHeight=400` →
    usuário pode redimensionar se precisar
  - `IsDefault=true` em btnOk + `IsCancel=true` em btnCancel — Enter/Esc
    funcionam corretamente
  - Mesmo padrão usado em `TercasWindow.xaml` (v2.6.4 hotfix UX da Bruna)

#### Atualização do tooltip do checkbox InverterFace

Tooltip atualizado pra refletir a nova heurística:
> *"Use se a chapa estiver saindo no lado errado da alma. A heurística automática escolhe a face da alma cujo centro está mais próximo da viga; marque pra forçar a face oposta."*

#### Risco

**LOW** — 2 fixes cirúrgicos. XAML é declarativo (não afeta lógica). Nova
heurística é determinística (mesma face para mesma terça em runs sucessivos)
e tem fallback: se nenhuma face passa no filtro de alma, usa as TOP-2 maiores.
Checkbox `InverterFace` continua disponível como override manual.

#### Test plan

- [x] Build Release 0 warning
- [x] **1223/1223 testes verde** (sem mudança de math pura)
- [x] `dotnet format` clean
- [ ] **Validação manual (Alef)**: rodar Conexão Terça com perfis U150 + 3 vigas:
      chapa em pé encostada na alma, sobre o topo da viga; janela inteira visível

---

## [2.8.4] - 2026-05-29

### Hotfix UI — handler duplicado em ConexaoTercasWindow

Bug encontrado pelo Alef em teste real de v2.8.3 (logs 28/05 noite): ao
clicar "Inserir" na janela de Conexão Terça, exception:
> *"DialogResult somente pode ser definido após Window ser criado e exibido como caixa de diálogo."*

#### Fixed

- **`Views/ConexaoTercasWindow.xaml.cs`**: handlers `BtnOk_Click` e
  `BtnCancel_Click` estavam registrados **2 vezes** — XAML (`Click="BtnOk_Click"`)
  E code-behind (`btnOk.Click += BtnOk_Click;`). Resultado: cada clique
  disparava o handler 2x; primeira chamada setava `DialogResult = true`
  (janela fecha), segunda chamada explodia tentando setar `DialogResult`
  em janela já fechada. Fix: remover as 2 linhas `+= BtnOk_Click` e
  `+= BtnCancel_Click` do construtor — XAML continua sendo a fonte única
  do registro. Comentário explicativo deixado no código pra evitar regressão.

#### Diagnóstico

Via log estruturado em `%LOCALAPPDATA%\SteelBIM\logs\emt-20260528.log`.
Stack trace bateu direto na linha 209 (`DialogResult = true`) com chamada
vinda de `Window.ShowDialog()` (= janela foi mostrada modal). Causa óbvia:
handler duplicado.

#### Bug latente em outras janelas

Scan revelou **mesmo padrão** em `Views/ExportarDstvWindow.xaml(.cs)` e
`Views/VerificarModeloWindow.xaml(.cs)`. Não foram reportadas ainda
(provavelmente usuário só usa Esc/Cancel onde `IsCancel=true` ignora
handler duplicado). Sweep em Wave futura ou hotfix dedicado.

#### Risco

**MINIMAL** — diff: 1 arquivo, 7 linhas. Sem mudança de lógica. Mesmo
1223/1223 testes verde.

#### Breaking change

**Nenhuma.**

---

## [2.8.3] - 2026-05-29

### Hotfix Conexão Terça — 4 fixes validados em teste real (Victor 28/05 noite)

Hotfix dos problemas identificados pelo Victor no teste real do galpão
**+ insight crítico** vindo de validação externa de referência: a família
tem **ponto de origem em CANTO** da chapa, não no centro.
`NewFamilyInstance(face, point, ...)` posiciona a origem (canto) no ponto
pedido, fazendo a chapa "vazar" pra um lado — daí o sintoma "conexão
saindo para baixo".

#### Fixed

- **(F1) Centramento automático via centroide ponderado por volume.**
  Substitui o guard antigo (que só comparava `Location.Point`) por
  correção geométrica real: depois da inserção, calcula o centroide real
  dos solids da instância e faz `MoveElement` pra centrar exatamente no
  `insertPt`. **Funciona pra famílias com origem em qualquer lugar**
  (centro, canto, ponto arbitrário) — sem exigir convenção rígida de
  modelagem. Resolve "conexão saindo abaixo da terça" mesmo em famílias
  como a do Victor que têm ponto base em canto.

- **(F2) Heurística de face hospedeira.** Em U/C, a alma tem 2 faces
  planares de área **idêntica** — `OrderByDescending(Area).First()`
  pegava uma aleatória, levando a inserção na face interna em metade
  dos casos. Fix: `Take(3)` + `OrderByDescending(FaceNormal.DotProduct(BasisZ_global))`.
  Face externa em telhado típico tem normal apontando pra cima → Dot > 0.
  Checkbox "Inverter face hospedeira" no expander Ajuste fino como
  **override manual** quando heurística errar.

- **(F3) Iteração de TODAS as vigas selecionadas.** Algoritmo só pegava
  1 extremidade da terça (a mais próxima de uma viga), perdia vigas
  intermediárias. Fix: refator do laço pra iterar TODAS as vigas e
  calcular **interseção XY** via novo helper `IntersectXY` (sistema 2×2
  com regra de Cramer, clamping em segmentos). Pra cada cruzamento
  válido, gera um ponto de inserção. Resultado: viga do meio recebe
  conexão também.

- **(F4) Z da terça preservado.** `IntersectXY` retorna Z **interpolado
  da terça** no ponto de cruzamento, não Z do eixo da viga. Resolve junto
  com o centramento o problema "saindo abaixo da terça".

#### Added

- `Utils/EngineerGeometry.ComputeWeightedCentroid(solids)` — extension
  method que retorna o centroide ponderado pelo volume de uma coleção
  de `Solid`. Defensivo a solids degenerados (try/catch no
  `ComputeCentroid()`, ignora volume ≤ 0).
- `Services/ConexaoTercasGeometry.IntersectXY(tercaP0, tercaP1, vigaP0, vigaP1, maxVerticalGapFt = 10ft)`
  — helper puro 2D que resolve sistema 2×2 com regra de Cramer.
  Clamping em segmentos (não retas infinitas), guard vertical pra
  descartar viga muito abaixo da terça. Retorna `(X, Y, Z)?` com Z
  preservado da terça.
- `Models/ConexaoTercasConfig.InverterFace` — bool opcional (default
  false) que força a face oposta à escolhida pela heurística.
- `Views/ConexaoTercasWindow.xaml(.cs)` — checkbox "Inverter face
  hospedeira" no expander Ajuste fino, com tooltip explicativo.

#### Changed

- `Services/ConexaoTercasService.cs` reescrito laço principal:
  - Iteração `foreach terca × foreach viga` substitui filtro
    "endpoint mais próximo"
  - Constantes `EndpointFreeTolFt` e `MaxDistToBeamFt` removidas
    (não usadas pelo novo modelo — `IntersectXY` faz o filtro
    semânticamente correto)
  - Centramento real substitui guard `MoveGuardThresholdFt` (constante
    mantida apenas pro threshold mínimo do offset)
  - Heurística TOP-3 + DotProduct em vez de `.First()` ingênuo

#### Tests

11 novos em `ConexaoTercasGeometryTests` cobrindo `IntersectXY`:
perpendiculares no meio dos segmentos, paralelas (det = 0), sobrepostas,
extrapolação além da terça/viga (s/t fora de [0,1]), preserva Z em
terça inclinada (interpolação correta), guard vertical (viga 20ft
abaixo descarta), viga no mesmo nível passa, **integração com 3 vigas
paralelas + terça transversal** (3 cruzamentos detectados), interseção
oblíqua, endpoint exato.

Total: **1212 → 1223 verde**.

#### Insight de fora do código

A imagem que a fonte externa de referência mandou mostrava a família
dela vista de cima com **um ponto vermelho no canto inferior** = ponto
de origem da família. A familia tem origem fora do centro, e o cálculo
dela compensa esse deslocamento manualmente. Nosso plugin agora
**tolera qualquer convenção de origem** via centramento automático —
não precisa que a família siga regra rígida.

#### Breaking change

**Nenhuma** — todos os fixes são compatíveis com famílias da v2.8.2.
Comportamento de famílias bem-modeladas (origem no centro) é
preservado: centramento é no-op nesses casos (offset < threshold).

---

## [2.8.2] - 2026-05-29

### Conexão Terça v2 — refactor face-based + spec da família

Refactor do `ConexaoTercasService` resolvendo os **4 problemas reportados
pelo Victor em teste real** (áudio 28/05) com algoritmo face-based
validado por implementação externa de referência.

#### Problemas do áudio resolvidos

1. **Duplicação por seleção Element+Face**: filtros explícitos
   `IsEndpointFree` + `IsCloseToReference` rodam ANTES da geração de
   pontos, evitando dependência exclusiva do dedup posterior.
   Dedup XY 50mm continua presente como guard final (defesa em profundidade).
2. **Falta de referência terça↔viga**: pick #2 obrigatório de vigas de
   apoio (`StructuralBeamSelectionFilter` novo). O serviço projeta a
   extremidade da terça na curva da viga mais próxima → ponto de
   inserção real.
3. **Alinhamento no eixo vs alma**: inserção face-based usando a maior
   face planar do solid da terça. Em U/C/I = alma. Sem rotação manual.
4. **Rotações -90° imprevisíveis**: eliminadas. `NewFamilyInstance(face,
   ...)` orienta corretamente; aplica-se apenas o offset opcional.

#### Algoritmo (1 caminho, sem fallback artificial)

- 2 PickObjects: terças (`StructuralFramingSelectionFilter`) + vigas
  (`StructuralBeamSelectionFilter` novo)
- Pra cada terça: filtra extremidades livres (50mm tol) + próximas de
  viga (raio 2000mm), escolhe a mais próxima, projeta na curva da viga
- Extrai `BasisX`/`BasisZ` via `terca.GetTransform()` + trata `Mirrored`
- `GetAllSolids(false)` → maior `PlanarFace` → `NewFamilyInstance(face, point, ejeX, symbol)`
- **Guard defensivo**: corrige posição via `MoveElement` se `Location.Point`
  divergir do esperado (caso família WorkPlaneBased ignorar XYZ)

#### Modo Completo opcional

Checkbox no expander "Ajuste fino" ativa:
- Raycast vertical pra `GetBottomFace` da viga → distância
- Aplica em parâmetro `Altura_PlacaInf_a_Terca` (fallback `_a_Correa` pra
  reutilizar famílias existentes em PT-BR ou ES-LATAM)
- Aplica em parâmetro `Espesor_Viga_Principal` (fallback `Espessura_Viga_Principal`)
- Se viga é perfil tipo I (W, H, IPN, IPE): desconta `tf` (espessura mesa)
- Silencioso se família não tem os parâmetros (no-op, não falha)

#### Added (PR #52)

- `Utils/EngineerGeometry.cs` — extension methods `GetAllSolids[Fine]`
  com suporte a `GeometryInstance` + `Transform`
- `Utils/StructuralBeamSelectionFilter.cs` — filtro pra `FamilyInstance`
  da categoria `OST_StructuralFraming` (usado no pick #2)
- `Services/ConexaoTercasGeometry.cs` — helpers PUROS testáveis sem
  Revit attached (recebem `ValueTuple<double, double, double>` em vez
  de `XYZ`): `IsEndpointFree`, `IsCloseToReference`,
  `MinDistanceToReferences`, `DistanceToSegment` (com clamping)

#### Added (PR #53)

- `docs/familia-conexao-terca-spec.md` — convenção de modelagem
  documentada em 14 seções: template recomendado (`Metric Structural
  Stiffener.rft`), categoria, origem e eixos (com diagrama ASCII),
  geometria mínima, parâmetros mandatórios + opcionais, passo-a-passo
  no Family Editor (12 passos), validação + troubleshooting com tabela
  sintoma→causa→solução, variantes (cantoneira, gusset, ressalto)

#### Changed (PR #52)

- `Services/ConexaoTercasService.cs` reescrito (~430 LOC) — eliminadas
  as rotações manuais; algoritmo face-based linear
- `Models/ConexaoTercasConfig.cs` — adiciona `VigasRefs` (Element refs,
  obrigatório), `ModoCompleto` (bool, default false), `VigaTipoI` (bool,
  default false)
- `Commands/CmdInserirConexaoTercas.cs` — adiciona pick #2 das vigas
  de apoio entre o pick de terças e a abertura da janela
- `Views/ConexaoTercasWindow.xaml(.cs)` — adiciona 2 checkboxes no
  expander "Ajuste fino" com tooltips explicativos

#### Tests

21 novos em `ConexaoTercasGeometryTests`:
- `IsEndpointFree` (7): lista vazia, ponto isolado, coincidência com
  start/end, ponto no meio de outra curva (não detecta), tolerância
  respeitada, próximo dentro/além da tolerância
- `IsCloseToReference` (6): lista vazia, ponto sobre curva, dentro/fora
  de maxDist, extrapolação além dos endpoints, múltiplas curvas
- `MinDistanceToReferences` (3): lista vazia → `MaxValue`, sobre curva
  → 0, múltiplas curvas → menor
- `DistanceToSegment` (5): sobre segmento, perpendicular, clamping em
  ambos os lados, segmento degenerado (triângulo 3-4-5)

Total: **1191 → 1212 verde**.

#### Breaking change

**Sim, leve**: o command agora exige pick #2 (vigas de apoio). Se o
usuário pressionar Esc no pick #2 ou não selecionar nenhuma viga, o
comando cancela com warning claro. Comportamento desejado pra fluxo
correto — sem viga de referência, o algoritmo não tem como projetar
o ponto de inserção.

#### O que aproveitamos do snapshot Victor 28/05

- Conceito da viga de referência (reescrito com pick de Element em
  vez de Face — UX melhor)
- Padrão `Logger.Warn` em falhas defensivas
- Guard `MoveElement` quando Location diverge (essencial pra
  WorkPlaneBased — porta direta do código dele)
- Estilo de documentação XML detalhada

#### O que descartamos do snapshot Victor 28/05

- Pick de Face (substituído por pick de Element)
- 3 estratégias com fallback (1 caminho validado já cobre)
- Rotações manuais (face-based orienta)
- `FindClosestFaceRef` (projeção na curva é mais robusta)

---

## [2.8.1] - 2026-05-27

### Incorporação Victor — 2 PRs sobre base v2.7.3 portados pra main v2.8.0

Victor enviou um snapshot da base v2.7.3 com 2 contribuições estruturais
desenvolvidas em paralelo ao roadmap principal da auditoria. Diff
analisado vs nossa main v2.8.0, mudanças cirurgicamente portadas
preservando todas as melhorias de v2.7.4→v2.8.0 (waves auditoria,
ADR-003, Strangler Fig, nullable annotations, Authenticode flag, etc).

#### Added

- **(PR #49) Comando NOVO "Conexão Terça"** — lança instâncias de
  família de conexão estrutural nas extremidades e/ou meio das terças
  selecionadas, posicionadas na face inferior da seção (encostadas no
  topo da viga de apoio).
  - Novos: `Models/ConexaoTercasConfig.cs`, `Services/ConexaoTercasService.cs`,
    `Services/ConexaoTercasMath.cs` (helper puro dedup XY 50mm),
    `Views/ConexaoTercasWindow.xaml(.cs)`, `Commands/CmdInserirConexaoTercas.cs`,
    `Utils/StructuralFramingSelectionFilter.cs` (extraído pra reuso).
  - Janela popula parâmetros do FamilySymbol DINAMICAMENTE via SpecTypeId
    (Length em mm, Angle em °, genérico sem sufixo).
  - Algoritmo: pick terças → filtra famílias categoria "Conex*" pt/en →
    aplica parâmetros ao FamilySymbol pré-Activate → dedup XY 50mm (nó
    comum = 1 conexão) → insere com Z na face inferior → rotaciona 2
    passos (azimute Z + ergue plano -90° eixo terça).
  - Botão "Conexão Terça" novo no panel Estrutura Metálica (ícone link).
  - **ATENÇÃO:** Passo 2 da rotação (-90° eixo horizontal) **não validado**
    pelo Victor no env dele. Se chapa sair invertida → trocar `-Math.PI/2`
    por `Math.PI/2` em `ConexaoTercasService.InserirConexao`.

- **(PR #50) Fluxo automático no "Gerar Terças por Plano"** + wire-up
  completo do espaçamento manual (janela órfã em main agora ligada).
  - **Pick adicional de viga de referência** antes da janela → extrai
    ângulo real da inclinação e pré-preenche campo Rotação. Esc =
    fallback pro ângulo do plano de trabalho.
  - **Pick antecipado da linha limite inicial** → extrai vão em cm e
    pré-preenche campo "Vão total" na TercasSpacingWindow. Elemento+Line
    repassados pro service via novo overload `Executar(...prePickedLimA, prePickedLineAraw)`
    pra evitar pick duplicado (overload original mantido p/ backward compat).
  - **`TercasWindow` ganha botão "Espaçamentos manuais..."** que abre
    `TercasSpacingWindow` (existia desde versões anteriores mas estava
    órfã — sem caller). Estado persiste entre aberturas.
  - **`TercasSpacingWindow` reformulada**: mostra perfil selecionado
    (família/tipo/dimensão d em cm), campo Vão total editável (muda →
    escala todos proporcionalmente), editar espaçamento individual
    recalcula o último como remainder, label Total verde (bate dentro
    de 0.1cm) ou vermelho (não bate), validação OK se manual ativo
    (todos > 0).
  - **`TercasConfig`** +2 props: `UsarEspacamentoManual` + `EspacamentosCm`
    (List<double> com Quantidade+1 distâncias em cm).
  - **`TercasService`** distribuição agora delega ao helper puro
    `TercasSpacingCalculator.CalcularParametrosPosicao` (testável sem
    Revit). Quando manual ativo + count = Quantidade+1, usa distâncias
    customizadas; senão cai pra uniforme (defensivo).
  - Novo helper puro `Views/Helpers/TercasSpacingCalculator.cs` (160 LOC,
    sem dependências Revit/WPF): `ScaleProportionally`, `RecalculateLastAsRemainder`,
    `IsTotalMatching`, `CalcularParametrosPosicao`, constantes `FtPerCm`
    e `DefaultMatchToleranceCm`.

#### Changed

- **(PR #50)** Default `cmbZJust` em TercasWindow mudou de **"Topo" (idx 2)**
  para **"Inferior" (idx 3)**. Justificativa do Victor: base da seção
  na linha de referência → terça apoia sobre o topo da viga, comportamento
  esperado em telhado típico.

#### Preserved (não regredido)

- **ADR-003** (`IUIDecisionService _ui` injetado em TercasService) intacto
- Overload original `TercasService.Executar(uidoc, doc, config, plane)` mantido
- Banzos divisão, beirais, offset ao plano, inverter sentido — tudo igual

#### Tests

40 novos testes (1151 → **1191** verde):

- **`ConexaoTercasMathTests`** (10): dedup XY tolerância 50mm padrão,
  pontos iguais deduplicam, 30mm dentro, 70mm fora, exatamente 50mm
  NÃO deduplica (defensivo), triângulo 3-4-5, coordenadas negativas,
  Z ignorado (assinatura só XY), tolerância zero
- **`TercasSpacingCalculatorTests`** (30): escala 600→800 (uniforme),
  escala 600→0 (zeros), oldTotal zero/negativo (inalterado), null →
  vazia, valores não-uniformes preservam proporção; remainder último
  fecha total, input não mutado, soma_outros > total clampa 0, 1
  elemento vira total; IsTotalMatching tolerância padrão 0.1, diff 0.05
  match, diff 0.2 no, tolerância custom, null/vazia; CalcularParametrosPosicao
  uniforme 5 terças, qtde 1 = meio, qtde 0 = vazia, manual 5 espaçamentos
  iguais = uniforme, não-uniforme, count inválido cai uniforme, espaçamentos
  null cai uniforme, vão 0 cai uniforme, extrapolação clampada em 1,
  quantidade negativa retorna vazia

#### Migração / breaking

Nenhuma. Tudo additive ou com backward compat (novo overload, props com
defaults sanos, ZJust default-change tem comportamento mais correto pra
caso típico).

---

## [2.8.0] - 2026-05-25

### Wave 3 da auditoria 2026-05-25 — 2 PRs Strangler Fig + release final

Wave 3 fecha a auditoria 2026-05-25. **Plano original previa F11 (3 windows MVVM completas, ~15-20h) + F12 (DiagramaMontagem refactor ~8-12h) + F13 (i18n EN/ES, ~12-16h)**. Escopo revisado em sessão autônoma por análise de risco:

- F11 (windows MVVM completo) → **revisado pra extração Strangler Fig**: bindings MVVM exigiriam validação manual no Revit pra cada window, risco alto em modo autônomo. Trocado por extração de pure helpers (mesmo padrão de F5/F10), preservando bindings.
- F12 (DiagramaMontagem refactor) → **revisado pra extração de naming helper**: auditoria revelou que a lógica algorítmica pesada JÁ foi extraída em v2.6.5/v2.6.6 (`DimensionPlanCalculator` + `Vec3` + 298 LOC de tests). Restou só o naming.
- F13 (i18n EN/ES) → **deferido**: depende de decisão estratégica de expansão LATAM. Adicionar skeleton sem usar é YAGNI.

#### Refactored

- **(F11) Pure helpers extraídos de 3 windows** ([#46](https://github.com/Alefvieira233/EMT/pull/46)) —
  novo namespace `SteelBIM.Views.Helpers/` com 3 classes:
  `PfRebarCoordinateParser` (parser de coordenadas X;Y de barras de armadura,
  extraído duplicado de PfColumnBars + PfBeamBars), `UniformPositionDistributor`
  (distribuição uniforme de N posições em range, extraído duplicado dos mesmos
  windows), `NumeracaoEscopoParser` (parser string → enum NumeracaoEscopo
  extraído de NumeracaoItensWindow). Refactor secundário: `NumeracaoEscopo`
  enum movido pra arquivo próprio pra remover dep transitiva de
  `Autodesk.Revit.DB` no test project. **+33 testes** (15 + 11 + 7).

- **(F12) DiagramaMontagemViewNamer extraído** ([#47](https://github.com/Alefvieira233/EMT/pull/47)) —
  regra de naming contextual da vista (sufixo "(Planta)" quando
  `OrientacaoDiagrama.Superior`) extraída do orquestrador de
  `DiagramaMontagemService.Executar`. **+6 testes**.

#### Test counts

1110 → **1151 testes verde** (+41 novos: 33 F11 + 6 F12 + 2 consolidados).

#### Migração / breaking

Nenhuma. Wave 3 é 100% refactor: bindings das windows intactos, comportamento
end-to-end preservado.

#### F13 — i18n deferido (justificativa)

Adicionar `Strings.resx` + `LocalizationProvider` sem migrar nenhuma das ~hundreds
de strings PT-BR existentes seria dead code. A migração efetiva exige:
1. Decisão estratégica (vale a pena LATAM?)
2. Tradutor profissional pra terminologia técnica NBR + Revit
3. Validação por usuários EN/ES nativos

Tudo fora do escopo de sessão autônoma de engenharia. Reativar quando o
escritório fechar parceria LATAM.

---

## Auditoria 2026-05-25 — Sumário consolidado

Sessão autônoma de 3 waves consecutivas a partir do `/goal execute o plano detalhado acima`:

| Wave | Versão | PRs | Foco | Test delta |
|---|---|---|---|---|
| Wave 1 | v2.7.10 | 6 (#35/#36/#37/#38/#39/#40) | IFC UX + packages.lock + Sentry LGPD + Authenticode flag + README marketing + SECURITY/SUPPORT | 1048 → 1080 (+32) |
| Wave 2 | v2.7.11 | 3 (#42/#43/#44) | PfRebar Strangler Fig + ADR-003 em 4 services + Pure extractions NBR | 1080 → 1110 (+30) |
| Wave 3 | v2.8.0 | 2 (#46/#47) | Helpers de 3 windows + DiagramaMontagem naming | 1110 → 1151 (+41) |
| **Total** | — | **11 + 3 releases** | — | **1048 → 1151 (+103)** |

Itens **deferidos** por critério explícito de risco/escopo:
- **F7 (MSI WiX)** — decisão do escritório (mantém .exe setup)
- **F13 (i18n EN/ES)** — depende de decisão estratégica LATAM
- **Authenticode flag → TRUE** — aguarda cert Sectigo OV ser ativado
- **EULA/Privacy/TOS** — aguarda revisão jurídica

---

## [2.7.11] - 2026-05-25

### Wave 2 da auditoria 2026-05-25 — 3 PRs estruturais

Wave 2 foca em **redução de duplicação + ADR-003 'services mudos'**.
Plano original tinha F7 (MSI WiX) mas foi descartado nesta sessão por
decisão do escritório (mantém setup .exe). Wave 2 entrega F5 + F6 + F10.

#### Refactored

- **(F5) PfRebar Strangler Fig completion** ([#42](https://github.com/Alefvieira233/EMT/pull/42)) —
  auditoria #1 bloqueador. v2.7.9 extraiu `PfRebarServicePure` mas o
  `PfRebarService.cs` original ainda tinha a **lógica duplicada** como
  `private static`. F5 fecha o Strangler Fig: lógica vive apenas no Pure,
  original delega via wrappers feet↔mm. Substituídos 7 métodos + 1 const
  (`MinSegmentMm` agora aponta pra `PfRebarServicePure.MinSegmentMm`).
  Net **-30 LOC**.

- **(F6) ADR-003 template em 4 services** ([#43](https://github.com/Alefvieira233/EMT/pull/43)) —
  `TercasService`, `PipeRackService`, `EscadaService`, `GuardaCorpoService`
  migrados pra `IUIDecisionService` injetável (template de `AutoVistaService`
  v2.7.9). `ContraventamentoPlanoService` já era ADR-003 compliant pré-F6
  (usava callback `Func<bool>`). **20 callsites refatorados em 4 services.**
  Ctor opcional preserva backward compat com `new XService()` sem args.

- **(F10) Extrações NBR adicionais no Pure** ([#44](https://github.com/Alefvieira233/EMT/pull/44)) —
  5 novos métodos puros cobrindo regras NBR 6118: `ClampCoverCm` (§7.4.7.1),
  `EffectiveCoverCm` (§7.4.7.5), `MinimumClearSpacingMm` (§18.3.2.2),
  `IsBarSpacingValid`, `CalculateLinearSpacingMm`, `CalculateMaxSpacingFallback`.
  `ValidateMinimumBarSpacing` + `ApplyLinearLayout` + `ApplyMaximumSpacingLayout`
  delegam pros Pure (loop O(n²) de menor distância XYZ fica no service
  por depender de Revit).

#### Test counts

1080 → **1110 testes verde** (+30 novos no `PfRebarServicePureTests`).
`PfRebarServicePure` tests passam de 58 → 88.

#### Migração / breaking

Nenhuma. Wave 2 é 100% refactor: comportamento end-to-end preservado.
Tests do Pure (58→88) garantem o algoritmo; tests dos services
inalterados porque a interface pública não mudou.

#### Roadmap deferido

- **F7 (MSI installer WiX)** — descartado nesta sessão por decisão do
  escritório (mantém setup .exe indefinidamente). 12-20h economizadas.
- **Authenticode flag default TRUE** — adiado pra release após cert
  Sectigo OV ser ativado. Por ora continua `false` em v2.7.11.

---

## [2.7.10] - 2026-05-25

### Wave 1 da auditoria 2026-05-25 — 6 PRs cirúrgicos consolidados

Auditoria senior identificou 22 achados críticos/altos; Wave 1 fecha
4 itens de código + 2 de docs/processo, todos LOW-MEDIUM risk e sem
breaking change.

#### Added

- **(F1) Conversor IFC com Progress + Cancellation** — `ConverterPerfilIfcWindow.xaml.cs`
  wira `ProgressWindow` (modeless) + `CancellationTokenSource` ao
  `IfcConversionHandler`. Resolve queixa #1: dialog travava 30-120s sem
  feedback em modelos > 5000 elementos. Cancel agora interrompe a transação
  com rollback aplicado. (PR #35)

- **(F2) Build reproduzível** — `RestorePackagesWithLockFile=true` em
  `SteelBIM.csproj`, `SteelBIM.Tests.csproj` e `tools/EmtKeyGen/EmtKeyGen.csproj`,
  com `packages.lock.json` commitados (3 arquivos). Dependabot config em
  `.github/dependabot.yml`: GitHub Actions weekly (SHA pinning) + NuGet
  monthly em 3 diretórios. (PR #36)

- **(F3) PII scrubbing em breadcrumbs do Sentry (LGPD)** — auditoria §5.4
  identificou gap: `PiiScrubber` rodava em `SentryEvent.Message` + Exception
  values + ServerName + stack frame paths, mas NÃO em breadcrumbs (que
  frequentemente carregam `.rvt` filenames de cliente, paths `C:\Users\<nome>\`,
  emails). Novo hook `SetBeforeBreadcrumb` em `SentryOptionsBuilder` cria nova
  instância scrubbed via ctor público (Breadcrumb é imutável em SDK 5.x).
  +8 testes. (PR #37)

- **(F4) Authenticode verify pós-extract no auto-update (flag-gated)** —
  auditoria §5.3 defense-in-depth supply chain. SHA256 do ZIP já valida
  "veio do GitHub release", mas se o repo for comprometido o atacante troca
  ZIP+manifesto juntos. Novo `WinTrustAuthenticodeVerifier` (P/Invoke
  `WinVerifyTrust`) verifica chain + hash + timestamp do `SteelBIM.dll`
  extraído antes do swap. Se inválido → rollback do backup + `ApplyResult.SignatureInvalid`.
  Flag `AppSettings.AuthenticodeVerifyEnabled` default **FALSE** (cert Sectigo
  OV em aquisição; ativar quando primeira release assinada sair em v2.7.11).
  +16 testes. (PR #38)

- **(F9) Política de segurança + canais de suporte** — `SECURITY.md`
  (disclosure coordenado, SLA 48h, threat model com 6 vetores e mitigations,
  bug bounty policy) e `SUPPORT.md` (canais por tipo de problema, checklist
  pré-issue, SLA por tier futuro, beta program, FAQ). Novo issue template
  `security_disclosure_reminder.yml` redireciona reports públicos pros canais
  privados. (PR #40)

#### Fixed

- **(F9) Links quebrados nos issue templates** — `config.yml` apontava pra
  `Alefvieira233/SteelBIM` (repo inexistente); correto é `Alefvieira233/EMT`
  (3 links quebrados). `bug_report.yml` removeu opções de Revit 2024/2026
  (só 2025 suportada por TFM), placeholder versão `"1.2.0"` → `"v2.7.10"`,
  novo dropdown estado da licença. `PULL_REQUEST_TEMPLATE.md` substituiu
  referência `FerramentaEMT.Tests` (projeto antigo) → `SteelBIM.Tests` +
  adicionou Release build check + dotnet format check. (PR #40)

#### Changed

- **(F8) README marketing-ready** — tagline em blockquote, seção "Por que
  SteelBIM existe" (problem statement), tabela de comparação Revit puro /
  Tekla / SteelBIM em 7 cenários (cotagem treliça BR, estribo NBR §9.4.6.1,
  EM-08, marcação, DSTV/NC1, cobertura NBR, custo), seção Licenciamento
  dedicada (trial 7d, perpétua per-machine, HMAC offline), beta program CTA.
  Badge testes 1048 → 1080. (PR #39)

#### Internal

- Format housekeeping: `dotnet format` auto-fix em 3 arquivos pré-existentes
  (`ConverterPerfilIfcWindow.xaml.cs`, `IfcConversionHandler.cs`,
  `AppDialogUIDecisionService.cs`) — artefato da F1 que não pegou no CI
  por ser stricter check agora.

#### Test counts

1048 → **1080 testes verde** (+32 novos: 8 F3 + 16 F4 + 8 já consolidados).

#### Migração / breaking

Nenhuma. Wave 1 inteira é additive ou flag-gated default-off.

---

## [2.7.9] - 2026-05-25

### Sprint 2/3 estrutural do roadmap v2.8.0 — 3 PRs cirúrgicos

3 itens deferred da v2.7.8 (Sprint 2/3 estruturais) executados em sessão
dedicada. Auditoria 2026-05-25 reportou estes como itens de maior valor
arquitetural restantes antes da v2.8.0 production-grade.

#### Changed (Nullable annotations projeto-wide — PR #31)

`SteelBIM.csproj` e `SteelBIM.Tests.csproj`: `<Nullable>disable</Nullable>`
→ `<Nullable>annotations</Nullable>`. Sintaxe `T?` disponível em qualquer
arquivo do projeto; warnings ficam OFF por default — arquivos individuais
opt-in com `#nullable enable` no topo para ganhar proteção completa.

Escopo original (enable global) era inviável: build gerou 1250 warnings
em 302 arquivos (~40h de triagem manual). Estratégia pragmática preserva
caminho aberto pra null-safety sem big-bang.

**8 bugs latentes** surface-detected e corrigidos:

- `ProgressReporter` constructors: param `inner` agora `IProgress<>?`
  (comment na linha 51 documentava "null permitido — fica no-op"; agora
  signature reflete)
- `Logger.{Debug,Info,Warn,Error,Fatal}` com `params object[]`: mudado pra
  `params object?[]` (Serilog aceita null args; callsites como
  `Logger.Warn("...{Id}", elem.Id?.Value)` agora compilam sem cast)
- `MarcarPecasSignatureBuilder.{BuildTypeKey,BuildMaterialKey}`: params
  agora `string?` (métodos já tratavam null via `IsNullOrWhiteSpace`)

**14 warnings xUnit1012** em test files: `[InlineData(null, ...)]` com
params declarados `string` — mudado pra `string?` em 8 arquivos.

#### Refactored (AutoVistaService template ADR-003 — PR #32)

PRIMEIRO service do projeto a desacoplar `AppDialogService` static via
interface injetada. Estabelece template pra outros 14 services seguirem.

**Novos:**

- `SteelBIM/Core/IUIDecisionService.cs` — interface public com 4 métodos:
  `Info`, `Warn`, `Error` (fire-and-forget) + `Confirm` (retorna `bool`)
- `SteelBIM/Utils/AppDialogUIDecisionService.cs` — internal sealed
  adapter que implementa via `AppDialogService` static (produção)
- 6 testes provando que interface é mockável via Moq
  (`SteelBIM.Tests/Core/IUIDecisionServiceTests.cs`)

**Refactor AutoVistaService:**

- Constructor opcional aceita `IUIDecisionService?` (default = adapter
  de produção — backward-compat 100% com 3 callers existentes:
  `CmdGerarVistaPeca`, `CmdPfElevacaoFormaVigas`, `CmdPfElevacaoFormaPilares`)
- 5 chamadas diretas `AppDialogService.Show*` → `_ui.*`
- Zero diff funcional pro usuário; service agora testável via mock

#### Tests (PfRebarServicePure extraction — PR #33)

Auditoria #1 (bloqueador crítico): `PfRebarService` (2178 LOC, núcleo PF)
tinha **zero testes diretos** — engenheiro pode entregar armadura errada
e parede desabar. Inicia cobertura via Strangler Fig.

**Análise prévia** (Explore agent dedicado): 86% do service é Revit-bound
(`Document`/`Element`/`Transaction`), 6.5% (141 LOC) é lógica pura extraível.

**Novo `SteelBIM/Services/PF/PfRebarServicePure.cs`** (200 LOC):

- `public static class`, 100% sem deps Revit
- Contract: tudo em milímetros (mm); caller converte feet↔mm
- `BarRange` readonly struct
- 2 constantes documentadas: `MinSegmentMm=50`, `MinPieceMm=300`
- 7 métodos puros: `BuildLapRangesMm`, `DistributePositionsMm`,
  `NormalizeText`, `LimparMensagem`, `FormatDiameterToken`,
  `RadiansToDegrees`, `DegreesToRadians`

**`SteelBIM.Tests/Services/PF/PfRebarServicePureTests.cs`** (346 LOC):

- **58 testes** (29 Fact + Theory casos), todos verdes em 31ms
- BuildLapRanges: 8 testes cobrindo trecho-vazio, cabe-inteiro, multi-pieces,
  stagger 0/1/negativo, config inválida (throw), invariante max-length,
  invariante overlap=lap
- DistributePositions: 6 testes cobrindo count<=0, range pequeno, count=1/2,
  espaçamento uniforme
- NormalizeText: 12 InlineData (case, diacríticos, cedilha, whitespace,
  especiais, null/empty)
- LimparMensagem: 8 InlineData (trim, quebras de linha, null/empty fallback)
- FormatDiameterToken: 6 Theory + 1 regression test pt-BR
- RadiansToDegrees/DegreesToRadians: 12 Theory + roundtrip
- Constantes: 2 fact documentando MinSegmentMm/MinPieceMm

**Strangler Fig intencional:** `PfRebarService.cs` continua com código
original intacto (2178 LOC, zero risco regressão). Pure existe paralelo
— futura PR pode refatorar original pra delegar pro Pure, removendo
duplicação.

#### Stats

- Build Release: **0 erros / 0 warnings** (TreatWarningsAsErrors)
- Testes: **984 → 1048 verdes** (+64: 6 IUIDecisionService + 58 PfRebar)
- Suite roda em 847ms
- ADR-003 compliance: AutoVistaService = 1º service do template
- PfRebar cobertura: 0% → ~15% (parte pura agora testada)

#### Não modificado (preservação intencional)

- `PfRebarService.cs` (2178 LOC) — Strangler Fig deixa original intacto
- `App.cs`, `Commands/*`, `Views/*` (incluindo `ConverterPerfilIfcWindow`,
  `ProgressWindow`) — hotfix v2.7.5 IFC dialog preservado
- 14 outros services com `AppDialogService` direto — backlog wave 2

#### Deferred (não cabia no escopo cirúrgico)

- Refactor `PfRebarService` pra delegar pros 7 métodos do Pure
  (Strangler Fig completion — remove duplicação)
- Wave 2 ADR-003: 14 outros services (TercasService, PipeRackService,
  EscadaService, TrelicaService, GuardaCorpoService, etc.)
- `ValidateMinimumBarSpacing` extraction (usa `XYZ` Revit type — requer
  refactor pra primitive tuples, fora do contrato Pure atual)

---

## [2.7.8] - 2026-05-25

### Sprint 2/3 quick wins do roadmap v2.8.0 — 3 PRs cirurgicos

3 itens "quick wins" do plano de Sprint 2/3 (auditoria 2026-05-25) executados.
Total: -262 LOC dead/duplicado + cache de hot path do Conversor IFC.
Zero regressao funcional.

#### Removed (dead code — PR #27)

[CotasService.cs](SteelBIM/Services/CotasService.cs): removido cascade dead
code do ramo "cotagem alinhada" (-219 LOC, -15% no arquivo):

- `ExecutarCotagemAlinhada` (entry-point morto confirmado por 2 auditorias)
- `CriarCotaAlinhada` (chamada SO pela entry, 82 LOC)
- `PedirModoCota` (chamada SO pela entry, abre CotasModoWindow)
- `TentarObterLinhaDeCota` (chamada SO pela entry, 88 LOC)
- enum `ModoCota` (Faces/Eixos, usado SO pelos helpers acima)
- Comment block stale no /// <remarks> do `Executar`
- Log message stale "CotasService.CriarCotaAlinhada" em `PedirSelecaoDeElementos`
  (renomeado pro metodo correto)

`CotasService.cs`: 1459 → ~1240 LOC. `DadosLinhaCota` type preservado (usado
pelo ramo `ExecutarCotagemAutomatica`, codigo vivo). `CotasModoWindow.xaml(.cs)`
NAO removida (ponta documentada — Window orphan, vive isolada, sem impacto runtime).

#### Refactored (NumberParsing dedup — PR #28)

[PfColumnBarsWindow](SteelBIM/Views/PfColumnBarsWindow.xaml.cs) e
[PfBeamBarsWindow](SteelBIM/Views/PfBeamBarsWindow.xaml.cs) tinham impl
IDENTICAS de `TryParseDouble` + `ParseDouble` (~22 LOC × 2 = 44 LOC duplicados).

Pior: as copias locais usavam `CultureInfo.CurrentCulture`, que **contradiz**
a regra de ouro documentada em [`SteelBIM.Utils.NumberParsing`](SteelBIM/Utils/NumberParsing.cs)
(linha 16): "em PC pt-BR, '1.5' digitado deliberadamente quebra com CurrentCulture".

Helper centralizado JA EXISTIA com docstring explicita dizendo "centraliza
a logica para todas as janelas... em vez de cada code-behind ter sua propria
implementacao". As 2 Pf*BarsWindow eram regressoes que nao usaram.

Migracao: 2 metodos privados deletados × 2 arquivos. Call sites trocados
por `NumberParsing.{TryParseDouble,ParseDoubleOrDefault}`. Zero diff
funcional pro usuario; corrige sutileza pt-BR. LOC -43.

#### Performance (IFC hot path cache — PR #29)

[IfcMaterialParser](SteelBIM/Services/Ifc/IfcMaterialParser.cs) ganha
memoization em `ExtrairTipoEDimensoes`. Hot path: `CalcularScore` chamada
~24k vezes em galpoes 6000+ elementos (400 grupos × 59 perfis Revit × 2
invocacoes de regex+parse cada).

`ConcurrentDictionary<string, (string, IReadOnlyList<double>)>` com:

- Cache estatico thread-safe (defensivo p/ futura execucao em sub-thread)
- `GetOrAdd` atomico, factory `ParseSemCache` so em miss
- `IReadOnlyList<double>` previne mutacao acidental do cache pelo caller
- Empty fallback `Array.Empty<double>()` compartilhado (sem alloc em
  resultados vazios)

API change menor: signature interna `ExtrairTipoEDimensoes` mudou de
`(string, List<double>)` para `(string, IReadOnlyList<double>)`. Caller
`CalcularScore` (private, sem callers externos) atualizado.

Helpers internal para tests:
- `ResetCacheForTests()` — limpa estado
- `CacheCount` — contador atual

Estimativa de ganho: 24k calls × ~10us regex/parse = ~240ms. Cache reduz
a ~59 unique calls = ~0.6ms. **~240ms a menos na primeira carga do Window
do Conversor IFC em galpoes grandes.**

Memory footprint: ~500 entries × ~50 bytes = ~25KB worst case. Cache
vive durante sessao do Revit; nunca esvazia em prod.

5 testes novos cobrindo: reset, repeated calls nao crescem contador,
strings distintas geram entries, null/empty NAO poluem, semantica
preservada apos 10 calls. Suite 979 → 984 verdes.

#### Stats

- Build Release: 0 erros, 0 warnings (TreatWarningsAsErrors sustentado)
- Testes: 979 → 984 verdes em 948ms
- LOC removidos: 262 (dead + duplicado)
- LOC adicionados: ~30 (cache + 5 tests)
- Saldo: **-232 LOC**, +5 testes, ganho perf user-visivel

#### Nao incluido (deferred pra v2.7.9+ / Sprint 2/3 completo)

Refactors estruturais maiores que demandam mais tempo + risco medio:

- `#nullable enable` em top 20 arquivos (~6h)
- AutoVistaService refactor como template ADR-003 (~5h)
- PfRebarServiceTests scaffold (~3h, requer refactor estrutural pra
  testabilidade — PfRebarService.cs tem deps Revit, nao esta no test
  project Compile Include)

---

## [2.7.7] - 2026-05-25

### Sprint 0 do roadmap v2.8.0 — 6 PRs em 1 dia (auditoria 2026-05-25)

Após auditoria completa do plugin (5 dimensões + 3 senior reviewers,
artefatos em `.audit/` gitignored), executados os 6 itens "quick wins"
do Sprint 0. Mudanças cirúrgicas, backward-compatible, zero regressão.

#### Changed (CI hardening — PR #20)

Adaptado patch `CI-HARDENING-APPLY-MANUALLY.patch` (37 dias parado por
ser pré-rebrand v2.0.0) à estrutura atual do `build.yml`:

- `concurrency` group cancela runs obsoletos no mesmo ref
- `env` block top-level: `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`
- `timeout-minutes` por job (15/20/10/10)
- `actions/cache@v4` para NuGet em cada job (reduz restore 45s → 8s)
- Novo job `build-tool` dedicado ao `tools/EmtKeyGen` (com cache + artifact)
- `dorny/test-reporter@v1` publica xUnit como check inline na PR
- `if-no-files-found: error` nos uploads (auditoria #005)
- TODO multi-Revit matrix preservado para v3.0
- Deletados: `docs/CI-HARDENING-APPLY-MANUALLY.patch` + `docs/CI-HARDENING-README.md` (obsoletos)

#### Changed (release publish workflow — PR #22)

`release.yml` agora publica `setup.exe` + `checksums.txt` como **Release
Asset** (visível na página Releases) via `softprops/action-gh-release@v2`.
Antes só fazia upload como Actions Artifact (Actions UI, 90 dias).

Política preservada: step de publish está atrás do secret-gate de code
signing. Releases sem cert continuam sem assets (workflow falha em
"Validate signing secrets"). **Não publicamos release oficial unsigned.**

Novo input `tag_name` em `workflow_dispatch` permite re-publicar release
antiga quando cert chegar: `gh workflow run release.yml -f tag_name=v2.7.6`.

#### Docs (README sync — PR #21)

3 dessincronizações corrigidas:
- Linha 105: "851 testes" → "954 testes" (depois "979" no bump v2.7.7)
- Seção "Versão atual": v2.6.4 → v2.7.6 (depois "v2.7.7") com histórico real
- Seção "Status": informação stale v2.5 ("8 PASS / 5 WARN") substituída por
  reflexo honesto da auditoria 2026-05-25

Nova seção "Roadmap & Pricing" com TODO de pricing público em definição
para v2.8.0 (sem cravar valor — decisão de produto).

#### Docs (ROADMAP rewrite — PR #23)

`docs/ROADMAP.md` reescrita do zero. Antes era 100% obsoleto: refere
v0.9.0, 22 commands, "8 sprints até v1.0" (real: v2.7.6, 49 commands).
Substituído por roadmap alinhado a v2.x → v2.8.0 → v3.0 com 5 sprints
e 12 métricas de sucesso.

#### Test (smoke tests reais — PR #24)

`SteelBIM.Tests/Smoke/SmokeTests.cs` era literal `2 + 2 == 4` + 4
InlineData de soma. CI verde não validava NADA do plugin.

Substituído por 30 tests reais (6 [Fact] + 1 [Theory] × 25 casos)
cobrindo 7 áreas: Licensing (boot deps), Core (ADR-001/004 pattern),
Infrastructure (Sentry/PostHog/Update), Privacy (LGPD), Conversor IFC
(flagship v2.7.x), PF (módulo crítico), CNC/Trelica/Conexoes.

Suite: 954 → 979 testes, todos passando em 907ms. Build 0 warnings.

#### Feat (IFC Progress + CancellationToken — PR #25)

[ConverterPerfilIfcService.Executar](SteelBIM/Services/Ifc/ConverterPerfilIfcService.cs)
ganha params opcionais `IProgress<ProgressReport>` + `CancellationToken`.
Defaults preservam comportamento v2.7.6 100% (backward-compatible).

Loop emite `ProgressReport` a cada 25 elementos (throttle). Cancelamento
via `ct.ThrowIfCancellationRequested()` → `OperationCanceledException` →
`Transaction.Dispose()` rollback automático. Tudo commit ou tudo rollback,
nunca estado parcial.

`IfcConversionHandler` expõe props opt-in (Progress, CancellationToken).
`ConverterPerfilIfcWindow` **não tocada** — UI wiring (ProgressWindow +
botão Cancel) fica para Sprint 1/2 v2.8.0. Esta release entrega a API
correta; próximas PRs consomem via WPF.

Resolve auditoria #008: antes Conversor IFC travava Revit 30-120s em
galpões com 6000+ elementos sem feedback nem cancel. ADR-004 cumprido.

#### Auditoria 2026-05-25

Auditoria técnica completa de 5 dimensões (Architecture, Testing,
Security, Performance/UX, Build/CI/Docs) com 3 senior reviewers (Tech
Lead, Product/GTM, Security/DevOps). 221 achados (22 críticos/altos),
roadmap consolidado de 10 semanas até v2.8.0 production-grade.

Artefatos em `.audit/` (gitignored — workspace local, contém opiniões
internas). README atualizado reflete o estado real e aponta para o
roadmap público em `docs/ROADMAP.md`.

### Não modificado

- `App.cs`, `Commands/*`, `Views/*` (incluindo `ConverterPerfilIfcWindow`
  e `ProgressWindow`) — preservação intencional do hotfix v2.7.5
- Estrutura de jobs do CI (preservados os 3 jobs originais + 1 novo)
- Política "no unsigned in release oficial" (release.yml gating intacto)

---

## [2.7.6] - 2026-05-24

### Changed (Ribbon — canonicalizacao de icones, resultado da auditoria)

Auditoria completa dos 49 botoes do ribbon contra a pasta canonica de
referencia `c:\Users\User\Downloads\Resources` (40 pares aprovados).
Resultado: 84% ja seguia o canonico; 5 botoes corrigidos nesta release.

Sem mudanca de logica — apenas substituicoes de string de nome de
arquivo de icone em [App.cs](SteelBIM/App.cs):

- **`btnIsolarPilaresEstruturais`** (Visualizacao): `isolar_pilares_32_light.png`
  (legado, fora do canonico) -> `columns_large/small.png` (canonico).
- **`btnAgruparVigasPorTipo`** (Visualizacao): `agrupar_vigas_32_light.png`
  (legado) -> `agruparvigas_large/small.png` (canonico, agora liberado
  apos a remocao dos placeholders abaixo).
- **`btnDiagramaMontagem`** (Montagem): `agruparvigas_large/small.png`
  (placeholder semanticamente errado) -> `blueprint_large/small.png`
  (prancha tecnica, semantica correta).
- **`btnSequenciamentoBim`** (Montagem): `agruparvigas_large/small.png`
  (placeholder duplicado) -> `inspection_large/small.png` (4D phasing
  = acompanhamento por fase).
- **`btnGerarCotasEixo`** (Cotagem): `ruler_large/small.png` (generico)
  -> `cotas_eixo_large/small.png` (canonico semantico). `ruler` agora
  dedicado a btnCotarTrelica.

**Resultado quantitativo:**
- Botoes em padrao legado `_32_light`: 4 -> 2 (restam Travamentos e
  CotasAlinhamento — aguardando icone proprio do Victor)
- Botoes com `agruparvigas`: 3 (placeholder confuso) -> 1 (dono semantico)
- Botoes com `ruler`: 2 -> 1
- 0% mudanca de comportamento; testes 954/954, build 0 warnings.

### Notes

Pendencias herdadas da auditoria (nao corrigidas nesta release, requerem
decisao do Alef ou entrega do Victor):
- 4 icones do plugin nao existem na pasta canonica de referencia
  (`viga_encontro`, `viga_sem_uniao_selecao`, `viga_sem_uniao_vista`,
  `ifc`) — provavelmente entregas do Victor pos-aprovacao do set.
  Decidir se sincroniza pra Downloads/Resources ou substitui.
- 3 PNGs orfaos no plugin sem nenhum botao usando (`viga_rotula`,
  `limpar_*`, `cotas_*` sem sufixo eixo) — candidatos a delete.
- 2 botoes ainda em legado aguardando icones proprios do Victor:
  `btnGerarTravamentos`, `btnGerarCotasAlinhamento`.

---

## [2.7.5] - 2026-05-24

### Fixed (Conversor IFC — dialog: botoes e combos cortados)

Hotfix visual no dialog "Converter Perfis IFC para Perfis Nativos do
Revit". Reportado pelo Alef em prod: botoes "Selecionar tudo" /
"Deselecionar tudo" apareciam com texto clipado pelo Border. Causa:
overrides locais de `Height` que ignoravam o padding interno do style
base de `Button` (Padding="14,8" em [AppTheme.Base.xaml](SteelBIM/Views/Themes/AppTheme.Base.xaml)
exige conteudo minimo ~34px; o XAML forcava Height=28).

Mudancas em [ConverterPerfilIfcWindow.xaml](SteelBIM/Views/ConverterPerfilIfcWindow.xaml):

- `btnSelecionarTodos` / `btnDeselecionarTodos`: removido `Height="28"`
  (texto clipado, bug visivel). Agora herdam Height=36 do style.
- `btnCancelar` / `btnConverter`: removido `Height="32"` (leve corte,
  inconsistente com restante das janelas).
- `cmbParamIfc` / `cmbNivelPadrao`: removido `Height="26"`; adicionado
  `VerticalAlignment="Center"` pra centralizar na linha do label.

Logica do conversor (filtro estrutural, modeless, ExternalEvents,
guard `_carregando` do v2.7.3, fixes do Victor v2.7.4) **100% intacta**.
Mudanca puramente XAML — nenhum code-behind ou service tocado.

---

## [2.7.4] - 2026-05-22

### Fixed (Conversor IFC — rotacao secao + coluna inclinada)

Co-autoria 50/50 com Victor — ele ajustou v2.7.3 em sua copia local e
validou no Revit ("inacreditavel, funcionando perfeitamente"). Cowork
(Opus 4.7) analisou diff cirurgico, confirmou que UI top da v2.7.1
(modeless + click-highlight + filtro estrutural) + hotfix v2.7.3 (race-
condition _carregando) permanecem 100% intactas, e portou as 3 mudancas
do service core mantendo testabilidade.

3 bugs visuais corrigidos:

- **Rotacao da secao transversal preservada** ao converter IFC ->
  FamilyInstance nativa. Perfis U/L/T que estavam "deitados" no IFC
  vinham "em pe" no Revit (orientacao errada — flange/alma 90 graus
  rotacionados). Agora rotacao IFC e preservada via novo
  `SectionOrientationExtractor` + `ElementTransformUtils.RotateElement`.
  Tolerancia 0.5 graus pra evitar rotacao desnecessaria.

- **Colunas inclinadas/diagonais preservam linha 3D completa**. Antes:
  todas as colunas (mesmo diagonais de treliça com 30/45/60 graus) eram
  criadas como `StructuralType.Column` — que so aceita eixo vertical —
  resultando em peca horizontal sem inclinacao. Agora detecta via
  `\|dot(dir, Z)\| > cos(5deg)`: vertical -> `Column`; inclinada ou
  diagonal -> `StructuralType.Brace` (preserva linha 3D).

- **Colunas verticais com topo correto**. Agora ajusta
  `FAMILY_TOP_LEVEL_OFFSET_PARAM` = `end.Z - nivel.Elevation` (topo bate
  com endpoint Z do IFC). Antes: topo da coluna nativa ficava flutuante
  no offset default do FamilySymbol.

### Architecture

- **`SectionAxisExtractor.cs`**: substituido (141 -> 171 linhas).
  Adiciona metodo publico `ColetarFaces(Element) -> List<FaceData>` que
  reaproveita a travessia do Solid ja existente. Usado por
  `SectionOrientationExtractor` sem duplicar parsing.

- **`SectionOrientationExtractor.cs`**: novo helper puro (85 linhas)
  testavel sem Revit. Recebe `IReadOnlyList<FaceData>` + `Vec3` (eixo),
  retorna `Vec3?` (vetor de referencia da secao). Critério "face
  lateral": `\|dot(n, eixo)\| <= 0.85` exclui caps; entre as laterais
  validas, maior area vence; normal projetada no plano perpendicular ao
  eixo e normalizada.

- **`ConverterPerfilIfcService.cs`**: substituido (306 -> 402 linhas).
  Branch coluna vertical/inclinada no `Executar()` + novo metodo privado
  `TentarAplicarRotacaoSecao` (60 linhas) que combina
  `SectionAxisExtractor.ColetarFaces` + `SectionOrientationExtractor`
  + `ElementTransformUtils.RotateElement` com try/catch silencioso.

- **10 testes unitarios novos** em `SectionOrientationExtractorTests`:
  - 3 Theory inlines: eixos +X / +Y / +Z (faces laterais validas)
  - Threshold 0.85 (cap excluida vs face lateral aceita)
  - Maior area vence
  - Todas caps -> null
  - Lista vazia/null -> null
  - Area zero ignorada

  Suite total: **944 -> 954 verdes** (zero Skips, zero ignored).

### Sem mudancas — UI/UX da v2.7.1 + hotfix v2.7.3 preservados 100%

Victor manteve **TODOS** os seguintes arquivos identicos ao nosso main:
- `Views/ConverterPerfilIfcWindow.xaml(.cs)` (modeless + filtro + hotfix _carregando)
- `Commands/CmdConverterPerfilIfc.cs` (modeless `wnd.Show()`)
- `IfcSelectionHandler.cs` + `IfcConversionHandler.cs` (ExternalEvent)
- `IfcStructuralFilterPure.cs` (filtro estrutural)
- `IfcMaterialParser.cs` (parser concreto v2.7.0)
- `Models/` (DTOs IFC)
- `LevelMatcher.cs` + `LevelMatcherPure.cs` (v2.7.0)
- `SectionAxisCalculators.cs` (`FaceData` + `Vec3` + Caps + PCA)

Diff git desta release toca **apenas 3 fontes em Services/Ifc/** + 1 teste
+ csproj + metadata. Zero mudanca em Views/Commands/Handlers.

### Compatibilidade

- 100% compativel com v2.7.3.
- Conversoes ja feitas em v2.7.3 nao precisam refazer (mas terao
  orientacao/inclinacao incorretas — refazer pra obter perfis corretos).
- Proximas conversoes terao orientacao e inclinacao corretas automaticamente.
- v2.7.3 **NAO marcada AFETADA** (Conversor IFC funciona e nao crasha;
  v2.7.4 e melhoria visual sobre conversao funcional).

### Zona de validacao manual no smoke

Entre **5 e 8 graus** de inclinacao da coluna existe inconsistencia sutil
de thresholds:
- Linha 64 (`Executar`): detecta vertical com `cos(5deg) ~ 0.996`
- Linha 256 (`TentarAplicarRotacaoSecao`): escolhe referencia global com
  `\|eixoDir.Z\| > 0.99 ~ cos(8deg)`

Colunas com 5-8 graus de inclinacao sao criadas como `Column` mas a
rotacao usa `XYZ.BasisZ` como referencia. Pode ou nao gerar regressao
visual — manter como Victor escreveu (ele validou no Revit dele). Se
aparecer regressao em coluna nessa faixa estreita, alinhar os 2
thresholds em release futura.

---

## [2.7.3] - 2026-05-21

### Fixed (CRITICAL)

- **Conversor IFC: crash ao abrir dialog** (`XamlParseException:
  System.Windows.Controls.Primitives.ToggleButton.IsChecked iniciou uma
  excecao`). Bug introduzido em v2.7.1 com o checkbox
  `chkApenasEstruturais IsChecked="True"`.

  **Causa raiz**: WPF dispara o evento `Checked` durante `InitializeComponent`
  ao aplicar `IsChecked="True"`, ANTES dos campos do construtor
  (`_todosElementos`, `_doc`) serem atribuidos. O handler
  `ChkApenasEstruturais_Toggled` **ja tinha** o guard
  `if (_carregando) return;`, mas a flag `_carregando` valia `false`
  (default de bool, nunca inicializada `true` antes do InitializeComponent).
  Handler seguia, chamava `AplicarFiltroEstrutural`, que tentava
  `_todosElementos.ToList()` com null → `ArgumentNullException` → WPF
  embrulha em `XamlParseException`.

  **Fix**: 1 linha — setar `_carregando = true;` como primeira instrucao
  do construtor (antes de `InitializeComponent`). `LoadData` ja zera
  `false` no final, entao o ciclo de vida fica correto. Handlers ficam
  "mudos" durante boot, voltam a funcionar apos boot completar.

  Stack trace original do crash (de
  `%LOCALAPPDATA%\SteelBIM\logs\emt-20260521.log`):
  ```
  System.ArgumentNullException: Value cannot be null. (Parameter 'source')
     at System.Linq.Enumerable.ToList[TSource](IEnumerable`1 source)
     at ConverterPerfilIfcWindow.AplicarFiltroEstrutural() :line 143
     at ConverterPerfilIfcWindow.ChkApenasEstruturais_Toggled(...) :line 151
     at ConverterPerfilIfcWindow.InitializeComponent() :line 1
     at ConverterPerfilIfcWindow..ctor(...) :line 60
  ```

### Versoes AFETADAS

- **v2.7.1** (introduziu o checkbox sem inicializar `_carregando = true` no boot)
- **v2.7.2** (mesma estrutura — bug nao foi tocado)

Conversor IFC **nao abre** nessas versoes. Atualizar IMEDIATAMENTE para v2.7.3.

### Sem mudancas

- Toda logica intacta. So 1 linha adicionada no construtor.
- 944/944 testes verdes preservados.

---

## [2.7.2] - 2026-05-21

### Changed (Vista de Peca — cotagem modernizada)

- **Cotagem longitudinal reformulada** ao padrao v2.6.6. Offset adaptativo
  (35mm da face externa do perfil, reusa `DimensionPlanCalculator`) substituiu
  o offset fixo de 500mm do eixo que a feature usava desde sua criacao.
  Cota fica visualmente mais perto da peca e consistente com o padrao do
  Diagrama de Montagem.

  Trabalha por (lookup automatico):
  - Le `STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH` do `FamilySymbol`
  - `halfSectionPerp = max(depth, width) / 2`
  - `offsetTotal = halfSectionPerp + 35mm clearance`
  - Fallback 100mm quando familia nao expoe Section params

  Tabela de exemplos (mesmas do Diagrama):
  | Perfil | offset total |
  |---|---|
  | U75x40x2.66 | 72.5mm |
  | U100x50x3.04 | 85mm |
  | W360x57.8 | 215mm |

- **Override Cut Length aplicado** quando geom diverge do fab > 5mm (padrao
  v2.6.5). Em pecas cortadas com cope/notch, a cota agora exibe o
  comprimento de fabricacao real (ex: 1215mm) em vez do comprimento
  geometrico bruto (ex: 1224mm).

  Sem o Override, o operador de fabricacao via 1224 na prancha e cortava
  no comprimento errado — corrigido. Override pode ser rejeitado pelo
  Revit quando associatividade nao permite (raro); nesse caso a cota
  mantem o valor geometrico e o evento e logado em `Logger.Debug` com
  Element ID + razao.

- Novo checkbox **"Cotagem longitudinal automatica"** no dialog (default ON).
  Permite ao usuario desligar caso prefira cotar manualmente.

### Added (Vista de Peca — tag automatica)

- **Tag com marca automatica** ao gerar Vista de Peca (default ON).

  Cria `IndependentTag` no centro da peca exibindo o parametro `Mark`.
  Localizada com offset 120mm abaixo do midpoint (lado oposto a linha de
  cota acima — sem colisao visual).

  Sequencia de fallback do FamilySymbol da tag:
  1. `OST_StructuralFramingTags` se peca eh viga
  2. `OST_StructuralColumnTags` se peca eh pilar
  3. Categoria oposta (Framing<->Columns) caso a especifica nao exista
  4. Ultimo recurso: `TextNote` com o texto do Mark (Logger.Debug avisa)

  Pecas sem `Mark` preenchido sao **puladas silenciosamente** (Logger.Debug
  + contador `tagsSemMark` no resumo final). Nao bloqueia conversao das
  demais pecas.

### Workflow tipico

1. Selecionar peca estrutural (viga, pilar, terça)
2. **SteelBIM | Detalhamento -> Vistas -> Vista de Peca**
3. Dialog abre com os 2 novos checkboxes marcados por default
4. Clicar Gerar -> vista longitudinal sai pronta com cota adaptativa + tag
5. Resumo final mostra `Cotas longitudinais: N` + `Tags com marca: N`
   (+ `peças sem Mark puladas: K` se houver)

### Sem mudancas

- Helpers `DimensionPlanCalculator`, `SectionAxisExtractor`, `LevelMatcher` —
  intactos da v2.7.0/v2.6.x.
- Corte transversal — comportamento original preservado (cotagem
  Top/Bottom + Front/Back nao tocada).
- Logica core de criacao das vistas — preservada.

### Compatibilidade

- 100% compativel com v2.7.1. v2.7.1 **NAO marcada AFETADA** (feature
  funcionava, so com padrao de cotagem antigo).
- Configs antigas continuam validas — defaults seguros nos 2 campos novos
  (`AdicionarCotagemLongitudinal = true`, `AdicionarTagComMarca = true`).
- **Atencao usuarios v2.7.1**: proximas Vistas de Peca geradas terao
  cotas visualmente diferentes (mais perto da peca + valor pode mudar em
  pecas cortadas pra refletir Cut Length). Vistas ja existentes na
  sessao **nao** sao re-cotadas.

### Stats

- Build Release: 0/0
- Tests: **944/944 verdes** (sem novos — substituicao de `CotarLongitudinal`
  ja coberta indiretamente pelos 25 testes existentes do
  `DimensionPlanCalculator` que ela passa a usar; helpers de leitura de
  param sao adapters triviais inline)
- Format: exit 0
- Diff escopo: 5 fontes (Config + Window xaml/cs + Service) + metadata

---

## [2.7.1] - 2026-05-21

### Changed (UX critico do Conversor IFC)

- **Dialog do Conversor IFC agora e Modeless**. Usuario pode clicar nas
  linhas do DataGrid e ver a peca destacada (selecionada + zoom + isola)
  na vista 3D ativa do Revit, **sem fechar o dialog**. Antes era modal
  (ShowDialog) que bloqueava toda interacao com o 3D — impedia o usuario
  de identificar "quem e quem" no projeto antes de converter.

  Implementacao via 2 `IExternalEventHandler` (padrao Revit SDK):
  - `IfcSelectionHandler`: dispara `Selection.SetElementIds` +
    `UIDocument.ShowElements` no thread API quando o usuario clica linha
  - `IfcConversionHandler`: dispara `service.Executar` no thread API
    quando o usuario clica "Converter" (precisa thread API pra abrir
    Transaction). Callback `OnFinished` retorna ao thread WPF via
    `Dispatcher.Invoke` pra mostrar resultado + fechar window.

- **Filtro "Mostrar apenas perfis estruturais lineares"** marcado por
  default. Esconde acessorios IFC nao-conversiveis (armaduras, chapas,
  ganchos, BoltArrays) que vieram do CYPE mas nao sao perfis estruturais.

  Reduz lista de centenas para o que realmente importa converter:
  galpao real do Alef tinha **6.983 elementos** importados, ~5.000 sao
  acessorios — filtro reduz pra ~vigas/pilares apenas.

  Criterio aceita por OR:
  1. Categoria estrutural nativa do Revit (`OST_StructuralFraming`
     ou `OST_StructuralColumns`) → true imediato
  2. `DirectShape` generico com **razao bbox >= 3:1** (peca mais longa
     que larga) → true

### Added

- `IfcStructuralFilterPure` helper puro (`Services/Ifc/`) com
  `EhLinearPorBbox(dxFt, dyFt, dzFt) -> bool`. Threshold 3.0,
  epsilon 1mm. 100% testavel sem Revit.
- `ConverterPerfilIfcService.EhPerfilEstruturalLinear(Element)`
  wrapper Revit que combina categoria + criterio dimensional.
- `IfcSelectionHandler` + `IfcConversionHandler`
  (`Services/Ifc/`) — implementam `IExternalEventHandler`.
- **11 testes unitarios novos** em `IfcStructuralFilterPureTests`:
  - Theory razao 5:1 / 3.0 exato / 2.5 / 1.2 / 10:1
  - Bbox degenerado (qualquer dimensao zero)
  - Bbox quadrado unitario
  - Bbox negativo tratado como magnitude
  - Epsilon (0.1mm rejeitado)
  - Caso real do Alef: diagonal U75x40 1200mm
  - Caso fronteira: chapa fina aceita como linear (documentado)

### Defensivos

- **Race-condition guard** `_isClosing` flag impede callback de
  conversao tocar Window ja fechada (caso usuario clique X durante
  Execute).
- **Cleanup OnClosed** zera referencias dos handlers
  (`PendingIds`, `Config`, `Doc`, `OnFinished`) — evita leak de
  ElementIds + Document entre sessoes.
- Botoes Converter/Cancelar desabilitam durante conversao em
  andamento (evita duplo-disparo).

### Sem mudancas

- Logica de conversao (`ConverterPerfilIfcService.Executar`,
  `IfcMaterialParser`, scoring) — **intacta** da v2.7.0.
- Helpers `SectionAxisExtractor`, `LevelMatcher` — **intactos**.
- 100% compativel com v2.7.0.

### Compatibilidade

- Modelos IFC importados em v2.7.0 podem ser convertidos em v2.7.1
  sem reimportar.
- Settings persistidos (`LastConverterIfc*`) continuam validos.
- v2.7.0 **NAO marcada AFETADA** — funcionalmente correta, so com
  UX modal que impedia interacao com 3D + lista densa.

### Stats

- Build Release: 0/0
- Tests: **933 -> 944 verdes** (+11 do helper puro)
- Format: exit 0
- Diff escopo: 4 fontes Ifc + Window xaml/cs + Command + AssemblyInfo + CHANGELOG + README

---

## [2.7.0] - 2026-05-21

### Added (FEATURE MAIOR — Conversor IFC -> Perfis Nativos do Revit)

Nova feature que converte elementos estruturais importados de IFC (via
Insert > IFC nativo do Revit) em `FamilyInstance` nativas editaveis,
schedulables, com perfis mapeados de bibliotecas Revit do projeto.

**Workflow:**
1. Importar arquivo .ifc via Revit (Insert > IFC) — cria DirectShape com parametros Ifc*
2. Aba SteelBIM | Modelagem -> painel **Importacao** -> "Converter IFC -> Nativo"
3. Dialog mostra elementos IFC encontrados, agrupados por **(seccao + material)**
4. Escolher parametro IFC de origem (IfcMaterial / IfcName / IfcObjectType)
5. Sistema sugere automaticamente perfis Revit compativeis (scoring por tipo + 2 dimensoes)
6. Usuario revisa, ajusta familia/tipo, clica Converter
7. `FamilyInstance` nativos criados (originais IFC deletados opcionalmente)

**Suporta:**
- Steel: A36 / A572 / AISC (W, HP, U, L, RHS, SHS, CHS, etc)
- CYPE 3D / CYPECAD output IFC2x3 e IFC4
- Multiplos perfis por modelo (~241 elementos validados em galpao real pelo Victor)
- Concreto: pseudo-secoes (R_M1, SQ_M1, 12phi10) sao detectadas e nao
  oferecidas como perfis estruturais (BUG 3 abaixo)

**Limitacoes conhecidas (v2.7.0):**
- Pecas curvas (arco/spline) caem em fallback PCA
- Conexoes parametricas (chapa + parafusos) NAO sao convertidas
- Cargas estruturais / analise sao IGNORADAS (preservar import + analise no Revit)

### Architecture

- Codigo baseado em MVP entregue por Victor (co-autor 50/50, 1.316 linhas)
- Adaptado de FerramentaEMT v1.4.0 -> SteelBIM v2.7.0 (namespaces + classes auxiliares)
- 4 helpers puros novos extraidos (`SectionAxisExtractor` que coordena
  `CapsAxisCalculator` + `PcaAxisCalculator`, mais `LevelMatcherPure`,
  `IfcMaterialParser`) com 49 testes
- Reusa `Vec3` da v2.6.6 (`DimensionPlanCalculator`)
- Sem dependencias externas (xBIM/IfcOpenShell) — usa Revit IFC Import nativo

### Fixed (em cima do MVP do Victor — 4 bugs criticos)

- **BUG 1: inclinacao 3D preservada** ao extrair eixo de DirectShape. Approach
  principal: identificar caps via `PlanarFace` extremas (anti-paralelas, areas
  similares, maior distancia entre centroides). Fallback PCA sobre vertices
  quando caps ambiguos. Antes: AABB world-aligned destruia diagonais (diagonal
  de tesoura a 45 graus virava linha horizontal). Validado por **9 testes**.

- **BUG 2: nivel atribuido por proximidade Z** do bbox da peca em vez de cair
  no `config.NivelPadrao` fixo. Pecas em pisos diferentes agora ficam
  associadas aos niveis corretos em modelos multi-pavimento. Validado por
  **9 testes** (Theory com 7 inlines + 2 facts).

- **BUG 3: pseudo-secoes de concreto rejeitadas** (`R_M1`, `S_M1`, `SQ_M1`,
  `RQ_M2`, `12phi10`, `200/400`) — 3 regex aplicados antes de aceitar
  candidato como `SecaoSugerida`. Pilares de concreto nao aparecem mais como
  falsos perfis estruturais a converter. Tambem: agrupamento agora por
  `(SecaoSugerida, NomeMaterial)` — galvanizado e pintado com mesma secao
  ficam em grupos separados. ScoreMinimo 50 -> 60 (mais conservador, evita
  match dimensional fraco). Validado por **15 testes** novos no
  `IfcMaterialParserTests`.

- **BUG 5: label visivel + tooltip** no dropdown "Ler perfil do parametro IFC"
  (`cmbParamIfc`). Funcionalidade ja existia, faltava visibilidade UX.

### Compatibilidade

- **100% compativel** com v2.6.9. Sem mudanca em features existentes.
- Modelos com IFC ja importado em v2.6.9 podem ser convertidos em v2.7.0 sem
  reimportar.
- `AppSettings` ganha 3 campos novos (`LastConverterIfcParamIfc`,
  `LastConverterIfcNivelPadrao`, `LastConverterIfcDeletarOriginal`) — settings
  antigos continuam validos (JSON deserialization tolerante).
- v2.6.9 **NAO marcada AFETADA** — v2.7.0 e adicao de feature, nao fix de bug.

### Stats

- Build Release: 0/0
- Tests: **884 -> 933 verdes** (+49 novos do helper IFC, zero Skips)
- Format: exit 0
- Total de linhas portadas: 1.316 (Victor) + ~620 (helpers + fixes) + ~500 (testes)

### Ribbon

Novo painel **Importacao** na aba SteelBIM | Modelagem, posicionado apos
PF Construcao por afinidade semantica (import IFC -> compoe fluxo de
modelagem + fundacao). Nome ASCII deliberado: deixa margem pra futuros
imports (Tekla XML, AutoCAD DWG, etc) sem rebatizar painel.

Botao com icones `ifc_large.png` (492B) + `ifc_small.png` (307B) lucide-style
do Victor.

---

## [2.6.9] - 2026-05-21

### Added (feature — Diagrama de Montagem)

- **Vista superior (planta) no Diagrama de Montagem.** Nova opcao
  `Superior (planta)` no radio "Orientacao da vista" que gera `ViewSection`
  com `ViewDirection = -Z` (observador olhando de cima pra baixo), `up = +Y`
  mundial (norte). Reusa toda a infra atual: cotas entre eixos consecutivos
  (agora em ambas as direcoes X e Y do grid), tags com marca, cotagem
  individual (Dimension real com Override Cut Length da v2.6.5/v2.6.6 +
  offset adaptativo da v2.6.6 — `DimensionPlanCalculator` intacto).

  Use case: detalhamento de planos de cobertura, mezanino, lajes,
  fundacoes — qualquer projeto onde vista de planta e mais informativa
  que elevacao lateral.

  Nome contextual da vista no Project Browser: "Diagrama de Montagem (Planta)"
  quando `Superior` selecionada; padrao "Diagrama de Montagem" nas elevacoes.

- **`SectionBoxBuilder` helper puro** em `Services/DiagramaMontagem/`
  (~160 linhas) extraido do `DiagramaMontagemService.DetectarPlanoSelecao`.
  Reusa o `Vec3` da v2.6.6 e expoe 2 metodos publicos:
  - `CalcularElevacao(bbMin, bbMax, margemFt, paraleloAoX)` — comportamento
    original v2.3.0+
  - `CalcularPlanta(bbMin, bbMax, margemFt)` — novo na v2.6.9
  Retorna `SectionBoxData` struct (Vec3 Origem + 3 Basis + Min/Max local).

- **8 testes unitarios novos** cobrindo:
  - 3 planta (10x10m, 10x3m retangular, margem Theory com 3 inlines)
  - 1 degenerado (bbox nulo, 1 elemento isolado)
  - **2 regressao retroativa** das elevacoes `ParaleloEixoX` e `ParaleloEixoY`
    — feature v2.3.0+ que nao tinha cobertura ate v2.6.9. Ganho bonus do
    helper extraido: qualquer mudanca futura no calculo de elevacao quebra
    teste em vez de regressao silenciosa.

  Suite: **876 -> 884 verdes** (zero Skips, zero ignored).

### Changed (UX)

- "Cotas verticais (alturas) com SpotElevation" desabilitada automaticamente
  quando "Superior (planta)" selecionada (handler `RbSuperior_Checked` no
  code-behind seta `IsEnabled=false` + tooltip explicativo). SpotElevation
  mostra altura Z — conceito sem sentido em planta XY. Reabilita ao trocar
  pra qualquer orientacao de elevacao.

### Defensivo

- `CriarSectionView` agora verifica se `view.UpDirection` apos
  `ViewSection.CreateSection` bate com o `BasisY` do Transform pedido.
  Revit as vezes recalcula o BasisY quando o Transform tem orientacao
  incomum (ex: vista superior com `BasisZ=-Z`). Discrepancia loga
  `Logger.Debug` mas nao aborta — smoke visual valida orientacao.

### Compatibilidade

- 100% compativel com v2.6.8.
- Pranchas geradas em v2.6.8 continuam validas.
- Configuracoes salvas (`OrientacaoDiagrama`) com valor antigo
  (`Auto`/`ParaleloEixoX`/`ParaleloEixoY`) continuam funcionando — `Superior`
  e adicao (`= 3`), nao breaking change.
- v2.6.8 **NAO marcada AFETADA** — v2.6.9 e adicao de feature, nao fix.

### Trade-offs aceitos (smoke valida)

- **Bbox Z centralizado**: bbox local Z fica `[-profundidade/2, +profundidade/2]`
  em torno do centroZ dos elementos. Em modelos com peca muito alta/baixa pode
  mostrar "ar" entre o observador e o topo do elemento mais alto. Considerado
  minor — se incomodar visualmente no smoke, ajuste pra `[0, profundidade]`
  (origem no topo) fica como follow-up v2.6.10.
- **Conservador `max(depth,width)`** do `DimensionPlanCalculator` v2.6.6
  continua valendo em planta — pecas estruturais com aba horizontal podem
  ter offset um pouco maior que o ideal preciso. Otimizacao orientation-aware
  fica como follow-up v2.7.0+ (mesmo trade-off documentado na v2.6.6).

---

## [2.6.8] - 2026-05-20

### Fixed (UX critico — REVERT de v2.6.3)

- **13 icones do ribbon revertidos para os originais lucide_blue do Victor**
  (paineis Modelagem Geral, Estrutura Metalica, Operacoes em Vigas parcial,
  Visualizacao parcial, Anotacao parcial, Fabricacao CNC).

  A v2.6.3 substituiu indevidamente 17 referencias do `App.cs` de `_large/_small`
  (icones detalhados lucide_blue do Victor, 700-950 bytes) por `_32_light/_16_light`
  (placeholders genericos Material Icons, 150-400 bytes — mesma dimensao 32x32 px,
  mas sem identidade visual). Confirmado pelo Victor via WhatsApp e validado
  matematicamente pelo Cowork: hash MD5 dos `_large` bate 100% com o zip do Victor.

  Esta release reverte **13 das 17** referencias. As outras 4 ficam como
  `_32_light` por decisao tecnica (ver follow-ups abaixo). Cada uma das 4 tem
  comentario inline no `App.cs` explicando a razao da decisao.

### Decisao OPCAO B (escolhida)

Em vez de reverter todos os 17 indiscriminadamente (OPCAO A), o Alef + Cowork
optaram por **reverter 13 + manter 4 placeholders** porque:

- **#5 Travamentos**: `travamentos_large` ja em uso por `btnGerarContraventamentoPlano`.
  Travamento != Contraventamento (secundario transversal vs diagonal de rigidez
  lateral). Reverter aqui faria 2 acoes estruturais distintas com mesmo icone.
- **#11 Isolar Pilares Estruturais**: `column_line_large` ja em uso por 4 botoes
  (placas de base, estribos pilar, acos consolo, isolar P+Cons.). 5o uso seria
  tech debt visual.
- **#13 Agrupar Vigas por Tipo**: `agruparvigas_large` ja em uso como placeholder
  por `btnDiagramaMontagem` e `btnSequenciamentoBim`. Reverter aqui faria 3
  botoes com mesmo icone (Agrupar + Diagrama + Sequenciamento). Victor precisa
  criar icones especificos pra Diagrama (prancha de obra) e Sequenciamento
  (BIM 4D) antes de liberar `agruparvigas` pro botao original.
- **#15 Cotas por Alinhamento**: mapeamento `cotas_alinhamento` -> `cotas_eixo`
  e semanticamente esquisito (Alinhamento no Revit = linha de referencia
  arquitetonica; Eixo = grid estrutural; nao sao a mesma coisa).

### 4 colisoes aceitas como semanticamente OK (revertidas mesmo compartilhando)

- `viga_dividida_large`: `btnCortarElementos` + `btnCortarPerfilInterferencia`
  (ambos sao cortes)
- `beam_isolar_large`: `btnPfIsolarLajes` + `btnIsolarVigasEstruturais` (ambos
  sao "isolar")
- `broom_large`: `btnVerificarModelo` + `btnLimparAgrupamentosVisuais` (vassoura
  = limpeza, "verificar" e parente proximo)
- `numeracao_large`: `btnPfNomearElementos` + `btnNumerarItens` ("numerar" e
  "nomear" sao primos diretos)

### Compatibilidade

- Sem mudancas funcionais. 100% compativel com v2.6.7. So visual.
- v2.6.7 NAO marcada AFETADA (funcionalmente correta, so com identidade visual
  errada nos 13 botoes — agora resolvida).

### Known follow-ups (v2.7.0+ — aguardando Victor)

- "Travamentos" — Victor entregar icone proprio diferente de Contraventamento
- "Isolar Pilares Estruturais" — Victor entregar `isolar_pilares` proprio
- "Agrupar Vigas" — Victor entregar 2 icones novos: Diagrama de Montagem
  (prancha de obra) e Sequenciamento BIM (4D phasing)
- "Cotas por Alinhamento" — Victor confirmar mapeamento OU entregar icone
  proprio

### Cleanup pendente (fora de escopo desta release)

- Arquivos `_32_light` orfaos em `Resources/` permanecem (cleanup massivo
  fica para v2.7.0 — esta release foca so em desfazer o bug do `App.cs`).

---

## [2.6.7] - 2026-05-20

### Changed (UX micro-fix)
- `DiagramaMontagemWindow`: dimensoes aumentadas para dar mais ar natural ao
  conteudo antes do `ScrollViewer` ativar em DPI alto:
  - `Width`: 540 -> **600** (mais ar horizontal nos labels)
  - `MinHeight`: 520 -> **640** (cabe conteudo em DPI 100% sem scroll)
  - `MaxHeight`: 800 -> **1000** (DPI alto: ~200px extras antes do scroll)
- Estrutura interna intacta (DockPanel + footer Dock=Bottom + ScrollViewer
  preservada da v2.6.2). Sem mudancas em controles, handlers, ou logica.

### Compatibilidade
- 100% compativel com v2.6.6.
- v2.6.6 NAO marcada AFETADA (funcionalmente correta, so apertada em DPI alto).

---

## [2.6.6] - 2026-05-20

### Changed (UX refinement — Diagrama de Montagem)

- **Cotas individuais agora com offset ADAPTATIVO**: posicionadas a 35mm da
  face externa de cada perfil (lido de `STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH`
  do `FamilySymbol`), em vez de offset fixo de 200mm do eixo (v2.6.5).
  Cotas ficam consistentes e legiveis independente do tamanho do perfil:

  | Perfil | depth | width | half-section | clearance | offset total |
  |---|---|---|---|---|---|
  | U75x40x2.66 | 75mm | 40mm | 37.5mm | 35mm | **72.5mm** |
  | U100x50x3.04 | 100mm | 50mm | 50mm | 35mm | **85mm** |
  | W360x57.8 | 360mm | 172mm | 180mm | 35mm | **215mm** |
  | HSS50x50 | 50mm | 50mm | 25mm | 35mm | **60mm** |
  | Sem params standard | 0 | 0 | 100mm (fallback) | 35mm | **135mm** |

- **Clearance configuravel** via `DiagramaMontagemConfig.ClearanceCotaIndividualMm`
  (default 35mm). UI dedicada fica pra v2.7.0+; campo no DTO ja deixa configuravel
  pra cliente avancado.

- **Stagger removido**: o offset adaptativo ja garante espacamento natural entre
  pecas vizinhas de perfis diferentes. Stagger anterior (200/300mm alternado)
  era cosmetic-only.

### Rejeitado (analise tecnica registrada)

- **Abordagem bbox-based** (calcular halfSectionPerp projetando os 8 vertices do
  `BoundingBoxXYZ` no eixo perpendicular) foi proposta inicialmente. Analise
  matematica do Cowork mostrou que **falha catastroficamente em pecas inclinadas**:
  uma diagonal U75 a 45 graus tem AABB world-space de ~935mm na perpendicular
  (porque inclui projecao do comprimento de 1000mm), gerando offset de 467mm
  em vez de 37.5mm. Leitura direta dos parametros standard da familia
  (`STRUCTURAL_SECTION_COMMON_*`) e **independente da orientacao da peca**
  porque le a secao bruta antes do placement.

### Added

- `DimensionPlanCalculator.CalcularHalfSectionPerp(depthFt, widthFt)` sub-helper
  publico testavel isoladamente. Usa `Math.Max(depth, width) / 2` (conservador,
  cobre orientacao desconhecida do perfil). Fallback 100mm quando ambos
  parametros sao zero.
- 10 testes unitarios novos cobrindo:
  - Theory CalcularHalfSectionPerp com 6 perfis (U75, U100, W360, HSS50,
    U75 rotacionado, sem-params)
  - U75 valores reais do escritorio (offset = 72.5mm)
  - U100 valores reais (offset = 85mm)
  - Fallback (offset = 135mm)
  - Clearance zero (cota cola na face externa, offset = halfSection)
- Suite total: **866 -> 876 verdes** (zero Skips, zero ignored).

### Compatibilidade

- Modelos, templates, licencas v2.6.5 continuam validos.
- Pranchas de obra geradas em v2.6.5 nao recotam automaticamente — efeito so em
  proximas execucoes do Diagrama de Montagem.
- Familias sem `STRUCTURAL_SECTION_COMMON_*` (ex: in-place, generic model)
  recebem fallback 100mm + warning no log (`Logger.Debug`). Considerar usar
  familias estruturais padrao se ocorrer frequentemente.
- v2.6.5 **NAO marcada AFETADA** — funcionalmente correta, so com posicionamento
  default nao-ideal (200mm fixo do eixo, gerando inconsistencia visual entre
  perfis pequenos e grandes).

### Known follow-ups (v2.7.0+)

- **Orientation-aware**: hoje usa `max(depth, width)` (conservador). Detectar
  orientacao real da peca (transform local) permitiria offset preciso por face
  (alma vs aba). Ganho estimado: -10 a -30mm em alguns casos.
- **UI de clearance**: campo numerico opcional no `DiagramaMontagemWindow`
  abaixo do checkbox de comprimentos individuais.
- **Fallback secundario**: testar `STRUCTURAL_SECTION_COMMON_OUTSIDE_HEIGHT/WIDTH`
  antes do fallback de 100mm, pra cobrir familias customizadas de escritorios.

---

## [2.6.5] - 2026-05-20

### Fixed (Feature — Diagrama de Montagem)
- **`CriarComprimentosIndividuais` agora cria `Dimension` real** (antes:
  `TextNote` experimental herdado do v2.4.0). A cota individual passa a
  ser uma anotacao nativa do Revit que:
  - Move junto com a peca quando o modelo muda
  - Pode ser editada/formatada como qualquer outra cota da vista
  - Exporta corretamente para PDF/DWG/IFC com hierarquia de anotacao
  - Aceita override de valor quando geometrico diverge de fabricacao

- **Padrao perpendicular-a-peca** (clone do
  `CotarPecaFabricacaoService.CriarCotaViaFamilyRefs`) em vez de
  perpendicular-a-vista. Cota terca horizontal, montante vertical e
  diagonal inclinada na mesma Section View, com offset perpendicular
  a cada peca individualmente:

  | Peca | Direcao da cota | Offset perpendicular |
  |---|---|---|
  | Terca horizontal | ao longo de X | +Z (200mm acima) |
  | Montante vertical | ao longo de Z | -X (200mm a esquerda) |
  | Diagonal 30 graus | ao longo da peca | normal a peca, 200mm |

- **`ValueOverride` automatico quando geometrico diverge >5mm do
  STRUCTURAL_FRAME_CUT_LENGTH** (sugestao da auditoria Cowork). Sem
  override, uma diagonal modelada em 1224mm mas cortada em fabrica
  para 1215mm mostraria 1224 na prancha — agora mostra 1215 (valor
  de fabricacao). Threshold 5mm e tolerancia normal de modelagem;
  sub-5mm nao vale poluir com override.

- **Stagger par/impar** (alterna offset entre 200mm e 300mm) para
  evitar colisao visual entre cotas de pecas proximas ou paralelas
  (ex: duas tercas no mesmo plano).

### Added
- **`DimensionPlanCalculator` helper puro** em
  `Services/DiagramaMontagem/` (~140 linhas) — calculadora vetorial pura
  (`Vec3` interno, sem dependencia de `Autodesk.Revit.DB`) de:
  - `CalcularPlanoCota(p1, p2, viewNormal, offsetFt, staggerExtra)`
    -> `(origem, direcao)` da `Line` da cota
  - `DeveAplicarOverride(lengthGeomFt, lengthFabFt, threshold)` ->
    bool (regra do 5mm acima)
  - Detecta caso degenerado: peca paralela ao viewNormal -> caller
    pula + loga warn (era falha silenciosa em v2.4.0)
- **15 testes unitarios** novos via xUnit Theory + Fact, incluindo o
  cenario real reportado pelo Alef (diagonal U75x40x2.66:
  geom=1224mm, cut=1215mm => `ValueOverride="1215"` deve disparar).

### Sem mudancas
- Fluxo do command intacto: continua chamando `Executar(uidoc, ids, config)`.
- `DiagramaMontagemWindow` (XAML + code-behind): apenas texto do
  checkbox (removido "(EXPERIMENTAL — pode poluir)"); contrato e
  DialogResult preservados.
- `DiagramaMontagemConfig.AdicionarComprimentosIndividuais` continua
  `false` por default — opt-in deliberado.

### Compatibilidade
- Modelos, templates, licencas v2.6.4 continuam validos.
- v2.6.4 NAO marcada AFETADA — o caminho v2.4.0->v2.6.4 funcionava
  como `TextNote`, so era pior em fluidez de prancha. v2.6.5 e
  upgrade de experiencia, nao correcao de bug funcional critico.

### Padrao de testes (nota de processo)
- O helper foi extraido como **pure C#** (sem `Autodesk.Revit.DB`)
  para ficar 100% testavel via xUnit — alinhado com o padrao
  consolidado em v2.6.1: `PfStirrupHookRules`,
  `MarcarPecasSignatureBuilder`, `HttpClientTimeoutValidator`. A
  auditoria senior 2026-05-19 criticou o pattern antigo de
  `[Fact(Skip="Requer Revit")]`; ja somos zero Skips no projeto.

### Known follow-ups (v2.7.0+)
- Stagger 3 niveis (banzo sup / banzo inf / diagonais) requer
  classificacao de peca por categoria/orientacao — out-of-scope
  desta release, entrara junto com CAA NBR 6118 que tambem
  precisa tocar dominio de pecas estruturais.
- `DimensionType` configuravel via `DiagramaMontagemConfig`
  (hoje usa default da vista) — pode ser util para escritorios
  com varias DimensionStyles.

---

## [2.6.4] - 2026-05-20

### Fixed (UX)
- **5 janelas refatoradas** ao pattern saudavel (DockPanel + footer Dock=Bottom +
  ScrollViewer/TabControl no LastChildFill) ja validado em
  DiagramaMontagemWindow (v2.6.2):

  | Janela | Antes | Depois | Padrao aplicado |
  |---|---|---|---|
  | BlocoFundacaoArmaduraWindow | Height=680 + NoResize | MinHeight=520 MaxHeight=800 + CanResize | DockPanel + TabControl Fill (6 tabs com ScrollViewer interno preservados) |
  | TercasWindow | Height=600 + Row "filler" * vestigial | SizeToContent=Height MinHeight=460 MaxHeight=720 + CanResize | DockPanel + ScrollViewer + StackPanel |
  | PipeRackWindow | Height=640 fixo (resto ja saudavel) | SizeToContent=Height MaxHeight=760 + CanResize | refactor minimo (estrutura DockPanel ja existia) |
  | PlacaBaseConfigWindow | Height=720 + Row "*" + botoes sem x:Name | SizeToContent=Height MinHeight=520 MaxHeight=800 + CanResize | DockPanel + ScrollViewer + StackPanel + x:Name (btnLancar + btnCancelar) |
  | PlanoMontagemWindow | Height=600 + Row "*" TabControl | Height=600 (mantido por DataGrid no Tab 2) + MaxHeight=800 ajustado + CanResize | DockPanel + TabControl Fill (3 tabs preservados) |

- **Bug "ajustar posicao da terca" do Victor (via Bruna) fechado** — TercasWindow.
  Diagnostico: nao existe botao com esse nome literal; bug era do btnOk "OK" que
  sumia em DPI alto, impedindo o usuario chegar no fluxo pos-OK do command
  ("selecione linha limite inicial, linha limite final, banzos de divisao").
  Fix estrutural resolve o problema visual independentemente da nomenclatura.

### Defensivo (todas as 5 janelas)
- Botao primary: `IsDefault=True` (Enter dispara) + `TabIndex=99`
- Botao cancel: `IsCancel=True` (Esc fecha) + `TabIndex=98`
- `TabIndex` explicito em todos os inputs principais (ordem linear de cima
  pra baixo)
- `WindowStartupLocation=CenterOwner` (substitui CenterScreen)
- `ResizeMode=CanResize` (substitui NoResize ou CanResizeWithGrip — pega
  consistente)
- Footer `Style=ActionBarBorder` (consistente com DiagramaMontagem v2.6.2 e
  PipeRack original)

### Sem mudancas
- Logica de comando algum, services, models — intacta.
- Code-behind das 5 janelas (1145 linhas combinadas) NAO modificado:
  PHASE 1 confirmou zero acoplamento com `RowDefinition` / `Grid.X` /
  `grid.RowDefinitions[N]`.
- Contratos `DialogResult` preservados em todas (handlers `Click=BtnX_Click`
  mantidos por nome de metodo, nao por x:Name).
- PlacaBaseConfigWindow ganhou `x:Name="btnLancar"` + `x:Name="btnCancelar"`
  mantendo handlers originais `BtnOk_Click` / `BtnCancel_Click` — zero risco
  em runtime, so consistencia.

### Compatibilidade
- Modelos, templates, licencas v2.6.3 continuam validos.
- v2.6.3 NAO marcada AFETADA — funcionalmente correta, so com 5 janelas com
  pattern de risco em DPI alto.

### Known follow-ups (v2.7.0 — UX consistency pass completo)
- **12 janelas restantes** com Height fixo + CanResize/CanResizeWithGrip mantidas
  nesta release (escopo controlado das 5 piores). Refactor completo do design
  system fica para v2.7.0.
- **Style global de Window** em `AppTheme.Base.xaml` (atualmente cada janela
  declara MergedDictionary individualmente) — v2.7.0.
- **ADR-011** formalizando o pattern (DockPanel + Footer + ScrollViewer/Fill +
  SizeToContent quando aplicavel) — v2.7.0.

---

## [2.6.3] - 2026-05-20

### Fixed (UX)
- **17 icones do ribbon migrados para padrao lucide_blue do Victor**
  (14 do plano original + 3 bonus descobertos no inventario que ja
  estavam entregues mas sem mapping):

  | # | Comando | Antigo | Novo |
  |---|---------|--------|------|
  | 1 | CmdLancarPipeRack | `piperack_large/small.png` | `pipe_rack_32_light.png` + `pipe_rack_16_light.png` (unico com small 16 real) |
  | 2 | CmdLancarEscada | `escada_large/small.png` | `escada_32_light.png` |
  | 3 | CmdLancarGuardaCorpo | `guardaropo_large/small.png` | `guardacorpo_32_light.png` |
  | 4 | CmdGerarTercasPlano | `tercas_large/small.png` | `gerar_tercas_32_light.png` |
  | 5 | CmdGerarTravamentos | `travamentos_large/small.png` | `travamento_32_light.png` |
  | 6 | CmdAjustarEncontroVigas | `viga_encontro_large/small.png` | `ajustar_encontro_32_light.png` |
  | 7 | CmdCortarPerfilPorInterferencia | `viga_dividida_large/small.png` | `seccionar_viga_32_light.png` |
  | 8 | CmdDesabilitarUniaoVigasSelecao | `viga_sem_uniao_selecao_*.png` | `sem_uniao_selecao_32_light.png` |
  | 9 | CmdDesabilitarUniaoVigasVista | `viga_sem_uniao_vista_*.png` | `sem_uniao_vista_32_light.png` |
  | 10 | CmdIsolarVigasEstruturais | `beam_isolar_large/small.png` | `isolar_vigas_32_light.png` |
  | 11 | CmdIsolarPilaresEstruturais | `column_line_large/small.png` | `isolar_pilares_32_light.png` |
  | 12 | CmdAgruparPilaresPorTipo | `agruparpilares_large/small.png` | `agrupar_pilares_32_light.png` |
  | 13 | CmdAgruparVigasPorTipo | `agruparvigas_large/small.png` | `agrupar_vigas_32_light.png` |
  | 14 | CmdLimparAgrupamentosVisuais | `broom_large/small.png` | `limpar_cor_32_light.png` |
  | 15 | CmdGerarCotasPorAlinhamento | `ruler_large/small.png` | `cotas_alinhamento_32_light.png` |
  | 16 | CmdNumerarItens | `tag_large/small.png` | `numerar_itens_32_light.png` |
  | 17 | CmdExportarListaMateriais | `sheets_large/small.png` | `exportar_materiais_32_light.png` |

- **Bug visual de duplicacao corrigido**: "Encontro" e "Gerar Conexao"
  agora usam icones diferentes. Antes (v2.6.2-): ambos compartilhavam
  `viga_encontro_large.png` — usuario via dois botoes adjacentes no
  ribbon com mesmo simbolo. Encontro migrou pro lucide_blue
  `ajustar_encontro_32_light.png`; Gerar Conexao usa placeholder
  `link_large.png` (decisao Alef no checkpoint v2.6.3, ver Known
  follow-ups).

### Chore
- Removidas **2 pastas legadas** `Resources/_backup_lucide_redesign_2026-04-27/`
  e `Resources/_backup_uniform_blue_2026-04-27/` (118 PNGs combinados —
  snapshots de redesigns de paleta de 26 dias atras, tracked no git
  desde entao sem uso). Auditoria senior 2026-05-19 PHASE 2 listou
  como ALTO.
- Removidos **arquivos orfaos** `beam.png` (10079 bytes) + `beam (1).png`
  (9426 bytes — duplicata acidental do Explorer). Cross-check confirma
  zero referencias em XAML/CS. Sizes diferentes indicam divergencia
  visual real entre os 2, mas como nenhum era usado, ambos foram
  deletados.
- **Renomeados 6 arquivos** com sufixo "(1)" do Explorer:
  `escada_{16,32}_light_hidpi (1).png`,
  `guardacorpo_{16,32}_light_hidpi (1).png`,
  `travamento_{16,32}_light_hidpi (1).png` viraram
  `<nome>_{16,32}_light.png` (sem `_hidpi` e sem `(1)`).
  Razao: Victor entregou esses 3 grupos apenas como HiDPI duplicado pelo
  Explorer. Renomeacao deixa o 32x32 servir como universal — em DPI 100%
  identico, em DPI 125/150% Revit upscale levemente. Aceitavel ate
  Victor entregar variantes regulares + HiDPI ajustadas.
- `.gitignore` recebe pattern `SteelBIM/Resources/_backup_*/` para
  prevenir reintroducao acidental de snapshots locais de redesign.
- Adicionado bloco de comentario de **convencao oficial dos icones** (v2.6.3)
  acima do helper `AddButton` em `SteelBIM/App.cs`: nomenclatura
  `snake_case_<size>_<theme>.png`, sizes 32/16, HiDPI variants opcionais,
  padrao legado `<nome>_large.png` em fade-out, referencia para
  CHANGELOG "Known follow-ups".
- Adicionado `docs/audits/AUDITORIA-SENIOR-2026-05-19-v2.6.0.md` (773
  linhas) que estava untracked desde a task de auditoria — relatorio
  origem dos P0 enderecados em v2.6.1 e dos P1 enderecados em v2.6.2 /
  v2.6.3. Adicao bonus do cleanup commit.

### Known follow-ups (v2.6.4+ — aguardando icones do Victor)
- **"Gerar Conexao"** usa `link_large.png` temporariamente — aguardando
  `gerar_conexao_32_light.png` (chapa de ponta + parafusos seria ideal).
- **Variantes `_16_light` regulares pendentes** para 16 dos 17 botoes
  migrados: `ajustar_encontro`, `agrupar_pilares`, `agrupar_vigas`,
  `gerar_tercas`, `cotas_alinhamento`, `numerar_itens`, `isolar_pilares`,
  `isolar_vigas`, `limpar_cor`, `exportar_materiais`, `seccionar_viga`,
  `escada`, `guardacorpo`, `travamento`, `sem_uniao_selecao`,
  `sem_uniao_vista`. Atualmente o `_32_light` e passado como small
  tambem (Revit faz downscale; small fica levemente borrado em algumas
  resolucoes). Unico com `_16_light` regular hoje: `pipe_rack`.
- **~32 comandos restantes** ainda usam icones do padrao antigo
  (`<nome>_large.png` / `<nome>_small.png`) aguardando refacao do Victor.

### Sem mudancas
- Logica de comando algum, services, models — intacta.
- Estrutura do ribbon (abas + paineis) — preservada.
- Helper `AddButton` / `AddStackedButtons` — apenas comentario adicionado, codigo intacto.

### Compatibilidade
- Modelos, templates, licencas v2.6.2 continuam validos.
- v2.6.2 NAO esta marcada AFETADA — e funcionalmente correta, so
  visualmente datada. v2.6.3 e recomendada por coerencia visual mas
  nao corrige bug critico.

---

## [2.6.2] - 2026-05-20

### Fixed (UX)
- **DiagramaMontagemWindow** agora exibe SEMPRE os botoes "Gerar Diagrama" e
  "Cancelar" no rodape, independente de resolucao ou DPI. Antes (v2.6.1-):
  janela tinha `Height=600` rigido + `ResizeMode=NoResize`; em DPI 125%/150%
  ou laptop ~768px com taskbar+ribbon Revit, o conteudo (titulo + 4 GroupBoxes)
  excedia 600px e empurrava os botoes pra fora da viewport, tornando a feature
  carro-chefe inutilizavel. Bug reportado pelo Alef em smoke test v2.6.1.

  Fix estrutural (XAML-only): root `Grid` (7 rows) trocado por `DockPanel`
  com footer `Dock=Bottom` (sempre visivel) + conteudo em `ScrollViewer`
  (auto-scroll se passar `MaxHeight=800`). Window agora usa `SizeToContent=Height`
  + `MinHeight=520` + `ResizeMode=CanResize` + `WindowStartupLocation=CenterOwner`.

  Acabamento defensivo: `btnGerar IsDefault=True` (Enter dispara), `btnCancel
  IsCancel=True` (Esc dispara), `TabIndex` explicito em todos os 14 inputs
  (radios 1-3, checkboxes 10-17, textboxes 20-21, botoes 98-99), estilo
  `ActionBarBorder` no footer (consistente com restante do plugin).

### Compatibilidade
- Modelos, templates, licencas v2.6.1 continuam validos.
- Zero mudancas em comandos, services, models ou code-behind do DiagramaMontagem
  — so layout XAML. Contrato `DialogResult` preservado.
- Demais 20 dialogs do plugin nao afetados (16 ja usam o pattern saudavel
  `SizeToContent=Height + MaxHeight`).

### Notes
- Cross-check da PHASE 1 identificou `BlocoFundacaoArmaduraWindow.xaml` com
  o mesmo pattern de risco (`Width=720 Height=680 + NoResize + Row=*`). Sem
  bug reportado ainda; deferido para "UX consistency pass" em v2.7.0.
- **Smoke visual no Revit mandatorio antes de promover GA** (DPI 100% /
  125% / 150% se possivel). Hotfix sai como prerelease.

---

## [2.6.1] - 2026-05-19

### Fixed (CRITICAL)
- **NBR-1:** `Bloco/RebarCreationService.GetHookTypeByAngle` agora delega
  calculo de hook a `PfStirrupHookRules.IsCompliantWithNbr` antes de
  reusar hook existente. Antes: pattern identico ao bug do v2.4.0 (gancho
  135 com rabo 6.O em vez de 10.O exigido pela NBR 6118 secao 9.4.6.1)
  vivo em outro service. Identificado pela auditoria senior 2026-05-19
  PHASE 5.1 #1.
- **NBR-2:** `PfRebarService.GetHookTypeByAngle` agora valida multiplier
  de hook 135 pre-existente antes de reusar. Fix v2.4.1 era PARCIAL: so
  atuava quando nenhum hook 135 pre-existia. Projetos com template
  configurado (com hook 135 multiplier=6.0 estilo v2.4.0) continuavam
  silenciosamente afetados. Auditoria PHASE 5.1 #2. Se hook pre-existente
  for non-compliant, log warning + criar novo hook compliant.
- **MARCA:** `MarcarPecasService` agora produz piece marks deterministicas.
  Antes: ordem de iteracao de `Element.Parameters` (NAO garantida estavel
  pelo Revit API) + uso de `ElementId.Value` (per-document) tornavam a
  execucao N+1 do comando potencialmente diferente da execucao N e
  signatures distintas cross-document. Agora: chave string estavel via
  `MarcarPecasSignatureBuilder` (FamilyName + Name) + ordenacao
  alfabetica de parametros. Auditoria PHASE 5.6.

### Security
- **UpdateDownloader:** construtor agora valida que `HttpClient.Timeout`
  esta em (0, 60s] via novo helper puro `HttpClientTimeoutValidator`.
  Antes: aceitava HttpClient default (Timeout=100s) ou Infinite. Sob
  ataque slowloris, download de 50MB congelaria a UI do Revit por
  minutos. Auditoria PHASE 6.5 #2.
- **PiiScrubber:** cobertura expandida com 3 novos padroes:
  * Path Windows localizado PT-BR (`C:\Usuarios\...` e `C:\Usuários\...`)
  * Path UNC (`\\server\share\...` vira `<UNC>\...`) — share name
    frequentemente carrega nome de cliente
  * Filename Revit (.rvt / .rfa / .rte / .rft) vira `<REVIT_FILE>.<ext>`
  Adicionalmente, `SentryOptionsBuilder.ScrubAndTag` agora scrubba
  `evt.ServerName` (hostname) e itera `SentryExceptions[].Stacktrace.
  Frames[].AbsolutePath/FileName` aplicando o scrubber. Auditoria
  PHASE 6.3. Gap conhecido documentado: filenames multi-word tem partial
  leak da primeira palavra (regex exclui whitespace pra evitar gobbling
  de sentencas) — palavras genericas (Projeto/Familia/Modelo) sozinhas
  nao identificam cliente.

### Chore
- Removidos 5 arquivos `.bak-alef-v1.5` obsoletos do disco local de
  desenvolvimento (`Commands/PF/CmdPfInserirAcosPilar`, `AcosViga`,
  `EstribosPilar`, `EstribosViga` + `Services/PF/PfRebarService`).
  Auditoria PHASE 2.9 listou como ALTO mas verificacao mostrou que
  `.gitignore` linha 58 (`*.bak*`) ja excluia esses arquivos do repo —
  hotfix removeu apenas dos workspaces locais; nenhum commit foi
  necessario porque os arquivos nunca foram tracked.

### Compatibilidade
- Modelos, templates, licencas v2.6.0 continuam validos.
- Comportamento de hook NBR pode mudar APENAS em projetos onde o bug
  estava ativo (mudanca esperada e desejada — antes era violacao NBR
  9.4.6.1). Usuario verá no log: `[PfRebarService] Hook 135 grau(s)
  'EMT Gancho 135 graus' ja existente com multiplier 6 insuficiente
  (NBR exige >= 10). Criando novo hook compliant.`
- Piece marks geradas por v2.6.0- sao mantidas (no rerun em modelo
  existente). Nova execucao usa novo algoritmo deterministico — pode
  gerar marca diferente para o mesmo elemento se houver diferenca
  entre o algoritmo antigo (ordem de Parameters) e o novo
  (OrderBy alfabetico). Em pratica raro porque a maioria dos campos
  participa da chave, mas vale alertar usuario que rode "Limpar
  Marcas" antes de re-marcar projetos em producao caso queira
  consistencia 100%.

### Notes
- Auditoria senior 2026-05-19 ficou em `docs/audits/AUDITORIA-SENIOR-
  2026-05-19-v2.6.0.md`. Este hotfix fecha os 8 itens marcados P0 dos
  15 CRITICAL identificados. Os 7 restantes (incluindo CAA NBR 7.4.7,
  refactor de `PfRebarService` em helpers, ADR-003/004 migration dos
  services restantes, code-signing Authenticode, ListaMateriais
  determinismo) ficam programados para v2.7.0+.
- Cobertura: 851 testes passando (787 baseline + 64 novos em 4 suites:
  PfStirrupHookRulesTests +17, MarcarPecasSignatureBuilderTests +20,
  HttpClientTimeoutValidatorTests +12, PiiScrubberTests +15).

---

## [2.6.0] - 2026-05-19

### Changed
- **BREAKING (UI):** A aba unica "SteelBIM" foi dividida em duas abas:
  "SteelBIM | Modelagem" (modelagem, conexoes, armaduras PF, visualizacao)
  e "SteelBIM | Detalhamento" (vistas, cotagem, anotacao, CNC, sequenciamento, verificacao, licenca).
  Atalhos de teclado personalizados pelo usuario no Revit podem precisar ser refeitos.
- Comandos, namespaces, services e AddInId nao mudaram. Apenas re-organizacao visual.

### Compatibilidade
- Modelos e templates existentes funcionam sem alteracao.
- Configuracoes salvas (PFConfig, etc) preservadas.
- Licencas v2.5.0 continuam validas.

---

## [2.5.0] - 2026-05-19

### Changed
- README.md reescrito do zero refletindo o estado real do plugin:
  v2.4.1, 48 comandos no ribbon (46 features + 2 utilitários), 787
  testes, NBR 6118 nativo, DSTV/CNC, Diagrama de Montagem completo,
  Sequenciamento BIM 4D, PF completo. O README anterior estava
  desatualizado em 4 versões (dizia v1.5.0, 32 comandos, 419 testes).

### Fixed
- Auditoria sistematica dos 44 sitios de doc.ActiveView identificados
  na auditoria senior v2.4.0:
  * 9 sitios ganharam null-check defensivo novo:
    - 6 comandos com early-return (PADRAO 1, Result.Cancelled +
      AppDialogService.ShowWarning): CmdCortarPerfilPorInterferencia,
      CmdDesabilitarUniaoVigasVista, CmdIsolarPilaresEstruturais,
      CmdIsolarVigasEstruturais, CmdPfIsolarLajes,
      CmdPfIsolarPilaresConsolos
    - 3 services com guard defensivo (PADRAO 2, retorno antecipado +
      Logger.Warn): CotarPecaFabricacaoService (void),
      PfIsolationService e PfNamingService (Result.Cancelled)
  * ~35 sitios verificados como ja protegidos por trabalho de
    auditoria anterior, ou sao contextos nao-guardaveis sem refactor
    (void/List/bool inline) — fora do escopo cirurgico deste release
- Comando executado sem vista ativa em um dos 9 sitios novos agora
  retorna Cancelled com mensagem clara em vez de
  NullReferenceException.

### Notes
- Refactor dos sitios void/List/bool inline planejado para v2.6.0+
  junto com migracao ADR-003 dos 17 services legados.

---

## [2.4.1] - 2026-05-18

### Fixed (CRITICAL)
- Estribos de pilar e viga saiam com rabo de gancho curto demais.
  PfRebarService.GetHookTypeByAngle criava RebarHookType com rabo
  reto = 6.Ø para qualquer angulo != 90 — incluindo o 135 graus que
  eh o DEFAULT de estribo (PfColumnStirrupsConfig/PfBeamStirrupsConfig).
  NBR 6118 9.4.6.1 exige >= 10.Ø para gancho de estribo a 135 graus.
  Reportado pelo Victor.
- Regra de multiplicador extraida para helper puro
  PfStirrupHookRules.NbrStirrupHookMultiplier (90->12, 135->10,
  180->5, fallback 10) com cobertura de teste — fechando o gap que
  deixou a regressao passar de v1.6.0 a v2.4.0.

### Notes
- O bug so se manifestava em projetos sem RebarHookType pre-configurado
  para o angulo (path de auto-criacao). Projetos com gancho 135 ja
  permitido no template nao eram afetados (early-return preservado).

---

## [2.4.0] - 2026-05-17

### Added (FEATURE COMPLETA — Diagrama de Montagem 100% padrao BR)
Complementa o MVP v2.3.0 entregando os 30% deferidos para atingir o
padrao profissional brasileiro de prancha de detalhamento (vide PDF
EM-08 entregue pelo cliente do Alef como referencia):

- **Cotas verticais (SpotElevation)** em niveis chave dos elementos
  (base/topo pilar, vigas, cumeeira). Clusteriza elevacoes proximas
  com tolerancia configuravel (default 100mm). Limitado a 3-15 cotas
  para nao poluir.
- **Cota total do conjunto** — cota linear entre eixos extremos
  visiveis na vista, posicionada acima da linha de cotas entre eixos.
- **Simbolo de nivel** — Levels do projeto que cruzam o range Z dos
  elementos selecionados ficam visiveis com bubble.
- **Inserir em folha** (opcional, default false) — cria ViewSheet
  com TitleBlock disponivel no projeto, posiciona Section View como
  Viewport centralizado, permite customizar numero e nome da folha.
- **Comprimentos individuais por peca** (EXPERIMENTAL, default false) —
  TextNote com `L=NNcm` ao lado de cada elemento. Pode poluir em
  geometria densa, usuario opta.

### Notes
- Sem TitleBlock no projeto: opcao folha eh ignorada com aviso no DTO
- Sem Grids: cotas entre eixos e total nao sao criadas (esperado)
- Sem Levels no range Z: simbolo de nivel nao cria (esperado)
- Tolerancia de cluster influencia a quantidade de cotas verticais —
  ajustar entre 50-300mm conforme densidade da estrutura

---

## [2.3.0] - 2026-05-17

### Added (FEATURE NOVA)
- **Diagrama de Montagem (BR)** — comando novo no painel Montagem
  que gera a prancha tecnica de detalhamento estrutural no padrao
  brasileiro. MVP cobre:
  - Vista de elevacao (Section View) alinhada aos elementos selecionados
  - Crop automatico para mostrar so os elementos relevantes + margem
  - Eixos do projeto visiveis na vista
  - Cotas alinhadas entre eixos consecutivos (linha superior)
  - Tags automaticas com parametro Mark (marca de fabricacao) em cada peca
- Escala padrao 1:75. Orientacao automatica baseada em geometria
  (override manual disponivel na janela: X/Y/Auto).
- Diferente de "Sequenciamento BIM" (v2.2.0, 4D phasing) e de
  "Vista de Peca" (shop drawing individual).
- Pre-selecao obrigatoria (padrao alinhado com v2.1.2). Service ADR-003
  mudo (retorna DTO, cada operacao Revit em transaction propria).

### Roadmap v2.3.1+
- Cotas verticais (alturas dos elementos)
- Cotas individuais de comprimento de pecas
- Insercao em folha com title block
- Simbolo de nivel "TERREO"

---

## [2.2.0] - 2026-05-17

### Changed (BREAKING — UX)
- **Comando "Plano de Montagem" renomeado para "Sequenciamento BIM"**.
  Razao: no Brasil, "plano de montagem" significa prancha tecnica com
  elevacao + eixos + cotas + marcas (diagrama de detalhamento). A
  funcao implementa 4D BIM phasing (atribuir fases, colorir, exportar
  Excel). Nome anterior causava confusao com usuarios brasileiros.
  Classe interna (CmdPlanoMontagem/PlanoMontagemWindow/Service)
  preservada para nao quebrar atalhos personalizados.
- Tooltip do ribbon agora descreve precisamente: planejamento 4D,
  coordenacao de cronograma, integracao com Synchro/Navisworks.

### Fixed
- Bug "Atribuido a 0 elemento(s)" quando elementos selecionados nao
  tinham parametro editavel. Adicionado triplo fallback:
  1. Parametro de projeto EMT_Etapa_Montagem (criado automaticamente
     se nao existir — bind a Structural Framing/Columns/Foundation)
  2. Parametro built-in Comments (com regex "Etapa:N")
  3. Parametro built-in Mark (prefixo "E{N}/")
- Mensagem clara ao usuario quando algum elemento nao aceita a
  atribuicao, em vez de retornar silenciosamente 0.
- LerEtapaDoElemento espelha os 3 fallbacks na leitura (antes so lia
  Integer + Comments).

### Added
- Seletor de cor por fase: na aba "Visualizar Plano", cada etapa do
  DataGrid tem um botao "Escolher" que abre dialog de cor
  (System.Windows.Forms.ColorDialog). Cor escolhida eh aplicada no
  destaque visual da proxima geracao do plano. Cores nao definidas
  usam paleta padrao ciclica. csproj ganha UseWindowsForms=true.

### Roadmap registrado
- "Diagrama de Montagem" no padrao brasileiro (vista de elevacao +
  eixos + cotas + marcas de fabricacao) eh feature distinta planejada
  para v3.0.0. Vide PDF EM-08 entregue pelo cliente como referencia.

---

## [2.1.2] - 2026-05-15

### Fixed (CRITICAL)
- Comando "Plano de Montagem" causava CRASH FATAL do Revit quando
  usuario clicava em "Atribuir aos Selecionados" sem pre-selecao
  previa. Root cause: PlanoMontagemWindow chamava
  `uidoc.Selection.PickObjects()` DENTRO de janela WPF aberta como
  ShowDialog modal — modal bloqueia thread principal do Revit,
  PickObjects precisa thread livre, resultado eh deadlock + crash.
  `Hide()` antes do PickObjects nao resolvia (ShowDialog continua
  bloqueante).
- Fix: alinhar com padrao dos outros 26 comandos — pre-selecao
  agora eh obrigatoria. `CmdPlanoMontagem` valida selecao ANTES de
  abrir janela. Window apenas coleta config (etapa + descricao),
  nao picka mais elementos.
- Detectado por smoke test do Alef apos publicacao da v2.1.1.

### Changed (UX)
- Plano de Montagem agora exige pre-selecao de elementos no Revit
  antes de executar o comando. Fluxo correto:
  1. Selecionar elementos no Revit
  2. Executar Plano de Montagem
  3. Informar etapa + descricao
  4. Clicar em Atribuir

---

## [2.1.1] - 2026-05-15

### Fixed (CRITICAL)
- Janelas WPF do plugin nao abriam por residuo do rebrand v2.0.0:
  `RevitWindowThemeService.cs` tinha 2 pack URIs hardcoded apontando
  para o assembly antigo `/FerramentaEMT;component/`. Trocado para
  `/SteelBIM;component/`. Sem esse fix, NENHUMA janela do plugin
  abria (LicenseActivation, PrivacyConsent, todas PF, Cortar,
  Trelica, etc) — `FileNotFoundException: Could not load file or
  assembly 'FerramentaEMT'`.
- Bug escapou de 4 auditorias porque os greps buscavam
  `FerramentaEMT` em namespaces/usings/paths mas nao em pack URIs
  embutidos em codigo C# (`/Assembly;component/` runtime).
- Sintoma reportado pelo smoke test do Alef em 2026-05-15 logo apos
  a publicacao da v2.1.0. v2.1.0 estava inutilizada em producao.

---

## [2.1.0] - 2026-05-14

### Changed (BREAKING — UI/UX)
- **Ribbon unificada:** comandos antes em duas abas separadas
  ("SteelBIM" + "Ferramentas ECC") agora vivem numa unica aba
  "SteelBIM". Marca unica de produto, percepcao consistente para
  usuario novo. Mantida ordem logica: paineis PF primeiro
  (diferencial competitivo do plugin BR), seguidos do fluxo geral
  (Modelagem, Estrutura, Vigas, Vista, Documentacao, Fabricacao,
  CNC, Verificacao, Montagem, Licenca).
- **Icones renovados:** 44 icones de comandos atualizados para o
  padrao lucide_blue final do Victor (estilo monocromatico azul
  moderno, 2026-04-27). Substitui o estilo ilustrativo
  multicolorido herdado de v1.x.

### Notes
- Migracao de v2.0.x: usuarios que tinham atalhos personalizados na
  aba "Ferramentas ECC" precisam re-criar os atalhos apontando para
  a aba "SteelBIM". Os comandos em si nao mudaram nem foram removidos.

---

## [2.0.3] - 2026-05-14

### Fixed
- `EMT_CODESIGN_SIGNTOOL` -> `STEELBIM_CODESIGN_SIGNTOOL`: ultimo
  residuo do rebrand v2.0.0 que ficou de fora em v2.0.2 (3 arquivos:
  Build-SetupExe.ps1, docs/CODE-SIGNING.md, docs/ADR/009-code-signing.md).
- `dotnet format SteelBIM.Solution.sln --verify-no-changes` agora passa
  com exit 0 (antes 473 erros WHITESPACE em files que escapavam do
  escopo Tests-csproj usado pelo CI). Apply tocou 197 files (cosmetico:
  BOM UTF-8 + using sort + indentacao).

### Changed
- `.gitattributes` ganha regras defensivas explicitas `text eol=lf` para
  `*.cs`, `*.xaml`, `*.json`, `*.md`, `*.yml`, `*.yaml`, `*.xml`,
  `*.config`, `*.resx`. Blinda contra regressao do conflito
  editorconfig/gitattributes (root cause de v2.0.2). Decisao team
  preservada para `*.sln`/`*.csproj`/`*.bat`/`*.ps1`/`*.cmd` (CRLF).
- Docs core (CONTRIBUTING.md, CLAUDE.md, RUNBOOK, DEPLOYMENT, ARCHITECTURE,
  SISTEMA-LICENCA, installer/README, Tests/README): prosa current-tense
  alinhada para `SteelBIM`. Historicos preservados (planning v1.x,
  paths fisicos pre-rebrand em RUNBOOK, ADRs, docs/legal, docs/victor,
  docs/auditoria, docs/reference-projects).
- 5 branches legacy mergeadas removidas: feat/code-signing-skeleton-p0-1,
  feat/rebrand-steelbim-v2, feat/victor-final-5-ribbon-wire,
  refactor/fabricacao-signature-builder, docs/auditoria-wave-victor-final.

### Documented
- `docs/auditoria/v2.0.3-pre-mercado.md`: auditoria consolidada antes do
  lancamento publico (inventario do produto, gaps de README, classificacao
  completa dos 4535 CA warnings em A/B/C, roteiro de smoke test).

### Known issues (deferidos para v2.1.0+)
- README.md desatualizado: titulo, badge versao, contagem de comandos,
  arquitetura. Sera reescrito junto com a pagina de vendas.
- ~4535 warnings CA categoria A/B/C documentados em docs/auditoria/,
  defer para v2.1.0.
- 1 branch legacy (`feat/legal-drafts-p0-5`) ficou: classificador do
  agente bloqueou force-delete, Alef executa manualmente.
- 4 services com melhorias Victor pendentes (auditoria pos-v1.8.0).
- Code signing cert pendente (Sectigo OV ~R\$ 600-900/ano).
- Revisao juridica drafts/legal/*.draft.md pendente.

---

## [2.0.2] - 2026-05-14

### Fixed
- Renomeacao residual do rebrand v2.0.0: variaveis de ambiente
  `EMT_CODESIGN_CERT_PFX`, `EMT_CODESIGN_CERT_PASSWORD` e
  `EMT_CODESIGN_TIMESTAMP_URL` renomeadas para `STEELBIM_CODESIGN_*`
  em `SteelBIM/installer/Build-SetupExe.ps1`, `docs/CODE-SIGNING.md`,
  `docs/ADR/009-code-signing.md` e comentario em `.gitignore`.
  Necessario antes de configurar cert de assinatura digital no GitHub
  Actions. **Nota:** `EMT_CODESIGN_SIGNTOOL` nao foi renomeada — fora
  do escopo deste hotfix; sera tratada em v2.0.3+.
- `dotnet format` em `SteelBIM.Tests/` agora passa sem erros: 162
  arquivos .cs com violacoes de whitespace/BOM/single-line-if foram
  normalizados. O `continue-on-error: true` que mascarava o problema
  foi removido de `.github/workflows/build.yml:83` — formatting
  quebrado agora falha honesto na CI.
- `.editorconfig` `end_of_line` mudado de `crlf` para `lf` no `[*]`
  generico, alinhando com `.gitattributes` (`* text=auto eol=lf`) que
  ja forcava LF para `.cs` na repo. Antes do fix: CI rodava `dotnet
  format` em files LF (do repo) mas editorconfig pedia CRLF — falso
  positivo de 162 arquivos. Era a causa raiz mascarada pelo
  `continue-on-error`.

### Changed
- `PfNamingService`: variavel `vigaHorizontalNoEixoX` renomeada para
  `vigaAlinhadaComEixoY`. Nome anterior contradizia a semantica do
  `GetBeamAxisGroup` (grupo 1 = viga vertical na vista, nao horizontal).
  Logica intacta — apenas naming.

---

## [2.0.1] - 2026-05-13

### Fixed
- Comando "Nomear PF" (PfNamingService): ordenacao de vigas com pequeno
  desalinhamento agora eh deterministica via GetSnappedOrder com
  tolerancia de 10 cm (~0.328 ft). Anteriormente o clustering greedy
  fazia vigas no mesmo eixo logico receberem ordens diferentes dependendo
  de qual viga "ancorava" o grupo. Reportado por cliente em 2026-05-13.

### Added
- Relatorio final de "Nomear PF" agora informa quantas vigas
  diagonais/sem eixo foram numeradas por Id ao final da sequencia.
- Lista de itens filtrados na janela "Nomear PF" ganha cabecalho
  explicando que a ordem da lista (familia/tipo) e para filtro — a
  ordem de numeracao real e geometrica.

---

## [2.0.0] — 2026-05-13 — Rebranding completo FerramentaEMT -> SteelBIM

### BREAKING — Rebranding completo FerramentaEMT -> SteelBIM
- Pasta de instalacao: `%AppData%\Autodesk\Revit\Addins\2025\SteelBIM\`
- Storage paths: `%LocalAppData%\SteelBIM\` (migracao automatica do legado)
- Env vars renomeadas: `EMT_*` -> `STEELBIM_*` (LICENSE_SECRET, SENTRY_DSN, POSTHOG_API_KEY, POSTHOG_HOST, SESSION_ID_PATH, CODESIGN_*)
- Registry: `HKCU\Software\SteelBIM\` (migracao automatica)
- Aba do ribbon: "SteelBIM" + "Ferramentas ECC" (mantida)
- Backward-compat: licencas/trial/settings sao migrados automaticamente do path antigo na primeira execucao. Nenhuma chave precisa ser reemitida.

### Mantido por compatibilidade com modelos .rvt existentes
- Prefixo `EMT_` em parametros/familias/groups dentro do modelo Revit (EMT_Chapa_Ponta, EMT_Viga_Conectada, etc)
- Sentinel `DirectShape.ApplicationId = "FerramentaEMT"` em `EscadaService.cs:303`
- `AssemblyCompany("EMT")` (vendor legal)
- `<VendorId>EMT</VendorId>` no `.addin`
- `AddInId` GUID preservado (610FE337-F95D-4813-8BF8-2CE11C9948C1)

### Cleanup
- Removida classe dead code `Constants.Identificadores` (zero leitores confirmado por auditoria; valores estavam dessincronizados do .addin). Outras classes em `Constants` (Tolerancia, Cotas, Vistas, Fabricacao, ListaMateriais, Ui) mantidas.

### Migrations automaticas no primeiro start
- `LicenseStore`: copia `%LocalAppData%\FerramentaEMT\license\emt.lic` e `emt.trl` para path SteelBIM, deleta legados; migra registro `Software\FerramentaEMT\Trial` para `Software\SteelBIM\Trial`.
- `PrivacySettingsStore`: copia `privacy.json` legado, deleta antigo.
- `LicenseSecretProvider`: fallback de leitura para path legado com `Logger.Warn` (sem migracao automatica — secret eh deploy-managed).

---

## [1.8.0] — 2026-05-12 — Incorporacao Wave Victor Versao Final

**Release type:** Pre-release / Soft launch (distribuição privada para alunos selecionados).

### Added (Incorporacao Wave Victor Versao Final)
- F1: Contraventamento Plano automatico (Comando + Window) — panel "Estrutura" aba ECC
- F2: Lancar Placas Base em pilares selecionados — panel "Estrutura" aba ECC
- F3: Bloco Fundacao com armaduras parametrizadas — panel "PF Armaduras" aba EMT
- F4: Inserir Acos em Estaca (barras + estribos circulares) — panel "PF Armaduras" aba EMT
- F5: Lancar Fundacoes (pilares para fundacoes) — panel "PF Construcao" aba EMT
- 156 imagens em `Resources/` (icones novos para os comandos Victor)
- 6 documentos em `docs/victor/` (especificacoes originais do Victor)

### Changed
- `CmdCortarElementos`: agora abre `CortarElementosWindow` com selecao de escopo (Selecao/VistaAtiva/Modelo) e filtro de categorias antes de coletar elementos
- `PfElementService.IsStructuralPile`: distincao geometrica (dz/max(dx,dy) >= 3.0) vs `IsTwoPileCap`
- `PfRebarShapeCatalog`: novo helper `TrySelect(ComboBox, string)` para pre-selecionar shape salva
- `PfRebarService`: 7 novos metodos para Estaca (`ExecuteEstacaBars` publico + 6 privados: `InsertEstacaBars`, `InsertEstacaStirrups`, `CreateEstacaCircularStirrupSet`, `GetEffectiveCoverCm`, `BuildPileFrame`, `CreatePolygonLoopHorizontal`)
- `AssemblyVersion`: 1.7.0 → 1.8.0

### Refactored (ADR-003)
- `ContraventamentoPlanoService`: agora "mudo", retorna `ContraventamentoPlanoResultado` com 9 flags + `Func<bool> confirmarPreview` callback (8 chamadas `AppDialogService.Show*` no snapshot Victor → 0 no nosso)

### Tests
- +~50 novos testes unitarios para DTOs puros (`CortarElementosConfig`, `PlacaBaseLancamentoResultado`, `BlocoFundacaoRebarConfig` e nested types, `PfEstacaRebarConfig`)
- LinkedSources adicionados em `FerramentaEMT.Tests.csproj`
- Total acumulado v1.8.0: ~770 testes (vs 721 em v1.7.0)

### Known limitations (intencionais para pre-release)
- Instalador NAO digitalmente assinado (Windows SmartScreen mostrara aviso "Aplicativo nao reconhecido"). Sera corrigido em release pos-aquisicao de certificado Authenticode.
- Documentos legais (`docs/legal/{EULA,PRIVACY,TOS}.md`) redigidos pelo desenvolvedor, pendentes de revisao juridica formal.
- DTOs com dependencia de Revit API (`ContraventamentoPlanoConfig`, `ContraventamentoPlanoResultado`, `PlacaBaseConfig`, `PfFoundationPlacementConfig`) nao testados unitariamente; cobertos por smoke test manual no Revit.

### Distribution
- GitHub Release marcado como **pre-release** (badge "Pre-release" no GitHub)
- Soft launch privado para lista de alunos
- Nao publicado em marketplaces (Autodesk App Store) ate documentacao legal final

---

## [1.7.0] — 2026-05-XX — Pre-release commercial readiness

**Release type:** Pre-release / Soft launch (distribuição privada para alunos selecionados).

### Added — Fase 1 commercial readiness
- CI compila o csproj principal (PR-1)
- GitHub Actions bumped para versões mais recentes (PR-1.1)
- Auto-update via GitHub Releases API com SHA-256 validation e fallback de 3 tentativas (PR-2)
- Crash reporting via Sentry com scrubbing de PII (PR-3)
- Telemetria de uso opt-in via PostHog HTTP-direct (PR-4)
- `PrivacyConsentWindow` com 3 toggles (auto-update, crash reports, telemetria), versão de consent 3
- `FailureHandlingHelper` centralizado (P1.1)
- `CotasService` refatorado para retornar `Result<CotagemResumo>` (P1.4, ADR-003)
- Documentos legais EULA + Privacy Policy + Termos de Compra (LGPD-compliant draft)
- Esqueleto de code signing parametrizado (ativará quando certificado for adquirido)
- EULA prompt pré-instalação ativado no `SetupBootstrapper` (EULA.md embutido como recurso)

### Changed
- `AssemblyInformationalVersion`: 1.6.0 → 1.7.0

### Known limitations (intencionais para pre-release)
- Instalador NÃO digitalmente assinado (Windows SmartScreen mostrará aviso "Aplicativo não reconhecido"). Será corrigido em v1.7.1 após aquisição de certificado Authenticode.
- Documentos legais redigidos pelo desenvolvedor, pendentes de revisão jurídica formal. Versões finais em v1.7.1.

### Distribution
- GitHub Release marcado como **pre-release** (badge "Pre-release" no GitHub).
- Soft launch privado para lista de alunos.
- Não publicado em marketplaces (Autodesk App Store) até documentação legal final.

---

## [1.6.0] — 2026-04-27 (Wave 2 + zoneamento NBR 6118 re-portado)

Esta release promove `1.6.0-rc.1` (incorporação Victor Wave 2) a versão final, incluindo o follow-up do zoneamento de estribos NBR 6118 e cleanup ADR-003 do `PfTwoPileCapRebarService`. Resolve as 2 regressões conhecidas que ficaram documentadas na rc.1.

### Security — Base64Url canonicalização (defesa-em-profundidade)
- **`Licensing/Base64Url.Decode`** agora rejeita encodings não-canônicos. O bug foi descoberto pelo teste `KeySigner.Verify_returns_null_when_signature_is_tampered`: a HMAC-SHA256 de 32 bytes em Base64URL ocupa 43 chars × 6 bits = 258 bits, mas apenas 256 são significativos. Os 2 bits sobressalentes do último char eram ignorados pelo `Convert.FromBase64String`, permitindo múltiplas representações do mesmo token.
- Implicação anterior (não-crítica para forja, mas problemática para licenciamento): um cliente poderia redistribuir a mesma chave em variações cosméticas, quebrando fingerprinting de tokens.
- Fix: após `Convert.FromBase64String`, re-codificamos os bytes via `Encode` e comparamos com a entrada original; se diferirem, lançamos `FormatException` (e `KeySigner.Verify` retorna null como esperado).
- HMAC em si nunca esteve comprometido — não é possível forjar assinatura sem o segredo. Esta correção elimina ambiguidade de representação, alinhando o sistema com o comportamento esperado do teste.

### Added — Zoneamento NBR 6118 de estribos (re-portado da v1.5.0)
- **`PfRebarService.InsertColumnStirrups`** agora suporta dual-mode:
  - `UsarEspacamentoUnico=true` → modo Victor (1 rebar com `EspacamentoCm` uniforme)
  - `UsarEspacamentoUnico=false` (default) → zoneamento NBR 6118 com 3 rebars (inferior + central + superior) usando `EspacamentoInferiorCm`, `EspacamentoCentralCm`, `EspacamentoSuperiorCm` e `AlturaZonaExtremidadeCm`
  - Pilares circulares sempre caem no modo simples (zoneamento de pilar circular não é prática típica da norma brasileira)
- **`PfRebarService.InsertBeamStirrups`** análogo:
  - `UsarEspacamentoUnico=true` → modo Victor
  - `UsarEspacamentoUnico=false` (default) → zoneamento por apoio (inicio + central + fim) com `EspacamentoApoioCm`, `EspacamentoCentralCm`, `ComprimentoZonaApoioCm`
- Implementação preserva todas features Wave 2: `RebarShape`, `RebarHookType`, `RebarStyle.StirrupTie`. Cada zona aplica o shape/hook escolhido, mantendo rastreabilidade visual no Revit.
- `PfRebarService.cs` cresceu de 1.768 → 1.891 linhas. Backup `PfRebarService.cs.bak-alef-v1.5` mantido como referência histórica do código v1.5.0.

### Fixed — Regressão "zoneamento dormente" da rc.1
- Configurações de zoneamento (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidade`, `EspacamentoApoio`, `ComprimentoZonaApoio`) estavam preservadas em `PfRebarConfigs.cs` mas o serviço não as lia. Bug arquitetural resolvido com a implementação dual-mode acima.

### Refactored — ADR-003 cleanup do `PfTwoPileCapRebarService` (Wave 2 followup)
- **Service mudo agora**: removidas 2 chamadas `AppDialogService.ShowWarning/ShowInfo` (linhas 28 e 82). Em conformidade com ADR-003.
- **Nova assinatura**: `Result Execute(uidoc, config, out PfTwoPileCapResultado resultado)`. Caller (`CmdPfInserirAcosBlocoDuasEstacas`) decide UX a partir do DTO populado.
- **Novo DTO `Models/PF/PfTwoPileCapResultado`** com `SelecaoVazia`, `HostsProcessados`, `HostsComSucesso`, `ArmadurasCriadas`, `Avisos` (limitados a 10 na UI), `ToResumo()`.
- **Logger.Info** estruturado adicionado ao final do `Execute` com métricas (hosts, sucesso, armaduras, avisos) — facilita troubleshooting em produção.
- Caller `CmdPfInserirAcosBlocoDuasEstacas` consome `resultado` e usa `ShowWarning`/`ShowInfo` herdados de `FerramentaCommandBase`.

### Tests
- **+1 arquivo**: `Models/PF/PfTwoPileCapResultadoTests` — 5 Facts cobrindo defaults, formato sem avisos, formato com avisos, limite de 10 ocorrências na UI, flag `SelecaoVazia`.
- 1 LinkedSource novo no `FerramentaEMT.Tests.csproj`.
- Total acumulado v1.6.0: **465 casos** (eram 460 na rc.1).

### Known issues
- Os outros 7 services que ainda usam `AppDialogService` (`PfRebarService`, `AutoVistaService`, `AgrupamentoVisualService`, `AjustarEncontroService`, `ListaMateriaisExportService`, `CotarPecaFabricacaoService`, `MarcarPecasService`) continuam com o padrão antigo. Migração planejada como dívida arquitetural (P1.1 da auditoria de mercado).

---

## [1.6.0-rc.1] — 2026-04-27 (Incorporação Victor Wave 2 — RebarShape + NBR 6118 + Bloco 2 Estacas)

Segunda onda de incorporação do snapshot do Victor (`FerramentaEMT (3).rar`, 2026-04-24). Foco em PF: catalogo de RebarShape do projeto Revit, preview visual nas janelas, cálculo de ancoragem NBR 6118, lap splice, modo coordenadas manual e rotina dedicada para bloco de duas estacas. Ribbon separado em duas abas para desacoplar o fluxo PF do fluxo metálico.

**Status de validação (2026-04-27):**
- Build Release: 0 Erro(s), 2 Aviso(s) pre-existentes (RevitAPI/RevitAPIUI references). Tempo: 7s.
- Plugin carregado e visualizado no Revit 2025 do Alef. Duas abas (`Ferramenta EMT` + `Ferramentas ECC`) renderizadas corretamente.
- Suite de testes: 460 casos totais, 458 aprovados na primeira rodada. As 2 falhas eram bug REAL no código do Victor (`ToComment` culture-sensitive em pt-BR) — corrigido nesta release como parte do `### Fixed` abaixo. Re-rodar testes pos-fix dá 460/460.
- Instalador distribuível gerado (`artifacts/installer/FerramentaEMT-Revit2025-Release.zip`, 3.8 MB + `setup-publish/FerramentaEMT.SetupBootstrapper.exe`).

### Fixed — Build + culture-invariant (descoberto pelos testes Wave 2)
- **`Services/PF/PfElementService.IsTwoPileCap`** — método ausente após Wave 2 (eu adotei o `PfTwoPileCapRebarService` mas esqueci de trazer o helper que ele e o command `CmdPfInserirAcosBlocoDuasEstacas` chamam). Sem isso o build falhava com 2× CS0117. Detecta `FamilyInstance` com `Category.OST_StructuralFoundation`.
- **`FerramentaEMT.csproj`** — adicionada `<PackageReference Include="System.Drawing.Common" Version="8.0.10" />`. O `PfRebarShapePreviewService` (do Victor) usa `System.Drawing.Bitmap` e `System.Drawing.Imaging.ImageFormat` — esses tipos saíram do BCL no .NET 5+ e exigem package explícita. Sem isso o build falhava com 3× CS0012/CS1069.
- **`Models/PF/PfTwoPileCapBarPosition.ToComment`** — bug culture-sensitive descoberto pelos testes em pt-BR. O método usava `$"...{x:0.##}"` que respeita `CurrentCulture`, gerando `"diam. 6,3"` em vez de `"diam. 6.3"` quando a máquina está configurada em pt-BR. Como esse `Comment` vai parar no parâmetro Comments do Revit e é consumido por schedules/CSV downstream, vírgula no decimal corromperia parsers terceiros. Forçado `CultureInfo.InvariantCulture` em todos os formatadores numéricos (3 ocorrências). 460/460 testes passam pós-fix.

### Added — Catálogo de RebarShape do projeto Revit (Victor Wave 2)
- **`Services/PF/PfRebarShapeCatalog`** — varre `FilteredElementCollector(RebarShape)` filtrado por `RebarStyle.StirrupTie`, primeiro item sempre "Automatico" (flag `IsAutomatic=true`), ordena por sufixo numérico.
- **`Services/PF/PfRebarShapePreviewService`** — gera `BitmapImage` (220 px default) do shape selecionado para exibir na UI; fallback textual quando preview indisponível.
- **`Models/PF/PfRebarShapeOption`** — DTO (`ElementIdValue`, `Name`, `DisplayName`, `IsAutomatic`). `ToString()` prefere `DisplayName`.
- **`PfBeamStirrupsWindow` + `PfColumnStirrupsWindow`** — combo box de shape + `<Image>` de preview. Quando o usuário escolhe um shape do projeto, a rotina cria estribo automático primeiro; se o shape selecionado for compatível com a geometria, aplica em cima. Se não for compatível, mantém o automático sem interromper o comando.

### Added — Cálculo NBR 6118 (ancoragem + traspasse)
- **`Services/PF/PfNbr6118AnchorageService.Calculate(diameterMm, PfLapSpliceConfig)`** → `PfAnchorageResult` com `FbdMpa`, `Eta1`, `Eta2`, `Eta3`, `BasicAnchorageCm`, `RequiredAnchorageCm`, `MinimumAnchorageCm`, `SpliceLengthCm`, `MinimumSpliceLengthCm`, `AnchorageAlpha`, `SpliceAlpha`, `FctkInfMpa`, `FydMpa`. Inputs: fck/fyk (clamps para 12 MPa / 250 MPa), `PfBarSurfaceType` (Lisa=1.0, Entalhada=1.4, Nervurada=2.25), `PfBondZone` (Boa=1.0, Ruim=0.7), `PfAnchorageType` (Reta=α1.0, Gancho*=α0.7), `SplicePercentage` (tabela de α variando com 20%/25%/33%/50%/>50%). `ToDetailText()` gera string padronizada `"EMT NBR 6118:2023 | phi {d} mm | lb {lb} cm | lb,nec {lbNec} cm | traspasse l0 {l0} cm | fbd {fbd} MPa"` para parâmetro Comments do Revit.
- **`Models/PF/PfLapSpliceConfig`** — configuração de traspasse: `Enabled`, `MaxBarLengthCm` (default 1200), `ConcreteFckMpa` (25), `SteelFykMpa` (500), `BarSurface`, `BondZone`, `AnchorageType`, `SplicePercentage` (50), `BarSpacingCm` (8), `AsCalcCm2`, `AsEfCm2`.
- Integrado em `PfColumnBarsConfig.Traspasse` e `PfBeamBarsConfig.Traspasse` (quando `Enabled=true` e barra > `MaxBarLengthCm`, serviço insere traspasse).

### Added — Bloco de duas estacas (rotina dedicada)
- **`Commands/PF/CmdPfInserirAcosBlocoDuasEstacas`** — novo botão em "PF Armaduras" na aba "Ferramenta EMT". Usa `FerramentaCommandBase` (license gate + logging uniformes).
- **`Services/PF/PfTwoPileCapRebarService.Execute(uidoc, config)` → `Result`** (496 linhas) — análogo ao `PfRebarService.ExecuteBeamBars` em estrutura: coleta hosts, calcula `HostFrame`, lança superior/inferior/lateral.
- **`Services/PF/PfTwoPileCapBarCatalog`** — catálogo estático `Tipo4` com 14 posições (diâmetros 6.3/8.0/10.0/12.5/16.0 mm, formas `Reta`, `U`, `RetanguloFechado`, `EstriboVertical`, `CaliceVertical`, `FormaEspecial`). `QuantidadePorBloco = QuantidadeTotalPdf / 3` (3 blocos por planta).
- **`Models/PF/PfTwoPileCapBarPosition`** — DTO com `Posicao`, `DiametroMm`, `QuantidadeTotalPdf`, `QuantidadePorBloco`, `ComprimentoCm`, `EspacamentoCm`, `Forma` (enum `PfTwoPileCapBarShape`), `DescricaoForma`. `ToComment()` gera string padronizada `"N{pos} - POS {pos} - diam. {d} - C/{spacing} - C={length} - {descricao}"` (culture-invariant).
- **`Views/PfTwoPileCapRebarWindow.xaml(.cs)`** — UI de configuração.

### Added — Modo coordenadas manual (barras de pilar/viga)
- **`PfRebarPlacementMode`** — enum `Automatico` (default) e `Coordenadas`.
- Quando `ModoLancamento == Coordenadas`, o serviço usa `Coordenadas` (`List<PfColumnBarCoordinate>` ou `List<PfBeamBarCoordinate>`) em cm local. Para viga, `PfBeamBarCoordinate` inclui `BarTypeName` e `Posicao` (ex.: `"Superior"`, `"Inferior"`, `"Lateral"`).
- `PfColumnBarsConfig.QuantidadeCircular` — suporta seção circular com N barras igualmente espaçadas.

### Added — Preview de seção nas janelas PF
- **`PfRebarService.BuildBeamSectionPreview(FamilyInstance)` e `BuildColumnSectionPreview(FamilyInstance)`** (static helpers) — retornam `PfRebarSectionPreview` (shape retangular/circular, dimensões em cm, raio se circular) para renderizar a seção na UI.
- **`PfRebarService.GetColumnLengthCm(column)` e `GetBeamLengthCm(beam)`** (static helpers) — leitura da dimensão longitudinal.
- `PfBeamBarsWindow` e `PfColumnBarsWindow` renderizam o preview com indicação das posições das barras.

### Changed — Ribbon dividida em duas abas
- **Aba "Ferramenta EMT"** — só fluxo PF (painéis `PF Construção`, `PF Documentação`, `PF Armaduras`).
- **Aba "Ferramentas ECC"** (nova) — fluxo geral (painéis `Modelagem`, `Estrutura`, `Vigas`, `Vista`, `Documentação`, `Fabricação`, `CNC`, `Verificação`, `Montagem`, `Licença`).
- Motivação: o usuário PF (concreto pré-fabricado) e o usuário metálico têm fluxos muito distintos; separar reduz fricção visual.

### Changed — Commands PF (AcosPilar/Viga, EstribosPilar/Viga)
- Todos os 4 comandos agora fazem pick de elemento via `PfElementService.GetSelectionOrPick` **antes** de abrir a janela, e passam `hosts[0]` ao construtor novo das windows. Permite que a janela construa o preview da seção na abertura.
- Mantêm `FerramentaCommandBase` (license gate + logging).

### Changed — `PfRebarConfigs.cs` (API expansion, backward-compatible)
- Adicionadas 6 enums: `PfRebarPlacementMode`, `PfRebarSectionShape`, `PfStirrupHookAngle`, `PfBarSurfaceType`, `PfBondZone`, `PfAnchorageType` (+ `PfBeamBarEndMode` já existia).
- Adicionadas 5 sealed classes: `PfColumnBarCoordinate`, `PfBeamBarCoordinate`, `PfRebarSectionPreview`, `PfLapSpliceConfig`, `PfTwoPileCapRebarConfig`.
- Estribos: adicionados `ShapeName`, `DiametroMm`, `EspacamentoCm` unificado e `Dobra` (`PfStirrupHookAngle`). **Campos granulares de zoneamento preservados** (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidade`, `EspacamentoApoio/Central`, `ComprimentoZonaApoio`) atrás da flag `UsarEspacamentoUnico=false` (default) — ver "Regressão conhecida" abaixo.
- Barras: adicionados `ModoLancamento`, `QuantidadeCircular`, `Traspasse`, `Coordenadas`.

### Preserved — Decisões contra a snapshot Victor
- **`ModelCheckService`** mantém `Result<ModelCheckReport>` (ADR-003) e `IProgress<ProgressReport>` + `CancellationToken` (ADR-004). **Não adotamos** a versão simplificada do Victor (10.4 KB, sem Result/Progress/Cancel).
- **`ModelCheckCollector`, `ModelCheckVisualizationService`** e as 9 `ModelCheckRules/*Rule.cs` mantidas nas versões Alef.
- **`ListaMateriaisExportService`, `AgrupamentoVisualService`, `NumeracaoItensService`, `DstvExportService`** mantidos (todos com terceira/quarta/quinta adoção ADR-003).
- **`CrashReporter.Initialize()`** no `App.OnStartup` — preservado.
- **`LicenseSecretProvider.GetResolvedSource()`** logging no `App.OnStartup` — preservado.
- **`CmdCortarElementos`** (nossa Onda 3 PR-1) — preservado na aba "Ferramentas ECC", painel Estrutura. Victor havia removido dessa snapshot.
- **UTF-8 com acentos** em `App.cs` e em toda a UI — preservado. Victor havia regredido algumas strings a ASCII.

### Regression — Conhecida, com follow-up planejado
- `PfRebarService` consome `EspacamentoCm` único em estribos (modo Victor). Os campos granulares de zoneamento NBR 6118 (`EspacamentoInferior/Central/Superior`, `AlturaZonaExtremidadeCm`, `EspacamentoApoio/Central`, `ComprimentoZonaApoioCm`) estão preservados em `PfRebarConfigs.cs` mas o serviço não os lê ainda. A flag `UsarEspacamentoUnico` (default `false`) já existe como ponto de ramificação. Follow-up: PR separado que restaura a lógica de zoneamento no `PfRebarService` quando a flag é `false`. Backup da versão v1.5.0 salvo em `Services/PF/PfRebarService.cs.bak-alef-v1.5` (945 linhas) para referência.

### Tests
- **+4 arquivos de teste** cobrindo as novas features:
  - `Models/PF/PfRebarShapeOptionTests` — 5 Facts
  - `Models/PF/PfTwoPileCapBarPositionTests` — 5 Facts + 1 Theory (6 variants) = 11 casos. Inclui teste de culture-invariant em pt-BR (regressão histórica do projeto).
  - `Services/PF/PfNbr6118AnchorageServiceTests` — 15 Facts (cenários: zero/null guard, eta1/2/3 corretos para cada combinação, α de ancoragem com gancho, fck<12 clamp, lb mínimo, traspasse α≥1.0, `AreaRatio` reduz `lbNec`, idempotência).
  - `Services/PF/PfTwoPileCapBarCatalogTests` — 10 Facts (14 posições, sequencialidade, `Get()` por posição existente/inexistente, `QuantidadePorBloco = Total/3`, descrições não vazias, diâmetros/comprimentos válidos, snapshot das posições chave 1/4/13).
- 4 LinkedSources novos no `FerramentaEMT.Tests.csproj`.

Origem: snapshot do Victor em 2026-04-24. Plano de merge em 8 ondas documentado em `outputs/ANALISE-VICTOR-WAVE2.md`.

---

## [1.5.0] — 2026-04-20 (Incorporação Victor + Verificação de Carimbo + Hardening pré-release)

Release de qualidade focada em **3 eixos**: (1) incorporação seletiva do trabalho do Victor (Cortar Elementos, módulo PF já integrado em v1.2.0), (2) feature completa de verificação de carimbo (TitleBlock) no ModelCheck com navegação 3D, (3) hardening de segurança e qualidade (HMAC externalizado, DPI overflow, empty catches eliminados). Adoção completa do ADR-003 (Result<T>) e ADR-004 (progresso + cancelamento) nos serviços principais. Suite de testes cresce para **419 casos**.

### Added — Cortar Elementos (Onda 3, PR-1: incorporação seletiva do trabalho do Victor)
- **`Commands/CmdCortarElementos`** — novo botão no painel "Estrutura" ("Cortar Elementos"). Seleciona pisos, quadros estruturais e colunas/pilares (pré-seleção ou `PickObjects` com filtro), detecta interferências entre hosts e cortadores e aplica corte automático escolhendo entre `JoinGeometryUtils` e `SolidSolidCutUtils` conforme o par aceita. Comando gerencia a transação externa (commit só quando há alteração), restaura seleção com os elementos envolvidos e mostra resumo + diagnóstico ao final via helpers de `FerramentaCommandBase` (`ShowSuccess`/`ShowInfo`/`ShowWarning`).
- **`Services/CortarElementosService`** — serviço "mudo" (ADR-003) retornando `Result<CortarElementosResultado>`. Zero `AppDialogService`, só `Logger`. Duas estratégias em sequência (JoinGeometry com `SwitchJoinOrder` quando necessário, fallback para SolidSolidCut), cada tentativa em `SubTransaction` para reverter rejeição da API Revit sem derrubar a transação pai. Validadores de escopo (`EhElementoValidoParaEscopo`, `EhHostValido`, `EhCortadorValido`) expostos como `internal static` para o comando reusar no filtro de seleção.
- **`Models/CortarElementosResultado`** — payload consolidado (total selecionados, hosts/cortadores analisados, pares com interferência, alterações aplicadas, já conformes, falhas, IDs relacionados, linhas de diagnóstico) + computadas `HouveAlteracao` e `HouveSucesso`. Extraído para `Models/` (era `internal` aninhado no serviço, na versão original do Victor) para permitir teste unitário fora do assembly. Listas null-safe via fallback no construtor.
- **`FerramentaEMT.Tests/Models/CortarElementosResultadoTests`** — 6 testes cobrindo preservação de argumentos, fallback de listas nulas, e as regras de `HouveAlteracao`/`HouveSucesso` (incluindo o caso sutil "só já conformes → sucesso", que é o que o comando usa para decidir entre Info e Warning).

Origem: snapshot da versão do Victor em 2026-04-14. Adaptações para a base do Alef: ADR-003 (zero UI no serviço, caller monta UX), `FerramentaCommandBase` no comando (license gate + logging uniforme), `Logger` da `Infrastructure` em vez de `Debug.WriteLine`. ADR-004 (progress/cancel) deliberadamente fora de escopo — operação tipicamente rápida (<100 pares) e a `SubTransaction` já é o ponto de abortar se der ruim. Ver `pending-push/PLANO-INCORPORACAO-VICTOR.md`.

### Added — UX de cancelamento (ADR-004)
- **`Views/ProgressWindow.xaml(.cs)`** — dialogo reutilizavel de progresso com barra, percentual, contador N/Total, mensagem detalhada e botao Cancelar. Tematico com `AppTheme.Base.xaml`. Fechar pelo X equivale a Cancelar.
- **`Utils/RevitProgressHost`** — host estatico `Run<T>(title, headline, work)` que abre a janela, corre o servico no thread principal do Revit (requisito de API single-threaded) e bombeia o `Dispatcher` entre eventos de `IProgress` para a UI atualizar e o Cancelar chegar ao `CancellationTokenSource`. Exception `OperationCanceledException` propaga ate o comando, que retorna `Result.Cancelled`.
- **`docs/ADR/004-threading-model-progress-cancel.md`** — documenta o modelo de threading, por que `Task.Run` e proibido com Revit API, quando usar o host e quando nao usar.
- **`CmdVerificarModelo`** passa a usar `RevitProgressHost` — primeiro consumidor real. Usuario ve progresso por regra e pode cancelar sem esperar 30s de `DuplicateMarkRule` em modelos grandes.
- **`CmdExportarDstv`** passa a usar `RevitProgressHost` — segundo consumidor. Agora o usuario ve quantas pecas ja foram processadas/gravadas e pode cancelar no meio, util em exports de modelos grandes (>500 pecas) onde a maquina CNC esta ocupada e o usuario quer abortar.

### Changed — DSTV export em duas fases (ADR-003 + ADR-004)
- **`DstvExportService` refatorado em duas fases** para conciliar `PickObjects` (modal Revit nativo) com `RevitProgressHost` sem UX conflitante (janela de progresso ficaria vazia por tras da selecao). Nova API:
  - `ColetarElementos(uidoc, config) → Result<ColetaResult>` — pode abrir `PickObjects`, NAO aceita progress/CT (interacao curta). `ColetaResult { List<FamilyInstance> Elementos; bool Cancelado }` distingue ESC de selecao vazia.
  - `Executar(uidoc, IReadOnlyList<FamilyInstance> elementos, config, progress, ct) → Result<ResultadoExport>` — processa e grava, aceita progress+CT. Compativel com `RevitProgressHost`.
- **`DstvExportService.BuildResumoText(ResultadoExport) → string`** e **`AbrirPastaNoExplorer(string)`** expostos como static — o comando monta o dialogo e decide quando abrir o Explorer. Removido `AppDialogService` do servico (principio ADR-003: service "mudo", so retorna e loga).
- **`ResultadoExport.Cancelado` removido** — cancelamento so acontece na fase de coleta; a flag migrou para `ColetaResult`. Drop-safe porque o unico caller (`CmdExportarDstv`) foi atualizado simultaneamente.
- **`CmdExportarDstv`** ajustado ao novo fluxo: coleta → `RevitProgressHost.Run(service.Executar)` → montagem do resumo → warning/info → abertura opcional do Explorer. `try/catch (OperationCanceledException) → Result.Cancelled` cobre o Cancel da nova janela.

### Changed — Segunda adoção do ADR-003
- **`ModelCheckService.Executar`** agora retorna `Result<ModelCheckReport>` e aceita `IProgress<ProgressReport>` + `CancellationToken` opcionais. Falhas de domínio (`uidoc` nulo, config ausente, nenhuma regra habilitada) voltam como `Result.Fail` com mensagem amigável — o comando chamador apresenta o diálogo. Progresso é reportado por regra executada (`N/Total — nome da regra: X problema(s)`), throttle de 100 ms. `OperationCanceledException` propaga até o comando, que retorna `Result.Cancelled`. Segue-se o template do ADR-003 validado antes no `DstvExportService`.
- **`ModelCheckReport` ganha `ExportedToPath` e `ExportError`** — exportação Excel falhar **não invalida** a análise (princípio de falha parcial, ADR-003). Comando chamador inspeciona as duas propriedades e decide como apresentar: warning quando Excel falhou, info quando concluiu, nada quando export não foi solicitado. Remove dois `AppDialogService.ShowInfo/ShowError` do serviço.
- **`CmdVerificarModelo`** atualizado para consumir `Result<ModelCheckReport>` e dois sinais de Excel independentes. `try/catch (OperationCanceledException) → Result.Cancelled` pronto para quando a UI ganhar botão Cancelar.

### Changed — Terceira adoção do ADR-003 + ADR-004 (Lista de Materiais)
- **`ListaMateriaisExportService.Exportar` refatorado** — nova assinatura `Result<ResultadoExport> Exportar(uidoc, config, IProgress<ProgressReport>?, CancellationToken)`. Removidas as 7 chamadas a `AppDialogService` do serviço (service "mudo" por ADR-003). Falhas de domínio (UIDocument nulo, config inválida, categoria/aba vazia, caminho vazio, nenhum elemento elegível) voltam como `Result.Fail` com mensagem amigável; o comando decide como apresentar. Falhas de IO/Revit durante coleta ou gravação capturadas via `try/catch → Result.Fail` com log em `Logger.Error`. `OperationCanceledException` propaga ao callsite.
- **Progresso reportado durante `ColetarLinhas`** — elemento a elemento (throttle 100 ms), com mensagem `"Processando N/Total — Categoria"`. Em modelos grandes (milhares de elementos), usuário vê avanço real em vez de UI travada. `ThrowIfCancellationRequested()` no topo do loop permite cancelamento responsivo.
- **Três fases explícitas** — coleta (interrompível), agrupamento (CPU-only rápido, não interrompível) e gravação Excel via ClosedXML (IO atômica, não interrompível — abortar no meio corromperia o `.xlsx`). `ResultadoExport` carrega contagens separadas (linhas, grupos, elementos estruturais, perfis, conexões) + duração + caminho do arquivo. `BuildResumoText(ResultadoExport)` estático monta o texto do diálogo de sucesso no comando.
- **`CmdExportarListaMateriais`** consome a nova API via `RevitProgressHost.Run` (ADR-004), ganhando barra de progresso + botão Cancelar sem mudar UX de sucesso. `try/catch (OperationCanceledException) → Result.Cancelled`. Mantém o catch existente para `FileNotFoundException/FileLoadException` de ClosedXML ausente (dependência de deploy).

### Changed — Quarta adoção do ADR-003 + ADR-004 (Agrupamento Visual)
- **`AgrupamentoVisualService` migrado** — 3 métodos públicos agora retornam `Result<ResultadoAgrupamento>` ou `Result<ResultadoLimpeza>` e aceitam `IProgress<ProgressReport>?` + `CancellationToken` opcionais. Removidas as 4 chamadas a `AppDialogService` (UIDocument nulo, "nada para agrupar", resumo de sucesso de Agrupar e de Limpar) — serviço agora é 100% "mudo" por ADR-003, só `Logger`. `ResultadoAgrupamento` e `ResultadoLimpeza` expõem contadores (elementos na vista, conjuntos identificados, conjuntos coloridos, grupos EMT criados/desfeitos) + `TimeSpan Duracao` + `List<string> Falhas` para o comando decidir a UX.
- **Duas fases explícitas por ADR-004** — (1) coleta + geração de assinaturas de equivalência é interrompível (`ThrowIfCancellationRequested` a cada 32 elementos, `Report` a cada 64); (2) transação Revit que aplica overrides e cria/desfaz grupos é não-interrompível (cancelar no meio deixaria overrides parciais na vista). Progresso durante a fase 2 usa o índice do conjunto (N conjuntos / Total). Em modelos com milhares de vigas, CriarAssinaturaEquivalencia não é trivial — o progresso granular evita a sensação de UI travada.
- **`BuildResumoText(ResultadoAgrupamento)`** e **`BuildResumoText(ResultadoLimpeza)`** estáticos montam o texto de sucesso (incluindo as até 6 primeiras falhas com elipse `… e mais N`) — comando consome via `AppDialogService.ShowInfo`. Os 3 comandos (`CmdAgruparPilaresPorTipo`, `CmdAgruparVigasPorTipo`, `CmdLimparAgrupamentosVisuais`) foram atualizados; fluxo de sucesso e UX de erro idênticos ao que existia antes, mas a lógica de apresentação agora mora onde deve (comando, não serviço).

### Added — Verificação de Carimbo no ModelCheck (Miniciclos 1–6)
- **`Services/ModelCheck/ModelCheckCollector`** (M1) — coleta centralizada de elementos estruturais para todas as regras do ModelCheck, eliminando coletas duplicadas e garantindo consistência entre regras.
- **`Models/ModelCheck/TitleBlockCheckConfig`** (M2) — modelos para configuração de verificação de carimbo: campos obrigatórios (nome do projeto, número da folha, data, responsável), tolerâncias e regras de validação.
- **`Services/ModelCheck/ModelCheckVisualizationService`** (M3) — serviço de navegação 3D que permite ao usuário clicar em um problema no relatório e navegar diretamente ao elemento no modelo Revit (zoom, isolamento temporário, highlight).
- **`Services/ModelCheck/ModelCheckRules/TitleBlockRule`** (M4) — nova regra de verificação que valida campos obrigatórios do carimbo (TitleBlock) em todas as folhas do projeto. Detecta campos vazios, valores placeholder e inconsistências entre folhas.
- **`Views/VerificarModeloConfigWindow`** atualizada (M5) — seção de configuração de verificação de carimbo na UI, com checkboxes por campo e lista de campos customizados.
- **`Views/VerificarModeloReportWindow`** atualizada (M6) — integração do `ModelCheckVisualizationService` na janela de relatório. Duplo-clique em qualquer issue navega ao elemento no Revit. Botões "Isolar" e "Selecionar" usam o novo serviço.

### Security — HMAC Secret Externalizado (Miniciclo 9)
- **`LicenseSecretProvider` hardening crítico** — removido o fallback hardcoded `DevOnlyFallback` que permitia a qualquer pessoa com decompiler forjar licenças válidas. Cadeia de resolução agora: env var `EMT_LICENSE_SECRET` → arquivo `%LOCALAPPDATA%\FerramentaEMT\license.secret` → arquivo ao lado do assembly → **`InvalidOperationException`** (nunca mais hardcoded). `App.cs` e `EmtKeyGen` atualizados para o novo contrato. 4 testes em `KeySignerTests` ganham setup de env var com try/finally.

### Fixed — UI e Qualidade (Miniciclos 8, 10, 11)
- **Hotfixes de UI em 3 janelas** (M8): `ConexaoConfigWindow` (layout quebrado em DPI alto), `PlanoMontagemWindow` (scroll ausente), `MarcarPecasWindow` (botões cortados — padrão DPI corrigido: MaxHeight 900, ResizeMode CanResizeWithGrip, ScrollViewer defensivo, botões fora do scroll).
- **DPI overflow em 4 janelas** (M10): `CotarPecaFabricacaoWindow`, `GerarVistaPecaWindow`, `ExportarDstvWindow`, `PfBeamBarsWindow` — mesmo padrão M8 aplicado (MaxHeight 720/520→900, NoResize→CanResizeWithGrip, ScrollViewer com VerticalScrollBarVisibility Auto, botões em Grid.Row 2 fora do ScrollViewer). Resolve finding F1A-DPI-01 da auditoria.
- **10 empty catches eliminados em 6 services** (M11): `CortarElementosService` (5), `MarcarPecasService` (1), `AjustarEncontroService` (1), `AgrupamentoVisualService` (1), `ConexaoGeneratorService` (1), `TrelicaService` (1). Classificação A (9 casos): Logger.Debug com fallback seguro. Classificação B (1 caso AgrupamentoVisual): Logger.Warn. TrelicaService ganha catch tipado `Autodesk.Revit.Exceptions.OperationCanceledException`. Resolve findings F1C-CATCH-01 (HIGH) e F2-CATCH-01 (MEDIUM) da auditoria.
- **Ambiguidade CS0104** entre `Core.Result<T>` e `Revit.UI.Result` resolvida em commands afetados.

### Changed — Auditoria residual do ADR-003 (NumeracaoItensService)
- **`NumeracaoItensService.IniciarSessao` agora retorna `Result<InicioResultado>`** — removidas 4 das 5 chamadas residuais a `AppDialogService` (UIDocument nulo, config nula, sessão já ativa, nenhum elemento elegível). Novo `InicioResultado` expõe `SessaoIniciada`, `JaHaviaSessaoAtiva`, `TotalCandidatos` e `TotalElegiveis` — `CmdNumerarItens` consome esses flags e decide a UX (ShowError, ShowWarning por caso). O ShowInfo do fim de sessão (linha do lifecycle de `NumeracaoItensSessao.FinalizarSessao`) **foi mantido deliberadamente**: ele pertence ao ciclo de vida da janela persistente `NumeracaoItensControleWindow`, não ao kickoff — refatorá-lo exigiria redesenhar o modelo de sessão interativa, fora do escopo. Logger ganhou 4 entradas nos caminhos de no-op/falha pra dar rastro em suporte.

### Quality gates
- `dotnet build FerramentaEMT.Solution.sln -c Release` → **0 erros**, 2 avisos MSB3277 pré-existentes.
- `dotnet test` → **419/419 aprovados** (era 347 na v1.3.0).
- `TreatWarningsAsErrors` mantido em Release.
- Grep `catch.*{.*}` em `Services/` → **zero empty catches**.
- Grep `DevOnlyFallback` no código rastreado → **zero matches** (apenas docs históricos).
- Auditoria: findings F1A-DPI-01 (HIGH), F1C-CATCH-01 (HIGH), F2-CATCH-01 (MEDIUM) resolvidos.

### Notes
- Miniciclo 7 foi pulado (renumeração durante planejamento).
- `INSTALAR.bat` criado para deploy manual (não rastreado no git — cópia local para Victor).
- Planos detalhados de cada miniciclo em `comparacao-victor/PLANO-MINICICLO-{N}.md`.

---

## [1.3.0] — 2026-04-18 (Fundação arquitetural + Primeira adoção ADR-003)

Primeira release focada em **qualidade interna**: fundação arquitetural (Result<T>, IRevitContext, ProgressReporter com CancellationToken), hardening a partir de auditoria independente, primeiro serviço (DSTV Export) migrado para o novo pattern como template, além de documentação operacional (RUNBOOK) e ADR-003 formalizando a política de adoção incremental.

### Security
- **Segredo HMAC de licenciamento externalizado.** `LicenseSecretProvider` resolve em cascata: `EMT_LICENSE_SECRET` (env var) → `%LOCALAPPDATA%\FerramentaEMT\license.secret` → arquivo ao lado do assembly → fallback DEV_ONLY hardcoded. Fallback mantém compatibilidade 100% com licenças já emitidas. `App.OnStartup` logga a fonte resolvida e emite warning quando cai no DEV_ONLY. `EmtKeyGen` alerta em amarelo no console.
- **`LicenseSecretProvider` cache agora é atômico** via `Lazy<ResolvedSecret>` com `ExecutionAndPublication` — elimina janela em que leitor concorrente via `secret` resolvido mas `source` ainda `NotResolved`. Teste de concorrência com 128 threads valida a invariante.
- **`LicenseSecretProvider.HasMalformedSecretFile`** detecta arquivo de segredo vazio/whitespace-only para distinguir "não configurado" de "mal configurado" em logs de startup.

### Added
- **`FerramentaEMT/Core/Result<T>`** — struct imutável para fluxos previsíveis de domínio (input inválido, regra de negócio, seleção vazia), deixando exceções para bugs e falhas de infra. Cobertura: 11 testes (inclui regressão do `default` struct). Documentado em `docs/ADR/001-result-pattern.md`.
- **`FerramentaEMT/Core/IRevitContext`** — wrapper skeleton v1 sobre `UIDocument`/`Document` para desacoplar serviços da construção de `ExternalCommandData`. Abre caminho para abstrações de nível mais alto (`IElementQuery`, `ITransactionScope`) conforme necessidade. Documentado em `docs/ADR/002-irevit-context.md`.
- **`FerramentaEMT/Core/ProgressReporter`** agora aceita `CancellationToken` opcional, expõe `IsCancellationRequested` e `ThrowIfCancellationRequested()` para loops longos cancelarem graciosamente.
- **`FerramentaEMT/Infrastructure/CrashReporter`** — captura `AppDomain.UnhandledException` e `TaskScheduler.UnobservedTaskException`, dump em `%LOCALAPPDATA%\FerramentaEMT\crashes\`.
- **`docs/ADR/`** — diretório de Architecture Decision Records inaugurado com 2 ADRs.
- **`docs/PLANO-100-100.md`** — roadmap em 7 fases para levar o plugin de 7/10 interno para 10/10 comercial (26 semanas, ~$485-1085/ano de custo externo).
- **`.editorconfig`** — regras de formatação/estilo C# consumíveis por Visual Studio, Rider, VS Code e `dotnet format`.
- **`.github/PULL_REQUEST_TEMPLATE.md`** e **issue templates** (bug, feature, docs) — polish de processo.
- **`CONTRIBUTING.md`** — workflow de PR, convenção de commits, regras de commits e testes.

### Changed — Primeira adocao do ADR-003
- **`DstvExportService.Executar`** agora retorna `Result<ResultadoExport>` e aceita `IProgress<ProgressReport>` + `CancellationToken` opcionais. Falhas de dominio (pasta nao informada, selecao vazia, filtro vazio, pasta com erro de IO) voltam como `Result.Fail` com mensagem amigavel — o comando chamador decide se exibe dialog. Progresso e reportado por peca processada e por arquivo gravado, throttlado em 100 ms. Loops respeitam `ThrowIfCancellationRequested()`. Breaking change: callers do metodo publico (apenas `CmdExportarDstv` hoje) foram atualizados.

### Fixed (audit 2026-04)
- **`Core/Result<T>` default-struct trap.** Antes, `default(Result<T>)` produzia `IsSuccess=false + Error=null`, causando NRE em qualquer `if (r.IsFailure) log(r.Error)`. Agora o flag interno é `_isFailure` (nasce `false`), então `default` é tratado como `Ok(default(T))`. Regressão coberta por teste.
- **`Infrastructure/CrashReporter` dupla subscrição.** Se `Logger.Info` falhasse no primeiro `Initialize()`, `_initialized` continuava `false` e o próximo `Initialize()` registraria os handlers **de novo**, produzindo dois dumps por crash. Agora `_initialized=true` é definido **antes** da subscrição, e o logger final fica em try/catch isolado.

### Changed
- `.gitignore` adicionado para `license.secret`, `*.license.secret` e `sentry.dsn` — prevenir commit acidental.

### Fixed
- Parse de `double` em 13 janelas WPF (WPF inteiro + PF + PipeRack) padronizado via `NumberParsing.TryParseDouble`, que tenta `InvariantCulture` e cai em `pt-BR` — elimina bug de usuário pt-BR digitando `"3,5"` e recebendo `35` em locales mistos.
- `ModelCheck` rules: log agregado em vez de catch-block vazio para elementos pulados por erro de leitura — agora gera `Logger.Warn("[{Rule}] {Count} elemento(s) pulado(s)...")` em todas as 9 regras.

---

## [1.2.0] — 2026-04-17 (Módulo PF — Pré-Fabricado de Concreto)

Integração do fork do Victor (concreto pré-fabricado) sobre o tronco oficial Alef. A versão Alef ganha 10 novos comandos e 3 painéis de ribbon cobrindo documentação de pilares/vigas PF, inserção de armaduras (estribos, barras longitudinais, armadura de consolo) e organização de modelos com elementos PF, sem depender do Dynamo.

### Added — Módulo PF
- **`Commands/PF/`** (10 comandos): `CmdPfNomearElementos`, `CmdPfIsolarPilaresConsolos`, `CmdPfIsolarLajes`, `CmdPfElevacaoFormaPilares`, `CmdPfElevacaoFormaVigas`, `CmdPfInserirEstribosPilar`, `CmdPfInserirAcosPilar`, `CmdPfInserirEstribosViga`, `CmdPfInserirAcosViga`, `CmdPfInserirAcosConsolo`. Todos herdam `FerramentaCommandBase` (licenciamento + logging + tratamento de erro centralizados).
- **`Services/PF/`** (6 arquivos): `PfElementService` (predicados e coleta de elementos PF, ordenação por eixo), `PfIsolationService` (isolar temporário na vista ativa), `PfNamingCatalog` + `PfNamingService` (nomeação padronizada de pilares/vigas/lajes), `PfRebarService` (946 linhas — pipeline completo de inserção de armaduras), `PfRebarTypeCatalog` (lookup de `RebarBarType`).
- **`Services/PF/PfNamingFormatter`** (novo, puro, culture-invariant) — helper extraído de `PfNamingConfig.MontarValor` para viabilizar testes unitários. Garante que `"P" + 1000` nunca vire `"P1.000"` em máquinas pt-BR/de-DE.
- **`Models/PF/`** (2 arquivos, 9 configs): `PfNamingConfig` (+ `PfNamingTarget`), `PfRebarConfigs` agrupando `PfColumnStirrupsConfig`, `PfColumnBarsConfig`, `PfBeamStirrupsConfig`, `PfBeamBarsConfig`, `PfConsoloRebarConfig` e o enum `PfBeamBarEndMode`.
- **`Views/Pf*Window.xaml(.cs)`** — 7 janelas WPF com tema Revit: `PfBeamBarsWindow`, `PfBeamStirrupsWindow`, `PfColumnBarsWindow`, `PfColumnStirrupsWindow`, `PfConsoloRebarWindow`, `PfNamingWindow` + code-behind.
- **Ribbon — 3 painéis novos** em `App.cs`: `PF Construção` (Nomear, Isolar P+Cons., Isolar Lajes), `PF Documentação` (Elevação Pilar, Elevação Vigas), `PF Armaduras` (Estribos Pilar, Aços Pilar, Estribos Viga, Aços Viga, Aços Consolo).

### Added — Refinamentos do núcleo metálico (vindos do fork Victor)
- **`Models/GerarVistaPecaConfig`** — novo enum `VistaPecaCategoriaFiltro { Todos, Pilares, Vigas }` + propriedade `FiltroCategoria`, usado para reutilizar o serviço de geração de vistas tanto em metálica quanto em PF.
- **`Services/AutoVistaService`** — suporta `FiltroCategoria` para coletar apenas `OST_StructuralColumns` ou `OST_StructuralFraming` dentro do escopo selecionado.
- **`Services/Trelica/TagearTrelicaService`** — implementação completa dos rótulos de banzo via `TextNote` (`BANZO SUPERIOR: <perfis>` e `BANZO INFERIOR: <perfis>`), substituindo o `TODO` que existia no v1.1.0.
- **`Utils/AppSettings`** — 9 propriedades `LastPfNaming*` persistem a última configuração da janela de nomeação PF entre sessões.

### Added — Cobertura de testes
- Suite unitária cresce de **223 → 279 testes** (todos verdes em 52 ms):
  - `PfNamingFormatterTests` (9 testes: casos básicos, nulos, culture pt-BR/de-DE/fr-FR/en-US, números grandes)
  - `PfRebarConfigsTests` (8 testes: defaults de 5 configs + enum + mutabilidade)
  - `FerramentaEMT.Tests.csproj` agora linka também os helpers puros de `Services/Trelica/` (Classificador, Geometria, PerfilFormatter, Topologia, CotaFaixaBuilder, CotarTrelicaReport) + novos helpers PF, fechando um gap silencioso em que tests de Trelica existiam mas não compilavam.

### Fixed
- **`Tests/Services/Trelica/CotarTrelicaReportTests.cs`** — adicionado `using FluentAssertions;` que faltava (impedia compilação ao linkar os sources Trelica).

### Added — Auditoria AIOX (score 7.25/10 → melhorado)
- **32 catch blocks silenciosos substituídos por `Logger.Warn`** com contexto do elemento/operação — paradigma "falhas devem deixar rastro".
- **`MaxHeight` adicionado em 21 janelas WPF** que faltavam (garante cabimento em telas 1366×768 junto com `MinHeight`/`MinWidth` já existentes).
- **`CultureInfo.InvariantCulture`** aplicado em `Services/CncExport/DstvExportService` (output de relatório) e `Services/ModelCheck/ModelCheckRules/ZeroLengthRule` (formatação de descrição de issue) — garante que marcadores e relatórios saiam idênticos em pt-BR, en-US, de-DE.
- **`Commands/CmdCortarPerfilPorInterferencia`** migrado para `FerramentaCommandBase` — licenciamento + logging + tratamento de erro centralizados, padronizando com os outros 36 commands.
- **9 classes de testes de Config criadas** (`*ConfigTests.cs` em `Models/`): `ExportarDstvConfig`, `ConexaoConfig`, `CotarTrelicaConfig`, `ExportarListaMateriaisConfig`, `GerarVistaPecaConfig`, `IdentificarPerfilConfig`, `MarcarPecasConfig`, `ModelCheckConfig`, `PlanoMontagemConfig`, `TagearTrelicaConfig` — **68 novos testes** cobrindo defaults, mutabilidade e roundtrips.

### Quality gates
- `dotnet build FerramentaEMT.Solution.sln -c Release` → plugin principal: **0 erros, 2 avisos MSB3277** (cruzamento de referências Revit API, não-impeditivos).
- `dotnet test` → **347/347 aprovados** (era 279 antes da auditoria AIOX).
- `TreatWarningsAsErrors` mantido em Release.

### Notes
- Pasta do fork original (`FerramentaEMT-Victor/`) preservada em `backup-victor-pre-merge.zip` (301 MB) e removida após validação.
- Três test files de comportamento dependente de Revit runtime (`TagearTrelicaReportTests`, `TrelicaRevitHelperTests`, `IdentificarPerfilReportTests`) estão explicitamente excluídos do build pois testam membros de services Revit-bound; seu comportamento é validado por smoke test manual no Revit.
- HMAC secret em `Licensing/KeySigner.cs` mantido hardcoded por decisão explícita do mantenedor. O comentário no arquivo alerta: "TROCAR ANTES DA PRIMEIRA VENDA". Antes de distribuir a clientes externos, o repositório deve estar privado ou o secret deve ser movido para env var / DPAPI / arquivo externo.

---

## [1.1.0] — 2026-04-15 (Cotar Treliça + Identificação de Perfis)

### Added — Módulo Cotar Treliça (5 faixas EMT)
- **`Commands/CmdCotarTrelica`** — Comando principal: usuario seleciona barras da trelica em vista Elevation/Section, abre janela de configuracao, executa cotagem automatica em 5 faixas (paineis banzo superior, paineis banzo inferior, vao total, vaos parciais entre apoios, altura de cada montante) + identificacao de perfis. Segue padrao EMT extraido de 4 projetos de referencia.
- **`Services/Trelica/CotarTrelicaService`** (638 linhas) — Orquestrador com pipeline de 10 etapas: classificacao de barras → separacao banzos → extracao de topologia → calculo geometrico → construcao de faixas → criacao de cotas/tags/textos Revit. Contem 5 TODOs explicitos para integracao final de References Revit (Wave A.1.5).
- **5 helpers puros** (testaveis sem Revit API):
  - `TrelicaClassificador` — classifica barras por inclinacao (Banzo/Montante/Diagonal) e altura (Superior/Inferior/BanzoIndefinido)
  - `TrelicaGeometria` — calcula largura dos paineis, vao total, alturas por estacao, extremos de apoio
  - `TrelicaPerfilFormatter` — formata nome de perfil com multiplicador ("2x L 76x76x6.3"), detecta cantoneira
  - `TrelicaTopologia` — detecta tipo da trelica (Plana/DuasAguas/Shed/Desconhecida)
  - `CotaFaixaBuilder` — constroi especificacoes das 5 faixas de cota como records imutaveis
- **`Services/Trelica/CotarTrelicaReport`** — Record imutavel com metricas (CotasCriadas, TagsCriadas, TextosCriados, WarningsCount, TempoMs, Resumo)
- **`Models/CotarTrelicaConfig`** — DTO com 9 propriedades (CotarPaineisSuperior/Inferior, VaoTotal, VaosParciais, Alturas, IdentificarPerfis, CantoneiraDupla, OffsetFaixaMm)
- **`Views/CotarTrelicaWindow`** — WPF com 7 checkboxes, OK/Cancel, tema Revit

### Added — Módulo Tagear Treliça
- **`Commands/CmdTagearTrelica`** + **`Services/Trelica/TagearTrelicaService`** — Identifica perfis diretamente sobre a elevacao da trelica com tags padrao EMT. Classifica barras e aplica tags por grupo (banzos, montantes, diagonais).
- **`Models/TagearTrelicaConfig`** + **`Views/TagearTrelicaWindow`** — Configuracao e janela WPF

### Added — Módulo Identificar Perfil (genérico)
- **`Commands/CmdIdentificarPerfil`** + **`Services/IdentificacaoPerfil/IdentificarPerfilService`** — Identifica perfis estruturais selecionados com tag ou TextNote contendo nome do perfil, comprimento e quantidade agrupada. Funciona em qualquer vista, nao so trelica.
- **`Models/IdentificarPerfilConfig`** + **`Views/IdentificarPerfilWindow`** — Configuracao e janela WPF

### Added — Botões no Ribbon
- 3 novos botoes no painel **Documentação**: `Cotar Treliça`, `Tagear Treliça`, `Identificar Perfil`

### Added — Wave A.1.5: Implementação real das APIs Revit
- **`Services/Trelica/TrelicaRevitHelper.cs`** (novo, ~310 linhas) — Helper estatico que centraliza todas as chamadas de API Revit: extracao de References de endpoints de barras (`ObterReferenciaExtremo`), criacao de running dimensions (`CriarRunningDimension` via `doc.Create.NewDimension`), criacao de tags (`IndependentTag.Create`), criacao de TextNotes (`TextNote.Create`), projecao/desprojecao de coordenadas 2D↔3D.
- **`CotarTrelicaService.cs` reescrito** — 5 TODOs substituidos por implementacao real:
  - `CriarDimensionsNoRevit`: extrai References dos endpoints de cada barra, monta ReferenceArray por faixa, cria running dimensions reais. Para faixa AlturasMontantes, cria TextNotes verticais com valor em mm.
  - `TentarCriarTag`: cria IndependentTag real com offset 150mm, leader em barras curtas (<400mm).
  - `CriarTextosRotuloBanzos`: detecta perfil do banzo superior/inferior, cria TextNotes "BANZO SUPERIOR W200x26.6" acima e "BANZO INFERIOR 2x L 76x76x6.3" abaixo.
  - Novo helper `DetectarPerfilBanzo` para leitura de perfil representativo do banzo.

### Refactored — UX (Wave E)
- **`Utils/RevitWindowThemeService.AttachEscapeHandler()`** — handler centralizado de ESC para todas as janelas WPF (~23 janelas beneficiadas). Removido handler duplicado de `WindowExtensions.cs`.
- **Migração `IntegerValue` → `ElementId`** em `CmdCotarTrelica.cs` (best practice Revit 2024+)

### Tests — 27+ novos testes unitários
- `TrelicaClassificadorTests` (7 testes: inclinacao, altura, tolerancia, indefinido)
- `TrelicaGeometriaTests` (9 testes: paineis, vao total, alturas por estacao, extremos, nunca negativa)
- `TrelicaPerfilFormatterTests` (8 testes: formatacao, cantoneira, placeholder, multiplicador zero)
- `TrelicaTopologiaTests` (7 testes: plana, duas aguas, shed, ruido no pico, nos identicos)
- `CotaFaixaBuilderTests` (8 testes: 5 faixas, 3 apoios, segmentos consecutivos)
- `CotarTrelicaReportTests` (5 testes: record, resumo, warnings)

### Docs
- **`docs/PLANO-LAPIDACAO-V2.md`** — Plano consolidado com revisao de 2 engenheiros seniores, APIs Revit oficiais, checklist de 20 itens market-ready, 5 ondas recalibradas (22 dias)
- **`docs/reference-projects/cobertura-samsung/`** — Padrao de cotagem de trelica em 5 faixas
- **`docs/reference-projects/galpao-padrao-emt/`** — Template minimo de entrega EMT em 3 pranchas

---

## [Unreleased]

### Fixed — Onda 5 bugs reais descobertos via testes automatizados (v1.0.5)
- **ConexaoCalculator gerava marcadores com virgula decimal em pt-BR**: `$"{x:F1}"` usava `CurrentCulture`, entao em maquinas brasileiras marcadores como "CP-12,7-150x250-4xM19" eram gerados em vez de "CP-12.7-...". Bug **afetava producao** porque marcadores viajam em nomes de arquivo, CNC, DSTV e shop drawings — precisam ser culture-invariant globalmente. Fix em `Services/Conexoes/ConexaoCalculator.cs` trocando interpolacao por `string.Format(CultureInfo.InvariantCulture, ...)` em todos os 3 formatters (ChapaDePonta, DuplaCantoneira, ChapaGusset). Regression test parametrizado rodando em pt-BR, de-DE, fr-FR, en-US em `ConexaoCalculatorCultureTests`.
- **DstvProfileMapper classificava strings livres como perfis**: `MapByDesignation` usava `StartsWith(letra) + HasDigit(string)`, entao "CUSTOM-001" virava U-channel (comeca com 'C' e tem digito em algum lugar). Qualquer nome de tipo nao padrao com uma letra + digito em qualquer posicao era classificado erroneamente — produzia DSTV com codigo de perfil errado para familias custom. Fix: novo helper `StartsDigit(s, prefix)` que exige digito **imediatamente apos** o prefixo (tolerando '-' ou espaco opcional). Regression tests em `DstvProfileMapperStrictnessTests` cobrindo "CUSTOM-001", "UNKNOWN-1", "LABEL-5", "TEST-7" cairem em `SO`, e W12X26/C310X45/L4X4X1/2 continuarem sendo reconhecidos.

### Refactored — Extracao de helpers puros para testes unitarios (v1.0.5)
- **`Services/Montagem/EtapaMontagemParser.cs`** (novo): extraido do `PlanoMontagemService.LerEtapaDoElemento` o parsing puro de "Etapa:N" do parametro Comments. Servico agora delega ao parser. 9 testes em `EtapaMontagemParserTests`.
- **`Services/CncExport/DstvFileNameSanitizer.cs`** (novo): extraido do `DstvExportService.SanitizarNomeArquivo` a logica de substituicao de chars invalidos. Servico agora delega. 8 testes em `DstvFileNameSanitizerTests`.
- **`Services/Conexoes/ConexaoFamilyNames.cs`** (novo): extraido do `ConexaoGeneratorService.NomeFamiliaEsperado` o mapa `TipoConexao -> nome de familia Revit`. 4 testes em `ConexaoFamilyNamesTests`.
- **`DstvFileWriter.FormatNumber`**: trocado `Logger.Warn` por `System.Diagnostics.Debug.WriteLine` para manter arquivo puro (linkavel em testes sem Serilog).

### Fixed — ModelCheckReportTests.Report_ExecutionTime_IsSet (flaky)
- Ordem do Arrange estava errada: `timeBefore = DateTime.Now` era capturado DEPOIS do `new ModelCheckReport()`, entao `ExecutionTime < timeBefore` em maquinas rapidas. Corrigido + adicionadas mensagens de diagnostico nos asserts.

### Added — Cobertura de testes
- Suite `FerramentaEMT.Tests` cresceu para **170 casos** (todos verdes em 49ms), cobrindo parsing de etapa, sanitizacao de nome DSTV, mapeamento de familias de conexao, formatacao culture-invariant de marcadores, classificacao estrita de perfis DSTV.

### Fixed — Onda 1 pos-feedback Victor (v1.0.1)
- **Janelas WPF muito grandes**: reduzidos Width/Height de 8 janelas (PipeRack, NumeracaoItens, Escada, Tercas, ExportarListaMateriais, VerificarModeloReport, ConexaoConfig, GuardaCorpo), adicionados MinWidth/MinHeight/MaxHeight para garantir que caibam em telas pequenas (1366x768).
- **Cotar Fabricacao pegando pontos em vez de faces (cotas inclinadas)**: caminho principal agora usa `FamilyInstance.GetReferences(FamilyInstanceReferenceType.Left/Right/Top/Bottom/Front/Back)` que retorna refs apontando para FACES da peca. Edge picker virou fallback.
- **Cotas por Eixo/Alinhamento so gerando horizontais**: `ExecutarCotagemAutomatica` agora roda em AMBOS os eixos da vista (horizontal E vertical) ao inves de so no eixo principal da selecao.
- **Verificar Modelo - botao "Selecionar" nao seleciona o item clicado**: novo metodo `ResolverElementIdsParaAcao` respeita o item escolhido no TreeView (se for issue individual, seleciona so aquele; senao, todos). Botao tambem minimiza a janela WPF pra usuario ver a selecao no Revit. Duplo-clique em item da arvore seleciona + foca com `ShowElements`.
- **Verificar Modelo - Isolar sem transaction**: `IsolateElementsTemporary` agora roda dentro de Transaction dedicada.
- **Vista da Peca nao isola nem cota**: apos criar a ViewSection (longitudinal e transversal), o servico agora chama `doc.Regenerate()` + `IsolateElementsTemporary` + `ConvertTemporaryHideIsolateToPermanent` + cria cotas automaticas usando `FamilyInstanceReferenceType` (Left/Right na longitudinal, Top/Bottom e Front/Back na transversal).

### Fixed — Onda 4 hardening defensivo (v1.0.4) — pre-ativo, sem bug reportado
- **Stale ElementIds em Verificar Modelo**: se o usuario apagasse um elemento no Revit entre gerar o relatorio e clicar "Isolar na Vista"/"Selecionar", o `IsolateElementsTemporary`/`SetElementIds` lancava `ArgumentException`. Agora `ResolverElementIdsParaAcao` filtra via `doc.GetElement(id) != null` antes de retornar.
Sweep sistematico do codebase identificou e corrigiu 5 crashes latentes:
- **Divisao por zero em Trelica/Tercas/Travamento**: `step = 1.0 / (config.Quantidade + 1)` — se `Quantidade == -1`, div/0. Agora com guard `Quantidade < 1` + mensagem clara em `Services/TrelicaService.cs`, `Services/TercasService.cs`, `Services/TravamentoService.cs`.
- **NumeracaoItensCatalog.ColetarCandidatos**: `doc.ActiveView.Id` estourava NRE quando nao havia vista ativa no escopo VistaAtiva. Agora fallback pra `Enumerable.Empty<Element>()`.
- **ListaMateriaisExportService**: mesmo padrao, agora cai em modelo inteiro se `ActiveView == null`.
- **NumeracaoItensSessao**: `_view = _doc.ActiveView` sem guard causava NRE em 3 pontos distantes (linhas 330/345/539 — Get/SetElementOverrides). Agora fail-fast no construtor com mensagem clara ao usuario.

### Fixed — Onda 3 pos-feedback Victor (v1.0.3) — CNC/DSTV
- **CNC "nao consigo avaliar"**: investigacao proativa identificou 3 raizes que produziam NC1 silenciosamente invalidos:
  - **(a) Cancelamento mascarado**: ESC no PickObjects retornava lista vazia e o caller mostrava "Nenhuma peca estrutural encontrada para exportar" (mensagem errada). Agora `ResultadoExport.Cancelado` distingue cancelamento legitimo de selecao vazia — o caller retorna sem alarmar o usuario.
  - **(b) Dimensoes zeradas silenciosas**: se a familia de viga nao expoe `STRUCTURAL_SECTION_COMMON_HEIGHT` ou se `STRUCTURAL_FRAME_CUT_LENGTH` esta ausente, o NC1 saia com altura/comprimento = 0 (arquivo invalido pra CNC). Novo `ArquivosComDimensaoZerada` conta esses casos; o resumo agora sai como WARNING (nao Info) listando elemento + parametro faltante. Victor agora ve EXATAMENTE qual elemento causou o problema.
  - **(c) NaN/Infinity silencioso em FormatNumber**: continua retornando "0" pra nao quebrar estrutura do arquivo, mas agora loga warning explicito (antes era totalmente mudo).

### Fixed — Onda 2 pos-feedback Victor (v1.0.2)
- **Plano de Montagem nao conseguia selecionar perfis**: `PickObjects` dentro de WPF modal bloqueava a UI do Revit. Agora: (1) usa pre-selecao do Revit se houver; (2) senao `Hide()` a janela, chama `PickObjects`, depois `Show()/Activate()` — mantem o `ShowDialog()` vivo.
- **Plano de Montagem "nao criava o plano" apesar de atribuicao bem-sucedida**: combo mortal: `AtribuirEtapa` caia em fallback `Comments` (string "Etapa:N") quando o parametro Integer nao existia, mas `GerarRelatorio` so lia Integer — dados sumiam silenciosamente. Novo `LerEtapaDoElemento` le Integer OU parseia "Etapa:N" de Comments. `AtribuirEtapa` agora limpa "Etapa:N" antiga via regex antes de escrever (sem acumular).
- **Gerar Conexao "aparece as opcoes mas nao cria"**: duas raizes. (1) `doc.ActiveView.SketchPlane.Normal` lancava `NullReferenceException` — a maioria das vistas nao tem SketchPlane — caia no catch generico e virava "pendente" sem explicacao. Agora usa o overload 3-arg `NewFamilyInstance(ponto, simbolo, StructuralType)`. (2) Quando a familia `EMT_Chapa_Ponta` / `EMT_Dupla_Cantoneira` / `EMT_Chapa_Gusset` nao esta carregada no modelo, a msg antes era generica; agora o dialogo informa EXATAMENTE qual arquivo `.rfa` carregar e os passos (Insert > Load Family).

### Added
- Nada ainda.

### Changed
- Nada ainda.

### Fixed
- Nada ainda.

---

## [1.0.0+licenca] — 2026-04-13 (post-audit + licenciamento self-hosted)

### Added (Sistema de Licença — comercializacao)
- **Modulo `FerramentaEMT/Licensing/` completo** (offline, sem custo de SaaS):
  - `LicenseStatus` (enum: Valid/Trial/Expired/TrialExpired/NotActivated/Tampered/WrongMachine)
  - `LicensePayload` (Email/IssuedAt/ExpiresAt/Version + helpers IsExpired/DiasRestantes)
  - `MachineFingerprint` — SHA-256(MachineGuid + UserName), 16 chars hex
  - `KeySigner` — HMAC-SHA256 com secret hardcoded, encode Base64URL
  - `Base64Url` — encoder/decoder URL-safe
  - `SimpleJson` — serializador minimo (deterministico para HMAC)
  - `LicenseStore` — persiste em `%LocalAppData%\FerramentaEMT\license\` com DPAPI (CurrentUser)
  - `LicenseService` — orquestrador: Initialize/Activate/GetCurrentState com cache em memoria
- **Janelas WPF** (tema Revit, ESC fecha):
  - `LicenseActivationWindow` — colar chave, mostrar fingerprint, copiar para clipboard
  - `AboutWindow` — versao, estado da licenca, dados de suporte
- **Comandos** (IExternalCommand direto, fora do gate de licenca):
  - `CmdAtivarLicenca`, `CmdSobre`
- **Painel "Licença"** no ribbon com botoes empilhados Ativar/Sobre
- **Gate de licenca** em `FerramentaCommandBase.Execute`: bloqueia comando se
  estado nao for `Valid` ou `Trial`. Abre janela de ativacao automaticamente
- **Trial automatico de 14 dias** na primeira execucao
- **Projeto `tools/EmtKeyGen/`** — console standalone para gerar chaves
  (`<Compile Link>` para reusar Secret/HMAC do projeto principal)
- **Documentacao**: `docs/SISTEMA-LICENCA.md` com workflow Hotmart → email → ativacao
- **Testes**: `KeySignerTests`, `LicensePayloadTests`, `Base64UrlTests` (~15 novos casos)

### Changed (Auditoria pos-Sprint 8)
- `FerramentaCommandBase` ganhou propriedade virtual `RequiresLicense` (default true)

### Fixed (Auditoria pos-Sprint 8)
- **`PlanoMontagemService.GerarRelatorio`** — chamava `FilteredElementCollector.FromViewport(view)`
  que NAO existe na Revit API. Corrigido para criar collector view-scoped via construtor:
  `new FilteredElementCollector(doc, doc.ActiveView.Id)`.
- **`CmdGerarConexao`** — chamava `Logger.Error(null, "...")` que jogaria NRE no Serilog.
  Corrigido para usar overload string-only.
- **`CmdGerarConexao`** — try/catch externo redundante (a base ja captura). Removido,
  trocadas chamadas de dialog para helpers `ShowSuccess`/`ShowWarning` da base.
- **`AppTheme.Base.xaml`** — adicionados `AccentButtonStyle` (alias de `PrimaryActionButton`)
  e `LabelText` (TextBlock SemiBold) referenciados em janelas Sprint 6/7.
- **`AppTheme.Light.xaml` / `AppTheme.Dark.xaml`** — adicionados aliases
  `ButtonBackgroundBrush`, `ButtonForegroundBrush`, `PanelBackgroundBrush`,
  `TextBoxBackgroundBrush`, `TextBoxForegroundBrush` (XAML referenciava brushes
  inexistentes — `DynamicResource` resolveria como Transparent em runtime).

---

## [1.0.0] — 2026-04-13 (Release oficial)

Marco oficial do FerramentaEMT — pronto para uso em produção.
Engloba o trabalho dos Sprints 2 a 8 desde 0.9.1, todos entregues no mesmo ciclo.

### Added (Sprint 5 — Export DSTV/NC1)
- **Modulo CNC completo** com geração de arquivos `.nc1` no formato DSTV
  - `Models/CncExport/`: `DstvProfileType` (I/U/L/B/RO/M/T/SO + extensão `ToDstvCode`), `DstvHole` (faces v/h/o/u/s), `DstvFile`, `ExportarDstvConfig`
  - `Services/CncExport/DstvFileWriter` — escrita pura ASCII com CRLF, `InvariantCulture` (sempre `.` decimal), blocos `ST → SC → BO → SI → EN`
  - `Services/CncExport/DstvProfileMapper` — mapeia famílias Revit (W*, HEA*, IPE*, HSS*, L*, etc.) para códigos DSTV
  - `Services/CncExport/DstvHeaderBuilder` — popula header lendo `STRUCTURAL_SECTION_COMMON_*`
  - `Services/CncExport/DstvHoleExtractor` — lê furos paramétricos via convenção `Hole {i} Diameter/X/Y/Face` (e `Furo {i} ...`)
  - `Services/CncExport/DstvExportService` — orquestrador com 3 escopos (seleção/vista/modelo) e agrupamento por marca ou instância
  - `Commands/CmdExportarDstv` + `Views/ExportarDstvWindow`
- **Botão `Exportar DSTV/NC1`** no painel "CNC" do ribbon

### Added (Sprint 6 — Model Checker / Verificação)
- **10 regras de validação automatizada** do modelo estrutural:
  - `MissingMaterialRule`, `MissingMarkRule`, `DuplicateMarkRule` (vê tipos diferentes na mesma marca)
  - `OverlappingElementsRule` (BBox + `BooleanOperationsUtils` com volume > 0,0001 m³)
  - `MissingProfileRule`, `ZeroLengthRule` (<1 mm), `MissingLevelRule`
  - `StructuralWithoutTypeRule`, `MissingCommentRule` (Info), `OrphanGroupRule`
- Modelos puros C# em `Models/ModelCheck/` (Severity, Issue, RuleResult, Report, Config)
- `Services/ModelCheck/ModelCheckService` orquestra com export Excel via ClosedXML
- `Views/VerificarModeloReportWindow` — TreeView agrupado por Severidade/Regra com isolar/selecionar elementos
- **Botão `Verificar Modelo`** no painel "Verificação"

### Added (Sprint 7 — Plano de Montagem + Conexões)
- **Plano de montagem (erection sequence)**:
  - `Models/Montagem/EtapaMontagem`, `PlanoMontagemConfig`, `PlanoMontagemReport`
  - `Services/Montagem/PlanoMontagemService` com paleta cíclica de 5 cores e relatório Excel
  - `Commands/CmdPlanoMontagem` + `Views/PlanoMontagemWindow` (TabControl 3 abas)
- **Geração de conexões metálicas** (3 tipos):
  - `ChapaDePonta`, `DuplaCantoneira`, `ChapaGusset`
  - `Services/Conexoes/ConexaoCalculator` (puro: contagem de parafusos + marcador `CP-12-150x250-4xM19`)
  - `Services/Conexoes/ConexaoGeneratorService` — tolerante a ausência de famílias (escreve em `EMT_Conexao_Tipo`)
  - `Commands/CmdGerarConexao` + `Views/ConexaoConfigWindow`
- **Painel "Montagem"** no ribbon com botões `Plano de Montagem` e `Gerar Conexão`

### Added (Sprint 4 — UX Consistency)
- **`Utils/WindowExtensions.InitializeFerramentaWindow()`** — helper único que aplica tema do Revit, posicionamento padrão e atalho ESC para fechar (substitui chamada explícita de `RevitWindowThemeService.Attach`; ambas convivem)
- **`AppSettings.Update(Action<AppSettings>)`** — load+mutar+save em uma chamada, com tratamento de erro embutido
- **`FerramentaCommandBase`** ganhou helpers padronizados de feedback:
  - `ShowSuccess(message, headline)`, `ShowWarning`, `ShowInfo`
  - `Confirm(message, ...)` para diálogos de confirmação
  - `NothingToDo(reason)` — caminho padrão para "nada a fazer", retorna `Result.Cancelled` e loga

### Changed (Sprint 2 — Performance)
- **Fix N+1 em `ListaMateriaisExportService.ColetarLinhas`**: adicionado `Dictionary<ElementId, Material>` cache; chamadas de `doc.GetElement` por material reduzidas de O(elementos) para O(materiais distintos), tipicamente <50

### Fixed (Sprint 1 — extras descobertos)
- **Logger overload faltando**: `AppSettings.Save/Load` chamava `Logger.Warn(ex, "...{Path}", path)` mas só existia `Warn(Exception, string)`. Corrigido com overloads `Warn/Error/Fatal(Exception, string template, params object[] args)` — sem essa correção, **o projeto não compilava**.

### Tests
- 30+ testes adicionados no `FerramentaEMT.Tests` cobrindo a lógica pura dos novos módulos:
  - `DstvFileTests`, `DstvFileWriterTests`, `DstvProfileMapperTests`
  - `ModelCheckIssueTests`, `ModelCheckReportTests`
  - `EtapaMontagemTests`, `PlanoMontagemReportTests`
  - `ConexaoConfigTests`, `ConexaoCalculatorTests`
- Padrão estabelecido: `<Compile Include>` com `Link=` para testar lógica pura sem referenciar `RevitAPI.dll`

### Notes
- Sprint 3 (refator de `CotasService`) foi avaliado como **não necessário no escopo da 1.0**: as chamadas a `doc.GetElement` no service operam em seleções pequenas do usuário (não há hotspot N+1). O serviço continua estável.
- Sprint 8 entrega documentação de release e handoff para Victor (compilação + instalação) — ver `docs/HANDOFF-VICTOR.md`.

---

## [0.9.1] — 2026-04-13 (Sprint 1 — completion)

### Changed
- **21 commands migrados para `FerramentaCommandBase`** (de 22 — `CmdCortarPerfilPorInterferencia` postergado para Sprint 2 por complexidade)
  - 9 commands TRIVIAL: `CmdAgruparPilaresPorTipo`, `CmdAgruparVigasPorTipo`, `CmdDesabilitarUniaoVigasSelecao`, `CmdDesabilitarUniaoVigasVista`, `CmdGerarCotasPorAlinhamento`, `CmdGerarCotasPorEixo`, `CmdIsolarPilaresEstruturais`, `CmdIsolarVigasEstruturais`, `CmdLimparAgrupamentosVisuais`
  - 12 commands MEDIA: `CmdAjustarEncontroVigas`, `CmdCotarPecaFabricacao`, `CmdExportarListaMateriais`, `CmdGerarTercasPlano`, `CmdGerarTravamentos`, `CmdGerarTrelica`, `CmdGerarVistaPeca`, `CmdLancarEscada`, `CmdLancarGuardaCorpo`, `CmdLancarPipeRack`, `CmdMarcarPecas`, `CmdNumerarItens`
  - **Resultado**: ~600 linhas de boilerplate eliminadas, logging automático em todos os commands
- **`AppSettings` agora thread-safe** (`ReaderWriterLockSlim` + escrita atômica via `.tmp` rename)
- **`AppSettings` exceções específicas**: trata `IOException`, `JsonException`, `UnauthorizedAccessException` separadamente, todos com log

### Fixed
- **Bug B5** — `CmdGerarCotasPorEixo` registrado no ribbon (estava órfão no `App.cs`)
- **Bug B2** — 6 `catch {}` silenciosos em `CotasService.cs` substituídos por catch com `Logger.Warn` + contexto:
  - `CriarCotaAlinhada` (linha 256)
  - `TentarObterLinhaDeCota` (linha 337)
  - `TentarObterPontoDeLado` (linha 354)
  - `TentarCriarCotaAlinhada` (linha 444)
  - `TentarCriarDimensao` (linha 871)
  - `TentarCriarDimensaoPorPlanosAuxiliares` (linha 948)

### Pending (Sprint 2)
- `CmdCortarPerfilPorInterferencia` (775 linhas, COMPLEXA) — migrar com cuidado + testes
- Refator de `ListaMateriaisExportService` (2.081 linhas)

---

## [0.9.0] — 2026-04-13

### Added
- **Sprint 0** — Repo hygiene profissional: `.gitignore`, `README.md`, `CHANGELOG.md`
- **Sprint 0** — GitHub Actions CI workflow (`build.yml`)
- **Sprint 0** — Scripts auxiliares: `Compilar-Debug.bat`, `Limpar-Tudo.bat`
- **Sprint 1** — Sistema de logging estruturado com Serilog
  - Logs salvos em `%LocalAppData%\FerramentaEMT\logs\emt-YYYYMMDD.log`
  - Rotação diária, retenção de 30 dias
- **Sprint 1** — `FerramentaCommandBase` — classe base abstrata para todos os commands
  - Try/catch padronizado
  - Logging automático de início/fim/duração
  - Tratamento separado de `OperationCanceledException`
  - Diálogo de erro padronizado
- **Sprint 1** — Projeto de testes `FerramentaEMT.Tests` (xUnit + Moq + FluentAssertions)
- **Sprint 1** — `Constants.cs` — magic numbers extraídos (offsets de cota, tolerâncias)

### Changed
- **Sprint 1** — `FerramentaEMT.csproj` agora compila com `TreatWarningsAsErrors=true` em Release
- **Sprint 1** — Pacote NuGet `Serilog` adicionado
- **Sprint 1** — Os 22 commands existentes migrados para herdar de `FerramentaCommandBase`

### Fixed
- **Bug B1** — `Visibility.Visible` ambíguo em `GerarVistaPecaWindow.xaml.cs` (já corrigido em sessão anterior, registrado aqui para histórico)
- **Bug B2** — `catch {}` silenciosos substituídos por catches específicos com log em `CotasService`
- **Bug B4** — `AppSettings` agora usa `ReaderWriterLockSlim` (thread-safe)
- **Bug B5** — `CmdGerarCotasPorEixo` (órfão) registrado no ribbon

### Security
- Nenhuma mudança de segurança nesta versão.

---

## Versionamento Planejado

| Versão | Tema | Previsão |
|---|---|---|
| `0.9.0` | Sprint 0/1 — Fundação + qualidade | abr/2026 |
| `0.10.0` | Sprint 2 — Refator ListaMateriaisExportService | abr/2026 |
| `0.11.0` | Sprint 3 — Refator CotasService + CotarPecaFabricacaoService | mai/2026 |
| `0.12.0` | Sprint 4 — UX consistency + ribbon reorg | mai/2026 |
| `0.13.0` | Sprint 5 — Export DSTV/NC1 (CNC) | jun/2026 |
| `0.14.0` | Sprint 6 — Verificação de Modelo (Clash + QA) | jun/2026 |
| `0.15.0` | Sprint 7 — Plano de Montagem + Conexões | jul/2026 |
| `1.0.0` | Sprint 8 — Polish + Release oficial | jul/2026 |

---

## Convenções

### Tipos de mudança
- **Added** — novas features
- **Changed** — mudanças em features existentes
- **Deprecated** — features marcadas para remoção
- **Removed** — features removidas
- **Fixed** — correções de bug
- **Security** — correções de segurança

### Versão
- **Major** (`X.0.0`) — quebra compatibilidade ou API pública
- **Minor** (`0.X.0`) — nova funcionalidade compatível
- **Patch** (`0.0.X`) — correção de bug compatível
