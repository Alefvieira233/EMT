# Code Signing — guia operacional

> **Status:** infraestrutura pronta (PR-5 skeleton). Ativacao end-to-end
> espera o `.pfx` do Alef.

Este documento ensina como comprar, instalar e usar um certificado de
code signing pra que o `setup.exe` da FerramentaEMT seja aceito pelo
Windows SmartScreen sem o aviso vermelho de "Aplicativo nao reconhecido".

Decisoes arquiteturais estao em [`ADR-009`](ADR/009-code-signing.md). Este
guia foca em **como fazer**, nao em **por que**.

---

## 1. Por que precisamos de code signing

Quando alguem baixa o `setup.exe` e da duplo-clique:

- **Sem assinatura digital:** Windows SmartScreen mostra tela vermelha
  "Aplicativo nao reconhecido". Pra prosseguir, usuario precisa clicar em
  "Mais informacoes" → "Executar mesmo assim". Auditoria interna estima
  **30-50% de desistencia** nesse ponto.
- **Com assinatura EV (Extended Validation):** **zero aviso** ja na
  primeira execucao em qualquer PC.
- **Com assinatura OV (Organization Validation):** aviso amarelo
  inicialmente; some depois de algumas centenas de downloads (SmartScreen
  reputation building).

Para uma operacao comercial, OV minimo. EV se ha orcamento.

---

## 2. Como comprar o certificado

| CA | Preco/ano (OV) | Preco/ano (EV) | Notas |
|---|---|---|---|
| **Sectigo** (ex-Comodo) | ~R$ 600-900 | ~R$ 1.500-2.500 | Mais barato. Burocracia razoavel (CNPJ + ID emissor). |
| **DigiCert** | ~R$ 1.500 | ~R$ 3.000-5.000 | Mais reconhecido. SmartScreen reputation builda mais rapido. Suporte 24/7. |
| **SSL.com** | ~R$ 800-1.200 | ~R$ 1.800-3.000 | Boa relacao custo-beneficio. Suporte em PT-BR limitado. |
| **GlobalSign** | ~R$ 1.000-1.500 | ~R$ 2.500-4.000 | Boa pra empresas em LATAM. |

**Recomendacao para v1.7.0:** Sectigo OV. Custo aceitavel + burocracia
razoavel. EV vale a pena apenas a partir de v2.0.0 (escala).

**Documentos exigidos (CNPJ ou CPF como pessoa fisica):**

- CNPJ + contrato social (se PJ) ou CPF + RG (PF)
- Comprovante de endereco
- Telefone validavel (CA liga pra confirmar identidade)
- Email corporativo (gmail nao serve pra OV)

**Fluxo aproximado:**

1. Acessar site da CA, escolher "Code Signing OV" ou "EV".
2. Preencher dados + uploadar documentos.
3. Aguardar validacao (3-7 dias uteis pra OV; 1-3 semanas pra EV
   porque exige token fisico).
4. Receber email com instrucoes de download/instalacao.

---

## 3. Como exportar o `.pfx` do Windows Certificate Store

A maioria das CAs entrega o certificado por:
- Email com link de download (chave gerada no browser, baixada como `.pfx`).
- Token USB fisico (apenas EV — nao podemos exportar pra `.pfx`,
  signtool conecta direto no token).

Caso 1 (`.pfx` ja em maos): pular pra §4.

Caso 2 (cert ja instalado no Windows Cert Store, nao temos `.pfx`):

```powershell
# Lista certificados de assinatura instalados:
Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert

# Exporta um especifico (substituir thumbprint):
$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Thumbprint -eq "ABCD1234..." }

# IMPORTANTE: chave privada exportavel exige -ExportPolicy permitir.
# Se a CA emitiu non-exportable, voce NAO conseguira exportar — vai
# precisar revoga-lo e re-emitir com policy diferente.
$pwd = Read-Host -AsSecureString -Prompt "Senha do .pfx"
Export-PfxCertificate -Cert $cert -FilePath "C:\certs\emt-codesign.pfx" -Password $pwd
```

**Senha forte obrigatoria** — minimo 16 caracteres, mistura
alfanumerica + simbolos. Esta senha eh o segundo fator do certificado.

---

## 4. Como configurar localmente (modo dev)

Apos ter `emt-codesign.pfx` em maos, configure via env vars do sistema:

```cmd
:: Definir como variaveis de ambiente do USUARIO (persistem em logon):
setx EMT_CODESIGN_CERT_PFX "C:\certs\emt-codesign.pfx"
setx EMT_CODESIGN_CERT_PASSWORD "sua-senha-forte-aqui"
```

Ou em PowerShell (sessao atual + persistente):

```powershell
[Environment]::SetEnvironmentVariable("EMT_CODESIGN_CERT_PFX", "C:\certs\emt-codesign.pfx", "User")
[Environment]::SetEnvironmentVariable("EMT_CODESIGN_CERT_PASSWORD", "sua-senha-forte-aqui", "User")
```

**REABRIR terminais/CMD apos `setx`** — a env var so eh herdada por
processos novos.

**Validacao:** rodar `Gerar-Setup.bat` e procurar a linha:

```
[Signing] Certificate found at C:\certs\emt-codesign.pfx. Signing setup.exe via ...\signtool.exe...
[Signing] OK — setup.exe assinado com timestamp http://timestamp.digicert.com
```

Caso queira testar a signature do executavel gerado:

```powershell
# Verificar via signtool (signtool.exe deve estar no PATH ou em Windows Kits 10):
& "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" verify /pa /v `
    "FerramentaEMT\artifacts\installer\FerramentaEMT-Revit2025-Setup.exe"
```

Output esperado: "Successfully verified" + cadeia de certificados validos.

---

## 5. Como configurar GitHub Actions (CI release)

A workflow `.github/workflows/release.yml` consome 2 secrets:

- `CODESIGN_CERT_BASE64` : `.pfx` codificado em base64.
- `CODESIGN_CERT_PASSWORD` : senha do `.pfx`.

**Codificar o `.pfx` em base64:**

```powershell
# Windows PowerShell (linha unica, sem quebras):
[Convert]::ToBase64String([System.IO.File]::ReadAllBytes("C:\certs\emt-codesign.pfx")) |
    Set-Clipboard
```

```bash
# Linux/macOS/WSL:
base64 -w0 cert.pfx | xclip -selection clipboard  # copia direto
# ou
base64 -w0 cert.pfx > cert.pfx.base64.txt          # arquivo
```

**Configurar no GitHub:**

1. Repo → Settings → Secrets and variables → Actions → New repository secret.
2. Nome: `CODESIGN_CERT_BASE64`. Valor: o blob base64 (clipboard).
3. Repetir pra `CODESIGN_CERT_PASSWORD`.
4. (Opcional) `CODESIGN_TIMESTAMP_URL` se quiser trocar de DigiCert.

**Disparar a workflow:**

```bash
# Manual:
gh workflow run release.yml --ref main

# Automatico (quando bumpa versao):
git tag v1.7.0 && git push origin v1.7.0
```

A workflow:
- Decodifica base64 em `.pfx` runner-local (`$RUNNER_TEMP`).
- Valida senha via `Get-PfxCertificate`.
- Roda `Gerar-Setup.bat` com env var apontando pro `.pfx`.
- Verifica assinatura via `signtool verify /pa /v`.
- Limpa o `.pfx` do runner (always block — mesmo em failure).
- Sobe `setup.exe + checksums.txt` como artifact.

---

## 6. Operational Runbook

### 6.1 Renovar certificado expirando

`signtool sign /tr http://timestamp.digicert.com` adiciona um
**counter-signature** ao binario com data atual. Quando o cert expira:

- **Binario assinado COM timestamp:** continua valido indefinidamente
  (Windows valida o timestamp, nao a data atual).
- **Binario assinado SEM timestamp:** vira invalido na hora — usuarios
  com SmartScreen ativo recebem aviso vermelho retroativamente.

**SEMPRE assinar com timestamp.** Nosso PS1 ja faz por padrao
(`/tr http://timestamp.digicert.com`).

Renovacao do cert: comprar novo (~30 dias antes do vencimento), trocar
PFX em ambos os locais (env var local + GitHub Secret), gerar release
nova. Binarios antigos continuam validos pelo timestamp.

### 6.2 Vazamento de certificado

Se o `.pfx` ou senha vazaram (commit acidental, laptop roubado, etc.):

1. **Imediato:** revogar via portal da CA. Codigo de revogacao gera nova
   chave e invalida a antiga (via OCSP).
2. Comprar novo certificado.
3. Atualizar env vars locais + GitHub Secrets.
4. Re-emitir release com cert novo.
5. Binarios antigos assinados com cert revogado VIRAM invalidos
   gradualmente (clientes Windows verificam OCSP em background).

**Prevencao:**
- `.gitignore` ja exclui `*.pfx`, `*.p12`.
- NUNCA commitar nem em branch privada.
- `git diff --cached` antes de cada commit.

### 6.3 Setup nao-assinado em emergencia (modo dev)

Caso precisamos gerar setup.exe SEM assinar (testes locais, demo, ou
quando cert esta sendo renovado):

```cmd
:: Limpar env vars temporariamente:
set EMT_CODESIGN_CERT_PFX=
set EMT_CODESIGN_CERT_PASSWORD=
:: Rodar normal:
Gerar-Setup.bat
```

PS1 detecta ausencia + emite warning visivel + gera setup.exe nao-assinado.
**NAO publicar em release oficial** — SmartScreen vai bloquear.

### 6.4 Trocar de CA

Migracao Sectigo → DigiCert (ou similar):

1. Comprar cert novo na nova CA.
2. **NAO revogar o antigo** ate ter o novo testado.
3. Atualizar env vars + GitHub Secrets.
4. Gerar release pre-test.
5. Validar: `signtool verify /pa /v setup.exe` deve mostrar root da nova CA.
6. Apos primeira release pos-CA-troca estavel, deixar antigo expirar
   normalmente (binarios antigos continuam validos pelo timestamp).

### 6.5 Auditoria periodica (recomendada anualmente)

- Verificar data de expiracao do cert: `Get-PfxCertificate -FilePath ...`.
- Confirmar GitHub Secrets ainda configurados (apenas testar em
  release dispatch, nao expor o conteudo).
- Re-rodar workflow release.yml manual com versao tag temporaria
  (`v1.x.x-test`) pra validar que tudo funciona end-to-end.

---

## 7. Troubleshooting

| Sintoma | Causa provavel | Solucao |
|---|---|---|
| `Cannot find signtool.exe` | Windows SDK nao instalado | Instalar via Visual Studio Installer ("Windows 10/11 SDK") OU baixar [Windows SDK standalone](https://developer.microsoft.com/windows/downloads/windows-sdk/). Ou definir `EMT_CODESIGN_SIGNTOOL` apontando para o exe. |
| `signtool failed with code 1 (timestamp server unreachable)` | Firewall corporativo bloqueando timestamp.digicert.com | Trocar pra `http://timestamp.sectigo.com` ou `http://timestamp.globalsign.com/?signature=sha2`. |
| `signtool failed with code 2 (PFX parse error)` | Senha errada ou .pfx corrompido | Validar manualmente: `Get-PfxCertificate -FilePath cert.pfx -Password (Read-Host -AsSecureString)`. Se falhar, re-exportar do Cert Store. |
| `SmartScreen ainda bloqueia mesmo assinado` | Reputation building em progresso (cert OV) | Esperar — apos ~500-1000 downloads, SmartScreen aprende. EV nao tem esse problema. |
| `Workflow release.yml falha em "Decode certificate"` | `CODESIGN_CERT_BASE64` malformado | Re-codificar com `base64 -w0` (sem quebra de linha) e colar de novo. |
| Cert expirado | Timestamp ausente OU SmartScreen rejeitou | Confirmar timestamp: `signtool verify /v setup.exe` deve mostrar "The signature is timestamped". Se nao, re-assinar. |

---

## 8. Referencias

- [Microsoft signtool docs](https://learn.microsoft.com/windows/win32/seccrypto/signtool)
- [DigiCert code signing best practices](https://www.digicert.com/code-signing/)
- [SmartScreen reputation overview](https://learn.microsoft.com/windows/security/threat-protection/microsoft-defender-smartscreen/microsoft-defender-smartscreen-overview)
- [ADR-009: Code Signing](ADR/009-code-signing.md)
- [AUDITORIA-MERCADO-2026-04-27.md §P0.1](../AUDITORIA-MERCADO-2026-04-27.md)
