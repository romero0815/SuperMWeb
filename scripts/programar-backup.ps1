# scripts/programar-backup.ps1
#
# Registra (o actualiza) la tarea programada que ejecuta backup-db.ps1 todos
# los dias. La usa el instalador y sirve para cambiar la hora a mano despues.
#
# Frente a una tarea creada a mano desde el Programador de tareas:
#   - Corre como SYSTEM: no necesita una sesion de Windows abierta.
#   - StartWhenAvailable: si el equipo estaba apagado a la hora programada, el
#     backup se hace apenas encienda en vez de perderse ese dia.
#   - Corre tambien con el equipo en bateria.
#
# NO toca otras tareas de backup (por ejemplo la del escritorio): solo crea o
# reemplaza la tarea llamada -NombreTarea. Elegir una hora distinta a la de
# cualquier otro backup de la misma base: SQL Server no permite dos a la vez.
#
# Uso (PowerShell como Administrador):
#   ./scripts/programar-backup.ps1 -Hora 03:00
#   ./scripts/programar-backup.ps1 -Hora 13:30 -Database SuperM -BackupDir C:\SuperM\backups
#   ./scripts/programar-backup.ps1 -Hora 03:00 -Simular    # muestra lo que haria

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')] [string]$Hora,
    [string]$Server = 'localhost',
    [string]$Database = 'SuperM',
    [string]$BackupDir = 'C:\SuperM\backups',
    [string]$NombreTarea = 'SuperM Backup Diario',
    [string]$ArchivoSalida = '',
    [switch]$Simular
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Escribir([string]$Texto) {
    if ($ArchivoSalida) { Set-Content -Path $ArchivoSalida -Value $Texto -Encoding UTF8 }
    else { Write-Output $Texto }
}

try {
    $scriptBackup = Join-Path $PSScriptRoot 'backup-db.ps1'
    if (-not (Test-Path $scriptBackup)) { throw "No se encontró $scriptBackup" }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw "sqlcmd no está instalado o no está en el PATH. Instale 'SQL Server Command Line Utilities'."
    }

    # El .bak lo escribe SQL Server con su propia cuenta de servicio, no SYSTEM:
    # localhost -> NT SERVICE\MSSQLSERVER; localhost\SQLEXPRESS -> NT SERVICE\MSSQL$SQLEXPRESS.
    $instancia = if ($Server -match '\\(.+)$') { 'MSSQL$' + $Matches[1] } else { 'MSSQLSERVER' }
    $cuentaSql = "NT SERVICE\$instancia"

    $argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptBackup`" -Server `"$Server`" -Database `"$Database`" -BackupDir `"$BackupDir`""
    $inicio = [datetime]::ParseExact($Hora, 'HH:mm', [Globalization.CultureInfo]::InvariantCulture)

    $resumen = "Tarea '$NombreTarea': todos los días a las $Hora (o al encender el equipo si estaba apagado), como SYSTEM.`r`n" +
               "Base $Database en $Server -> $BackupDir"

    if ($Simular) {
        Escribir ("[SIMULACIÓN, no se registró nada]`r`n$resumen`r`n" +
                  "Acción: powershell.exe $argumentos`r`n" +
                  "Permiso de escritura en la carpeta para: $cuentaSql")
        exit 0
    }

    if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }
    & icacls.exe $BackupDir /grant "${cuentaSql}:(OI)(CI)M" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $resumen += "`r`nAviso: no se pudo dar permiso de escritura a $cuentaSql en $BackupDir."
    }

    $accion = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argumentos
    $disparador = New-ScheduledTaskTrigger -Daily -At $inicio
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $configuracion = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew

    Register-ScheduledTask -TaskName $NombreTarea -Action $accion -Trigger $disparador `
        -Principal $principal -Settings $configuracion `
        -Description 'Backup diario de la base de SuperM (instalador de SuperMWeb).' -Force | Out-Null

    Escribir $resumen
    exit 0
}
catch {
    Escribir $_.Exception.Message
    exit 1
}
