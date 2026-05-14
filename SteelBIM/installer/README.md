# Instalacao Separada

Este fluxo existe para distribuicao do add-in sem mexer no ambiente atual de desenvolvimento.

## Gerar o pacote

No repositorio, execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Distribution.ps1
```

Saidas geradas:

- `artifacts\installer\package`
- `artifacts\installer\SteelBIM-Revit2025-Release.zip`

## Gerar setup.exe

Para gerar um `setup.exe` separado do fluxo de desenvolvimento:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-SetupExe.ps1
```

Saida gerada:

- `artifacts\installer\SteelBIM-Revit2025-Setup.exe`

## Instalar em outro computador

1. Extraia o zip.
2. Abra PowerShell na pasta extraida.
3. Execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-SteelBIM.ps1
```

Ou execute o `setup.exe` gerado acima.

O script copia os arquivos para:

- `%AppData%\Autodesk\Revit\Addins\2025\SteelBIM`

E cria o manifesto:

- `%AppData%\Autodesk\Revit\Addins\2025\SteelBIM.Distribuicao.addin`

## Desinstalar

Na mesma pasta do pacote, execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-SteelBIM.ps1
```

## Observacoes

- O fluxo de desenvolvimento continua separado. O seu `.addin` local do repositorio nao e alterado.
- O pacote usa a compilacao `Release` por padrao.
- O `dotnet` precisa estar disponivel no `PATH` para gerar o zip e o `setup.exe`.
- Se precisar instalar em outro ano do Revit, use `-RevitYear 2025`, `-RevitYear 2026`, etc.
- Para testar sem usar `%AppData%`, os scripts aceitam `-AddinsRoot <pasta>`.
- O `setup.exe` e um bootstrapper proprio, compilado em .NET e empacotado de forma separada do add-in principal.
