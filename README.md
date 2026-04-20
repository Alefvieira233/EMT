# FerramentaEMT

**Plugin profissional para Autodesk Revit 2025 — automação de estruturas metálicas**

Desenvolvido para acelerar o fluxo de modelagem, detalhamento e fabricação de estruturas em aço, do projeto ao chão de fábrica.

[![Build Status](https://github.com/Alefvieira233/EMT/actions/workflows/build.yml/badge.svg)](https://github.com/Alefvieira233/EMT/actions)
[![Version](https://img.shields.io/badge/version-1.5.0-blue.svg)](CHANGELOG.md)
[![Revit](https://img.shields.io/badge/Revit-2025-orange.svg)](https://www.autodesk.com/products/revit)
[![License](https://img.shields.io/badge/license-Proprietary-red.svg)](LICENSE)

---

## Visão Geral

FerramentaEMT é um conjunto de comandos especializados em estrutura metálica que cobre todo o ciclo:

- **Modelagem assistida** — pipe rack, treliças, terças, travamentos, escadas, guarda-corpo
- **Detalhamento** — cotagem automática por alinhamento e por eixo
- **Fabricação** — marcação de peças, lista de materiais consolidada, vistas de fabricação
- **Organização** — agrupamento visual por tipo, isolamento de pilares e vigas
- **Limpeza de modelo** — corte de perfis por interferência, ajuste de encontro de vigas

## Comandos Disponíveis (32)

### Painel: Modelagem
| Comando | Descrição |
|---|---|
| `Pipe Rack` | Lança estrutura de pipe rack parametrizada |
| `Treliça` | Cria treliças a partir de eixos selecionados |
| `Terças no Plano` | Distribui terças automaticamente em telhado |
| `Travamentos` | Adiciona contraventamentos entre vigas |
| `Escada` | Lança escada metálica completa |
| `Guarda-Corpo` | Cria guarda-corpo padrão |

### Painel: Detalhamento
| Comando | Descrição |
|---|---|
| `Cotas por Alinhamento` | Gera cotas alinhadas para vigas e pilares |
| `Cotas por Eixo` | Gera cotas perpendiculares aos eixos |
| `Cotar Peça (Fabricação)` | Cotagem completa em vista de fabricação |
| `Vista de Peça` | Cria vista de fabricação isolada da peça |

### Painel: Fabricação
| Comando | Descrição |
|---|---|
| `Marcar Peças` | Atribui marcas únicas por geometria |
| `Numerar Itens` | Numeração sequencial de itens |
| `Exportar Lista de Materiais` | Gera Excel consolidado por marca |

### Painel: Organização
| Comando | Descrição |
|---|---|
| `Agrupar Pilares por Tipo` | Pinta pilares por seção |
| `Agrupar Vigas por Tipo` | Pinta vigas por seção |
| `Isolar Pilares` | Esconde tudo exceto pilares |
| `Isolar Vigas` | Esconde tudo exceto vigas |
| `Limpar Agrupamentos` | Remove overrides visuais |

### Painel: Limpeza de Modelo
| Comando | Descrição |
|---|---|
| `Cortar por Interferência` | Resolve clashes em encontros |
| `Ajustar Encontro de Vigas` | Aproxima/encosta vigas concorrentes |
| `Desabilitar União (Seleção)` | Quebra união nas vigas selecionadas |
| `Desabilitar União (Vista)` | Quebra união em todas as vigas da vista |

### Painel: PF Construção (Pré-Fabricado de Concreto)
| Comando | Descrição |
|---|---|
| `Nomear PF` | Nomeia pilares/vigas/lajes PF com filtros por família, tipo e parâmetro |
| `Isolar P+Cons.` | Isola pilares estruturais + famílias PF com modelo Consolo |
| `Isolar Lajes` | Isola famílias PF com tipagem `Modelo = Laje` |

### Painel: PF Documentação
| Comando | Descrição |
|---|---|
| `Elevação Pilar` | Gera elevação longitudinal + corte transversal para pilares, sem Dynamo |
| `Elevação Vigas` | Gera elevação longitudinal + corte transversal para vigas, sem Dynamo |

### Painel: PF Armaduras
| Comando | Descrição |
|---|---|
| `Estribos Pilar` | Estribos com cobrimento, 3 zonas de espaçamento (inferior/central/superior) |
| `Aços Pilar` | Barras longitudinais com tipo de vergalhão e grid de posições |
| `Estribos Viga` | Estribos com zonas de apoio + corpo central, via Revit API `Rebar` |
| `Aços Viga` | Barras superiores, inferiores e laterais com gancho e modo de ponta |
| `Aços Consolo` | Armadura base (tirantes, suspensões, estribos V/H) de consolos PF |

---

## Instalação Rápida (Usuário Final)

1. Feche o Revit.
2. Baixe a release mais recente em [Releases](../../releases).
3. Execute o instalador `.msi` (será assinado a partir da v1.0).
4. Abra o Revit 2025 — a aba **EMT** aparecerá no ribbon.

## Build do Código-Fonte (Desenvolvedor)

### Pré-requisitos
- Windows 10/11 x64
- [Autodesk Revit 2025](https://www.autodesk.com/products/revit) instalado em `C:\Program Files\Autodesk\Revit 2025\`
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — instale via `winget install Microsoft.DotNet.SDK.8`

### Compilar e Instalar (1 comando)

```bat
INSTALAR.bat
```

O script:
1. Verifica que o Revit não está aberto
2. Compila em modo Release (`dotnet build -c Release`)
3. Gera o arquivo `FerramentaEMT.addin` apontando para a DLL
4. Copia binários para `%AppData%\Autodesk\Revit\Addins\2025\`

### Scripts Auxiliares
| Script | Função |
|---|---|
| `Compilar-e-Instalar.bat` | Build Release + deploy completo |
| `Compilar-Debug.bat` | Build Debug com PDB (anexar debugger no Revit) |
| `Limpar-Tudo.bat` | Limpa `bin/`, `obj/` e desinstala do AppData |
| `Diagnostico-SDK.bat` | Verifica .NET SDK, MSBuild e Revit |
| `Instalar-DotNet-SDK.bat` | Instala .NET 8 SDK via winget |
| `Gerar-Setup.bat` | Empacota .msi para distribuição |

---

## O que Testar (v1.5.0)

Checklist para validacao manual no Revit 2025:

### Comandos novos/alterados
- [ ] **Cortar Elementos** — selecionar pisos + pilares, executar, verificar cortes aplicados
- [ ] **Verificar Modelo** — habilitar verificacao de carimbo, rodar em projeto com folhas, conferir issues de campos vazios
- [ ] **Verificar Modelo — Navegacao 3D** — duplo-clique em issue navega ao elemento correto
- [ ] **Exportar DSTV** — verificar barra de progresso + botao Cancelar funcionando
- [ ] **Exportar Lista de Materiais** — verificar progresso + cancelamento

### DPI e janelas (testes em 125% e 150% DPI)
- [ ] `CotarPecaFabricacaoWindow` — scroll funciona, botoes visiveis
- [ ] `GerarVistaPecaWindow` — scroll funciona, botoes visiveis
- [ ] `ExportarDstvWindow` — scroll funciona, botoes visiveis
- [ ] `PfBeamBarsWindow` — scroll funciona, botoes visiveis
- [ ] `MarcarPecasWindow` — scroll funciona, botoes visiveis
- [ ] Todas as janelas devem permitir resize (grip no canto inferior direito)

### Seguranca
- [ ] Sem env var `EMT_LICENSE_SECRET` e sem arquivo `license.secret`: plugin deve mostrar erro claro ao tentar usar licenciamento
- [ ] Com env var configurada: licenciamento funciona normalmente

### Estabilidade geral
- [ ] Executar cada comando pelo menos 1x sem crash
- [ ] Verificar log em `%LOCALAPPDATA%\FerramentaEMT\logs\` — erros inesperados?

---

## Arquitetura

Padrão **Command → Service → Model**:

```
FerramentaEMT/
├── App.cs                    # Registro do ribbon e botões
├── Commands/                 # IExternalCommand (ponto de entrada do Revit)
│   └── Cmd*.cs
├── Services/                 # Lógica de negócio (sem acoplar UI)
│   └── *Service.cs
├── Models/                   # POCOs (configurações, resultados, dados)
├── Views/                    # WPF Windows (XAML + code-behind)
├── Utils/                    # Helpers genéricos (diálogos, conversões, tema)
├── Resources/                # Ícones PNG do ribbon (32px e 16px)
├── Templates/                # XLSX de templates para export
└── Infrastructure/           # Logging, configuração, base classes
```

### Regras de Ouro
1. **Commands são finos** — só validação de entrada + chamada ao Service
2. **Services não conhecem UI** — recebem `Document`/`UIDocument` e configuração
3. **Toda transação é explícita** — `using (var t = new Transaction(doc, "Nome"))`
4. **Failures preprocessor obrigatório** em transações que podem falhar
5. **Logs em todo command** via `FerramentaCommandBase`

---

## Convenções de Código

- **C# 12 / .NET 8**
- **Nomenclatura**: PT-BR para domínio (Pilar, Viga, Marca), EN para infra (Logger, Service)
- **Async**: usar `async/await` em I/O (export Excel, network)
- **Exceptions**: nunca `catch {}` — sempre logar
- **WPF**: `Visibility` deve ser qualificado como `System.Windows.Visibility` (dentro de Window)

---

## Roadmap

Veja [CHANGELOG.md](CHANGELOG.md) e [docs/ROADMAP.md](docs/ROADMAP.md) para o plano completo.

### Status por Release
- **v1.0 – v1.3** *(entregue)* — DSTV/NC1, Verificação de Modelo, Plano de Montagem, Conexões, Lista de Materiais, Sistema de Licença (HMAC), Instalador MSI, fundação ADR-003/004
- **v1.5.0** *(entregue — atual)* — Incorporação do Victor (Cortar Elementos), verificação de carimbo (TitleBlock) com navegação 3D, HMAC secret externalizado, DPI overflow corrigido em 7 janelas, 10 empty catches eliminados, ADR-003/004 adotado em todos os serviços principais. **419 testes passando.**
- **v2.0** *(planejado)* — Multi-Revit (2024/2025/2026), telemetria opt-in, auto-updater, i18n (en-US, es-ES).

---

## Contribuindo

Este é um projeto proprietário em desenvolvimento. Para reportar bugs ou sugerir features, abra uma [Issue](../../issues).

## Suporte

- **Issues**: [GitHub Issues](../../issues)
- **Email**: alefchristiangomesvieira@gmail.com

## Licença

Proprietário © 2026 ALEF — Todos os direitos reservados.
