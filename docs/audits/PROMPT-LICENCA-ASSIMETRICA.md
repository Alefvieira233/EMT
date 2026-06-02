# Prompt — Migração da licença para assinatura ASSIMÉTRICA (ECDsa P-256)

> **Como usar:** cole TODO este arquivo como prompt no seu Claude Code (Windsurf),
> que tem build real e pode compilar/testar/auto-corrigir. Ele contém o design, o
> conteúdo exato dos arquivos e os passos de verificação. A parte que **só você**
> pode fazer (gerar o par de chaves) está na seção 7.

---

## 0. Contexto e objetivo

Hoje a licença usa **HMAC-SHA256 (simétrico)**: o segredo que *verifica* a chave é o
mesmo que a *assina*, e ele precisa estar dentro do plugin distribuído. Logo, qualquer
um com o plugin instalado pode extrair o segredo do próprio disco e **forjar licenças
ilimitadas**. (Auditoria: `docs/audits/AUDITORIA-PROFUNDA-2026-05-30.md` §3.1.)

**Objetivo:** trocar para **assinatura assimétrica ECDsa P-256 / SHA-256** (BCL pura,
sem dependência NuGet nova):
- **EmtKeyGen** assina com a **chave privada** (fica só com o produtor, via env/arquivo, nunca no repo).
- **Plugin** verifica com a **chave pública** embarcada (segura para versionar).

**Cutover limpo, sem retrocompatibilidade HMAC** — só o Alef e o Victor têm licença
hoje; eles reemitem as 2 chaves. Não há risco de travar terceiros.

## 1. Restrições importantes (NÃO violar)

1. **NÃO apague nem altere `SteelBIM/Licensing/LicenseSecretProvider.cs`.** Ele ainda é
   usado por `SteelBIM/Infrastructure/CrashReporting/SentryDsnProvider.cs` (resolução de
   arquivo de segredo do DSN). Só remova suas referências em `App.cs` e `KeySigner.cs`.
2. **Atualize TODOS os consumidores de `KeySigner`.** Confirmados: `KeySignerTests.cs` e
   `SmokeTests.cs` fazem roundtrip Sign/Verify usando o env-secret HMAC. **Faça uma busca
   global** por `KeySigner.Sign`, `KeySigner.Verify` e `LicenseSecretProvider.EnvVarName`
   nos testes e atualize cada setup para instalar um par de chaves efêmero (seção 6).
3. **ECDSA é randomizado:** `SignData` produz assinatura diferente a cada chamada. **Remova**
   qualquer teste que afirme determinismo (ex.: `Sign_is_deterministic_for_same_payload`).
4. Mantenha as assinaturas públicas `KeySigner.Sign(LicensePayload)` e
   `KeySigner.Verify(string)` para não mexer em `LicenseService.cs` nem no `EmtKeyGen`.
5. Branch de trabalho: `claude/great-turing-Vqlig` (mesmo PR #63). Compile em **Release**
   (`TreatWarningsAsErrors`) e rode `dotnet test` antes de commitar.

## 2. Novo arquivo: `SteelBIM/Licensing/LicenseKeys.cs`

```csharp
#nullable enable
using System;
using System.Security.Cryptography;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Chave PUBLICA de verificacao de licenca (ECDsa P-256 / SHA-256). Segura para
    /// versionar e embarcar: so VERIFICA, nao ASSINA. A privada fica so com o produtor
    /// (LicensePrivateKeyProvider) e NUNCA entra no repo. Ver ADR-011.
    /// </summary>
    public static class LicenseKeys
    {
        /// <summary>
        /// SubjectPublicKeyInfo (DER) da chave publica de PRODUCAO, em Base64.
        /// >>> SUBSTITUIR pelo output de `EmtKeyGen genkeypair` (secao 7). Enquanto for o
        ///     placeholder, Verify retorna null para qualquer chave (fail-closed). <<<
        /// </summary>
        public const string PublicKeySpkiBase64 = "COLE_AQUI_A_CHAVE_PUBLICA_DO_genkeypair";

        // Seam de teste: o projeto de testes COMPILA este arquivo no proprio assembly,
        // entao pode setar este campo internal. NAO e' nova superficie de ataque (um
        // atacante com reflection in-process ja venceria qualquer cheque). Producao nunca seta.
        internal static string? TestOverridePublicKeySpkiBase64;

        public static ECDsa CreatePublicKey()
        {
            string spki = TestOverridePublicKeySpkiBase64 ?? PublicKeySpkiBase64;
            ECDsa ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _);
                return ecdsa;
            }
            catch { ecdsa.Dispose(); throw; }
        }
    }
}
```

## 3. Novo arquivo: `SteelBIM/Licensing/LicensePrivateKeyProvider.cs`

```csharp
#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Resolve a chave PRIVADA de assinatura (ECDsa P-256, PKCS#8 base64). Usado SO pelo
    /// EmtKeyGen. Ordem: env STEELBIM_LICENSE_PRIVATE_KEY -> %LOCALAPPDATA%\SteelBIM\
    /// license.private.key -> arquivo ao lado do executavel. NUNCA commitar a privada.
    /// </summary>
    public static class LicensePrivateKeyProvider
    {
        public const string EnvVarName = "STEELBIM_LICENSE_PRIVATE_KEY";
        public const string KeyFileName = "license.private.key";

        internal static string? TestOverridePrivateKeyPkcs8Base64;

        public static ECDsa CreatePrivateKey()
        {
            string? pkcs8B64 = TestOverridePrivateKeyPkcs8Base64 ?? ResolvePkcs8Base64();
            if (string.IsNullOrWhiteSpace(pkcs8B64))
            {
                throw new InvalidOperationException(
                    "Chave privada de licenca nao configurada. Defina '" + EnvVarName
                    + "' (PKCS#8 base64) ou coloque '" + KeyFileName + "' em '%LOCALAPPDATA%\\SteelBIM\\' "
                    + "ou ao lado do executavel. Gere com 'EmtKeyGen genkeypair'.");
            }
            ECDsa ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(pkcs8B64!.Trim()), out _);
                return ecdsa;
            }
            catch { ecdsa.Dispose(); throw; }
        }

        private static string? ResolvePkcs8Base64()
        {
            string? env = SafeEnv(EnvVarName);
            if (!string.IsNullOrWhiteSpace(env)) return env!.Trim();
            foreach (string? path in new[] { LocalAppDataPath(), AssemblyAdjacentPath() })
            {
                string? content = SafeRead(path);
                if (!string.IsNullOrWhiteSpace(content)) return content!.Trim();
            }
            return null;
        }

        private static string? SafeEnv(string name)
        { try { return Environment.GetEnvironmentVariable(name); } catch { return null; } }

        private static string? LocalAppDataPath()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "SteelBIM", KeyFileName);
            }
            catch { return null; }
        }

        private static string? AssemblyAdjacentPath()
        {
            try
            {
                string? dir = Path.GetDirectoryName(typeof(LicensePrivateKeyProvider).Assembly.Location);
                return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir!, KeyFileName);
            }
            catch { return null; }
        }

        private static string? SafeRead(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return File.ReadAllText(path!); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch { return null; }
        }
    }
}
```

## 4. Reescrever `SteelBIM/Licensing/KeySigner.cs` (substituir o arquivo inteiro)

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Assina e valida chaves de licenca com assinatura ASSIMETRICA ECDsa P-256 / SHA-256.
    /// Token: &lt;payload-base64url&gt;.&lt;signature-base64url&gt;. Assinatura usa a chave PRIVADA
    /// (EmtKeyGen, via LicensePrivateKeyProvider); verificacao usa a PUBLICA embarcada
    /// (LicenseKeys). Ver docs/ADR/ADR-011-licenca-assimetrica.md.
    /// </summary>
    public static class KeySigner
    {
        /// <summary>Assina um payload com a chave privada resolvida (EmtKeyGen).</summary>
        public static string Sign(LicensePayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (ECDsa priv = LicensePrivateKeyProvider.CreatePrivateKey())
                return Sign(payload, priv);
        }

        /// <summary>Assina com uma chave privada explicita (testes).</summary>
        public static string Sign(LicensePayload payload, ECDsa privateKey)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            byte[] payloadBytes = Encoding.UTF8.GetBytes(SimpleJson.Serialize(payload));
            string payloadB64 = Base64Url.Encode(payloadBytes);
            byte[] sig = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256);
            return payloadB64 + "." + Base64Url.Encode(sig);
        }

        /// <summary>Valida o token com a chave PUBLICA embarcada (plugin). Null se invalido.</summary>
        public static LicensePayload Verify(string token)
        {
            ECDsa pub = SafeCreatePublicKey();
            if (pub == null) return null;
            using (pub) return Verify(token, pub);
        }

        /// <summary>Valida com uma chave publica explicita (testes). Null se invalido.</summary>
        public static LicensePayload Verify(string token, ECDsa publicKey)
        {
            if (string.IsNullOrWhiteSpace(token) || publicKey == null) return null;
            string trimmed = token.Trim();
            int dot = trimmed.IndexOf('.');
            if (dot <= 0 || dot >= trimmed.Length - 1) return null;

            byte[] payloadBytes;
            byte[] sig;
            try
            {
                payloadBytes = Base64Url.Decode(trimmed.Substring(0, dot));
                sig = Base64Url.Decode(trimmed.Substring(dot + 1));
            }
            catch { return null; }

            bool ok;
            try { ok = publicKey.VerifyData(payloadBytes, sig, HashAlgorithmName.SHA256); }
            catch { return null; }
            if (!ok) return null;

            try { return SimpleJson.Deserialize(Encoding.UTF8.GetString(payloadBytes)); }
            catch { return null; }
        }

        private static ECDsa SafeCreatePublicKey()
        {
            // Placeholder/chave invalida -> null -> fail-closed (nenhuma licenca valida).
            try { return LicenseKeys.CreatePublicKey(); }
            catch { return null; }
        }
    }
}
```

## 5. Edições

### `tools/EmtKeyGen/Program.cs`
- Adicionar `using System.Security.Cryptography;`.
- Logo no início do `Main`, antes do resto, tratar o subcomando `genkeypair`:

```csharp
if (args != null && args.Length >= 1 &&
    args[0].Equals("genkeypair", StringComparison.OrdinalIgnoreCase))
{
    using ECDsa ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    Console.WriteLine("=== CHAVE PUBLICA (cole em SteelBIM/Licensing/LicenseKeys.cs -> PublicKeySpkiBase64) ===");
    Console.WriteLine(Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo()));
    Console.WriteLine();
    Console.WriteLine("=== CHAVE PRIVADA (GUARDE EM SEGREDO; env STEELBIM_LICENSE_PRIVATE_KEY; NUNCA commitar) ===");
    Console.WriteLine(Convert.ToBase64String(ec.ExportPkcs8PrivateKey()));
    return 0;
}
```
- Remover o bloco que lê/loga `LicenseSecretProvider.GetResolvedSource()` (linhas ~30-44).
  `KeySigner.Sign(payload)` agora resolve a privada via env automaticamente.
- Atualizar o doc-comment do topo: o segredo HMAC virou par de chaves; documentar
  `genkeypair` e a env `STEELBIM_LICENSE_PRIVATE_KEY`.

### `tools/EmtKeyGen/EmtKeyGen.csproj`
- No `<ItemGroup>` de `<Compile Include>`, **remover** o link de `LicenseSecretProvider.cs`
  e **adicionar**:
```xml
    <Compile Include="..\..\SteelBIM\Licensing\LicenseKeys.cs">
      <Link>Linked\LicenseKeys.cs</Link>
    </Compile>
    <Compile Include="..\..\SteelBIM\Licensing\LicensePrivateKeyProvider.cs">
      <Link>Linked\LicensePrivateKeyProvider.cs</Link>
    </Compile>
```

### `SteelBIM/App.cs`
- Substituir o bloco que loga a fonte do segredo HMAC (`LicenseSecretProvider.GetResolvedSource()`):
```csharp
            // v2.8.9: licenca agora usa assinatura assimetrica (ECDsa). O plugin embarca
            // apenas a chave PUBLICA de verificacao — nao ha mais segredo HMAC a resolver.
            Logger.Info("[Licensing] verificacao de licenca por chave publica embarcada (ECDsa P-256)");
```
  (Remove a dependência de `LicenseSecretProvider` em `App.cs`. NÃO remova o `using SteelBIM.Licensing`.)

### `SteelBIM.Tests/SteelBIM.Tests.csproj`
- Junto dos outros `<Compile Include>` de Licensing, **adicionar**:
```xml
    <Compile Include="..\SteelBIM\Licensing\LicenseKeys.cs"
             Link="LinkedSources\Licensing\LicenseKeys.cs" />
    <Compile Include="..\SteelBIM\Licensing\LicensePrivateKeyProvider.cs"
             Link="LinkedSources\Licensing\LicensePrivateKeyProvider.cs" />
```
  (Mantenha o link de `LicenseSecretProvider.cs` — `LicenseSecretProviderTests` continua válido.)

## 6. Testes

### Novo: `SteelBIM.Tests/Licensing/LicenseTestKeys.cs`
```csharp
#nullable enable
using System;
using System.Security.Cryptography;
using SteelBIM.Licensing;

namespace SteelBIM.Tests.Licensing
{
    /// <summary>Instala um par ECDsa P-256 efemero nos seams de teste, para os testes
    /// assinarem/verificarem sem a chave de producao.</summary>
    internal static class LicenseTestKeys
    {
        public static void InstallEphemeral()
        {
            using ECDsa ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            LicenseKeys.TestOverridePublicKeySpkiBase64 =
                Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
            LicensePrivateKeyProvider.TestOverridePrivateKeyPkcs8Base64 =
                Convert.ToBase64String(ec.ExportPkcs8PrivateKey());
        }
    }
}
```

### `KeySignerTests.cs` e `SmokeTests.cs` (e qualquer outro que use KeySigner)
- Em cada teste/ctor que hoje faz
  `Environment.SetEnvironmentVariable(LicenseSecretProvider.EnvVarName, "...")` +
  `LicenseSecretProvider.ResetCacheForTests()` **para usar o KeySigner**, troque por uma
  única chamada `LicenseTestKeys.InstallEphemeral();` (e remova o try/finally do env-secret).
  As chamadas `KeySigner.Sign(payload)` / `KeySigner.Verify(token)` permanecem iguais.
- **Remover** `Sign_is_deterministic_for_same_payload` (ECDSA é randomizado).
- **Adicionar** um teste de rejeição por chave errada:
```csharp
[Fact]
public void Verify_ComChavePublicaErrada_RetornaNull()
{
    LicenseTestKeys.InstallEphemeral();
    string token = KeySigner.Sign(SamplePayload());
    using ECDsa outra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    KeySigner.Verify(token, outra).Should().BeNull();   // chave errada rejeita
    KeySigner.Verify(token).Should().NotBeNull();        // chave correta valida
}
```
- **NÃO** mexa em `LicenseSecretProviderTests.cs` (continua testando o provider, que segue vivo).

## 7. Geração do par de chaves (SÓ O ALEF — a privada nunca toca o repo)

Depois de aplicar o código acima e compilar o EmtKeyGen:

```bat
dotnet run --project tools\EmtKeyGen -- genkeypair
```
1. Copie a **CHAVE PUBLICA** impressa e cole em `LicenseKeys.cs` → `PublicKeySpkiBase64`
   (substituindo o placeholder). Commit dessa mudança.
2. Guarde a **CHAVE PRIVADA** em local seguro. Para emitir chaves, exponha-a como env:
   ```bat
   set STEELBIM_LICENSE_PRIVATE_KEY=<conteudo_pkcs8_base64>
   ```
   (ou salve em `%LOCALAPPDATA%\SteelBIM\license.private.key`). **Nunca** commitar.
3. Reemita as 2 licenças (Alef e Victor):
   ```bat
   dotnet run --project tools\EmtKeyGen -- "alefchristiangomesvieira@gmail.com" 3650
   dotnet run --project tools\EmtKeyGen -- "email-do-victor@..." 3650
   ```
   Ative cada uma no Revit (Ribbon → Licença → Ativar Licença).

## 8. Verificação e commit

```bat
dotnet build SteelBIM.Solution.sln -c Release   :: TreatWarningsAsErrors precisa passar
dotnet test SteelBIM.Tests/SteelBIM.Tests.csproj -c Debug
```
- Garanta que **todos os callers de KeySigner** foram migrados (busca global por
  `KeySigner.` e `LicenseSecretProvider.EnvVarName` nos testes).
- Commit em `claude/great-turing-Vqlig`; o PR #63 roda o CI (Windows + Nice3point).
- Crie `docs/ADR/ADR-011-licenca-assimetrica.md` documentando a decisão (ECDsa P-256,
  cutover limpo, chave pública embarcada, privada só no produtor, `genkeypair`).

## 9. Checklist de aceite

- [ ] `KeySigner` não referencia mais `LicenseSecretProvider` (mas o arquivo permanece p/ Sentry).
- [ ] Plugin compila com a chave pública real embarcada (não o placeholder).
- [ ] `dotnet build -c Release` (0 warnings) e `dotnet test` verdes.
- [ ] Token forjado/alterado e token de chave errada → `Verify` retorna null.
- [ ] As 2 licenças reemitidas ativam no Revit.
- [ ] Chave privada NUNCA aparece em `git status`/commits.
