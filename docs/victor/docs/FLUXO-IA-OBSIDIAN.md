# Fluxo IA + Obsidian

Este projeto passa a usar o Obsidian como memoria operacional do plugin.

## Objetivo

Garantir que cada sessao de implementacao ou revisao:

- consulte o contexto acumulado antes de comecar
- registre o que foi feito ao final
- acumule aprendizados tecnicos e decisoes

## Vault atual

- `C:\Users\Victor\OneDrive\.IA\Pensamentos`
- pasta do projeto no vault: `01 Plugin\FerramentaEMT`

## Fluxo padrao

### Antes de iniciar uma tarefa ou review

1. Ler o contexto do Obsidian:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\Get-ObsidianContext.ps1`
   - ou usar o atalho: `powershell -ExecutionPolicy Bypass -File .\scripts\Start-Task.ps1 -TaskTitle "Nome da tarefa"`
2. Revisar:
   - `FerramentaEMT - Hub.md`
   - `Fluxo - Ferramentas e Processo.md`
   - `Checklist - Inicio e Revisao.md`
   - logs recentes
3. Cruzar o contexto do vault com os arquivos reais do repositorio.

### Durante a sessao

1. Implementar normalmente no codigo.
2. Anotar mentalmente:
   - decisoes
   - descobertas
   - riscos
   - proximos passos

### Ao final da sessao

1. Criar ou atualizar um log no Obsidian:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\New-ObsidianSessionLog.ps1 -Title "Resumo da sessao"`
   - ou usar o atalho interativo: `powershell -ExecutionPolicy Bypass -File .\scripts\End-Task.ps1 -Title "Resumo da sessao"`
2. Completar:
   - o que foi feito
   - o que foi aprendido
   - riscos em aberto
   - proximos passos
3. Se a sessao mudar entendimento estrutural do projeto, atualizar tambem o hub.

## Limites atuais

- O CLI `obsidian` nao esta disponivel neste ambiente.
- Por isso, o fluxo atual escreve diretamente nos arquivos Markdown do vault.
- Se o CLI for instalado depois, o processo pode ser migrado para comandos nativos do Obsidian.

## Notas principais no vault

- `01 Plugin/FerramentaEMT/FerramentaEMT - Hub.md`
- `01 Plugin/FerramentaEMT/Fluxo - Ferramentas e Processo.md`
- `01 Plugin/FerramentaEMT/Checklist - Inicio e Revisao.md`
- `01 Plugin/FerramentaEMT/Logs/`

## Scripts principais

- `scripts/Get-ObsidianContext.ps1`
- `scripts/New-ObsidianSessionLog.ps1`
- `scripts/Start-Task.ps1`
- `scripts/End-Task.ps1`
