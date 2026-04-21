# Deployment — FerramentaEMT v1.5.0

> Guia para instalar o plugin numa maquina nova (desenvolvedor ou cliente).
> Para o fluxo de venda e ativacao de licenca, veja [SISTEMA-LICENCA.md](SISTEMA-LICENCA.md).

---

## 1. Pre-requisitos

| Requisito | Versao | Como verificar |
|-----------|--------|----------------|
| Windows | 10/11 x64 | `winver` |
| Autodesk Revit | 2025 | Tela inicial do Revit |
| .NET 8 SDK | 8.0+ | `dotnet --version` |

Se o .NET 8 SDK nao estiver instalado:

```cmd
:: Opcao 1: script incluso
Instalar-DotNet-SDK.bat

:: Opcao 2: manual via winget
winget install Microsoft.DotNet.SDK.8
```

---

## 2. Build e instalacao (desenvolvedor)

```cmd
:: 1. Clone o repositorio
git clone https://github.com/Alefvieira233/EMT.git
cd EMT

:: 2. Feche o Revit (obrigatorio)

:: 3. Execute o instalador
INSTALAR.bat
```

O script:
1. Verifica que `Revit.exe` nao esta em execucao
2. Compila em Release (`dotnet build -c Release`)
3. Gera o manifesto `FerramentaEMT.addin`
4. Copia binarios para `%AppData%\Autodesk\Revit\Addins\2025\`

Abra o Revit 2025 — a aba **EMT** aparece no ribbon.

---

## 3. Configuracao do secret HMAC (obrigatorio para licenciamento)

O sistema de licenca usa HMAC-SHA256. O secret **nunca** e incluido no
repositorio. Sem ele, qualquer tentativa de ativar/gerar licenca falhara com:

> `HMAC secret not configured. Set the environment variable 'EMT_LICENSE_SECRET'...`

### Opcoes de configuracao (em ordem de prioridade)

O `LicenseSecretProvider` resolve o secret na seguinte ordem — o primeiro
que responder ganha:

#### Opcao 1: Variavel de ambiente (recomendado para dev)

```cmd
:: Permanente (usuario atual)
setx EMT_LICENSE_SECRET "seu-secret-base64-aqui"

:: Temporario (sessao atual)
set EMT_LICENSE_SECRET=seu-secret-base64-aqui
```

Reinicie o Revit apos `setx` para que ele enxergue a variavel.

#### Opcao 2: Arquivo em %LOCALAPPDATA% (recomendado para cliente)

```cmd
:: Criar a pasta se nao existir
mkdir "%LOCALAPPDATA%\FerramentaEMT" 2>nul

:: Gravar o secret no arquivo
echo seu-secret-base64-aqui > "%LOCALAPPDATA%\FerramentaEMT\license.secret"
```

Caminho completo: `C:\Users\<usuario>\AppData\Local\FerramentaEMT\license.secret`

#### Opcao 3: Arquivo junto ao assembly

Colocar `license.secret` na mesma pasta da DLL do plugin:

```
%AppData%\Autodesk\Revit\Addins\2025\FerramentaEMT\license.secret
```

Menos recomendado — o arquivo pode ser sobrescrito numa reinstalacao.

### Verificacao

Apos configurar, rode o EmtKeyGen para confirmar:

```cmd
dotnet run --project tools\EmtKeyGen
```

Saida esperada:

```
  Fonte do segredo HMAC: EnvironmentVariable    (ou LocalAppDataFile)
```

Se aparecer erro vermelho, o secret nao foi encontrado em nenhuma fonte.

### Geracao do secret (primeira vez)

Se voce nao tem um secret ainda, gere um com PowerShell:

```powershell
[System.Convert]::ToBase64String((1..32 | %{ Get-Random -Min 0 -Max 256 } | %{ [byte]$_ }))
```

**IMPORTANTE:** trocar o secret invalida TODAS as licencas em uso. Guarde-o
em local seguro e nunca o compartilhe.

---

## 4. Geracao de chave de licenca

```cmd
:: Modo interativo
dotnet run --project tools\EmtKeyGen

:: Modo automatizado
dotnet run --project tools\EmtKeyGen -- "cliente@exemplo.com" 365
```

O EmtKeyGen usa o mesmo `LicenseSecretProvider` — precisa do secret configurado.

---

## 5. Estrutura no PC do cliente

```
%LocalAppData%\FerramentaEMT\
  license\
    emt.lic          <- chave ativada, criptografada DPAPI
    emt.trl          <- data inicio do trial, criptografada DPAPI
  logs\
    emt-YYYYMMDD.log <- logs diarios (retencao 30 dias)
  crashes\
    *.json           <- dumps de crash (se houver)
  license.secret     <- secret HMAC (Opcao 2 acima)

%AppData%\Autodesk\Revit\Addins\2025\
  FerramentaEMT\
    FerramentaEMT.dll
    (demais DLLs de dependencia)
  FerramentaEMT.addin  <- manifesto do Revit
```

---

## 6. Desinstalacao

```cmd
:: Remove binarios e manifesto
del "%AppData%\Autodesk\Revit\Addins\2025\FerramentaEMT.addin"
rmdir /s /q "%AppData%\Autodesk\Revit\Addins\2025\FerramentaEMT\"

:: Remove dados locais (opcional)
rmdir /s /q "%LocalAppData%\FerramentaEMT\"
```

---

## 7. Diagnostico

| Problema | Verificacao |
|----------|-------------|
| Aba EMT nao aparece no Revit | Verificar se `.addin` existe em `Addins\2025\` |
| Erro de HMAC | Rodar `dotnet run --project tools\EmtKeyGen` para testar resolucao |
| Licenca "WrongMachine" | Cliente trocou de PC — gerar nova chave (ver SISTEMA-LICENCA.md) |
| Crash ao abrir | Ver `%LocalAppData%\FerramentaEMT\logs\` e `crashes\` |
| Build falha | Rodar `Diagnostico-SDK.bat` para verificar .NET SDK e Revit |

---

## 8. Scripts auxiliares

| Script | Funcao |
|--------|--------|
| `INSTALAR.bat` | Build Release + deploy completo |
| `Compilar-Debug.bat` | Build Debug com PDB (attach debugger) |
| `Limpar-Tudo.bat` | Limpa `bin/`, `obj/` e desinstala do AppData |
| `Diagnostico-SDK.bat` | Verifica .NET SDK, MSBuild e Revit |
| `Instalar-DotNet-SDK.bat` | Instala .NET 8 SDK via winget |
| `Gerar-Setup.bat` | Empacota .msi para distribuicao |

---

ALEF / EMT — Abril 2026
