# installer/build-installer.ps1
#
# Genera el instalador de SuperMWeb con Inno Setup 6:
#   1. Publica la app self-contained con scripts/publish.ps1.
#   2. Compila installer/SuperMWeb.iss con ISCC.
#
# La salida queda FUERA del repo (C:\MiniMarket\SuperWeb\artifacts\SuperMWeb):
# si la publicacion quedara dentro de la carpeta del proyecto, el SDK web
# recogeria sus .json como contenido en el siguiente build.
#
# Uso:
#   ./installer/build-installer.ps1
#   ./installer/build-installer.ps1 -Version 1.2.0

[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$ArtifactsDir = "$PSScriptRoot\..\..\artifacts\SuperMWeb",
    [string]$Iscc = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ArtifactsDir = [System.IO.Path]::GetFullPath($ArtifactsDir)
$publishDir = Join-Path $ArtifactsDir 'publish'
$outputDir = Join-Path $ArtifactsDir 'instalador'

if (-not $Iscc) {
    $Iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Iscc) { throw 'No se encontro ISCC.exe. Instala Inno Setup 6 o pasa -Iscc.' }
}

Write-Host "==> Publicando en $publishDir" -ForegroundColor Cyan
& "$PSScriptRoot\..\scripts\publish.ps1" -OutDir $publishDir
if (-not (Test-Path (Join-Path $publishDir 'SuperMWeb.exe'))) {
    throw 'La publicacion no genero SuperMWeb.exe'
}

Write-Host "==> Compilando instalador con $Iscc" -ForegroundColor Cyan
$isccArgs = @(
    "/DAppVersion=$Version",
    "/DPublishDir=$publishDir",
    "/DOutputDir=$outputDir",
    (Join-Path $PSScriptRoot 'SuperMWeb.iss')
)
& $Iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "==> Instalador generado" -ForegroundColor Green
Write-Host "    $outputDir\SuperMWeb-Setup-$Version.exe"
