# scripts/backup-db.ps1
#
# Backup completo de la base de SuperM a un .bak con fecha y hora en el nombre.
# Usa sqlcmd con Integrated Security (mismo esquema que la app).
#
# Se hace con COPY_ONLY: es una copia independiente que no altera la secuencia
# de otros backups de la misma base (por ejemplo el backup diario que ya tiene
# programado el sistema de escritorio, o diferenciales futuros).
#
# Uso:
#   ./scripts/backup-db.ps1
#   ./scripts/backup-db.ps1 -BackupDir "D:\Backups\SuperM"
#   ./scripts/backup-db.ps1 -Database "SuperM2"     # equipo de desarrollo
#
# Para agendarlo como tarea diaria: ./scripts/programar-backup.ps1

[CmdletBinding()]
param(
    [string]$Server    = "localhost",
    [string]$Database  = "SuperM",
    [string]$BackupDir = "C:\SuperM\backups",
    [int]   $RetencionDias = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$bakFile   = Join-Path $BackupDir "$Database`_$timestamp.bak"

# Chequear que sqlcmd este disponible.
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd no encontrado en PATH. Instala 'SQL Server Command Line Utilities' o Visual Studio 2022."
}

# SQL Server Express (EngineEdition 4) no admite COMPRESSION: ahi el backup
# se hace sin comprimir en vez de fallar.
$edicion = & sqlcmd -S $Server -E -b -h -1 -W -Q "SET NOCOUNT ON; SELECT SERVERPROPERTY('EngineEdition')"
if ($LASTEXITCODE -ne 0) { throw "sqlcmd no pudo consultar la edicion de SQL Server (exit code $LASTEXITCODE)" }
$compresion = if ((($edicion | Out-String).Trim()) -eq '4') { '' } else { 'COMPRESSION,' }

$sql = @"
BACKUP DATABASE [$Database]
TO DISK = N'$bakFile'
WITH COPY_ONLY, FORMAT, INIT, $compresion
     NAME = N'SuperM full backup $timestamp',
     STATS = 10;
"@

Write-Host "==> Backup de $Database a $bakFile" -ForegroundColor Cyan
& sqlcmd -S $Server -E -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "sqlcmd BACKUP fallo con exit code $LASTEXITCODE" }

Write-Host "==> Backup completado" -ForegroundColor Green

# ---- Retencion: borrar backups mas viejos que N dias ----
# Solo archivos <Base>_*.bak de esta carpeta: nunca toca los .bak de otras
# tareas de backup (por ejemplo C:\backup_super del escritorio).
$limite = (Get-Date).AddDays(-$RetencionDias)
$viejos = Get-ChildItem $BackupDir -Filter "$Database`_*.bak" |
    Where-Object { $_.LastWriteTime -lt $limite }
if ($viejos) {
    Write-Host "==> Eliminando $(@($viejos).Count) backups > $RetencionDias dias"
    $viejos | Remove-Item -Force
}

Write-Host ""
Write-Host "Para restaurar:"
Write-Host "    ./scripts/restore-db.ps1 -BakFile '$bakFile' -Database '$Database'"
