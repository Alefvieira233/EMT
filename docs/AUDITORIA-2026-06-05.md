# Auditoria end-to-end + plano de ajustes/otimizações — 2026-06-05

> 4 revisores paralelos (features novas; lista de materiais; transações/Revit-API; qualidade
> transversal) sobre o estado v2.8.34. Base **sólida, sem P0 catastrófico**. Achados organizados em
> ondas, cada item com **risco** e marcação **SEGURO-JÁ** vs **VALIDAR-NO-REVIT**.
> Regra: cirúrgico, CI-verde por onda, **nunca quebrar** fluxo de sucesso. Branch
> `claude/great-turing-Vqlig`.

## Veredito por área
- **Transações/Revit-API:** muito boa. Sem nesting ilegal, Activate+Regenerate completos, collectors
  materializados, unidades consistentes. Único P1: `CotasService` não suprime warnings de cota.
- **Features novas (pórtico/pintura/coroamento):** corretas no geral; pontos de geometria e estado
  global a refinar (chapa Z, área multi-sólido, shared-params).
- **Lista de materiais:** funciona; **1 regressão introduzida nesta sessão** (pilar de concreto sem
  material vira aço) + faltam total de fundação e refinos.
- **Qualidade/testes:** cobertura pura excelente (29/30 helpers); restam pendências da auditoria
  anterior (ConexaoConfigWindow, dup ObterNomePerfil, catch vazio).

---

## ONDA 1 — Correções SEGURAS de alto valor (zero/baixo risco, aditivas)
1. **[REGRESSÃO desta sessão] Pilar de concreto sem material vira aço.**
   `InferirMaterialBaseTipo` (ListaMateriaisExportService.cs ~1126) marca `Pilares` como
   `isPerfilEstrutural=true`; pilar de concreto sem material e sem "concreto" no nome cai em aço
   (peso 7850, seção metálica). **Fix:** remover `Pilares` do `isPerfilEstrutural` (manter Vigas,
   Contraventamentos, PerfisConexao — esses são sempre aço). Pilar sem sinal volta a "Outro".
   Atualizar/!adicionar teste em `ListaMateriaisPesoCalcTests`. Risco: Baixo.
2. **[P1] CotasService sem supressão de warnings.** `CotasService.cs` ~240, ~670, ~723: após
   `t.Start()`, adicionar `FailureHandlingHelper.SwallowWarnings(t);` (padrão já usado em
   AutoVista/DiagramaMontagem). Evita diálogo modal vermelho abortando cotagem em lote. Risco: Baixo.
3. **[P1] AreaPinturaService — restaurar SharedParametersFilename sempre.** `AreaPinturaService.cs`
   ~190: `doc.Application.SharedParametersFilename = previousSharedFile ?? string.Empty;` (hoje só
   restaura se não-vazio → pode deixar apontando p/ temp apagado). + null-check em
   `OpenSharedParameterFile()`. Risco: Baixo.
4. **[P2] Total de fundação na Planilha Base.** Concreto e metálica têm linha TOTAL; fundação não.
   Adicionar `EscreverLinhaTotalLdm` por subseção de fundação (unidade de `ObterUnidadeFundacaoBase`).
   Risco: Baixo.
5. **[P2] LetraEixo base-26 real.** `GerarPorticoService.cs` ~516: índice ≥26 vira "E27" e colide com
   "E". Implementar A..Z, AA, AB… Risco: Baixo.
6. **[P2] Variável morta `nVaos`.** `PorticoGeometriaCalculator.cs:152` declarada e não usada. Remover.
7. **[P2] catch vazios → Logger.Warn:** `ConexaoTercasService.cs:251` (Curve.Project),
   `CoroamentoCageService.DiametroBarraCm` (~96). Só observabilidade. Risco: Baixo.
8. **[P2] DirectShape.IsValidCategoryId** antes de `CreateElement` na chapa
   (`GerarPorticoService.cs` ~426). Hardening. Risco: Baixo.
9. **[P2] Romaneio: formatação + "0.0 kg/m".** `MontarDescricaoMetalicaRomaneio`: cultura explícita
   consistente e omitir o trecho kg/m quando comprimento=0. Extrair como helper PURO + teste. Risco: Baixo.

## ONDA 2 — Limpezas estruturais SEGURAS
10. **Extrair `ObterNomePerfil` duplicado** (`TagearTrelicaService.cs:344` ≡
    `IdentificarPerfilService.cs:235`) para helper único; ambos delegam. Risco: Baixo.
11. **ConexaoConfigWindow — perda silenciosa de sub-config** (`ConexaoConfigWindow.xaml.cs:58-103`):
    se um campo do tipo selecionado falha o parse, o sub-bloco é pulado e ainda loga "sucesso".
    Validar por campo do tipo selecionado e `return null` com aviso; preservar o caminho "grupo
    vazio = opcional". Risco: Baixo (aditivo na validação).
12. **Padronizar `1e-9`→`RevitUtils.EPS`** só nos guards de degenerância óbvios (NÃO tocar `1e-6`).
    Por arquivo. Risco: Baixo.

## ONDA 3 — VALIDAR NO REVIT (não aplicar às cegas; exigem o usuário testando)
- **Chapa de topo do pilar (Z).** Hoje extruda de `topo.Z` para cima, ocupando o nível do banzo
  inferior da treliça. Provável melhor: assentar com o TOPO em `topo.Z` (extrudar para baixo) ou
  centrada. Decidir com o usuário + validar visualmente.
- **Sinal da inclinação da terça** (`InclinacaoTercaRad`): confirmar no Revit se água 1 = +β é o
  sentido certo; se invertido, trocar o sinal base.
- **Área de pintura — multi-sólido/faces internas:** pode contar faces internas/sólidos sobrepostos
  (perfis tubo/caixa). Avaliar união booleana antes de somar, ou assumir como aproximação documentada.
- **DirectShape genérico na lista:** hoje todo DirectShape vira aço. Filtrar por nome/subcategoria
  ("chapa") para não capturar sólidos auxiliares.
- **Owner das janelas WPF** apontando p/ o Revit (`RevitWindowThemeService.Attach`) — corrige
  "janela atrás do Revit"; aplicar com try/catch e validar 2-3 fluxos.
- **`LicenseActivationWindow.xaml.cs:162` MessageBox→AppDialogService** — toca fluxo de licença.

## ONDA 4 — Otimização "padrão de escritório" (lista) — maior esforço
- Colunas dedicadas por perfil na seção metálica (Perfil/Bitola | Qtd | Comp. total | kg/m | Peso
  total) em vez de tudo na descrição; **tabela de parafusos/ferragem** por bitola/comprimento;
  **romaneio por marca** de fabricação. (Restruturação da Planilha Base — fazer com cuidado, sem
  quebrar Detalhe/Resumo/template ModeloLDM.)

## NÃO MEXER (confirmado correto)
- `LicenseSecretProvider` usa `Console` de propósito (linkado no EmtKeyGen, sem Logger).
- Transações/SubTransações, Activate+Regenerate, collectors materializados, unidades.
- Cobertura de testes pura (29/30). `Constants.cs` já removido.

## DoD por onda
Build Release 0 warnings, testes verdes, format, gitleaks; sem regressão; bump de versão + CHANGELOG.
