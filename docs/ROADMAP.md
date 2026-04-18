# Roadmap — FerramentaEMT

Plano de execucao em **8 sprints de 2 semanas** ate v1.0.

## Status Atual

- **Versao:** 0.9.0
- **Sprint atual:** 0/1 (concluido)
- **Proximo:** Sprint 2 — Refator ListaMateriaisExportService

---

## Sprint 0 — Setup & Fundacao ✅

- [x] `.gitignore` profissional
- [x] `README.md` completo (instalacao, build, comandos)
- [x] `CHANGELOG.md` (Keep a Changelog)
- [x] GitHub Actions CI (`.github/workflows/build.yml`)
- [x] Scripts: `Compilar-Debug.bat`, `Limpar-Tudo.bat`
- [x] Solution unificada (`FerramentaEMT.Solution.sln`)

## Sprint 1 — Base de Qualidade ✅

- [x] **Logging Serilog** (`Infrastructure/Logger.cs`)
  - Logs em `%LocalAppData%\FerramentaEMT\logs\emt-*.log`
  - Rotacao diaria, retencao 30 dias
  - Plugado em `App.OnStartup` / `App.OnShutdown`
- [x] **`FerramentaCommandBase`** — base abstrata com try/catch + logging + Stopwatch
- [x] **`Constants.cs`** — magic numbers extraidos (Tolerancia, Cotas, Vistas, Fabricacao, ListaMateriais, Ui, Identificadores)
- [x] **`.csproj`** atualizado: Serilog, `TreatWarningsAsErrors=true` em Release
- [x] **Projeto de testes** `FerramentaEMT.Tests` (xUnit + Moq + FluentAssertions)
- [ ] Migrar 22 commands para herdar de `FerramentaCommandBase` (proxima sessao)
- [ ] Substituir `catch {}` silenciosos em `CotasService` (proxima sessao)
- [ ] Registrar `CmdGerarCotasPorEixo` no ribbon (proxima sessao)
- [ ] `AppSettings` thread-safe com `ReaderWriterLockSlim` (proxima sessao)

## Sprint 2 — Refator God Class I (planejado)

**Alvo:** `ListaMateriaisExportService.cs` (2.081 linhas → 6 classes < 350 linhas cada)

- [ ] Snapshot test (caracterizacao do XLSX atual)
- [ ] Extrair `FabricacaoSignatureBuilder`
- [ ] Extrair `MarcaConsolidator`
- [ ] Extrair `PerfilMetadataExtractor`
- [ ] Extrair `PesoCalculator`
- [ ] Extrair `ExcelWorkbookBuilder` (wrapper ClosedXML)
- [ ] Extrair `MateriaisXlsxFormatter`
- [ ] **Otimizacao N+1**: cache de materiais (esperado: 8x mais rapido em obras 2k peças)
- [ ] 30+ testes unitarios

## Sprint 3 — Refator God Class II (planejado)

**Alvo:** `CotasService.cs` (1.415 linhas) + `CotarPecaFabricacaoService.cs` (674 linhas)

- [ ] Quebrar `CotasService` em 6 classes
- [ ] Investigar bug B3 (CF entra em "Modificar | Corte" inesperado)
- [ ] Refinar `CotarPecaFabricacaoService` (combinar 3 iteracoes em 1)
- [ ] DRY: compartilhar `FabricacaoSignatureBuilder` entre Marcar e Lista
- [ ] 25+ testes unitarios

## Sprint 4 — UX Consistency (planejado)

- [ ] `FerramentaWindowBase` (tema + AppSettings persistencia automatica)
- [ ] Migrar 8+ janelas WPF para herdar dela
- [ ] Reorganizar ribbon: Marcacao | Cotas | Vistas | Listagem | Export CNC
- [ ] Padronizar feedback ao usuario (TaskDialog final em todo command)
- [ ] Ícones 32px e 16px padronizados

## Sprint 5 — Feature Killer: DSTV/NC1 Export (planejado)

- [ ] Estudar formato NC1/DSTV (ST, BO, AK, EN)
- [ ] Validar com 1 fabricante real
- [ ] Implementar `Services/CncExport/DstvExportService` + builders
- [ ] `CmdExportarDstv` + `ExportarDstvWindow.xaml`
- [ ] 20+ testes unitarios
- [ ] Documentacao + video demo

## Sprint 6 — Verificacao de Modelo (planejado)

- [ ] 10 regras de validacao (sem material, sem marca, pilar sobreposto, etc)
- [ ] `Services/ModelCheck/` com `IModelCheckRule` plugavel
- [ ] `CmdVerificarModelo` + janela de relatorio
- [ ] Export Excel + isolamento visual
- [ ] Processamento paralelo

## Sprint 7 — Plano de Montagem + Conexoes (planejado)

- [ ] `CmdPlanoMontagem` (etapas, timeline, parametro shared)
- [ ] Gerador de Conexoes:
  - [ ] Chapa de ponta (end plate)
  - [ ] Dupla cantoneira (clip angle)
  - [ ] Chapa de gusset
- [ ] Contagem automatica de parafusos

## Sprint 8 — Polish & Release 1.0 (planejado)

- [ ] Manual do usuario PDF (30+ paginas)
- [ ] Wiki por command
- [ ] Video demo (5min)
- [ ] Instalador MSI (WiX/Inno)
- [ ] Assinatura digital do MSI
- [ ] Telemetria opt-in
- [ ] QA final em 3 maquinas
- [ ] Tag v1.0.0 + release notes

---

## Metricas de Sucesso v1.0

| Metrica | Hoje | Meta |
|---|---|---|
| God classes (>500 linhas) | 5 | 0 |
| Cobertura testes | 0% | 70% |
| Bugs conhecidos | 7 | 0 |
| Comandos | 22 | 30+ |
| Lista 1000 pecas | 12s | < 3s |
| Tem instalador? | Nao | MSI assinado |
| Pronto pra escalar? | Nao | Sim |
