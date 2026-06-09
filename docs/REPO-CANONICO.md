# Repositório canônico e como continuar — LEIA ANTES DE TRABALHAR

> Documento-âncora para evitar confusão entre repositórios. Criado em 2026-06-09.

## Decisão

**O repositório oficial e único é `Alefvieira233/emt`, branch `claude/great-turing-Vqlig`.**
É a cópia com histórico git real, todas as funções e CI verde.

- `emt` está em **v2.8.39** (HEAD `2ec5dad`, escada) e tem TUDO, menos o **Detalhamento Completo**.
- `Alefvieira233/steelbim` é uma **cópia do .zip numa subpasta** (`EMT-claude-great-turing-Vqlig/`),
  sem histórico e com CI provavelmente quebrado. A única coisa única lá é o **Detalhamento**.

## Regra de ouro
**Não apague o repo `steelbim` enquanto o Detalhamento não estiver dentro do `emt` e com build verde.**
Enquanto isso, ele é o único lugar com o Detalhamento.

## O que falta para consolidar (ordem)

### Passo 1 — Trazer o Detalhamento para o `emt` (rodar no Cowork, na sua máquina)
O Detalhamento mora no `steelbim/EMT-claude-great-turing-Vqlig/` e na sua máquina — fora do alcance
do ambiente de nuvem. Por isso este passo roda no **Cowork**. Cole o prompt da seção
"PROMPT COWORK" abaixo.

### Passo 2 — (Opcional) Dar o nome "steelbim" ao repo oficial
Só depois do Passo 1 concluído e com CI verde:
1. **Apague** o repo `Alefvieira233/steelbim` (a cópia bagunçada; o Detalhamento já estará no `emt`).
2. GitHub → repo `emt` → **Settings → Rename** para `steelbim`. O rename preserva todo o histórico.

### Daqui pra frente
- **Um repositório só.** Nunca mais baixar .zip e trabalhar em pasta solta — sempre `git clone` real.
- Toda mudança: branch → commit → push → CI verde.

---

## PROMPT COWORK (Passo 1 — colar no Claude Cowork)

```
Objetivo: trazer a função "Detalhamento Completo" (que está em
C:\Users\User\Documents\EMT-claude-great-turing-Vqlig (2)\EMT-claude-great-turing-Vqlig, e/ou no
repo steelbim subpasta EMT-claude-great-turing-Vqlig) para dentro do repo oficial Alefvieira233/emt,
branch claude/great-turing-Vqlig, SEM desfazer nada que já existe no emt (ele está mais novo: v2.8.39
com a correção da escada). É ADITIVO.

1) Clone limpo do emt:
   cd C:\Users\User\Documents
   git clone --branch claude/great-turing-Vqlig https://github.com/Alefvieira233/emt.git emt-oficial
   cd emt-oficial

2) Copie SÓ os 10 arquivos NOVOS do Detalhamento (da pasta-fonte do .zip) para os mesmos caminhos
   aqui — eles não existem no emt, então é adição pura:
   SteelBIM/Models/Detalhamento/DetalhamentoCompletoConfig.cs
   SteelBIM/Models/Detalhamento/DetalhamentoPlano.cs
   SteelBIM/Services/Detalhamento/DetalhamentoPlanner.cs
   SteelBIM/Services/Detalhamento/DetalhamentoGeometriaPura.cs
   SteelBIM/Services/Detalhamento/DetalhamentoCompletoService.cs
   SteelBIM/Views/DetalhamentoCompletoWindow.xaml
   SteelBIM/Views/DetalhamentoCompletoWindow.xaml.cs
   SteelBIM/Commands/CmdDetalhamentoCompleto.cs
   SteelBIM.Tests/Services/Detalhamento/DetalhamentoPlannerTests.cs
   SteelBIM.Tests/Services/Detalhamento/DetalhamentoGeometriaPuraTests.cs

3) Para os 4 arquivos EDITADOS, NÃO sobrescreva — MESCLE a adição do Detalhamento na versão ATUAL
   do emt (que já tem mudanças minhas mais novas):
   - SteelBIM/App.cs: adicione o painel "Detalhamento Automático" + o AddButton de
     "SteelBIM.Commands.CmdDetalhamentoCompleto" (ícones vista_peca_large/small.png) em
     BuildAbaDetalhamento. Mantenha tudo o que já existe.
   - SteelBIM.Tests/SteelBIM.Tests.csproj: ADICIONE os 4 <Compile Include> (com Link) dos fontes
     PUROS do Detalhamento (DetalhamentoCompletoConfig, DetalhamentoPlano, DetalhamentoPlanner,
     DetalhamentoGeometriaPura). NÃO remova nenhum include existente.
   - SteelBIM/AssemblyInfo.cs: o emt já está 2.8.39; some o Detalhamento -> bump para **2.8.40**.
   - CHANGELOG.md: mantenha as entradas do emt e ACRESCENTE a entrada do Detalhamento (v2.8.40).

4) Valide (não comite se algo falhar — me mostre o erro):
   dotnet build SteelBIM/SteelBIM.csproj -c Release
   dotnet test  SteelBIM.Tests/SteelBIM.Tests.csproj -c Release
   dotnet format SteelBIM/SteelBIM.csproj --verify-no-changes
   dotnet format SteelBIM.Tests/SteelBIM.Tests.csproj --verify-no-changes

5) git status (confirme: 10 novos + os 4 editados, nada a mais). Se ok:
   git add -A
   git commit -m "feat(detalhamento): Detalhamento Completo integrado ao emt (v2.8.40)"
   git push origin claude/great-turing-Vqlig

Regra: aditivo. Se o git status mostrar algum arquivo existente alterado fora desta lista, pare e
me mostre antes de comitar.
```
