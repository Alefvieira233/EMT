# PLANO — Lista de Materiais nível profissional — 2026-06-04

> Pedido: a lista exportada estava "fraca/básica" e **faltavam terças e contraventamento**. Tornar
> a lista de material no padrão de escritório (peso por perfil, totais, agrupamento por bitola).
> Branch `claude/great-turing-Vqlig`. Cada onda: cirúrgica, CI-verde, bump de versão.

## Diagnóstico (auditoria do pipeline)
Pipeline: `CmdExportarListaMateriais` → `ExportarListaMateriaisWindow` → `ListaMateriaisExportService`
(`Exportar` → `ColetarElementos` → `ClassificarElemento`/`InferirMaterialBaseTipo` → `AgruparLinhas`
→ `SalvarWorkbook` via ClosedXML). Abas: Capa, **Planilha Base** (a lista oficial, 4 colunas:
Item/Descrição/Unidade/Quantidade), Detalhe (12 colunas + totais), Resumo.

**Causa raiz das terças/contrav sumirem:** a seção metálica da Planilha Base só inclui grupos com
`MaterialBaseTipo==Metalico && PesoTotalKg>0`. Perfis **sem material estrutural atribuído** caíam em
`Outro` (a inferência por texto só reconhece "aço/steel" no nome — "U150x65" não bate) e com peso 0
→ **desapareciam da lista**. (Pilar de concreto aparecia porque o nome/material tem "concreto".)

## Onda 1 — Incluir todo o aço estrutural (EXECUTADA, v2.8.31)
`ListaMateriaisPesoCalc.InferirBase` ganhou `isPerfilEstrutural` (default false, retrocompatível):
viga/terça/contrav/pilar metálico/perfil de conexão **sem material → aço por padrão**.
`InferirMaterialBaseTipo` passa `true` para essas categorias. Com `MaterialBaseTipo=Metalico` e
volume>0, o peso sai por densidade padrão de aço (7850) quando não há kg/m → entra na lista. Testes
puros novos (perfil sem material = aço; concreto/fundação ainda têm prioridade).

## Onda 2 — Planilha Base profissional (seção metálica) — A FAZER
Hoje a seção metálica tem 4 colunas e agrupa por {Material,TipoPerfil}. Tornar padrão romaneio:
- Colunas da seção metálica: **Item | Perfil/Bitola | Descrição | Qtd | Comprimento total (m) |
  Peso linear (kg/m) | Peso total (kg)**.
- Agrupar por **perfil/bitola** (o dado já existe em `TipoPerfil`); **subtotal por perfil** e
  **TOTAL GERAL DE AÇO (kg)** ao fim da seção. Concreto: manter m³ por elemento + **TOTAL (m³)**.
- Reusar os subtotais já calculados em `CriarAbaResumo` ("Totais por material e tipo/perfil").
- Implementar com cuidado: ClosedXML, sem quebrar as abas Detalhe/Resumo (que já têm SUMPRODUCT).

## Onda 3 — Fundação e chapas (DirectShape) — A FAZER
- **Fundação:** garantir unidade **m³** (concreto) e peso/volume corretos (o print mostrou
  "200.000 kg" — volume/peso suspeito). Investigar `ObterVolumeMetrosCubicos` e a seção 3.
- **Chapas/DirectShape (OST_GenericModel):** hoje **não são coletadas**. Adicionar `OST_GenericModel`
  em `ObterFiltrosCategoria`/`ClassificarElemento` para a chapa de topo do pilar entrar como aço
  (peso por densidade × volume).

## Onda 4 — Robustez de escopo — A FAZER
- Default de escopo hoje é **Vista Ativa** (`ExportarListaMateriaisConfig`/janela) — pode perder
  elementos fora da vista. Avaliar default **Modelo inteiro** + aviso no resumo de quantos
  elementos entraram, para nunca mais "sumir" silenciosamente.

## DoD por onda
Build Release 0 warnings, testes verdes, format, gitleaks; sem regressão nas abas existentes;
validação do .xlsx pelo usuário no Excel.
