# Runbook — FerramentaEMT

Guia de troubleshooting e operação do plugin. Se você é usuário final e algo
não funcionou, comece aqui. Se nada aqui resolve, abra uma issue no GitHub
anexando o que a seção "Como coletar dados para um bug report" pede.

---

## Onde ficam os arquivos importantes

Todos ficam em `%LOCALAPPDATA%\FerramentaEMT\` (geralmente
`C:\Users\<seu-usuario>\AppData\Local\FerramentaEMT`):

- `logs\ferramenta-emt-YYYYMMDD.log` — log diário rotacionado. Últimos 7 dias mantidos.
- `crashes\crash-*-*.txt` — dumps de crashes capturados pelo `CrashReporter`.
- `license.secret` *(opcional)* — segredo HMAC para validar chaves em dev. Em produção normal, não existe.

O plugin também lê (mas não cria) `license.key` na mesma pasta — é onde fica a
sua chave de licença após ativação.

---

## Problemas comuns

### O plugin não abre / não aparece na ribbon

1. Abra o Revit como administrador uma única vez — o add-in precisa criar
   `%LOCALAPPDATA%\FerramentaEMT\` na primeira execução.
2. Confirme que o arquivo `.addin` está em
   `%APPDATA%\Autodesk\Revit\Addins\<versão>\FerramentaEMT.addin` e que o caminho
   apontado lá existe.
3. Verifique o log em `%LOCALAPPDATA%\FerramentaEMT\logs\` — procure por linhas
   com `[ERR]` ou `[FATAL]` no arquivo do dia.

### "Licença expirada" ou "Licença inválida"

1. Abra o comando `Sobre FerramentaEMT` na ribbon — ele mostra status, validade
   e user ID da licença carregada.
2. Se a mensagem for *"Arquivo de licença não encontrado"*, cole o arquivo
   `license.key` recebido em `%LOCALAPPDATA%\FerramentaEMT\`.
3. Se for *"Assinatura inválida"* em uma licença que já funcionou: o segredo
   HMAC pode ter sido rotacionado. Solicite reemissão ao suporte.
4. Se você é desenvolvedor e quer usar um segredo próprio para testes, crie
   `%LOCALAPPDATA%\FerramentaEMT\license.secret` com a string do segredo
   (sem aspas, sem espaços). O `EmtKeyGen` precisa usar o mesmo arquivo.

### O Revit travou ou congelou durante uma operação

1. Aguarde até 60 segundos — operações em lotes grandes (Export DSTV de 1000
   peças, Model Check completo) podem demorar.
2. Se travou de verdade, force o encerramento. Na próxima abertura, verifique
   `%LOCALAPPDATA%\FerramentaEMT\crashes\` — se há um arquivo novo, anexe-o
   ao bug report.

### Uma janela WPF do plugin abre mas os números ficam errados (vírgula × ponto)

O plugin usa `NumberParsing.TryParseDouble` (aceita ambos os formatos). Se
mesmo assim der erro, reporte com um screenshot da tela e o valor exato que
você digitou — provavelmente é um campo que ainda não foi migrado.

---

## Como coletar dados para um bug report

Por favor inclua tudo isto em qualquer issue no GitHub:

1. Versão do Revit (Menu *Informações → Sobre*).
2. Versão do plugin (comando *Sobre FerramentaEMT* na ribbon).
3. Log do dia em `%LOCALAPPDATA%\FerramentaEMT\logs\` (compacte em zip).
4. Se houve crash, o arquivo mais recente em `%LOCALAPPDATA%\FerramentaEMT\crashes\`.
5. Screenshot da tela no momento do problema.
6. Passos que você fez na ordem.

**Não anexe `license.key` nem `license.secret` em issues públicas** — eles
contêm dados de licenciamento.

---

## Ações de manutenção

### Limpar logs antigos

```powershell
Remove-Item "$env:LOCALAPPDATA\FerramentaEMT\logs\ferramenta-emt-*.log" -Force
```

O plugin recria a pasta automaticamente na próxima execução.

### Limpar crash dumps

```powershell
Remove-Item "$env:LOCALAPPDATA\FerramentaEMT\crashes\*.txt" -Force
```

Após enviar ao suporte, pode apagar com segurança.

### Reinstalar o plugin do zero

1. Feche o Revit.
2. Apague o `.addin` em `%APPDATA%\Autodesk\Revit\Addins\<versão>\`.
3. Apague a pasta do assembly apontada pelo `.addin`.
4. Reinstale a versão nova normalmente.

*Não apague `%LOCALAPPDATA%\FerramentaEMT\`* — sua licença mora lá.

---

## Informações para suporte interno

- Segredo HMAC rotaciona? Sim, é possível. O fallback hardcoded em
  `LicenseSecretProvider.DevOnlyFallback` mantém back-compat com licenças
  antigas durante uma janela de transição. Ver `docs/SISTEMA-LICENCA.md`.
- `LicenseSecretProvider.GetResolvedSource()` retorna de onde o segredo foi
  carregado — útil para diagnosticar por que uma chave parou de funcionar.
- Logs são escritos via Serilog wrapper (`FerramentaEMT/Infrastructure/Logger.cs`).
  Nível padrão: `Information`. Para debug, editar `Logger.Initialize` e subir
  para `Debug` ou `Verbose`.

---

## Histórico de artefatos arquivados

Itens removidos do working tree mas preservados fora dele para histórico:

- `backup-victor-pre-merge.zip` (302 MB) — snapshot do fork do Victor antes do
  merge da v1.2.0 (PF — Pré-Fabricado de Concreto). Movido em 2026-04-28 de
  `C:\Users\User\Downloads\FerramentaEMT\` para
  `C:\Users\User\Downloads\FerramentaEMT-archive\`.
- `pending-push/` (~717 KB, 6 patches da onda v1.4.0-rc.1: refactor LDM/agrupamento/numeracao
  + corte onda 3) — patches gerados em 2026-04-18 que ficaram fora de qualquer
  branch local. Movido em 2026-04-28 para
  `C:\Users\User\Downloads\FerramentaEMT-archive\pending-push\`. Reaplicar
  manualmente se ainda relevante.

Para restaurar qualquer um, basta `mv` de volta. Não estão sob versionamento
git (raiz `Downloads\FerramentaEMT\` nunca foi um repo).
