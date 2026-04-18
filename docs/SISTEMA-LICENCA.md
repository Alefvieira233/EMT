# Sistema de Licença — FerramentaEMT 1.0.0

> Guia operacional para o ALEF (vendedor) e para o Victor (compilação).
> Cobre como gerar chaves, distribuir, ativar e o que fazer quando o cliente
> reinstalar o Windows ou trocar de máquina.

---

## 1. Visão geral

O FerramentaEMT tem licenciamento **offline e self-hosted** — sem dependência de
servidor, sem custo mensal de SaaS, sem internet obrigatória no PC do cliente.

Funciona assim:

- **Trial automático**: na primeira execução do plugin, o cliente ganha
  **14 dias** gratuitos. Tudo liberado nesse período.
- **Licença anual**: ALEF gera uma chave assinada (HMAC-SHA256) válida por
  365 dias e envia por email.
- **Ativação**: cliente cola a chave em `Ribbon → Licença → Ativar Licença`. A
  chave é amarrada ao identificador da máquina (`MachineGuid` + usuário Windows)
  e armazenada criptografada (DPAPI) em `%LocalAppData%\FerramentaEMT\license\`.
- **Validação**: a cada execução de comando do plugin, o `FerramentaCommandBase`
  consulta `LicenseService.GetCurrentState()`. Se inválida → bloqueia e abre a
  janela de ativação automaticamente.

---

## 2. Como vender (workflow do ALEF)

### 2.1. Receber o pagamento (Hotmart / Kiwify)

Configure o produto na Hotmart (ou Kiwify):
- Tipo: produto digital
- Preço: R$ 500,00 / ano (ou o que decidir)
- Entrega: manual (sem download automático — você envia a chave por email)

A plataforma cuida do PIX/cartão/boleto e te repassa o líquido. Sem mensalidade
fixa para você.

### 2.2. Gerar a chave

Quando receber a notificação de venda, abra o terminal na pasta do projeto e
rode:

```cmd
dotnet run --project tools\EmtKeyGen
```

O programa pergunta:

```
Email do cliente: cliente@exemplo.com
Validade em dias [365]:
```

Aperte Enter para usar 365 dias (padrão anual). A saída é a chave:

```
Chave gerada:
------------------------------------------------------
eyJlIjoi...long-base64...HMAC...
------------------------------------------------------
Detalhes:
  Email   : cliente@exemplo.com
  Emitida : 13/04/2026 14:32 UTC
  Expira  : 13/04/2027 14:32 UTC (365 dia(s))
```

Modo automatizado (para futuro script):

```cmd
dotnet run --project tools\EmtKeyGen -- "cliente@exemplo.com" 365
```

### 2.3. Enviar para o cliente

Email padrão:

> Olá [Nome],
>
> Obrigado pela compra do FerramentaEMT! Sua licença está pronta.
>
> 1. Instale o plugin baixando o instalador em [link interno do drive].
> 2. Abra o Revit 2025 → aba **Ferramenta EMT** → painel **Licença** →
>    botão **Ativar Licença**.
> 3. Cole abaixo a chave abaixo no campo "Chave":
>
> ```
> eyJlIjoi...long-base64...HMAC...
> ```
>
> 4. Clique em **Ativar**. Pronto — válido até 13/04/2027.
>
> Qualquer dúvida estou à disposição.
> ALEF / EMT

### 2.4. Renovação anual

Quando faltar ~30 dias para vencer, o cliente vai começar a ver a janela de
ativação. Repita os passos 2.1 → 2.3 com nova validade. A chave anterior vira
automaticamente "Expirada" — basta o cliente colar a nova.

---

## 3. Suporte: cliente trocou de máquina ou reinstalou Windows

A chave fica amarrada ao `MachineGuid` (que muda quando o Windows é reinstalado)
e ao nome de usuário. Se o cliente trocar qualquer um dos dois, ele vê:

> "Esta licença foi ativada em outra máquina. Entre em contato para liberar
>  reativação."

Procedimento:
1. O cliente abre `Ribbon → Licença → Sobre` e te manda um print mostrando o
   campo **ID desta máquina** (16 caracteres hex).
2. Você confirma com ele que é o mesmo cliente (email da venda original).
3. Você gera **uma chave nova** com o mesmo email e envia. Não precisa fazer
   nada com a chave antiga — ela continua "morta" naquela outra máquina.
4. Anote no seu controle: nome do cliente + número de reativações.
   Sugestão: limite informal de 1-2 reativações por ano antes de fazer
   perguntas. Isso evita que alguém compartilhe a licença com 5 colegas.

---

## 4. Estados possíveis da licença

| Status         | Quando aparece                                       | Plugin libera? |
|----------------|------------------------------------------------------|----------------|
| `Valid`        | Chave paga, dentro do prazo, mesma máquina           | ✅ Sim         |
| `Trial`        | Primeiros 14 dias após instalar                      | ✅ Sim         |
| `Expired`      | Chave passou da data de expiração                    | ❌ Não         |
| `TrialExpired` | Trial de 14 dias esgotou e nunca ativou paga         | ❌ Não         |
| `NotActivated` | Estado inicial transitório (vira `Trial` rapidinho)  | ❌ Não         |
| `Tampered`     | Arquivo de licença foi adulterado/corrompido         | ❌ Não         |
| `WrongMachine` | A licença foi gerada/ativada noutra máquina          | ❌ Não         |

---

## 5. Onde os arquivos ficam (no PC do cliente)

```
%LocalAppData%\FerramentaEMT\license\
├── emt.lic   ← chave + fingerprint, criptografada DPAPI (só lida pelo usuário)
└── emt.trl   ← data de início do trial, criptografada DPAPI

%LocalAppData%\FerramentaEMT\logs\
└── emt-YYYYMMDD.log   ← logs (Serilog, retenção 30 dias)
```

Apagar `emt.lic` reseta a licença. Apagar `emt.trl` **não** reinicia o trial —
o sistema vai detectar que existe um histórico ausente e tratar como
`NotActivated`. Para resetar trial completamente, basta apagar a pasta inteira
`license\` (ALEF pode instruir o cliente a fazer isso só em caso de teste).

---

## 6. Segurança / proteção contra pirataria

O sistema **não é à prova de cracker dedicado** (nada offline é). Mas é
suficiente para impedir compartilhamento casual de chave porque:

1. **HMAC-SHA256 com secret hardcoded** → cliente não consegue forjar uma chave
   nova. Para "criar" uma chave, ele teria que fazer engenharia reversa do
   plugin para extrair o secret e usar o algoritmo certo. Trabalho real.
2. **Amarração de máquina via MachineGuid + usuário** → mandar a chave para o
   colega não funciona, porque o `LicenseStore.LoadLicense()` valida que o
   `fingerprint` salvo bate com o atual.
3. **DPAPI (CurrentUser)** no arquivo → outro usuário Windows na mesma máquina
   precisa de licença separada. Ninguém consegue copiar `emt.lic` para outra
   máquina e descriptografar.
4. **Timestamp de expiração no payload** → não é sobrescrevível sem invalidar
   o HMAC.

Para reforçar no futuro (opcional):
- Adicionar verificação online ocasional (se internet disponível) para revogar
  chaves vazadas.
- Adicionar limite de N ativações por chave (precisa de servidor).
- Misturar mais hardware no fingerprint (CPU ID, MAC) — porém quanto mais
  hardware, mais reativações por troca de peça.

---

## 7. Trocar o secret (situação rara — se o secret vazar)

O secret está em: `FerramentaEMT/Licensing/KeySigner.cs`, constante `Secret`.

1. Gere novo valor (PowerShell):
   ```powershell
   [System.Convert]::ToBase64String((1..32 | %{ Get-Random -Min 0 -Max 256 } | %{ [byte]$_ }))
   ```
2. Substitua a constante no arquivo.
3. Recompile o plugin **e** o EmtKeyGen.
4. Distribua a nova versão do plugin para TODOS os clientes ativos.
5. Gere novas chaves para todos eles (script de mass-renewal — pode ser feito
   depois com o `EmtKeyGen` em batch).

⚠️ **Trocar o secret invalida 100% das licenças em uso. Faça apenas em caso de
incidente real de vazamento.**

---

## 8. Checklist antes da primeira venda

- [ ] Trocar `Secret` em `KeySigner.cs` por valor aleatório real (NÃO deixar o
      placeholder `"EMT-PROD-SECRET-CHANGE-BEFORE-FIRST-SALE-2026-ALEF"`).
- [ ] Compilar `dotnet build -c Release` sem erros.
- [ ] Compilar EmtKeyGen: `dotnet build tools\EmtKeyGen -c Release`.
- [ ] Gerar uma chave de teste para o seu próprio email com 7 dias de validade.
- [ ] Instalar o plugin numa máquina virtual / outro PC.
- [ ] Ativar com a chave de teste — confirmar que aparece "Licença ativada".
- [ ] Tentar mover `emt.lic` para outro PC — deve dar `Tampered` ou
      `WrongMachine`.
- [ ] Configurar Hotmart/Kiwify com email automático apontando para você.
- [ ] Preparar template do email de boas-vindas + entrega de chave.
- [ ] Backup do código atual no git com tag `v1.0.0`.

---

ALEF / EMT — Abril 2026
