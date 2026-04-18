# Plano de Lapidação do FerramentaEMT — análise completa (15/04/2026)

> Documento técnico consolidando o estado atual do plugin (v1.0.5), o padrão de
> entrega do escritório EMT capturado em 4 projetos de referência reais, as
> lacunas identificadas entre o que o plugin faz hoje e o que o escritório
> precisa que ele faça, e um roadmap priorizado de implementação —
> começando pela função `Cotar Treliça`.

---

## 1. Sumário executivo

**Onde estamos.** O FerramentaEMT v1.0.5 é um plugin Revit 2025 maduro, com
**28 comandos em 9 painéis na ribbon**, **~215.000 linhas equivalentes de código
C#**, **196 testes verdes** e infraestrutura sólida (logger estruturado, diálogos
centralizados, tema claro/escuro sincronizado com o Revit, sistema de licença
próprio com trial de 14 dias). O plugin já cobre muito bem as camadas de
**modelagem paramétrica** (pipe rack, escada, guarda-corpo, terças, travamentos,
treliça, encontros de viga, seccionamento por interferência), **fabricação**
(marcação por assinatura, vista de peça, cotagem de fabricação, export DSTV/NC1),
**documentação** (numeração manual, lista de materiais em Excel, cotas por
alinhamento e por eixo), **montagem** (plano de erection com cores por etapa,
geração paramétrica de 3 tipos de conexão) e **qualidade** (ModelCheck com 10
regras + relatório Excel).

**O que os projetos de referência mostram.** Cruzando o código atual com os 4
projetos executivos reais já catalogados em `docs/reference-projects/`
(Igreja Patos, Ampliação Vulcaflex, Cobertura Samsung, Galpão padrão EMT),
aparece uma família visual **muito consistente** que o escritório usa em toda
entrega. A "cara" dessa família é dominada por seis padrões que o plugin
ainda não produz automaticamente:

1. **Cotagem de treliça em 5 faixas** (painéis do banzo superior, painéis do
   banzo inferior, vão total, cotas parciais entre apoios, altura vertical de
   cada montante).
2. **Tags de perfil específicas do escritório** (`2x L 1 3/4 x 3/16` para
   cantoneira dupla, `BANZO SUPERIOR <perfil>` / `BANZO INFERIOR <perfil>` nos
   banzos, paralelas ao membro).
3. **Plano geral de cobertura 1:100** com treliças, terças numeradas,
   contraventamento, linhas de corrente, calha, balões `VER DET NN`, 2–3
   perspectivas 3D de rodapé e tabela geral de perfis.
4. **Planta de locação 1:50** com tags `Pnn <perfil>`, `Snn`, `Vnn`, detalhe
   isométrico da sapata, detalhe da ancoragem e tabelas de aço.
5. **Balões isométricos 3D de detalhe** nos pontos críticos da treliça (emenda
   de banzo, ligação de terça, apoio no pilar, montante central, mão francesa,
   agulha central).
6. **Pacote de 3 pranchas EST-01 + EST-02 + EST-03** como entrega mínima EMT, com
   o texto-modelo das notas gerais CONCRETO/METÁLICA e a série de tabelas
   padrão em cada prancha.

**O que é prioridade.** `Cotar Treliça` é a ponta do iceberg. Ela sozinha
entrega ~40% do valor visual do escritório em uma única prancha (EST-03). Vem
depois `Tagear Treliça com Identificador de Perfil`, `Plano Geral de Cobertura`
e o pacote `Gerar Projeto Completo` (EST-01+02+03). Em paralelo, três
monólitos do plugin (`ListaMateriaisExportService`, `CotasService`,
`CmdCortarPerfilPorInterferencia`) pedem refatoração em helpers puros com
testes, para reduzir risco e abrir espaço para as novas features.

**Proposta de próximos passos.** Cinco ondas de trabalho, totalizando ~2–3
semanas de desenvolvimento focado:

| Onda | Entregável | Impacto no escritório | Esforço |
|---|---|---|---|
| A | `Cotar Treliça` (5 faixas + tags) | Alto — substitui 1 dia de trabalho manual por 3 segundos | 3–5 dias |
| B | `Tagear Treliça` e `Identificar Perfil` | Médio — padroniza nomenclatura em todo modelo | 1–2 dias |
| C | `Plano Geral de Cobertura` com tabelas e perspectivas | Alto — elimina trabalho de diagramação da EST-02 | 4–6 dias |
| D | `Locação + Fundação` (EST-01) e `Gerar Projeto Completo` | Alto — entrega 3 pranchas prontas num clique | 5–7 dias |
| E | Refatorações (monólitos → helpers puros), novos testes, novas regras de ModelCheck | Médio — reduz risco e acelera ondas seguintes | 2–3 dias |

---

## 2. Estado atual do plugin (v1.0.5)

### 2.1. Comandos e ribbon (28 comandos em 9 painéis)

**Modelagem.** `CmdLancarPipeRack`, `CmdLancarEscada`, `CmdLancarGuardaCorpo`.

**Estrutura.** `CmdGerarTercasPlano`, `CmdGerarTravamentos`, `CmdGerarTrelica`
(monta montantes+diagonais paramétricos entre dois banzos, com offset Z,
inversão de sentido e quantidade de divisões).

**Vigas.** `CmdAjustarEncontroVigas`, `CmdCortarPerfilPorInterferencia` (maior
comando: 775 linhas), `CmdDesabilitarUniaoVigasSelecao`,
`CmdDesabilitarUniaoVigasVista`.

**Vista.** `CmdIsolarVigasEstruturais`, `CmdIsolarPilaresEstruturais`,
`CmdAgruparPilaresPorTipo`, `CmdAgruparVigasPorTipo`,
`CmdLimparAgrupamentosVisuais`.

**Documentação.** `CmdNumerarItens` (numeração manual com UI de avançar/voltar),
`CmdExportarListaMateriais` (Excel completo),
`CmdGerarCotasPorAlinhamento` e `CmdGerarCotasPorEixo`.

**Fabricação.** `CmdGerarVistaPeca`, `CmdCotarPecaFabricacao`, `CmdMarcarPecas`.

**CNC.** `CmdExportarDstv` (gera `.nc1` válido DSTV 1.0 para máquinas CNC).

**Verificação.** `CmdVerificarModelo` (ModelCheck com 10 regras + relatório Excel).

**Montagem.** `CmdPlanoMontagem`, `CmdGerarConexao` (3 tipos: chapa de ponta,
dupla cantoneira, chapa gusset).

**Licença.** `CmdAtivarLicenca`, `CmdSobre`.

### 2.2. Services (camada de lógica)

**15 services de nível raiz** (os maiores, por ordem de tamanho):

- `ListaMateriaisExportService` — 86 KB (monólito alvo de refatoração).
- `CotasService` — 56 KB (monólito alvo de refatoração).
- `CotarPecaFabricacaoService` — 29 KB.
- `AutoVistaService` — 27 KB.
- `MarcarPecasService` — 27 KB.
- `NumeracaoItensService` — 24 KB.
- `AgrupamentoVisualService` — 23 KB.
- `EscadaService` — 22 KB.
- `PipeRackService` — 16 KB.
- `AjustarEncontroService` — 9,2 KB.
- `GuardaCorpoService` — 7,8 KB.
- `TercasService` — 7,4 KB.
- `TravamentoService` — 5,9 KB.
- `TrelicaService` — **5,6 KB** (pequeno — é só a modelagem, não a documentação).
- `NumeracaoItensCatalog` — 10 KB.

**Subservices especializados, bem fatorados:**

- `Services/CncExport/` — 6 arquivos (orquestrador, sanitizer puro,
  writer ASCII, header builder, hole extractor, profile mapper).
- `Services/Conexoes/` — 3 arquivos (calculator puro, family names puro,
  generator service).
- `Services/ModelCheck/` — 11 arquivos (orquestrador + interface + 10 regras).
- `Services/Montagem/` — 2 arquivos (parser puro + service).

### 2.3. Views, Models, Infrastructure, Utils

**20 janelas WPF** (Views/), sempre pareadas Command ↔ Window ↔ Service.
A maior é `NumeracaoItensWindow` (20 KB CodeBehind), refletindo o fluxo PickMode
contínuo com filtros e destaque visual.

**Models/** com DTOs de config por feature + subpastas
`CncExport/`, `Conexoes/`, `ModelCheck/`, `Montagem/`.

**Infrastructure/** com `Logger` (wrapper Serilog) e `Constants` (nomes de
parâmetros, códigos DSTV, tolerâncias, caminhos).

**Utils/** com `AppDialogService` (encapsula TaskDialog),
`AppSettings` (persistência JSON com `ReaderWriterLockSlim`), `RevitUtils`
(GetElementLevel, GetElementCurve, IsolateElementsTemporary, etc.),
`RevitWindowThemeService`, `WindowExtensions`.

**Licensing/** isolado com KeySigner HMAC-SHA256, LicenseStore (DPAPI),
MachineFingerprint, LicenseService.

### 2.4. Testes

**196 testes verdes em 19 arquivos**, cobrindo:

- Licensing (3 arquivos): Base64Url, KeySigner, LicensePayload.
- Models (2 arquivos): ConexaoConfig, EtapaMontagem.
- Services — CncExport (5 arquivos): FileNameSanitizer (8 casos),
  File, FileWriter, ProfileMapper, ProfileMapperStrictness.
- Services — Conexoes (3 arquivos): Calculator, CalculatorCulture (fix pt-BR),
  FamilyNames (4 casos).
- Services — ModelCheck (2 arquivos): Issue, Report.
- Services — Montagem (2 arquivos): EtapaMontagemParser (9 casos),
  PlanoMontagemReport.
- Smoke (1 arquivo): integração genérica.

### 2.5. Helpers puros já extraídos (base para o padrão)

Três classes 100% testáveis sem Revit API, reutilizáveis como referência:

- `Services/Montagem/EtapaMontagemParser.cs` (52 linhas, 9 testes).
- `Services/CncExport/DstvFileNameSanitizer.cs` (38 linhas, 8 testes).
- `Services/Conexoes/ConexaoFamilyNames.cs` (27 linhas, 4 testes).

Padrão estabelecido: helper puro em `Services/<Area>/<Nome>.cs` + testes em
`FerramentaEMT.Tests/Services/<Area>/<Nome>Tests.cs`. Este é o padrão que as
ondas futuras devem replicar.

### 2.6. Pontos fortes do código atual

1. **Disciplina de padrões.** Todo comando herda de `FerramentaCommandBase`,
   todo diálogo passa por `AppDialogService`, todo log passa por `Logger`. Não
   há `TaskDialog.Show`, `MessageBox.Show` ou `Console.WriteLine` no código de
   produção.
2. **Transações nomeadas e fechadas** em `using`, o que facilita debug e rollback.
3. **Culture-invariant** nos lugares certos (fix v1.0.5 em
   `ConexaoCalculator` para evitar "CP-12,7-..." em pt-BR).
4. **Gate de licença centralizado** no `FerramentaCommandBase`, com exceção
   limpa para os comandos que precisam rodar fora do gate (`CmdAtivarLicenca`,
   `CmdSobre`).
5. **Tema WPF sincronizado** com o tema do Revit.
6. **Testes cobrem os pontos frágeis** (culture, sanitização de nomes de
   arquivo, parsing de comentários, classificação estrita de perfis DSTV).

### 2.7. Pontos fracos / dívidas

1. **Monólitos.** `ListaMateriaisExportService` (86 KB) e `CotasService`
   (56 KB) concentram muita responsabilidade e têm cobertura de testes zero.
   São pontos de risco para regressões futuras.
2. **Ausência de orquestradores de prancha.** Hoje não há uma camada
   `Services/PranchaBuilder/` que saiba montar uma prancha completa
   (título, vistas, tabelas, carimbo) — cada feature resolve isoladamente.
3. **Não existe pasta dedicada para treliça.** `TrelicaService.cs` está solto
   na raiz de `Services/`. Quando chegar a hora de adicionar `CotarTrelica`,
   `TagearTrelica`, `DetalharTrelica`, vamos acumular vários arquivos soltos
   na raiz — vale criar `Services/Trelica/` agora.
4. **Catálogo de notas gerais / texto-modelo não existe.** O texto das
   notas gerais CONCRETO e METÁLICA é fixo no escritório, mas hoje não há
   nenhum arquivo `Resources/Notas/` que armazene e reutilize.
5. **Tabelas-padrão do escritório não têm suporte.** As tabelas de perfis,
   telhas, placas, parafusos, âncoras e aço são recorrentes em todas as
   pranchas EMT, mas o plugin hoje só sabe exportar em Excel (externo ao
   Revit) — não insere como `DrawingSheet` schedule com layout fixo.

---

## 3. Padrão EMT de entrega — o que as 4 referências mostram

### 3.1. Escalas padrão por tipo de desenho

Consolidado das 4 referências. Sempre que o plugin gerar uma vista/prancha,
essas são as escalas a usar por padrão:

| Desenho | Escala |
|---|---|
| Planta de locação de fundações | 1:50 |
| Detalhe de fundação / viga baldrame (corte) | 1:50 |
| Plano geral da cobertura | 1:100 |
| Elevação frontal/posterior do galpão | 1:50 |
| Elevação de treliça pequena (vão ≤ 20 m) | 1:25 |
| Elevação de treliça grande (vão > 20 m) | 1:50 |
| Quadro de platibanda | 1:75 |
| Detalhe isométrico de ligação | sem escala / `{3D}` |
| Detalhe de chapa topo/base de pilar | 1:10 |
| Croqui individual de terça | 1:20 |
| Croqui individual de LC | 1:20 |
| Croqui individual de chapa | 1:2 |
| Diagrama de montagem | 1:75 |

### 3.2. Nomenclatura e prefixos padrão

Todos os projetos catalogados usam exatamente a mesma nomenclatura. Esse deve
ser o **dicionário interno** do plugin:

| Elemento | Prefixo / padrão | Exemplo |
|---|---|---|
| Pilar metálico | `P` + nº + perfil | `P11 2U300x100x25x3.00` |
| Sapata | `S` + nº | `S11` |
| Viga baldrame | `V` + nº | `V12` |
| Treliça principal (em planta) | `TRELIÇA NN - <perfil>` | `TRELIÇA 01 - U150x65x4,76` |
| Treliça em elevação (catálogo) | `TR - NN (Qx)` | `TR - 01 (6x)` |
| Terça | `TERÇA NN - <perfil>` ou `TERÇAS NN - <perfil>` (plural quando tipo é repetido) | `TERÇA 26 - UE 200x75x25x3,0` |
| Contraventamento | `CONTRAVENTAMENTO BARRA <Ø>mm TIP` | `CONTRAVENTAMENTO BARRA 10mm TIP` |
| Linha de corrente | `LINHAS DE CORRENTE` ou `(9x) LC NN` em croqui | |
| Calha | `CALHA METÁLICA` | |
| Chapa de ligação | `CH nn` ou `CHAPA <esp>mm` | `CH 03`, `CHAPA 5mm` |
| Chumbador / parafuso | `<qtd>x ASTM A307 <Ø>mm` ou `Ø<d> mm, ISO 898.C4.6` | `4x ASTM A307 12mm` |
| Cantoneira dupla (perfil composto) | `2x <perfil>` (prefixo) ou `<perfil> 2` (sufixo) | `2x L 1 3/4 x 3/16` |
| Banzo | `BANZO SUPERIOR <perfil>` / `BANZO INFERIOR <perfil>` | |

### 3.3. Padrão de cotagem de treliça (5 faixas)

Confirmado em Igreja Patos, Samsung (EM-02 e EST-08) e Galpão Padrão EMT
(EST-03). **Tudo em milímetros inteiros, sem ângulos, sem cotas individuais
de diagonal.**

1. **Banzo superior.** Cota de painel nó a nó, colocada acima do banzo.
2. **Banzo inferior.** Cota de painel nó a nó, colocada abaixo do banzo.
3. **Vão total.** Uma cota grande externa (embaixo do banzo inferior),
   mostrando a soma.
4. **Cotas parciais entre apoios** (para treliças com pilares intermediários
   ou agulhas). Sobrepõe a cota do vão total.
5. **Altura de cada montante.** Texto vertical no eixo da montante, com o
   valor da altura da montante naquele ponto. Em treliça de duas águas,
   esses números crescem do apoio até a cumeeira.

Mais as **tags de perfil** paralelas a cada membro individual, e os **rótulos
de banzo** `BANZO SUPERIOR <perfil>` / `BANZO INFERIOR <perfil>` próximos ao
eixo do banzo correspondente.

### 3.4. Estrutura padrão de prancha EMT

Todas as pranchas do escritório seguem o mesmo layout:

- **Área principal central**: o desenho em escala (planta, elevação ou corte).
- **Balões de detalhe** ao redor da área principal, ligados por linha de
  chamada, cada um com seu título (`PERSPECTIVA - LIG. TERÇA - TR01`, etc.).
  Cada balão é uma view 3D/vista isolada do detalhe.
- **Tabelas laterais à direita**, empilhadas: perfis metálicos geral, placas,
  parafusos, telhas, âncoras, aço de sapatas, etc. (o conjunto varia por tipo
  de prancha).
- **Notas gerais** em bloco de texto (CONCRETO ou METÁLICA, com normas ABNT
  citadas).
- **Perspectivas isométricas no rodapé** (2 ou 3, sem cotas).
- **Carimbo padrão EMT** no canto inferior direito, com obra, endereço, cliente,
  responsável técnico (`ALEF CHRISTIAN GOMES VIEIRA`), desenhista, data,
  número da prancha, revisão, e-mail (`engenheiroalefvieira@gmail.com`).

### 3.5. Sequência mínima de entrega (Galpão padrão EMT)

Três pranchas cobrem 100% da documentação executiva de um galpão pequeno:

- **EST-01** — Locação das sapatas + armação + perspectivas + materiais.
- **EST-02** — Plano geral da cobertura + perspectivas + materiais.
- **EST-03** — Detalhamento das treliças + elevações + perspectivas + materiais.

Para galpões maiores (Vulcaflex, Samsung) a sequência cresce (EM-01 a EM-36,
CO-01 a CO-06, EST-08), mas a filosofia visual é a mesma.

---

## 4. Matriz de cobertura — plugin x referência

### 4.1. O que o plugin já cobre (✅)

| Capacidade do escritório | Como o plugin cobre |
|---|---|
| Modelar treliça paramétrica | `CmdGerarTrelica` + `TrelicaService` |
| Modelar terças com subdivisões | `CmdGerarTercasPlano` + `TercasService` |
| Modelar travamentos (frechais + diagonais) | `CmdGerarTravamentos` + `TravamentoService` |
| Modelar pipe rack completo | `CmdLancarPipeRack` + `PipeRackService` |
| Modelar escada e guarda-corpo | `CmdLancarEscada`, `CmdLancarGuardaCorpo` |
| Ajustar encontros de viga | `CmdAjustarEncontroVigas` + `AjustarEncontroService` |
| Seccionar perfil por interferência | `CmdCortarPerfilPorInterferencia` |
| Isolar e agrupar por tipo | 5 comandos de vista |
| Cotagem genérica por alinhamento/eixo | `CotasService` |
| Cotagem de fabricação de peça individual | `CotarPecaFabricacaoService` |
| Vista de peça (shop drawing) | `AutoVistaService` |
| Marcação de peças por assinatura | `MarcarPecasService` |
| Export DSTV/NC1 | `DstvExportService` + mapeadores |
| Lista de materiais em Excel | `ListaMateriaisExportService` |
| Verificação de modelo (10 regras) | `ModelCheckService` |
| Plano de montagem com etapas + cores | `PlanoMontagemService` |
| Gerar conexões paramétricas (3 tipos) | `ConexaoGeneratorService` |
| Numeração manual com UI contínua | `NumeracaoItensService` |

### 4.2. O que o plugin cobre parcialmente (△)

| Capacidade do escritório | Lacuna |
|---|---|
| Cotagem em vista | Existe cotagem genérica, mas sem lógica especial para treliça (5 faixas, altura de montante, rótulos de banzo). |
| Vista de peça | Gera vistas de peça individual; ainda não gera uma elevação da treliça inteira como conjunto lógico. |
| Marcação/numeração | Existe numeração manual com filtros; ainda não aplica automaticamente a nomenclatura EMT (`Pnn`, `Snn`, `Vnn`, `Tnn`, `TRELIÇA NN`) sem intervenção do usuário. |
| Lista de materiais | Exporta em Excel fora do Revit; ainda não insere as tabelas nativas do Revit (schedule) dentro da prancha com layout EMT fixo. |

### 4.3. O que o plugin NÃO cobre hoje (❌) — lacunas acionáveis

Listadas por ordem de impacto no escritório:

1. **Cotar Treliça (5 faixas + tags + rótulos de banzo).** Padrão confirmado em
   3 de 4 referências. Hoje é feito 100% manual — o usuário entra na
   elevação, insere cotas uma a uma, digita perfil em cada membro. Tempo
   médio estimado por treliça: 20 a 40 minutos.
2. **Tagear Treliça (perfil em cada membro, com prefixo `2x`).** Complementa a
   1 — se o Cotar Treliça chamar internamente o Tagear Treliça, o usuário
   ganha os dois recursos de uma vez.
3. **Plano Geral de Cobertura automático.** Gera planta 1:100 com:
   - Contornos de treliças tagueadas `TRELIÇA NN - <perfil>`,
   - Linhas de terças tagueadas `TERÇAS NN - <perfil>`,
   - Contraventamentos com legenda `CONTRAVENTAMENTO BARRA <Ø>mm TIP`,
   - Linhas de corrente,
   - Calha perimetral,
   - Balões `VER DET NN` onde houver detalhes remissivos,
   - 2–3 perspectivas isométricas no rodapé,
   - Tabela geral de perfis na lateral direita.
4. **Locação de Sapatas/Pilares/Vigas-baldrame automática.** Planta 1:50 com
   tags `Pnn`, `Snn`, `Vnn`, cotas entre eixos, grid alfanumérico.
5. **Detalhe Isométrico de Ligação (balão).** Um clique sobre um nó da treliça
   cria uma view 3D pequena e insere o balão na elevação atual, ligado por
   linha de chamada, com título automático (`PERSPECTIVA - LIG. TERÇA - TR01`).
6. **Detalhe de Mão Francesa, Agulha Central, Chapa Topo de Pilar, Apoio em
   Pilar.** Cinco detalhes tipicamente repetidos — cada um poderia virar um
   botão "Criar Detalhe XXX" que insere uma vista com cotas padrão.
7. **Catálogo de Treliças numa prancha só (`TR - NN (Qx)`).** Para obras com
   várias treliças diferentes.
8. **Elevação Frontal/Posterior do Galpão.** Vista de fachada automática com
   pilares, treliça no topo, cota externa total, cota de altura.
9. **Croqui individual de Terça, LC e Chapa** (catálogo de fabricação).
10. **Diagrama de Montagem** (plantas por fase, 1:75).
11. **Inserção automática das tabelas padrão** (perfis, telhas, placas,
    parafusos, âncoras, aço) como schedules nativos do Revit.
12. **Inserção automática das notas gerais** (CONCRETO e METÁLICA) como bloco
    de texto fixo na prancha.
13. **Gerar Projeto Completo (EST-01 + EST-02 + EST-03).** Orquestrador que
    chama os itens 3 + 4 + 1 + 2 + 5 + 10 e entrega uma pasta com 3 PDFs.
14. **Detalhe de Ancoragem (placa de base + chumbadores).** Gerar automatico
    com base no pilar selecionado.

### 4.4. Dívidas técnicas (⚠)

| Dívida | Impacto | Ação sugerida |
|---|---|---|
| `ListaMateriaisExportService` 86 KB sem testes | Alto risco de regressão | Extrair `MaterialSignatureBuilder`, `PesoTotalCalculator`, `ExcelColumnFormatter` como helpers puros |
| `CotasService` 56 KB sem testes | Alto risco de regressão | Extrair `CotaGeometryHelper`, `AlinhamentoClassifier`, `CotaDeduplicator` como helpers puros |
| `CmdCortarPerfilPorInterferencia` 775 linhas | Alto acoplamento | Extrair `InterferenceDetector`, `BeamSplitter` para Services |
| Ausência de `Services/Trelica/` | Crescimento desorganizado quando chegarem Cotar/Tagear/Detalhar | Criar agora e mover `TrelicaService` para lá |
| Texto-modelo de notas não existe | Retrabalho em cada feature de prancha | Criar `Resources/Notas/NotasConcreto.txt` e `NotasMetalica.txt` |

---

## 5. Roadmap proposto — 5 ondas

### Onda A — `Cotar Treliça` (3–5 dias) ★ prioridade máxima

**O que entrega ao escritório.** Botão que, com a treliça pré-selecionada e um
"OK" na janela de opções, gera automaticamente:

- Cotas de painel sobre o banzo superior.
- Cotas de painel sob o banzo inferior.
- Cota do vão total.
- Cotas parciais entre apoios.
- Altura vertical de cada montante.
- Tag de perfil em cada membro (prefixo `2x` para cantoneira dupla).
- Rótulo `BANZO SUPERIOR <perfil>` e `BANZO INFERIOR <perfil>`.

**Premissa confirmada.** O usuário pré-seleciona a treliça no Revit (seleção
múltipla de elementos ou seleciona um assembly/grupo que represente a
treliça), abre a elevação/corte que mostra a treliça, clica no botão, confirma
a janela com um OK e tudo sai cotado e identificado de acordo com o padrão EMT.

Detalhes técnicos estão na seção 6.

### Onda B — `Tagear Treliça` e `Identificar Perfil` (1–2 dias)

- `CmdTagearTrelica` — aplica só as tags de perfil (reaproveita componentes
  da Onda A). Útil para quando o usuário quer só identificar sem cotar.
- `CmdIdentificarPerfil` — em qualquer vista, aplica tag de perfil padrão
  EMT em todos os membros visíveis (viga, pilar, terça, LC, etc.), usando
  a regra de nomenclatura da seção 3.2.
- Serviço comum: `Services/Trelica/TagFormatter.cs` (helper puro) +
  `Services/Identificacao/PerfilTagFormatter.cs`.

### Onda C — `Plano Geral de Cobertura` com tabelas e perspectivas (4–6 dias)

- `CmdGerarPlanoGeralCobertura` — gera vista de planta 1:100 com:
  - Contornos de treliças identificados (`TRELIÇA NN - <perfil>`).
  - Terças identificadas (`TERÇAS NN - <perfil>`) com prefixo `T` paramétrico.
  - Contraventamentos e LCs com legenda-texto padrão.
  - Balões `VER DET NN` remissivos (parametrizados por nó de ligação notável).
  - Tabela geral de perfis inserida como Revit Schedule na prancha.
  - 2–3 perspectivas isométricas pré-configuradas no rodapé.
- Nova pasta `Services/PranchaBuilder/` com orquestrador da prancha + builders
  específicos (view, schedule, text note, title block).

### Onda D — `Locação + Fundação` (EST-01) e `Gerar Projeto Completo` (5–7 dias)

- `CmdGerarLocacaoFundacao` — gera a EST-01 (planta 1:50 com sapatas + tags
  `Pnn/Snn/Vnn` + detalhe isométrico + notas CONCRETO + tabelas).
- `CmdGerarProjetoCompleto` — orquestra A + C + D e entrega as 3 pranchas.

### Onda E — refatorações e testes (2–3 dias)

- Extrair helpers puros dos 3 monólitos (ListaMateriais, Cotas,
  CortarPerfilPorInterferencia).
- Adicionar 3 novas regras ao ModelCheck baseadas nas convenções descobertas:
  `TrelicaNaoCotadaRule`, `PerfilSemTagRule`,
  `NomenclaturaNaoPadronizadaRule`.
- Criar `Resources/Notas/` com os textos-modelo das notas gerais CONCRETO e
  METÁLICA.
- Criar `Services/Template/` para gerenciar o carimbo EMT e a inserção
  automática de notas nas pranchas novas.

---

## 6. Desenho detalhado da Onda A — `Cotar Treliça`

### 6.1. Fluxo de uso (UX confirmada)

1. Usuário está no Revit com a treliça já modelada.
2. Abre a elevação/corte que mostra a treliça inteira.
3. **Seleciona todos os membros da treliça** (seleção múltipla via retângulo
   ou via filtro) — pré-seleção é obrigatória, garante que a ferramenta
   saiba exatamente qual treliça processar.
4. Clica no botão `Cotar Treliça` no painel **Documentação** da ribbon EMT.
5. Abre uma janela WPF pequena com opções (todas marcadas por padrão, com
   botão OK e Cancelar):
   - Escala alvo: auto (detecta pelo vão: ≤ 20 m → 1:25, > 20 m → 1:50) /
     1:25 / 1:50 / 1:100 (override manual).
   - Tipo detectado: Duas águas / Plana / Shed / Auto — com possibilidade
     de override.
   - Faixas de cota a aplicar: [x] banzo superior, [x] banzo inferior,
     [x] vão total, [x] parciais entre apoios, [x] altura de montantes.
   - [x] Tags de perfil em cada membro.
   - [x] Rótulos `BANZO SUPERIOR` / `BANZO INFERIOR`.
   - [x] Prefixo `2x` para cantoneira dupla detectada.
6. Clica OK. Uma transação "Cotar Treliça" roda.
7. Mensagem de sucesso: "Treliça cotada em 2,3 s — 47 cotas e 23 tags
   criadas." (com botão de desfazer nativo Revit disponível).

### 6.2. Arquitetura proposta

Nova pasta `Services/Trelica/` dedicada (resolvendo também a dívida 2.7.3):

```
Services/Trelica/
├── TrelicaService.cs            (modelagem — mover o atual para cá)
├── CotarTrelicaService.cs        (orquestrador da nova feature)
├── TrelicaClassificador.cs       (helper PURO: classifica membros em banzo/montante/diagonal)
├── TrelicaGeometria.cs           (helper PURO: extrai painéis, alturas, pontos de referência)
├── TrelicaPerfilFormatter.cs     (helper PURO: formata "2x L 1 3/4 x 3/16" etc.)
├── TrelicaTopologia.cs           (helper PURO: detecta duas águas / plana / shed)
└── CotaFaixaBuilder.cs           (helper PURO: calcula os 5 conjuntos de cotas e suas posições)
```

E na camada de Command / View / Model:

- `Commands/CmdCotarTrelica.cs` — herda de `FerramentaCommandBase`, roda o
  gate de licença, chama a janela e depois o serviço.
- `Views/CotarTrelicaWindow.xaml(.cs)` — janela WPF com as opções da 6.1.
- `Models/CotarTrelicaConfig.cs` — DTO de configuração.

Na camada de testes:

```
FerramentaEMT.Tests/Services/Trelica/
├── TrelicaClassificadorTests.cs
├── TrelicaGeometriaTests.cs
├── TrelicaPerfilFormatterTests.cs
├── TrelicaTopologiaTests.cs
└── CotaFaixaBuilderTests.cs
```

### 6.3. Algoritmo

**Entrada.** Lista de `Element` pré-selecionados (vigas/perfis) + `View`
ativa (elevação) + `CotarTrelicaConfig`.

**Passo 1 — extrair geometria de cada membro.** Para cada elemento,
`RevitUtils.GetElementCurve` devolve a curva do eixo. Normaliza para 2D
projetando no plano da vista. Resultado: lista de segmentos `(p0, p1, perfil)`.

**Passo 2 — classificar (helper puro `TrelicaClassificador`).**

- Calcula ângulo de cada segmento em relação ao plano horizontal da vista.
- Segmento com ângulo ≈ 0° (com tolerância 5°) ao longo do topo → **banzo
  superior**.
- Segmento com ângulo ≈ 0° ao longo da base → **banzo inferior**.
- Em treliça de duas águas, o banzo superior será duas inclinações ± α — o
  classificador detecta o pico e agrupa as duas metades como "banzo superior"
  único.
- Segmento com ângulo ≈ 90° (vertical) → **montante**.
- Segmento com ângulo intermediário → **diagonal**.

**Passo 3 — detectar topologia (helper puro `TrelicaTopologia`).**

- Se o banzo superior tem dois segmentos com inclinação oposta e se encontram
  no meio → **duas águas**.
- Se banzo superior é horizontal e paralelo ao inferior → **plana**.
- Se banzo superior tem inclinação única em relação ao inferior → **shed**.

**Passo 4 — extrair painéis e alturas (helper puro `TrelicaGeometria`).**

- Ordena os nós do banzo superior por X crescente.
- Cada par consecutivo de nós gera uma cota de painel do banzo superior.
- Idem para banzo inferior.
- Para cada montante, a altura é a distância entre o ponto onde a montante
  cruza o banzo inferior e o ponto onde cruza o banzo superior.

**Passo 5 — formatar perfil (helper puro `TrelicaPerfilFormatter`).**

- Lê `STRUCTURAL_SECTION_COMMON_*` do tipo do elemento.
- Aplica as regras da seção 3.2:
  - Perfis W/HEA/IPE → `W200X26.6` (maiúsculo, sem espaço).
  - Perfis U formados a frio → `U150x65x4,76` (vírgula decimal).
  - Perfis UE formados a frio → `UE200x75x25x3,0`.
  - Cantoneiras L → `L 1 3/4 x 3/16` (polegadas fracionadas, com espaços).
  - Cantoneira dupla (detectada por convenção de família `2L...` ou por
    parâmetro `EMT_PerfilComposto = true`) → prefixo `2x`.
  - Barras redondas → `BARRA REDONDA <Ø>mm`.

**Passo 6 — construir faixas de cota (helper puro `CotaFaixaBuilder`).**

- Cada faixa gera uma lista de segmentos `(p0, p1)` e um offset vertical a
  partir do banzo de referência.
- O serviço `CotarTrelicaService` recebe isso e cria `Dimension` nativas
  do Revit em uma única transação.

**Passo 7 — criar cotas e tags (dentro de `CotarTrelicaService`).**

- Abre transação nomeada "Cotar Treliça".
- Para cada faixa da 6, chama `Document.Create.NewDimension` com o
  `DimensionType` de cotagem linear e as `ReferenceArray` construídas a
  partir dos pontos-chave.
- Para cada membro classificado em 2, chama `Document.Create.NewTag` com o
  perfil formatado em 5 e orientação paralela ao membro.
- Para cada banzo, cria um `TextNote` com `BANZO SUPERIOR <perfil>` ou
  `BANZO INFERIOR <perfil>` próximo ao membro.
- Commit da transação.

**Passo 8 — feedback.** Retorna `CotarTrelicaReport` com contagem de cotas,
tags e notas criadas. O Command exibe `AppDialogService.ShowInfo`.

### 6.4. Casos de borda tratados

- **Vista ativa não é elevação/corte.** Aborta com mensagem clara.
- **Seleção vazia.** Aborta com mensagem clara.
- **Seleção contém elementos que não são membros de treliça** (ex.: uma terça
  selecionada por engano). Classifica mesmo assim, mas apresenta aviso final
  listando os elementos não-classificados.
- **Treliça em mansarda, arco ou topologia não prevista.** Com `Tipo = Auto`,
  o `TrelicaTopologia` retorna `Desconhecido`; o serviço então aplica o
  modo fallback (só cotas de painel + tags, sem altura de montante).
- **Perfil composto sem convenção reconhecida** (não é `2L...`, não tem
  `EMT_PerfilComposto`). Coloca tag sem prefixo `2x` e gera aviso.
- **Unidades do documento não são mm.** Converte internamente via
  `UnitUtils.ConvertFromInternalUnits`.
- **Duas treliças selecionadas ao mesmo tempo.** Detecta via componente
  conexo do grafo de nós e avisa que só uma pode ser processada por vez.

### 6.5. Testes unitários previstos

Todos os helpers da 6.2 são puros (sem Revit API), então testáveis com
`FakeSegment` records. Exemplos:

- `TrelicaClassificadorTests.Segmento_Horizontal_Classifica_Como_Banzo()`
- `TrelicaClassificadorTests.Segmento_Vertical_Classifica_Como_Montante()`
- `TrelicaClassificadorTests.Duas_Inclinacoes_Opostas_No_Topo_Formam_Banzo_Superior_Unico()`
- `TrelicaGeometriaTests.Paineis_Banzo_Superior_Ordenados_Por_X()`
- `TrelicaGeometriaTests.Altura_Montante_Eh_Distancia_Vertical_Entre_Banzos()`
- `TrelicaPerfilFormatterTests.W200_Formata_Como_W200X26_6()`
- `TrelicaPerfilFormatterTests.Cantoneira_Dupla_Recebe_Prefixo_2x()`
- `TrelicaPerfilFormatterTests.U_Frio_Usa_Virgula_Decimal()`
- `TrelicaTopologiaTests.Duas_Aguas_Detectado_Quando_Pico_No_Meio()`
- `TrelicaTopologiaTests.Plana_Detectado_Quando_Banzos_Paralelos()`
- `CotaFaixaBuilderTests.Cinco_Faixas_Com_Offsets_Corretos()`

Meta: **+30 testes nessa onda**, levando o total para ~226 testes.

### 6.6. Estimativa de esforço

- Helpers puros (6.2) + testes (6.5): 1–2 dias.
- `CotarTrelicaService` (Revit API): 1 dia.
- `CmdCotarTrelica` + `CotarTrelicaWindow` + registro na ribbon + ícone: 0,5 dia.
- Ajustes de usabilidade e tratamento de bordas (6.4): 0,5–1 dia.
- Total: **3 a 5 dias** de trabalho focado.

---

## 7. Desenho das Ondas B–E (visão curta)

### 7.1. Onda B — `Tagear Treliça` e `Identificar Perfil`

Reaproveita `TrelicaPerfilFormatter`, `TrelicaClassificador` e a
lógica de criação de tag do `CotarTrelicaService`. Dois comandos novos,
dois services finos que delegam nos helpers, ~8 testes adicionais.

### 7.2. Onda C — `Plano Geral de Cobertura`

- Nova pasta `Services/PranchaBuilder/`.
- `CmdGerarPlanoGeralCobertura` + `PlanoGeralCoberturaService`.
- Helpers puros: `TercaIdentifier`, `TrelicaLocator`,
  `ContraventamentoClassifier`, `LinhaCorrenteClassifier`.
- Uso de `ViewSchedule` para a tabela de perfis (schedule nativo do Revit,
  não Excel externo).
- Criação automática de 3 `View3D` com isométricas pré-configuradas
  (SE, SW, NE com iluminação padrão).

### 7.3. Onda D — `Locação + Fundação` e `Gerar Projeto Completo`

- `CmdGerarLocacaoFundacao` + `LocacaoFundacaoService`.
- Helpers puros: `SapataTagFormatter`, `ChumbadorExtractor`,
  `TabelaAcoSchema`.
- `CmdGerarProjetoCompleto` — orquestrador que chama os comandos das
  Ondas A + C + D numa única transação, gera 3 sheets e exporta 3 PDFs.

### 7.4. Onda E — lapidações

- Monólito `ListaMateriaisExportService` (86 KB) — extrair
  `MaterialSignatureBuilder`, `PesoTotalCalculator`, `ExcelColumnFormatter`
  como helpers puros + testes.
- Monólito `CotasService` (56 KB) — extrair `CotaGeometryHelper`,
  `AlinhamentoClassifier`, `CotaDeduplicator` + testes.
- `CmdCortarPerfilPorInterferencia` (775 linhas) — extrair
  `InterferenceDetector` e `BeamSplitter`.
- 3 novas regras de ModelCheck: `TrelicaNaoCotadaRule`, `PerfilSemTagRule`,
  `NomenclaturaNaoPadronizadaRule`.
- `Resources/Notas/NotasConcreto.txt` e `Resources/Notas/NotasMetalica.txt`
  (texto-modelo consolidado do escritório).
- `Services/Template/CarimboEmtInserter.cs` para inserir o carimbo padrão EMT
  em qualquer sheet nova.

---

## 8. Riscos e mitigações

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Revit API não expõe o parâmetro `STRUCTURAL_SECTION_COMMON_*` em famílias antigas | Média | Fallback via leitura de nome do tipo + shared parameter `EMT_Perfil` |
| Seleção múltipla vira parcial (usuário esquece um membro) | Alta | O serviço detecta componentes conexos e avisa quando o grafo é incompleto |
| Topologia não reconhecida (arco, mansarda) | Média | Modo fallback com cotas básicas + aviso com sugestão de ajuste manual |
| Cota sobreposta a elemento do modelo | Baixa | Offsets calculados a partir do bounding box da treliça + margem configurável |
| Tag paralela ao membro fica ilegível em membros curtos | Média | Regra: se comprimento < 400 mm, tag se torna perpendicular com linha de chamada |
| Refatoração dos monólitos quebra features existentes | Baixa | Extração incremental com testes antes + depois (característica da Onda E) |

---

## 9. Métricas de sucesso

Ao final das 5 ondas, o plugin deve atingir:

- **5 novos comandos** (Cotar Treliça, Tagear Treliça, Identificar Perfil,
  Plano Geral, Locação+Fundação) + 1 orquestrador (Gerar Projeto Completo).
- **~60 testes novos** (levando o total de 196 para ~256).
- **~15 helpers puros adicionais** (levando o total de 3 para ~18).
- **3 novas regras de ModelCheck** (levando o total de 10 para 13).
- **Redução dos 3 monólitos principais** em ~30% cada por extração de helpers.
- **Tempo de entrega de um galpão pequeno** (EST-01/02/03) reduzido de
  ~2 dias úteis de diagramação manual para ~10 minutos de trabalho
  no plugin + revisão visual.

---

## 10. Decisões tomadas (confirmar antes de começar)

1. **Pré-seleção obrigatória** para `Cotar Treliça` (o usuário seleciona os
   elementos da treliça antes de clicar no botão). ✅ CONFIRMADO pelo Alef
   em 15/04/2026.
2. **Unidade: mm inteiros** em todas as cotas novas. ✅ padrão EMT em todas
   as 4 referências catalogadas.
3. **Formato de perfil nas tags:**
   - Laminados (W/HEA/IPE): maiúsculo, sem espaço, ponto decimal (`W200X26.6`).
   - Formados a frio (U/UE): vírgula decimal (`U150x65x4,76`, `UE200x75x25x3,0`).
   - Cantoneiras em polegadas fracionadas (`L 1 3/4 x 3/16`), com `2x` prefixo
     quando dupla.
   - Barra redonda (`BARRA REDONDA 10mm`).
   Confirmado por inspeção das 4 referências.
4. **Criar pasta `Services/Trelica/`** agora e mover o `TrelicaService.cs`
   atual para lá (pequeno refactor "oportunístico", zero risco).
5. **Começar pelos helpers puros + testes**, depois o serviço Revit, depois o
   Command/Window. Mesmo padrão das 3 extrações anteriores da v1.0.5.

---

## 11. Próximos passos imediatos (próximas ~2 horas enquanto Alef estiver fora)

Esta sessão vai executar, em ordem:

1. Scaffold da pasta `Services/Trelica/` com o esqueleto dos helpers
   `TrelicaClassificador`, `TrelicaGeometria`, `TrelicaPerfilFormatter`,
   `TrelicaTopologia`, `CotaFaixaBuilder` — cada um já com assinatura de
   métodos + XML doc em português + comentário apontando para a seção
   correspondente deste plano.
2. Scaffold de `Models/CotarTrelicaConfig.cs`.
3. Scaffold de `Commands/CmdCotarTrelica.cs` herdando de
   `FerramentaCommandBase` (sem chamar o serviço ainda, só a janela).
4. Scaffold de `Views/CotarTrelicaWindow.xaml(.cs)` com as opções da 6.1.
5. Scaffold dos testes unitários vazios em
   `FerramentaEMT.Tests/Services/Trelica/` para servir de template.
6. Relatório final no workspace com todos os links.

A implementação completa (passos 6–8 do algoritmo, corpo dos helpers com
lógica real) fica para a próxima sessão conjunta, com Alef presente para
validar as escolhas de implementação nó a nó.

---

*Documento gerado em 15/04/2026. Fonte de dados: mapeamento exaustivo da
v1.0.5 + 4 projetos de referência (`docs/reference-projects/`).*
