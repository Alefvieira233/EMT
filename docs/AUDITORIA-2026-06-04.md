# Auditoria completa do plugin SteelBIM — 2026-06-04

> Auditoria file-a-file conduzida por 5 revisores paralelos (transações/Revit API, comandos/UI,
> lógica pura/testes, padrões transversais/dead-code, segurança/build). Estado base: v2.8.26.
> **Veredito global: base muito madura, ZERO P0.** Todos os achados acionáveis são **aditivos e de
> baixo risco** — nenhuma melhoria altera um fluxo de sucesso existente. Refactors arriscados foram
> deliberadamente **deixados de fora** (documentados aqui como "não executar").

## Princípio de execução
Cada mudança precisa **SOMAR** e **nunca quebrar** função existente. Por isso:
- Preferimos **adicionar** uma chamada/guarda já consagrada no próprio codebase a reescrever lógica.
- Extrações de lógica pura preservam a assinatura e o resultado (o serviço passa a delegar).
- Toda mudança passa pelo CI (build Release 0 warnings, testes, format, gitleaks) antes de seguir.

---

## Onda 1 — Robustez de transações (P1, correção real, EXECUTADA)
Padrão dominante do projeto (15+ call-sites): `FamilySymbol.Activate()` **seguido de** `doc.Regenerate()`
antes de usar o símbolo. Quatro pontos esqueciam o `Regenerate`, causando falha intermitente "na 1ª
vez que a família/title block é usada na sessão". Corrigido (adição de 1 linha, padrão idêntico):

- `Services/Conexoes/ConexaoGeneratorService.cs` — Regenerate após Activate, antes de `NewFamilyInstance`.
- `Services/Layout/PrancharVistasService.cs` — Regenerate após Activate, antes de `ViewSheet.Create`.
- `Services/AutoVistaService.cs` — idem (title block da prancha de detalhe).
- `Services/DiagramaMontagem/DiagramaMontagemService.cs` — idem.
- `Services/TravamentoService.cs` — `CriarLinhaComSentido` ganha guard `DistanceTo < EPS` (retorna
  null; callers já tratam) — evita `Line.CreateBound` lançar e fazer rollback do lote inteiro.

## Onda 2 — Extração de lógica pura + testes (aditivo, risco nulo)
Mover aritmética escalar para helpers puros testáveis; o serviço Revit passa a **delegar** (resultado
idêntico). Cada helper ganha testes xUnit.
- `GuardaCorpoService.CalcularAlturasTravessas` → `GuardaCorpoCalculo.AlturasTravessas` (distribuição
  uniforme + guarda div/0).
- `GuardaCorpoService` segmentação de postes → `GuardaCorpoCalculo.SegmentosPorEspacamento`.
- `EscadaService` (contagem de degraus + Blondel 63–65 cm + inclinação ≤ 60°) → `EscadaCalculo`.

## Onda 3 — Novos testes para puros existentes (risco nulo)
- `OrdenacaoNatural.Comparar`: null/vazio e fronteira letra-vs-dígito.
- `EtapaMontagemParser.Parse`: overflow (número gigante → 0).
- `NumberParsing`: caso de separador de milhar ambíguo.
- `PfNbr6118AnchorageService.GetSpliceAlpha`: `[Theory]` nas fronteiras da tabela NBR 6118.
- Linkar + testar `CotarPecaFabricacaoConfig.TemCotaSelecionada()`.

## Onda 4 — Honestidade / guardas defensivas (baixo risco, aditivo)
- `Views/ConexaoConfigWindow.xaml.cs`: parse falho de um campo pulava o sub-bloco (chapa/cantoneira/
  gusset) silenciosamente e ainda logava "sucesso". → validar por campo e `return null` com aviso.
- `Views/PfEstacaRebarWindow.xaml.cs:64`: `catch {}` vazio → `Logger.Warn`.
- `Views/EscadaWindow.xaml.cs`: clamp/validação de `quantidadeDegraus` (alinha com as demais janelas).
- `Services/TercasService.cs` e `Services/Ifc/ConverterPerfilIfcService.cs`: `x!` (NRE silenciosa)
  → guarda explícita (`if (x is null) return ...`).

## Onda 5 — Limpezas seguras (baixo risco)
- Remover `Infrastructure/Constants.cs` (classe 100% não referenciada — confirmado por grep).
- Extrair `ObterNomePerfil` duplicado (idêntico em `IdentificarPerfilService` e `TagearTrelicaService`).
- `Licensing/LicenseSecretProvider.cs`: `Console.Error.WriteLine` → `Logger.Warn` (ou remover o
  arquivo, que é legado inerte pós-ADR-011).

---

## Deixado de FORA (arriscado / requer validação no Revit — NÃO executado autonomamente)
- **Owner das janelas WPF apontando para o Revit** (`RevitWindowThemeService.Attach`): corrige
  "janela atrás do Revit", mas é mudança ampla (todas as janelas) e não-testável fora do Revit.
  Recomendado aplicar com `try/catch` + validação manual em 2–3 fluxos.
- **`MessageBox` → `AppDialogService` em `LicenseActivationWindow`**: toca o fluxo de licença/update;
  baixo-médio risco, validar manualmente.
- **`#nullable enable` em lote** (~150 arquivos): pode emitir warnings e quebrar Release (TWAE).
  Fazer só por-arquivo ao tocar, como manda o CLAUDE.md.
- **Consolidar as 4 famílias de `TrySet*`/`SetLookupParameter`**: assinaturas/erro divergem.
- **`gitleaks` bloqueante** e **Authenticode no auto-update**: processo/infra, fora de código; o
  segundo depende do certificado de code-signing.

## Confirmado CORRETO (não mexer)
Transações sem aninhamento ilegal; `SubTransaction` com rollback por host (exemplar); unidades via
`UnitUtils`/`FT_PER_MM` (numericamente idênticos); licença ECDsa P-256 **fail-closed**; chave pública
real (não placeholder); privada nunca entra no CI; `TreatWarningsAsErrors=true` em Release já presente;
versão AssemblyInfo sincronizada com o CHANGELOG.
