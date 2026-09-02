# scripts/install-service.ps1
#
# Instala SuperMWeb como servicio de Windows con arranque automatico y
# reinicio automatico ante fallo.
#
# Requiere ejecutarse como Administrador (Set-ExecutionPolicy y sc.exe
# necesitan privilegios elevados).
#
# Uso:
#   ./scripts/install-service.ps1
#   ./scripts/install-service.ps1 -PublishDir "C:\SuperM\SuperMWeb"
#   ./scripts/install-service.ps1 -ServiceName "SuperMWeb" -DisplayName "SuperM Web POS"

[CmdletBinding()]
param(
    [string]$ServiceName = "SuperMWeb",
    [string]$DisplayName = "SuperM Web POS",
    [string]$Description = "Punto de venta SuperM sobre Kestrel en http://localhost:5000",
    [string]$PublishDir = "$PSScriptRoot\..\publish",
    [string]$ExeName    = "SuperMWeb.exe"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---- Verificacion admin ----
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Este script requiere PowerShell como Administrador."
}

# ---- Localizar el binario ----
if (-not [System.IO.Path]::IsPathRooted($PublishDir)) {
    $PublishDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $PublishDir))
}
$exePath = Join-Path $PublishDir $ExeName
if (-not (Test-Path $exePath)) {
    throw "No se encontro $exePath. Ejecuta primero ./scripts/publish.ps1"
}

Write-Host "==> Instalando servicio '$ServiceName'" -ForegroundColor Cyan
Write-Host "    Binario:  $exePath"
Write-Host "    Display:  $DisplayName"

# ---- Detener/borrar si ya existe ----
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "==> Servicio existente detectado; deteniendo y borrando"
    if ($existing.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
    }
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# ---- Crear servicio ----
# binPath usa comillas dobles internas escapadas para permitir espacios en la ruta.
$binPath = "`"$exePath`""

$createArgs = @(
    'create', $ServiceName,
    'binPath=', $binPath,
    'start=', 'auto',
    'DisplayName=', $DisplayName
)
Write-Host "==> sc.exe $($createArgs -join ' ')"
& sc.exe @createArgs
if ($LASTEXITCODE -ne 0) { throw "sc create fallo con exit code $LASTEXITCODE" }

& sc.exe description $ServiceName $Description | Out-Null

# ---- Failure actions (reinicio automatico) ----
# 1er fallo: reinicia a los 5s. 2do: 5s. Reset del contador tras 1 dia.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# ---- Iniciar ----
Write-Host "==> Iniciando servicio"
Start-Service -Name $ServiceName

Start-Sleep -Seconds 3
$svc = Get-Service -Name $ServiceName
Write-Host ""
Write-Host "==> Estado: $($svc.Status)" -ForegroundColor Green
Write-Host ""
Write-Host "Verifica en el navegador: http://localhost:5000/health"
Write-Host "Logs en: $PublishDir\logs\"
