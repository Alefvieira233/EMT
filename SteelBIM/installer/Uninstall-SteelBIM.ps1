[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$PackageRoot,
    [string]$RevitYear,
    [string]$AddinsRoot,
    [string]$InstallRoot,
    [string]$ManifestName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-Path -LiteralPath $Path
    return [System.IO.Path]::GetFullPath($resolved.Path).TrimEnd('\')
}

$packageRootFull = Resolve-NormalizedPath -Path $PackageRoot
$metadataPath = Join-Path $packageRootFull "package-metadata.json"
if (-not (Test-Path -LiteralPath $metadataPath)) {
    throw "Arquivo package-metadata.json nao encontrado em $packageRootFull"
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($RevitYear)) {
    $RevitYear = [string]$metadata.revitYear
}

if ([string]::IsNullOrWhiteSpace($AddinsRoot)) {
    $addinBaseDir = Join-Path $env:APPDATA ("Autodesk\Revit\Addins\{0}" -f $RevitYear)
} else {
    $addinBaseDir = [System.IO.Path]::GetFullPath($AddinsRoot)
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $addinBaseDir ([string]$metadata.installFolderName)
}

if ([string]::IsNullOrWhiteSpace($ManifestName)) {
    $ManifestName = [string]$metadata.manifestFileName
}

$manifestPath = Join-Path $addinBaseDir $ManifestName

if ((Test-Path -LiteralPath $manifestPath) -and $PSCmdlet.ShouldProcess($manifestPath, "Remover manifesto .addin")) {
    Remove-Item -LiteralPath $manifestPath -Force
}

if ((Test-Path -LiteralPath $InstallRoot) -and $PSCmdlet.ShouldProcess($InstallRoot, "Remover arquivos instalados")) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}

Write-Host ""
Write-Host "Desinstalacao concluida."
Write-Host "Manifesto removido: $manifestPath"
Write-Host "Pasta removida:     $InstallRoot"
