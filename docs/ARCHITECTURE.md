# Architecture — FerramentaEMT

Documento de referencia da arquitetura do plugin. Atualizado a cada Sprint major.

---

## 1. Visao Geral

FerramentaEMT e um Revit add-in (.NET 8.0) escrito em C# 12, com interface WPF.
Segue arquitetura em camadas:

```
┌──────────────────────────────────────┐
│   Revit (UIControlledApplication)    │  ← host
└──────────────┬───────────────────────┘
               │ IExternalApplication
┌──────────────▼───────────────────────┐
│   App.cs  ←  Logger.Initialize()     │  ← entry point
└──────────────┬───────────────────────┘
               │ ribbon registration
┌──────────────▼───────────────────────┐
│   Commands (IExternalCommand)        │  ← thin layer
│   herda de FerramentaCommandBase     │
└──────────────┬───────────────────────┘
               │ delega
┌──────────────▼───────────────────────┐
│   Views (WPF dialogs)                │  ← coleta config do user
│   Window + code-behind + BuildConfig │
└──────────────┬───────────────────────┘
               │ passa config
┌──────────────▼───────────────────────┐
│   Services (logica de negocio)       │  ← a "alma" do plugin
│   *Service.cs                         │
└──────────────┬───────────────────────┘
               │ usa
┌──────────────▼───────────────────────┐
│   Models (POCOs)  +  Utils (helpers) │
└───────────────────────────────────────┘
```

## 2. Estrutura de Diretorios

```
FerramentaEMT/
├── App.cs                    # IExternalApplication, registra ribbon
├── FerramentaEMT.addin       # manifest gerado pelo .bat
├── FerramentaEMT.csproj      # .NET 8 + WPF + ClosedXML + Serilog
│
├── Commands/                 # IExternalCommand (entry points)
│   ├── FerramentaCommandBase.cs    # ★ base abstrata
│   ├── CmdMarcarPecas.cs
│   ├── CmdExportarListaMateriais.cs
│   ├── PF/                          # v1.2.0 — Pré-fabricado de concreto (10 cmds)
│   │   ├── CmdPfNomearElementos.cs
│   │   ├── CmdPfIsolar*.cs          # Lajes, PilaresConsolos
│   │   ├── CmdPfElevacaoForma*.cs   # Pilares, Vigas
│   │   └── CmdPfInserir*.cs         # Estribos/Aços: Pilar, Viga, Consolo
│   └── ... (32 total)
│
├── Services/                 # logica de negocio
│   ├── MarcarPecasService.cs
│   ├── ListaMateriaisExportService.cs    # 2.081 linhas (a refatorar Sprint 2)
│   ├── CotasService.cs                    # 1.415 linhas (a refatorar Sprint 3)
│   ├── Trelica/                           # v1.1.0 — helpers puros + service
│   ├── IdentificacaoPerfil/               # v1.1.0
│   ├── PF/                                # v1.2.0 — Pré-fabricado de concreto
│   │   ├── PfElementService.cs            #   predicados + coleta ordenada
│   │   ├── PfIsolationService.cs          #   isolar temporário na vista
│   │   ├── PfNamingCatalog.cs             #   enumeração de candidatos
│   │   ├── PfNamingService.cs             #   nomeação c/ transação
│   │   ├── PfNamingFormatter.cs           #   ★ puro (culture-invariant)
│   │   ├── PfRebarService.cs              #   946 linhas — pipeline Rebar
│   │   └── PfRebarTypeCatalog.cs          #   lookup RebarBarType
│   └── Shared/                            # logica pura (testavel) — a popular
│
├── Models/                   # POCOs (sem comportamento)
│   ├── MarcarPecasConfig.cs
│   ├── ListaMateriaisConfig.cs
│   └── ...
│
├── Views/                    # WPF Windows
│   ├── MarcarPecasWindow.xaml
│   ├── MarcarPecasWindow.xaml.cs
│   └── ...
│
├── Utils/                    # helpers stateless
│   ├── AppDialogService.cs            # diálogos consistentes
│   ├── AppSettings.cs                  # persistencia (TODO: thread-safe)
│   ├── RevitUtils.cs                   # conversoes, helpers
│   └── RevitWindowThemeService.cs      # tema dark/light auto
│
├── Infrastructure/           # ★ NOVO (Sprint 1)
│   ├── Logger.cs                       # wrapper Serilog
│   └── Constants.cs                    # magic numbers extraidos
│
├── Resources/                # icones PNG do ribbon
└── Templates/                # XLSX de templates pra export
```

E na raiz:
```
FerramentaEMT  OFICIAL/
├── FerramentaEMT/                       # projeto principal (acima)
├── FerramentaEMT.Tests/                 # ★ NOVO (Sprint 1) — xUnit
├── FerramentaEMT.Solution.sln           # solution unificada
├── .github/workflows/build.yml          # CI
├── README.md
├── CHANGELOG.md
└── docs/
    ├── ROADMAP.md
    └── ARCHITECTURE.md (este arquivo)
```

## 3. Camadas e Responsabilidades

### 3.1 Commands (camada fina)

- Implementam `IExternalCommand` via `FerramentaCommandBase` (ja faz try/catch + log)
- **Responsabilidade unica**: orquestrar o fluxo (validar, abrir Window, chamar Service)
- **NUNCA**: logica de geometria, transacao Revit, calculo de marca

```csharp
[Transaction(TransactionMode.Manual)]
public class CmdMarcarPecas : FerramentaCommandBase
{
    protected override string CommandName => "Marcacao de Pecas";

    protected override Result ExecuteCore(UIDocument uidoc, Document doc)
    {
        var window = new MarcarPecasWindow(uidoc);
        if (window.ShowDialog() != true) return Result.Cancelled;

        var config = window.BuildConfig();
        var service = new MarcarPecasService();
        var resultado = service.Executar(uidoc, config);

        return resultado.Sucesso ? Result.Succeeded : Result.Failed;
    }
}
```

### 3.2 Services (logica de negocio)

- Recebem `Document`/`UIDocument` + objeto de config
- Encapsulam transacoes Revit
- Retornam objeto de resultado (DTO)
- **Idealmente**: separar logica pura em `Services/Shared/` para testar sem Revit

### 3.3 Views (WPF)

- Cada Window tem `BuildConfig()` que retorna o POCO de configuracao
- Tema sincronizado via `RevitWindowThemeService.Attach(this)`
- **Sprint 4**: criar `FerramentaWindowBase` para padronizar persistencia

### 3.4 Models (POCOs)

- Sem comportamento, so dados (props get/set)
- Imutaveis quando possivel
- Validacao em construtor ou via FluentValidation (futuro)

### 3.5 Utils (helpers)

- Stateless (preferencialmente static)
- Sem dependencia de UI
- Servem multiplos services/commands

### 3.6 Infrastructure ★ (Sprint 1)

- `Logger.cs` — Serilog com sink de arquivo
- `Constants.cs` — magic numbers nomeados
- (Futuro) `IRevitDocumentWrapper.cs` — abstracao para testar com mock

### 3.7 Subsistema PF — Pré-Fabricado de Concreto ★ (v1.2.0)

Módulo paralelo aos serviços metálicos, integrado ao mesmo ribbon e mesmo
`FerramentaCommandBase`. Foco: documentação e armaduras de elementos de
concreto pré-fabricado (pilares, vigas, lajes, consolos), sem depender do
Dynamo.

```
Commands/PF/  → Services/PF/  → Models/PF/
(10 cmds)       (6 services)    (9 configs + 2 enums)
                     ↓
               PfElementService         # predicados: IsStructuralColumn/Beam,
                                        # IsPfConsolo, IsPfLaje, GetBeamAxisGroup
                     ↓
               PfRebarService           # ExecuteColumnStirrups / ColumnBars /
                                        # BeamStirrups / BeamBars / ConsoloBars
                                        # — cada um abre 1 Transaction e delega
                                        # para Insert*() por host
                     ↓
               Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(...)
```

**Reúso do núcleo metálico**: `PfElevacaoForma*` chama
`AutoVistaService.Executar()` com `FiltroCategoria = Pilares|Vigas`
(introduzido em `GerarVistaPecaConfig`). Assim os módulos não duplicam
geração de vistas — PF é apenas uma configuração diferente.

**Testabilidade**: `PfNamingFormatter` é puro (culture-invariant) e tem
suite xUnit. `PfRebarConfigs` também (defaults documentados por teste).
O `PfRebarService` em si é Revit-bound e validado por smoke test no Revit.

## 4. Padroes Obrigatorios

### 4.1 Transacoes Revit
```csharp
using (var t = new Transaction(doc, "Nome legivel pra Undo"))
{
    t.Start();
    // operacoes que modificam o modelo
    t.Commit();
}
```
- **Nunca** transacao aninhada (causa erro)
- Use `TransactionGroup` se precisar de varias transacoes em sequencia
- Sempre passe `IFailuresPreprocessor` se a operacao pode gerar warnings

### 4.2 FilteredElementCollector
```csharp
var vigas = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_StructuralFraming)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();
```

### 4.3 Logging
```csharp
Logger.Info("[{Cmd}] processando {N} elementos", CommandName, elementos.Count);
Logger.Error(ex, "Falha ao gerar cota da peca {Id}", elem.Id);
```

### 4.4 Conversao de Unidades
- **Internamente Revit usa feet** (e nao milimetros!)
- Sempre converta: `UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)`
- Use helpers em `RevitUtils` quando existir

## 5. Convencoes de Codigo

| Item | Regra |
|---|---|
| Idioma codigo | C# 12 |
| Idioma comentarios/nomes domain | PT-BR (Pilar, Viga, Marca) |
| Idioma infra | EN (Logger, Service, Builder) |
| Indentacao | 4 espacos |
| Nullable | `disable` (compatibilidade Revit) |
| `async/await` | em I/O (Excel, network) — nao em Revit API |
| Exceptions | Nunca `catch {}` — sempre logar |
| WPF Visibility | `System.Windows.Visibility.Visible` (qualificado) |
| Naming testes | `<Cenario>_<Quando>_<EsperaQue>` |

## 6. Anti-Patterns a Evitar

- ❌ God Class (>500 linhas em um service) → quebrar
- ❌ Magic numbers no codigo → usar `Constants`
- ❌ `catch {}` silencioso → catch especifico + log
- ❌ Logica de geometria em Command → mover pra Service
- ❌ `new XService()` direto em command → ok por enquanto, mas DI no futuro
- ❌ String concat de SQL/path → usar `Path.Combine`, parametros
- ❌ `Thread.Sleep` em comando Revit → usa `IdlingHandler` se precisar

## 7. Pontos de Atencao

### 7.1 Compatibilidade com Versoes do Revit
- Atualmente: **Revit 2025 only**
- HintPath em `.csproj` aponta pra `C:\Program Files\Autodesk\Revit 2025\`
- Para suportar 2024 e 2026: criar configurations + multiple TargetFrameworks (futuro)

### 7.2 Performance
- Listas grandes (>1000 elementos): usar `Parallel.ForEach` quando seguro
- Cache de coletores: nao rodar `FilteredElementCollector` 10× pela mesma vista
- Preferir `IList<ElementId>` a `List<Element>` quando possivel

### 7.3 Threading
- **API do Revit so funciona na UI thread**
- WPF usa Dispatcher para marshaling
- Operacoes de I/O (Excel, log file) podem ser async
- `AppSettings` precisa de lock (Sprint 1 TODO)

## 8. Decisoes de Arquitetura (ADRs)

| # | Decisao | Justificativa | Data |
|---|---|---|---|
| 001 | .NET 8.0 (nao Framework 4.8) | Revit 2025 suporta .NET 8 | mar/2026 |
| 002 | WPF (nao WinForms) | Tema, binding, modernidade | mar/2026 |
| 003 | Serilog (nao NLog/log4net) | Estruturado, sink simples | abr/2026 |
| 004 | xUnit (nao NUnit/MSTest) | Padrao .NET moderno | abr/2026 |
| 005 | ClosedXML (nao EPPlus) | Licenca permissiva | abr/2026 |
| 006 | Sem MVVM puro nas Views | Custo alto, beneficio baixo nesse contexto | abr/2026 |

ADRs detalhadas serao adicionadas em `docs/adr/` quando necessario.

## 9. Como Expandir

### Adicionar novo Command
1. Criar `Commands/CmdMinhaCoisa.cs` herdando `FerramentaCommandBase`
2. (Opcional) Criar `Views/MinhaCoisaWindow.xaml` se precisa de config
3. Criar `Services/MinhaCoisaService.cs` com a logica
4. Criar `Models/MinhaCoisaConfig.cs` se precisa de config
5. Adicionar icones em `Resources/`
6. Registrar botao em `App.cs` (em algum painel apropriado)
7. Atualizar `README.md` com a tabela de comandos
8. Atualizar `CHANGELOG.md`

### Adicionar nova Skill (logica reutilizavel)
1. Criar em `Services/Shared/` (logica pura, sem Revit)
2. Criar testes em `FerramentaEMT.Tests/Services/Shared/`
3. Documentar uso no XML doc da classe
