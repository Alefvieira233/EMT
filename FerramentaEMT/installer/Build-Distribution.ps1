[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RevitYear = "2025",
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-Path -LiteralPath $Path
    return [System.IO.Path]::GetFullPath($resolved.Path).TrimEnd('\')
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Comando obrigatorio nao encontrado no PATH: $CommandName"
    }
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AllowedRoot
    )

    $allowedRootFull = Resolve-NormalizedPath -Path $AllowedRoot
    $pathFull = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')

    if (-not $pathFull.StartsWith($allowedRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Recusa em limpar diretório fora da raiz permitida: $pathFull"
    }

    if (Test-Path -LiteralPath $pathFull) {
        Remove-Item -LiteralPath $pathFull -Recurse -Force
    }

    New-Item -ItemType Directory -Path $pathFull -Force | Out-Null
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-NormalizedPath -Path (Join-Path $scriptRoot "..")

Assert-CommandAvailable -CommandName "dotnet"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\installer"
}

$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
$projectPath = Join-Path $repoRoot "FerramentaEMT.csproj"
$sourceAddinPath = Join-Path $repoRoot "FerramentaEMT.addin"
$deployDir = Join-Path $repoRoot ("artifacts\deploy\{0}\net8.0-windows" -f $Configuration)
$packageRoot = Join-Path $outputRootFull "package"
$payloadFolderName = "FerramentaEMT"
$payloadRoot = Join-Path $packageRoot ("payload\{0}" -f $payloadFolderName)
$zipPath = Join-Path $outputRootFull ("FerramentaEMT-Revit{0}-{1}.zip" -f $RevitYear, $Configuration)
$metadataPath = Join-Path $packageRoot "package-metadata.json"

New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null

Write-Host "Compilando projeto em $Configuration..."
& dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o projeto."
}

if (-not (Test-Path -LiteralPath $deployDir)) {
    throw "Pasta de deploy nao encontrada: $deployDir"
}

[xml]$addinXml = Get-Content -LiteralPath $sourceAddinPath
$addinNode = @($addinXml.RevitAddIns.AddIn | Where-Object { $_.Type -eq "Application" }) | Select-Object -First 1
if ($null -eq $addinNode) {
    throw "Nao foi possivel localizar um no <AddIn Type=`"Application`"> em $sourceAddinPath"
}

Reset-Directory -Path $packageRoot -AllowedRoot $outputRootFull
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

Write-Host "Copiando arquivos do add-in para o pacote..."
Copy-Item -Path (Join-Path $deployDir "*") -Destination $payloadRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $scriptRoot "Install-FerramentaEMT.ps1") -Destination (Join-Path $packageRoot "Install-FerramentaEMT.ps1") -Force
Copy-Item -LiteralPath (Join-Path $scriptRoot "Uninstall-FerramentaEMT.ps1") -Destination (Join-Path $packageRoot "Uninstall-FerramentaEMT.ps1") -Force
Copy-Item -LiteralPath (Join-Path $scriptRoot "README.md") -Destination (Join-Path $packageRoot "README.md") -Force

$metadata = [ordered]@{
    name = [string]$addinNode.Name
    addInType = [string]$addinNode.Type
    addInId = [string]$addinNode.AddInId
    fullClassName = [string]$addinNode.FullClassName
    vendorId = [string]$addinNode.VendorId
    vendorDescription = [string]$addinNode.VendorDescription
    assemblyFile = "FerramentaEMT.dll"
    installFolderName = $payloadFolderName
    manifestFileName = "FerramentaEMT.Distribuicao.addin"
    revitYear = $RevitYear
    configuration = $Configuration
    packageCreatedAtUtc = [DateTime]::UtcNow.ToString("o")
}

$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Write-Host "Gerando zip de distribuicao..."
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Pacote gerado com sucesso."
Write-Host "Pasta do pacote: $packageRoot"
Write-Host "Arquivo zip:     $zipPath"
