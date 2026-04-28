# ADR 009: Code signing parametrizado via signtool + GitHub Actions secrets

- Status: aceita (skeleton). Ativacao end-to-end pendente do `.pfx`
  do Alef.
- Data: 2026-04-28
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: AUDITORIA-MERCADO-2026-04-27.md (item P0.1),
  ADR-006 (auto-update — define quem entrega o setup.exe ao usuario),
  ADR-007/008 (mesmo padrao de Operational Runbook)

## Contexto

A AUDITORIA-MERCADO-2026-04-27.md item P0.1 identificou que o
`setup.exe` da FerramentaEMT eh entregue **nao-assinado**, e o Windows
SmartScreen mostra a tela vermelha "Aplicativo nao reconhecido" em
~30-50% dos PCs (comportamento dependente da reputation do binario,
mais agressivo em PCs novos).

Em vendas:
- Cliente menos tecnico interpreta como virus → desistencia imediata.
- Mesmo cliente tecnico se incomoda — primeiro contato com produto
  comercial pago vira friction.
- Sem assinatura, **nao podemos aparecer em web stores corporativas**
  (alguns clientes empresariais exigem cert valido pra rodar `.exe`).

Esta ADR documenta a infraestrutura introduzida na PR-5 pra resolver
P0.1, mesmo antes de ter o certificado fisico em maos.

## Decisao

Implementar code signing como **skeleton parametrizado**:

1. **`Build-SetupExe.ps1`** ganha passo de `signtool sign` opcional,
   ativado por env var `EMT_CODESIGN_CERT_PFX`. Sem env var: warning
   + setup.exe nao-assinado (modo dev). Com env var: assinatura
   automatica + falha rapida se algo errado.
2. **`.github/workflows/release.yml`** novo, com signing condicional
   via secrets `CODESIGN_CERT_BASE64` + `CODESIGN_CERT_PASSWORD`.
   Falha rapido se secrets ausentes (release oficial NAO publica
   nao-assinado).
3. **`docs/CODE-SIGNING.md`** com guia operacional completo: como
   comprar, exportar, configurar local + CI, troubleshooting.
4. **`.gitignore`** reforcado com `*.pfx`, `*.p12`, `*.cer.password`
   — paridade com `license.secret`/`sentry.dsn`/`posthog.apikey`.

Esta abordagem desacopla **infraestrutura** de **aquisicao do cert** —
Alef pode pagar e receber o `.pfx` em paralelo, e a ativacao
end-to-end eh apenas: configurar env var local + 2 GitHub Secrets +
re-rodar `Gerar-Setup.bat`.

### Por que NAO outras alternativas

| Alternativa | Razao para descarte |
|---|---|
| **AzureSignTool / cloud signing** | Adiciona dep de Azure Key Vault + custo recorrente (~U$ 5/mes). Nao temos Azure ja configurado e o ganho de "cert no cloud" nao compensa pra single-developer team. |
| **SignPath** | Servico managed especializado (~U$ 100-500/mes). Vale pra empresas regulamentadas; over-engineering pra v1.7.0. |
| **Self-signed cert** | Browsers e SmartScreen rejeitam por design — nao resolve P0.1. So serve pra dev interno (e mesmo nesse caso, eh ruido). |
| **signtool.exe local + env var** (esta ADR) | Solucao mais simples possivel: 1 var de ambiente + 1 secret. Reusa Windows SDK ja presente em runners GitHub-hosted. Auditavel: 90 linhas em PS1 + 140 em workflow YAML. |

### Por que `signtool` em vez de outras CLIs

| Ferramenta | Razao |
|---|---|
| **signtool.exe** (Windows SDK) | Adotado. Ja vem com Visual Studio / Windows SDK. Suporta SHA256 digest + timestamp RFC 3161. |
| **osslsigncode** | Linux-friendly, mas nosso CI eh windows-latest. signtool eh nativo. |
| **AzureSignTool CLI** | Wrapper de signtool com Key Vault — descartado conforme tabela acima. |

### Por que timestamp eh obrigatorio

Sem `/tr`: certificado expirado **invalida retroativamente** todos os
binarios assinados. Usuarios com SmartScreen ativo recebem aviso
vermelho de repente.

Com `/tr http://timestamp.digicert.com /td sha256`: assinatura recebe
**counter-signature** com data atual fixada por servidor de timestamp.
Cert expira → binarios assinados antes da expiracao continuam validos
indefinidamente (Windows valida o timestamp, nao a data atual).

Servidores de timestamp validos:
- `http://timestamp.digicert.com` (default — DigiCert).
- `http://timestamp.sectigo.com`.
- `http://timestamp.globalsign.com/?signature=sha2`.
- `http://tsa.starfieldtech.com`.

Configuracao: env var `EMT_CODESIGN_TIMESTAMP_URL` (default DigiCert).

### Auto-discovery de signtool

`signtool.exe` esta em `%ProgramFiles(x86)%\Windows Kits\10\bin\<sdk-version>\<arch>\signtool.exe`.
Multiple SDK versions podem estar instaladas; nosso PS1 procura a mais
recente (sort descending) preferindo x64 > x86.

Override manual: `EMT_CODESIGN_SIGNTOOL` com path absoluto.

## Layout das mudancas

```
FerramentaEMT/installer/Build-SetupExe.ps1   (modificado: +90 linhas)
.github/workflows/release.yml                (novo: 143 linhas)
docs/CODE-SIGNING.md                         (novo: guia operacional)
docs/ADR/009-code-signing.md                 (este ADR)
.gitignore                                   (reforcado: *.pfx, *.p12)
```

Zero mudanca em codigo C# / testes — PR-5 eh exclusivamente infra.

## Operational Runbook

### Como Alef compra o certificado

| CA | Preco/ano OV | Preco/ano EV | Notas |
|---|---|---|---|
| **Sectigo** | R$ 600-900 | R$ 1.500-2.500 | Mais barato. Burocracia razoavel. |
| **DigiCert** | R$ 1.500 | R$ 3.000-5.000 | Mais reconhecido. SmartScreen reputation rapida. |
| **SSL.com** | R$ 800-1.200 | R$ 1.800-3.000 | Boa relacao custo-beneficio. |
| **GlobalSign** | R$ 1.000-1.500 | R$ 2.500-4.000 | Bom suporte LATAM. |

**Recomendacao v1.7.0:** Sectigo OV. Migrar pra EV em v2.0.0 se
houver escala (>500 downloads/mes).

Documentos exigidos: CNPJ + contrato social (PJ) ou CPF + RG (PF) +
comprovante de endereco + telefone validavel + email corporativo.

Fluxo: 3-7 dias uteis (OV) ou 1-3 semanas (EV — exige token fisico).

### Como configurar o cert apos receber

1. Exportar `.pfx` do Cert Store (ver `docs/CODE-SIGNING.md` §3) ou
   usar o `.pfx` recebido por email da CA.
2. Local (Alef): `setx EMT_CODESIGN_CERT_PFX "C:\certs\emt.pfx"` +
   `setx EMT_CODESIGN_CERT_PASSWORD "..."`.
3. CI: codificar em base64 (`base64 -w0`) e colar como secret
   `CODESIGN_CERT_BASE64`. Senha como `CODESIGN_CERT_PASSWORD`.
4. Validar localmente: `Gerar-Setup.bat` deve logar
   `[Signing] OK — setup.exe assinado com timestamp ...`.
5. Validar CI: `gh workflow run release.yml --ref main` deve passar
   o step "Verify signature" com `Successfully verified`.

### Como renovar antes de expirar

1. Comprar novo cert na mesma CA (~30 dias antes do vencimento).
2. Atualizar env vars locais + GitHub Secrets.
3. Gerar release nova com cert novo.
4. Binarios antigos continuam validos pelo timestamp counter-signature
   — nao precisa republicar releases anteriores.

### Como responder a vazamento

1. **Imediato:** revogar via portal da CA.
2. Comprar cert novo.
3. Atualizar env vars + GitHub Secrets.
4. Re-emitir release com cert novo.
5. Binarios antigos VIRAM invalidos via OCSP — clientes Windows
   detectam em background ao re-executar o `.exe`.

### Como desativar signing em emergencia

```cmd
set EMT_CODESIGN_CERT_PFX=
Gerar-Setup.bat
```

PS1 detecta ausencia + emite warning + gera setup.exe nao-assinado.
NAO publicar em release — apenas dev local pra testes.

## Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| Cert vaza por commit acidental | `.gitignore` exclui `*.pfx`, `*.p12`. ADR-009 lista como red line. `git diff --cached` antes de cada commit. CI runner deleta PFX no `if: always()` step. |
| Cert vaza por base64 logado | `CODESIGN_CERT_BASE64` eh secret do GitHub — masked nos logs. Workflow nunca imprime `$env:CODESIGN_CERT_BASE64` (apenas usa em decode + delete). |
| Senha errada → signtool falha tarde | Workflow valida via `Get-PfxCertificate` ANTES de chamar signtool. Falha rapida com mensagem clara. |
| signtool nao encontrado | Auto-discovery em Windows Kits 10. Override via `EMT_CODESIGN_SIGNTOOL`. Fallback warning no PS1 que documenta como instalar Windows SDK. |
| Timestamp server fora do ar | DigiCert eh ~99.9% uptime. Fallbacks documentados em `docs/CODE-SIGNING.md` §6 (Sectigo, GlobalSign, etc). Override via `EMT_CODESIGN_TIMESTAMP_URL`. |
| SmartScreen reputation lenta com OV | Esperar — apos ~500-1000 downloads SmartScreen aprende. EV nao tem essa janela mas custa 2-3x mais. Re-avaliar EV em v2.0.0. |
| Cert expira sem renovar | Timestamp em todos os binarios garante validade indefinida dos releases ja publicados. Renovacao manual com 30 dias de antecedencia (todo no calendario do Alef). |
| Workflow release.yml roda em PR de outsider sem secrets | GitHub Secrets nao sao expostos a PRs de fork por design. Workflow falha rapido em "Validate signing secrets" — comportamento esperado. |
| Tag re-pushada nao re-dispara release.yml | GitHub trata `tag push` como evento idempotente. Se precisar re-publicar, deletar tag remota + re-criar (raro). |
| signtool retorna 0 mas binario nao tem assinatura valida | Step "Verify signature" usa `signtool verify /pa /v` — falha se assinatura ausente, hash quebrado, root nao confiavel, ou timestamp invalido. Defesa em profundidade. |

## Validacao

- Build local Release + CI=true: 0 erros, 0 avisos (PS1 nao toca csproj).
- `dotnet test`: 716/716 verde (skeleton nao toca codigo C#).
- `Gerar-Setup.bat` em modo dev (sem env var): warning visivel,
  setup.exe nao-assinado, ainda funcional pra Alef testar localmente
  com Revit.

End-to-end (workflow CI com secrets reais) NAO eh validavel ate
Alef configurar `CODESIGN_CERT_BASE64` + `CODESIGN_CERT_PASSWORD`.

## Consequencias

### Positivas

- Infraestrutura pronta — Alef pode comprar o cert e ativar em
  ~30 minutos (configurar env vars + secrets + dispatch workflow).
- Auditavel: todas as decisoes em `docs/CODE-SIGNING.md` + este ADR.
- Backward compat: dev local sem cert continua gerando setup.exe
  funcional (apenas com warning de "nao-assinado").
- Defense in depth: 4 layers de protecao (gitignore, secret masking,
  workflow PFX cleanup, signtool verify final).

### Neutras

- `Build-SetupExe.ps1` cresce de 129 → 219 linhas (~70% das adicoes
  sao comentarios + auto-discovery de signtool).
- Novo workflow `.github/workflows/release.yml` (143 linhas) — nao
  auto-dispara sem tag push ou manual dispatch.

### Negativas

- Sem o `.pfx` real, P0.1 NAO esta resolvido. Skeleton nao protege
  usuarios de SmartScreen — apenas prepara o terreno.
- Custo recorrente: R$ 600-2.500/ano (Sectigo OV ate DigiCert OV).

## Rollback

Se precisarmos remover code signing inteiro:

1. Reverter `Build-SetupExe.ps1` (apenas o bloco entre os comentarios
   `=== v1.7.0 (PR-5 P0.1)` — ~80 linhas).
2. Deletar `.github/workflows/release.yml`.
3. Limpar `.gitignore` das linhas `*.pfx`/`*.p12`/`*.cer.password`.
4. Remover env vars locais (`EMT_CODESIGN_*`).
5. Deletar GitHub Secrets `CODESIGN_*`.
6. `docs/CODE-SIGNING.md` + este ADR ficam como historia.

Reset eh trivial — todas as mudancas sao aditivas.

## Referencias

- AUDITORIA-MERCADO-2026-04-27.md item P0.1 (origem desta decisao).
- ADR-006 (auto-update — quem entrega o setup.exe ao usuario,
  consume o resultado de signing).
- ADR-007/008 (mesmo padrao de Operational Runbook).
- [docs/CODE-SIGNING.md](../CODE-SIGNING.md) — guia operacional completo.
- [Microsoft signtool documentation](https://learn.microsoft.com/windows/win32/seccrypto/signtool).
- [SmartScreen reputation overview](https://learn.microsoft.com/windows/security/threat-protection/microsoft-defender-smartscreen/microsoft-defender-smartscreen-overview).
