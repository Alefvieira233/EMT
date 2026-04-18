# Handoff para o Victor — FerramentaEMT 1.0.0

> Documento gerado em 2026-04-13 para passar o release 1.0.0 do FerramentaEMT do
> ALEF para o Victor (estagiário) executar o build, gerar o instalador e
> distribuir aos usuários do Revit 2025.

---

## 1. Contexto rápido

O ALEF não compila/testa localmente. Toda a evolução do código (Sprints 2 a 8)
foi escrita e revisada à mão, com testes unitários para a lógica pura, mas o
código que depende do `RevitAPI.dll` (commands, services Revit, views WPF) **não
foi compilado nesta máquina**. O seu papel é:

1. Fazer o checkout/extrair a versão atual do projeto.
2. Compilar em modo `Release`.
3. Rodar os testes unitários.
4. Gerar o instalador `.exe`.
5. Distribuir aos usuários (ou subir num drive interno).

Se algum erro de compilação aparecer, a maioria vai ser de "using" faltando ou
nome de namespace — anote a mensagem completa e me avise; são correções de
30 segundos.

---

## 2. Requisitos da máquina

| Item | Versão | Observação |
|---|---|---|
| Windows | 10 / 11 x64 | obrigatório |
| Revit | 2025 | precisa estar instalado em `C:\Program Files\Autodesk\Revit 2025\` (ou ajustar `HintPath` no `.csproj`) |
| .NET SDK | 8.0 (x64) | rodar `Instalar-DotNet-SDK.bat` se não tiver |
| PowerShell | 5.1+ | nativo do Windows, normalmente já tem |

Verificar com `Diagnostico-SDK.bat` (na raiz do projeto `FerramentaEMT/`).

---

## 3. Pipeline de build (passo a passo)

Ordem **exata** dos comandos a rodar a partir da pasta `FerramentaEMT  OFICIAL/`:

### 3.1. Limpeza (opcional mas recomendado para release)

```cmd
cd FerramentaEMT
Limpar-Tudo.bat
```

Apaga `bin/`, `obj/`, `artifacts/` e cache de build.

### 3.2. Compilar Release + rodar testes

```cmd
dotnet build ..\FerramentaEMT.Solution.sln -c Release
dotnet test ..\FerramentaEMT.Solution.sln -c Release
```

O projeto está com `TreatWarningsAsErrors=true` em Release, então qualquer
warning vai falhar o build — me avise se acontecer.

Os testes devem terminar com algo como `Tests passed: 30+`.

### 3.3. Gerar o instalador `.exe`

```cmd
Gerar-Setup.bat
```

Esse script chama `installer\Build-SetupExe.ps1` que por sua vez:

1. Compila o projeto em Release.
2. Copia os arquivos de `bin/Release/net8.0-windows/` + `Resources/` + manifesto
   `.addin` para `artifacts/deploy/`.
3. Empacota tudo em `artifacts/installer/FerramentaEMT-Revit2025-Setup.exe`.

O arquivo final fica em:

```
FerramentaEMT  OFICIAL\FerramentaEMT\artifacts\installer\FerramentaEMT-Revit2025-Setup.exe
```

### 3.4. Validar localmente

Antes de distribuir:

1. Execute o `.exe` na sua máquina (com o Revit 2025 fechado).
2. Abra o Revit 2025.
3. Confirme que aparece a aba **"Ferramenta EMT"** com os painéis:
   `Modelagem | Estrutura | Vigas | Vista | Documentação | Fabricação | CNC | Verificação | Montagem`
4. Abra um modelo qualquer e clique em **CNC → Exportar DSTV/NC1**. Deve abrir
   uma janela de configuração com o tema sincronizado com o Revit (claro/escuro).

Se travar, abra o log em:

```
%LocalAppData%\FerramentaEMT\logs\emt-YYYYMMDD.log
```

---

## 4. O que é novo na 1.0.0 (resumo executivo)

| Sprint | Novidade | Impacto |
|---|---|---|
| 5 | Export DSTV/NC1 (CNC) | Engenharia gera arquivos `.nc1` direto do modelo, sem intermediário |
| 6 | Verificação de Modelo | 10 regras automáticas (clash, marcas, perfis sobrepostos, etc.) com relatório Excel |
| 7 | Plano de Montagem | Atribui etapa, colore peças, exporta cronograma |
| 7 | Gerar Conexão | 3 tipos (chapa de ponta, dupla cantoneira, gusset) com bolt count |
| 4 | UX consistente | Tema sincronizado, ESC fecha janelas, feedback padronizado |
| 2 | Performance | Cache de materiais (até 50× mais rápido em projetos grandes) |

Detalhes completos no `CHANGELOG.md`.

---

## 5. Estrutura de pastas (quem faz o quê)

```
FerramentaEMT  OFICIAL/
├── FerramentaEMT/                  ← projeto principal (DLL do plugin)
│   ├── App.cs                       ← registra o ribbon (aba + painéis + botões)
│   ├── Commands/                    ← 1 classe por botão do ribbon (IExternalCommand)
│   │   └── FerramentaCommandBase.cs ← classe base com try/catch + logging + helpers
│   ├── Services/                    ← lógica de negócio (chama Revit API)
│   │   ├── CncExport/               ← Sprint 5
│   │   ├── ModelCheck/              ← Sprint 6
│   │   ├── Montagem/ + Conexoes/    ← Sprint 7
│   │   └── ListaMateriaisExportService.cs ← cache de materiais (Sprint 2)
│   ├── Models/                      ← classes de dados (puro C#, sem Revit)
│   ├── Views/                       ← janelas WPF (.xaml + .xaml.cs)
│   ├── Utils/                       ← infraestrutura compartilhada
│   │   ├── AppSettings.cs           ← persistência thread-safe + Update helper
│   │   ├── AppDialogService.cs      ← diálogos padronizados
│   │   ├── RevitWindowThemeService  ← sincroniza tema light/dark
│   │   └── WindowExtensions.cs      ← helper para inicializar janelas (Sprint 4)
│   ├── Infrastructure/Logger.cs     ← Serilog (rotação diária, %LocalAppData%)
│   ├── Resources/                   ← ícones .png do ribbon
│   └── installer/                   ← scripts de empacotamento
│       ├── Build-SetupExe.ps1       ← (chamado por Gerar-Setup.bat)
│       └── Install-FerramentaEMT.ps1 ← instalador "manual" sem .exe
│
├── FerramentaEMT.Tests/             ← xUnit (30+ testes da lógica pura)
├── FerramentaEMT.Solution.sln       ← solution (compila os 2 projetos juntos)
├── docs/
│   ├── ARCHITECTURE.md              ← visão geral (já existia)
│   ├── ROADMAP.md                   ← sprints planejados (já existia)
│   └── HANDOFF-VICTOR.md            ← este arquivo
├── CHANGELOG.md                      ← histórico de versões
└── README.md
```

---

## 6. Convenções de código (para você não estranhar)

- **Comentários em português** sem acentos em alguns arquivos antigos — é
  intencional (workaround de encoding em ambientes legados); novo código pode
  usar acentos sem medo.
- **Strangler Fig** — alguns arquivos têm `V2` no nome (`SomethingServiceV2`).
  São versões refatoradas que convivem com a original durante a migração. **Não
  delete a versão antiga sem antes confirmar comigo.**
- **`FerramentaCommandBase`** — todos os commands herdam dela. Se for adicionar
  comando novo, basta sobrescrever `CommandName` e `ExecuteCore(uidoc, doc)`.
- **`AppDialogService`** — sempre use ele para mostrar mensagens (não use
  `MessageBox.Show` direto). Garante visual consistente.
- **Logger** — `Logger.Info("[Cmd] mensagem {Param}", valor)`. Aceita
  exceção opcional como primeiro argumento.

---

## 7. Distribuição aos usuários

O `.exe` gerado é um instalador silencioso simples:

1. Encerra o Revit se estiver aberto.
2. Copia a DLL + recursos para `%ProgramData%\Autodesk\Revit\Addins\2025\FerramentaEMT\`.
3. Cria o manifesto `.addin` em `%ProgramData%\Autodesk\Revit\Addins\2025\`.

Para desinstalar: rodar `installer\Uninstall-FerramentaEMT.ps1` ou apagar as
duas pastas acima manualmente.

> **Importante**: distribuir só o `.exe`, **nunca** o código-fonte para clientes
> finais. O código fica no repositório interno.

---

## 8. Em caso de problema durante o build

| Sintoma | O que verificar |
|---|---|
| `Cannot find RevitAPI.dll` | Caminho `HintPath` no `.csproj` aponta pro Revit 2025 instalado? |
| `Could not load file or assembly Serilog` | Rodou `dotnet restore`? |
| `WindowExtensions does not exist` | Confirme que `Utils/WindowExtensions.cs` está na pasta (foi adicionado no Sprint 4) |
| Testes falham com "type or namespace not found" | Veja `FerramentaEMT.Tests.csproj` — todos os arquivos de lógica pura estão linkados via `<Compile Include>` |
| Setup `.exe` não é gerado | Olhe a saída do `Build-SetupExe.ps1`; faltam SDK ou permissão de admin? |

Se nada disso resolver, copie a saída completa do erro e me chame.

---

## 9. Checklist final antes de divulgar a 1.0.0

- [ ] `dotnet build -c Release` sem warnings nem erros
- [ ] `dotnet test -c Release` com 100% verde (>30 testes)
- [ ] `Gerar-Setup.bat` produz o `.exe` em `artifacts/installer/`
- [ ] Instalador roda numa máquina limpa com Revit 2025
- [ ] Aba "Ferramenta EMT" aparece com 9 painéis
- [ ] Pelo menos 1 comando de cada novo painel testado em modelo real:
  - [ ] CNC → Exportar DSTV/NC1
  - [ ] Verificação → Verificar Modelo
  - [ ] Montagem → Plano de Montagem
  - [ ] Montagem → Gerar Conexão
- [ ] CHANGELOG e README atualizados
- [ ] Tag `v1.0.0` criada no git (`git tag -a v1.0.0 -m "Release 1.0.0"`)
- [ ] Backup do `.exe` salvo em local seguro

Boa, Victor! Qualquer dúvida me chama. — ALEF / EMT
