# ADR 011: Licenca assimetrica (ECDsa P-256 / SHA-256) — cutover do HMAC

- Status: aceita
- Data: 2026-05-30
- Autores: Alef Vieira (com Claude)
- Contexto relacionado: `docs/audits/AUDITORIA-PROFUNDA-2026-05-30.md` §3.1,
  `docs/audits/PROMPT-LICENCA-ASSIMETRICA.md`, ADR-007 (crash reporting),
  ADR-008 (telemetria), ADR-009 (code signing)

## Contexto

Ate v2.8.8 inclusive, as licencas usavam HMAC-SHA256 (simetrico): o mesmo
segredo que assinava (no `EmtKeyGen`, executado pelo Alef) era usado pra
validar (dentro do plugin distribuido). Como o segredo precisava estar
disponivel no processo Revit do cliente — resolvido por
`LicenseSecretProvider` (env -> `%LOCALAPPDATA%\SteelBIM\license.secret`
-> arquivo adjacente -> fallback DEV_ONLY) — qualquer cliente com acesso
de disco ao seu proprio computador podia extrair o segredo e **forjar
licencas ilimitadas**, expirando quando quisesse, com qualquer email.

A auditoria profunda de 2026-05-30 (§3.1) marcou isso como o problema
P0 do sistema de licenca: a defesa estava no lugar errado (segredo de
verificacao = segredo de emissao). Modelos comerciais sao construidos em
cima da premissa de que o produtor controla a emissao das licencas — com
HMAC simetrico essa premissa cai.

## Decisao

Migrar para **assinatura assimetrica ECDsa sobre curva NIST P-256
(secp256r1) com hash SHA-256**, em cutover limpo, sem retrocompatibilidade
HMAC. Apenas o Alef e o Victor tem licencas ativas hoje; ambos reemitem
a chave apos o cutover.

### Modelo:
- **Chave privada (ECDsa P-256, PKCS#8 base64)**: fica SO com o produtor.
  Resolvida pelo `LicensePrivateKeyProvider` na ordem env
  `STEELBIM_LICENSE_PRIVATE_KEY` -> `%LOCALAPPDATA%\SteelBIM\license.private.key`
  -> arquivo ao lado do `EmtKeyGen.exe`. NUNCA entra no repo.
- **Chave publica (SubjectPublicKeyInfo, DER base64)**: embarcada no
  plugin como `const string` em `SteelBIM/Licensing/LicenseKeys.cs`.
  Versionavel e segura: SO VERIFICA assinaturas, nao gera novas.

### Token (formato preservado):
`<payload-base64url>.<signature-base64url>` — mesma estrutura compacta
de antes. So mudou o algoritmo: HMAC-SHA256 sai, ECDSA-SHA256 entra. O
`LicenseService` e o WPF de ativacao nao mudam (callam
`KeySigner.Verify(token)` igual antes).

### Geracao do par (somente uma vez):
`dotnet run --project tools\EmtKeyGen -- genkeypair` -> imprime publica
SPKI e privada PKCS#8 em base64. Cola a publica em `LicenseKeys.cs`
(commit), guarda a privada na env (NUNCA commit).

### Reemissao das licencas ativas:
Apos o cutover, `dotnet run --project tools\EmtKeyGen --
"alefchristiangomesvieira@gmail.com" 3650` (mesma sintaxe de antes,
agora gera token assinado com ECDsa).

## Alternativas consideradas

1. **Manter HMAC + ofuscacao do segredo** (rejeitado): qualquer ofuscacao
   no plugin distribuido eh decifravel; debugger anexado le memoria. Nao
   resolve o vetor de ataque.
2. **HMAC + servidor de validacao online** (rejeitado): exige internet
   no Revit do cliente. Plugin e' offline-first por contrato.
3. **RSA-2048 em vez de ECDsa P-256** (rejeitado): chave publica RSA tem
   ~270 bytes (base64); ECDsa P-256 tem ~91 bytes. Performance de verificacao
   ECDsa eh ~10x mais rapida que RSA-2048. Mesmo nivel de seguranca classica
   (~128 bits). Para o tamanho do payload tipico, sem trade-off significativo
   alem do tamanho da publica.
4. **Ed25519** (considerado mas rejeitado): suporte BCL chegou estavel
   em .NET 8, mas `ECDsa.Create(ECCurve.NamedCurves.nistP256)` tem
   superficie de API mais maduras e melhor suporte cross-version. Para
   o caso de uso (1-2 chaves por ano), nao compensa o risco.

## Consequencias

### Positivas
- Cliente com acesso fisico ao plugin ja **nao consegue mais forjar
  licencas**: o plugin so possui a chave publica.
- O ataque agora exige obter a **chave privada** do Alef — fora do
  modelo de ameaca do plugin (e' problema de seguranca operacional
  do Alef, igual a senha do GitHub).
- Token mantem mesmo formato `payload.sig` -> nenhuma mudanca no
  `LicenseService`, na UI de ativacao, no fluxo de armazenamento.
- `LicenseSecretProvider` continua vivo (usado por
  `SentryDsnProvider`); migracao foi cirurgica.

### Negativas / cuidados
- **Perda da chave privada do Alef** (HD corrompido sem backup) =
  perda da capacidade de emitir novas licencas. Mitigacao: manter
  backup criptografado da privada em local seguro (ex.: gerenciador
  de senhas, drive criptografado, papel impresso em cofre).
- **ECDsa eh randomizado** (diferente de HMAC): `SignData` produz
  assinatura diferente a cada chamada. Removemos o teste
  `Sign_is_deterministic_for_same_payload` que era valido em HMAC.
- **Cutover hard**: nenhuma licenca emitida em HMAC continua valida.
  Os dois usuarios (Alef + Victor) precisam reemitir.

## Testes

- Roundtrip Sign->Verify (`KeySignerTests.Sign_then_Verify_returns_equivalent_payload`)
- Tamper no payload (`Verify_returns_null_when_payload_is_tampered`)
- Tamper na assinatura (`Verify_returns_null_when_signature_is_tampered`)
- Garbage input (`Verify_returns_null_for_garbage_input`)
- Null payload throws (`Sign_throws_on_null_payload`)
- **NOVO**: chave publica errada rejeita (`Verify_ComChavePublicaErrada_RetornaNull`)
  — valida que assimetria funciona (par publico errado -> rejeita).

Todos os testes usam `LicenseTestKeys.InstallEphemeral()` para instalar
um par ECDsa efemero nos seams internos (`LicenseKeys.TestOverride*` +
`LicensePrivateKeyProvider.TestOverride*`), evitando dependencia da
chave de producao no CI.

## Operacional

Quando precisar gerar uma chave nova pra cliente:
1. `set STEELBIM_LICENSE_PRIVATE_KEY=<pkcs8_base64_da_sua_chave>` (ou ja
   ter `%LOCALAPPDATA%\SteelBIM\license.private.key` populado uma vez).
2. `dotnet run --project tools\EmtKeyGen -- "email@cliente.com" 365`
3. Copiar o token impresso e enviar pro cliente. Ele ativa via Ribbon ->
   Licenca -> Ativar Licenca como sempre.

Se um dia precisar **rotacionar** a chave de producao (suspeita de
vazamento, troca de equipamento, etc.):
1. Gerar novo par via `genkeypair`.
2. Atualizar `LicenseKeys.cs` -> `PublicKeySpkiBase64` com a nova publica.
3. Commit + nova release do plugin.
4. Reemitir todas as licencas ativas com a nova privada. Apos os
   clientes atualizarem, as licencas antigas param de funcionar.
