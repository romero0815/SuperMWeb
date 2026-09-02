# scripts/publish.ps1
#
# Publica SuperMWeb como binario self-contained para win-x64.
# El resultado va a ./publish (por defecto) o al parametro -OutDir.
#
# Uso:
#   ./scripts/publish.ps1
#   ./scripts/publish.ps1 -OutDir "C:\SuperM\SuperMWeb"
#
# Se ejecuta desde la raiz del repo o desde scripts/. Ambos funcionan.

[CmdletBinding()]
param(
    [string]$OutDir = "$PSScriptRoot\..\publish",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$csproj = Join-Path $repoRoot 'SuperMWeb.csproj'

if (-not (Test-Path $csproj)) {
    throw "No se encontro $csproj. Ejecuta el script desde el repo SuperMWeb."
}

# Normalizar la ruta de salida a absoluta.
if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutDir))
}

Write-Host "==> Publicando SuperMWeb" -ForegroundColor Cyan
Write-Host "    Proyecto:      $csproj"
Write-Host "    Configuracion: $Configuration"
Write-Host "    Runtime:       win-x64 (self-contained)"
Write-Host "    Salida:        $OutDir"
Write-Host ""

if (Test-Path $OutDir) {
    Write-Host "==> Limpiando salida previa"
    Remove-Item $OutDir -Recurse -Force
}

$publishArgs = @(
    'publish', $csproj,
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    '-o', $OutDir
)

Write-Host "==> dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo con exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "==> Publicacion completada" -ForegroundColor Green
Write-Host "    Binario: $OutDir\SuperMWeb.exe"
Write-Host ""
Write-Host "Siguiente paso: instalar como servicio Windows"
Write-Host "    ./scripts/install-service.ps1 -PublishDir '$OutDir'"
