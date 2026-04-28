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
        throw "Recusa em limpar diretorio fora da raiz permitida: $pathFull"
    }

    if (Test-Path -LiteralPath $pathFull) {
        Remove-Item -LiteralPath $pathFull -Recurse -Force
    }

    New-Item -ItemType Directory -Path $pathFull -Force | Out-Null
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-NormalizedPath -Path (Join-Path $scriptRoot "..")
$powershellExe = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"

if (-not (Test-Path -LiteralPath $powershellExe)) {
    throw "powershell.exe nao encontrado em $powershellExe"
}

Assert-CommandAvailable -CommandName "dotnet"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\installer"
}

$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
$distributionScript = Join-Path $scriptRoot "Build-Distribution.ps1"
$distributionZipPath = Join-Path $outputRootFull ("FerramentaEMT-Revit{0}-{1}.zip" -f $RevitYear, $Configuration)
$bootstrapProject = Join-Path $scriptRoot "SetupBootstrapper\SetupBootstrapper.csproj"
$embeddedPackageDir = Join-Path $scriptRoot "SetupBootstrapper\EmbeddedPackage"
$embeddedPackagePath = Join-Path $embeddedPackageDir "FerramentaEMT-Package.zip"
$publishDir = Join-Path $outputRootFull "setup-publish"
$setupExePath = Join-Path $outputRootFull ("FerramentaEMT-Revit{0}-Setup.exe" -f $RevitYear)
$publishedExePath = Join-Path $publishDir "FerramentaEMT.SetupBootstrapper.exe"

New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null
New-Item -ItemType Directory -Path $embeddedPackageDir -Force | Out-Null

Write-Host "Gerando pacote base para o setup..."
& $powershellExe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $distributionScript -Configuration $Configuration -RevitYear $RevitYear -OutputRoot $outputRootFull
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao gerar o pacote base."
}

if (-not (Test-Path -LiteralPath $distributionZipPath)) {
    throw "Arquivo zip de distribuicao nao encontrado: $distributionZipPath"
}

Copy-Item -LiteralPath $distributionZipPath -Destination $embeddedPackagePath -Force
Reset-Directory -Path $publishDir -AllowedRoot $outputRootFull

Write-Host "Compilando setup.exe self-contained..."
& dotnet publish $bootstrapProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o setup.exe."
}

if (-not (Test-Path -LiteralPath $publishedExePath)) {
    throw "Executavel publicado nao encontrado: $publishedExePath"
}

Copy-Item -LiteralPath $publishedExePath -Destination $setupExePath -Force

# v1.7.0 (PR-2 auto-update): gerar checksums.txt no formato sha256sum
# (canonico: <hex64>  <filename>). Asset consumido pelo UpdateDownloader
# para validar SHA256 do .zip baixado antes de extrair.
$checksumsPath = Join-Path $outputRootFull "checksums.txt"
$artifactsToHash = @(
    @{ Path = $distributionZipPath; Name = (Split-Path -Leaf $distributionZipPath) },
    @{ Path = $setupExePath;        Name = (Split-Path -Leaf $setupExePath) }
)

$lines = @()
foreach ($item in $artifactsToHash) {
    if (Test-Path -LiteralPath $item.Path) {
        $hashHex = (Get-FileHash -Algorithm SHA256 -LiteralPath $item.Path).Hash.ToLowerInvariant()
        # Dois espacos entre hash e nome (compat sha256sum + Sha256Calculator.FindHashForFile)
        $lines += "$hashHex  $($item.Name)"
    }
}
[System.IO.File]::WriteAllText($checksumsPath, ($lines -join "`n") + "`n", [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "Setup gerado com sucesso."
Write-Host "Arquivo:    $setupExePath"
Write-Host "Checksums:  $checksumsPath"
