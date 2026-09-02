# scripts/restore-db.ps1
#
# Restaura la base SuperM desde un .bak.
#
# ADVERTENCIA: sobrescribe la base actual. Detener el servicio SuperMWeb
# antes de ejecutar. El script lo verifica y detiene automaticamente.
#
# Uso:
#   ./scripts/restore-db.ps1 -BakFile "C:\SuperM\backups\SuperM_20260902_120000.bak"

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BakFile,
    [string]$Server   = "localhost",
    [string]$Database = "SuperM",
    [string]$ServiceName = "SuperMWeb"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $BakFile)) {
    throw "No existe el archivo $BakFile"
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd no encontrado. Instala SQL Server Command Line Utilities."
}

# Detener el servicio si esta corriendo (necesario para tomar exclusive lock).
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$restart = $false
if ($svc -and $svc.Status -eq 'Running') {
    Write-Host "==> Deteniendo servicio $ServiceName"
    Stop-Service -Name $ServiceName -Force
    $restart = $true
}

$sql = @"
USE [master];
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database]
FROM DISK = N'$BakFile'
WITH REPLACE, STATS = 10;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

Write-Host "==> Restaurando $Database desde $BakFile" -ForegroundColor Cyan
& sqlcmd -S $Server -E -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "sqlcmd RESTORE fallo con exit code $LASTEXITCODE" }

Write-Host "==> Restauracion completada" -ForegroundColor Green

if ($restart) {
    Write-Host "==> Reiniciando servicio $ServiceName"
    Start-Service -Name $ServiceName
}
