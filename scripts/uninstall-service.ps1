# scripts/uninstall-service.ps1
#
# Detiene y borra el servicio SuperMWeb. Deja el binario intacto en
# el PublishDir; solo remueve el registro del SCM.
#
# Requiere Administrador.

[CmdletBinding()]
param(
    [string]$ServiceName = "SuperMWeb"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) { throw "Este script requiere PowerShell como Administrador." }

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Host "El servicio '$ServiceName' no existe. Nada que hacer." -ForegroundColor Yellow
    return
}

if ($svc.Status -eq 'Running') {
    Write-Host "==> Deteniendo '$ServiceName'"
    Stop-Service -Name $ServiceName -Force
}

Write-Host "==> Borrando '$ServiceName'"
& sc.exe delete $ServiceName | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc delete fallo con exit code $LASTEXITCODE" }

Write-Host "==> Servicio removido" -ForegroundColor Green
