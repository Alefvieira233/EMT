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

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Clear-Directory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AllowedRoot
    )

    $allowedRootFull = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\')
    $pathFull = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')

    if (-not $pathFull.StartsWith($allowedRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Recusa em limpar diretório fora da raiz permitida: $pathFull"
    }

    if (Test-Path -LiteralPath $pathFull) {
        Get-ChildItem -LiteralPath $pathFull -Force | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $pathFull -Force | Out-Null
    }
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

if ([string]::IsNullOrWhiteSpace($ManifestName)) {
    $ManifestName = [string]$metadata.manifestFileName
}

if ([string]::IsNullOrWhiteSpace($AddinsRoot)) {
    $addinBaseDir = Join-Path $env:APPDATA ("Autodesk\Revit\Addins\{0}" -f $RevitYear)
} else {
    $addinBaseDir = [System.IO.Path]::GetFullPath($AddinsRoot)
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $addinBaseDir ([string]$metadata.installFolderName)
}

$payloadRoot = Join-Path $packageRootFull ("payload\{0}" -f [string]$metadata.installFolderName)
if (-not (Test-Path -LiteralPath $payloadRoot)) {
    throw "Pasta payload nao encontrada: $payloadRoot"
}

Ensure-Directory -Path $addinBaseDir
Clear-Directory -Path $InstallRoot -AllowedRoot $addinBaseDir

if ($PSCmdlet.ShouldProcess($InstallRoot, "Instalar arquivos do add-in")) {
    Copy-Item -Path (Join-Path $payloadRoot "*") -Destination $InstallRoot -Recurse -Force
}

$assemblyPath = Join-Path $InstallRoot ([string]$metadata.assemblyFile)
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Assembly principal nao encontrado apos a copia: $assemblyPath"
}

$manifestPath = Join-Path $addinBaseDir $ManifestName
$manifestXml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
    <AddIn Type="$($metadata.addInType)">
        <Name>$($metadata.name)</Name>
        <Assembly>$assemblyPath</Assembly>
        <AddInId>$($metadata.addInId)</AddInId>
        <FullClassName>$($metadata.fullClassName)</FullClassName>
        <VendorId>$($metadata.vendorId)</VendorId>
        <VendorDescription>$($metadata.vendorDescription)</VendorDescription>
    </AddIn>
</RevitAddIns>
"@

if ($PSCmdlet.ShouldProcess($manifestPath, "Gravar manifesto .addin")) {
    $manifestXml | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

$outrosManifests = Get-ChildItem -LiteralPath $addinBaseDir -Filter *.addin -File |
    Where-Object { $_.FullName -ne $manifestPath }

$duplicados = @()
foreach ($arquivo in $outrosManifests) {
    if (Select-String -LiteralPath $arquivo.FullName -Pattern ([string]$metadata.addInId) -Quiet) {
        $duplicados += $arquivo.FullName
    }
}

Write-Host ""
Write-Host "Instalacao concluida."
Write-Host "Arquivos copiados para: $InstallRoot"
Write-Host "Manifesto criado em:   $manifestPath"

if ($duplicados.Count -gt 0) {
    Write-Warning "Foram encontrados outros manifestos com o mesmo AddInId. Remova duplicatas para evitar carregamento duplicado:"
    $duplicados | ForEach-Object { Write-Warning " - $_" }
}
