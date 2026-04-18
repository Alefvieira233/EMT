[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-OptionalArgument {
    param(
        [AllowNull()][AllowEmptyString()][string]$Value,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return @()
    }

    return @($Name, $Value)
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$archivePath = Join-Path $scriptRoot "FerramentaEMT-Package.zip"
if (-not (Test-Path -LiteralPath $archivePath)) {
    throw "Arquivo de pacote nao encontrado: $archivePath"
}

$expandedRoot = Join-Path $env:TEMP ("FerramentaEMT-Setup-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $expandedRoot -Force | Out-Null

try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $expandedRoot -Force

    $installScript = Join-Path $expandedRoot "Install-FerramentaEMT.ps1"
    if (-not (Test-Path -LiteralPath $installScript)) {
        throw "Script de instalacao nao encontrado apos extrair o pacote: $installScript"
    }

    $argumentos = @(
        "-ExecutionPolicy", "Bypass",
        "-NoProfile",
        "-File", $installScript,
        "-PackageRoot", $expandedRoot
    )

    $argumentos += Get-OptionalArgument -Name "-RevitYear" -Value $env:FERRAMENTAEMT_REVITYEAR
    $argumentos += Get-OptionalArgument -Name "-AddinsRoot" -Value $env:FERRAMENTAEMT_ADDINSROOT
    $argumentos += Get-OptionalArgument -Name "-InstallRoot" -Value $env:FERRAMENTAEMT_INSTALLROOT
    $argumentos += Get-OptionalArgument -Name "-ManifestName" -Value $env:FERRAMENTAEMT_MANIFESTNAME

    & powershell.exe @argumentos
    if ($LASTEXITCODE -ne 0) {
        throw "A instalacao retornou codigo $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $expandedRoot) {
        Remove-Item -LiteralPath $expandedRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
