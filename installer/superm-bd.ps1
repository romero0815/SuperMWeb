# installer/superm-bd.ps1
#
# Ayudante del instalador de SuperMWeb. Usa System.Data.SqlClient de .NET
# Framework, que viene con Windows 10: asi el instalador no depende de sqlcmd
# ni de un proveedor OLE DB concreto.
#
#   -Accion Probar        Conecta con seguridad integrada y verifica que la base
#                         tenga el esquema de SuperM. Solo lectura.
#   -Accion Permisos      Da a la cuenta del servicio (NT AUTHORITY\SYSTEM)
#                         acceso de lectura, escritura, ejecucion y backup en la
#                         base. Todo en UNA transaccion: si algo falla no queda
#                         nada a medias. No toca tablas, datos ni procedimientos.
#                         Corre con la cuenta del administrador que instala, que
#                         debe poder crear logins y usuarios en SQL Server.
#   -Accion TareasBackup  Lista las tareas programadas de backup que ya existen
#                         en el equipo (por ejemplo la del escritorio) como
#                         "Nombre|HH:mm", para no programar otra a la misma hora.
#
# El resultado se escribe en -ArchivoSalida (UTF-8) para que el instalador lo
# muestre. Codigo de salida 0 = correcto.
#
# -Simular ejecuta Permisos y revierte la transaccion al final: sirve para
# comprobar el script contra una base real sin cambiar nada.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Probar', 'Permisos', 'TareasBackup')] [string]$Accion,
    [string]$Servidor = '',
    [string]$BaseDatos = '',
    [string]$ArchivoSalida = '',
    [string]$Login = 'NT AUTHORITY\SYSTEM',
    [string]$ExcluirTarea = '',
    [switch]$Simular
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Escribir([string]$Texto) {
    if ($ArchivoSalida) { Set-Content -Path $ArchivoSalida -Value $Texto -Encoding UTF8 }
    else { Write-Output $Texto }
}

function MensajeInterno($Excepcion) {
    while ($Excepcion.InnerException) { $Excepcion = $Excepcion.InnerException }
    return $Excepcion.Message
}

function Escalar($Conexion, [string]$Sql, $Transaccion = $null, [hashtable]$Parametros = @{}) {
    $cmd = $Conexion.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 60
    if ($Transaccion) { $cmd.Transaction = $Transaccion }
    foreach ($clave in $Parametros.Keys) { [void]$cmd.Parameters.AddWithValue($clave, $Parametros[$clave]) }
    return $cmd.ExecuteScalar()
}

# ---- Tareas de backup existentes (no usa la base) ----
if ($Accion -eq 'TareasBackup') {
    try {
        $lineas = foreach ($tarea in Get-ScheduledTask) {
            if ($tarea.TaskName -eq $ExcluirTarea -or $tarea.TaskPath -like '\Microsoft\*') { continue }

            $acciones = ($tarea.Actions | ForEach-Object {
                if ($_.PSObject.Properties['Execute']) { "$($_.Execute) $($_.Arguments)" }
            }) -join ' '
            if ($acciones -notmatch 'sqlcmd|backup|\.bak') { continue }

            foreach ($disparador in $tarea.Triggers) {
                if ($disparador.PSObject.Properties['StartBoundary'] -and $disparador.StartBoundary) {
                    '{0}|{1}' -f $tarea.TaskName, ([datetime]$disparador.StartBoundary).ToString('HH:mm')
                }
            }
        }
        Escribir ((@($lineas) | Select-Object -Unique) -join "`r`n")
        exit 0
    }
    catch {
        Escribir (MensajeInterno $_.Exception)
        exit 1
    }
}

if (-not $Servidor -or -not $BaseDatos) {
    Escribir 'Faltan el servidor o la base de datos.'
    exit 1
}

try {
    $csb = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $csb['Data Source'] = $Servidor
    $csb['Initial Catalog'] = $BaseDatos
    $csb['Integrated Security'] = $true
    $csb['Connect Timeout'] = 10
    $conexion = New-Object System.Data.SqlClient.SqlConnection $csb.ConnectionString
    $conexion.Open()

    if ($Accion -eq 'Probar') {
        # Objetos sin los que la aplicacion no arranca o no puede vender.
        $faltantes = Escalar $conexion @'
SELECT STUFF(
    CASE WHEN OBJECT_ID(N'dbo.Productos', N'U') IS NULL THEN N', tabla Productos' ELSE N'' END +
    CASE WHEN OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL THEN N', tabla Usuarios' ELSE N'' END +
    CASE WHEN OBJECT_ID(N'dbo.Deudor', N'U') IS NULL THEN N', tabla Deudor' ELSE N'' END +
    CASE WHEN OBJECT_ID(N'dbo.RegistrarVenta', N'P') IS NULL THEN N', procedimiento RegistrarVenta' ELSE N'' END +
    CASE WHEN OBJECT_ID(N'dbo.RegistrarCompra', N'P') IS NULL THEN N', procedimiento RegistrarCompra' ELSE N'' END +
    CASE WHEN TYPE_ID(N'dbo.TVP_DetalleVenta') IS NULL THEN N', tipo TVP_DetalleVenta' ELSE N'' END,
    1, 2, N'')
'@
        if ($faltantes -isnot [DBNull] -and $faltantes) {
            Escribir "Se conectó a $Servidor, pero la base $BaseDatos no tiene el esquema de SuperM. Falta: $faltantes."
            exit 2
        }

        $productos = Escalar $conexion 'SELECT COUNT(*) FROM dbo.Productos WHERE Active = 1'
        $version = Escalar $conexion "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + N' - ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100))"
        Escribir "Conexión correcta.`r`nSQL Server $version`r`nBase $BaseDatos con $productos productos activos."
        exit 0
    }

    # ---- Permisos para la cuenta del servicio ----
    # El servicio corre como LocalSystem, que en un SQL Server local entra como
    # NT AUTHORITY\SYSTEM. Desde SQL Server 2012 ese login ya no es sysadmin
    # por defecto y no tiene acceso a las bases de usuario.
    # XACT_ABORT hace que cualquier error aborte y revierta la transaccion
    # completa, tambien los errores dentro de EXEC (@sql).
    $sql = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @usuario sysname, @mensaje nvarchar(500), @sql nvarchar(max);

IF SUSER_ID(@login) IS NULL
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login) + N' FROM WINDOWS';
    EXEC (@sql);
END

IF IS_SRVROLEMEMBER(N'sysadmin', @login) = 1
    SET @mensaje = @login + N' ya es sysadmin del servidor; no hace falta dar permisos en la base.';
ELSE
BEGIN
    SELECT @usuario = name FROM sys.database_principals WHERE sid = SUSER_SID(@login);
    IF @usuario IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login);
        EXEC (@sql);
        SET @usuario = @login;
    END

    IF @usuario = N'dbo'
        SET @mensaje = @login + N' es el propietario de la base; ya tiene todos los permisos.';
    ELSE
    BEGIN
        -- GRANT EXECUTE a nivel de base cubre los procedimientos y los tipos
        -- tabla (TVP_DetalleVenta, TVP_DetalleCompra) que usan.
        SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@usuario) + N'; ' +
                   N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@usuario) + N'; ' +
                   N'ALTER ROLE db_backupoperator ADD MEMBER ' + QUOTENAME(@usuario) + N'; ' +
                   N'GRANT EXECUTE TO ' + QUOTENAME(@usuario) + N';';
        EXEC (@sql);
        SET @mensaje = N'Permisos de lectura, escritura, ejecución y backup otorgados a ' + @login +
                       N' en la base ' + DB_NAME() + N'.';
    END
END

SELECT @mensaje;
'@

    # Todo en una transaccion: o se aplica completo o no queda nada (por
    # ejemplo, un login creado sin sus roles). Con -Simular se revierte siempre.
    $transaccion = $conexion.BeginTransaction()
    try {
        $mensaje = Escalar $conexion $sql $transaccion @{ '@login' = $Login }
        if ($Simular) {
            $transaccion.Rollback()
            $mensaje = "[SIMULACIÓN, cambios revertidos] $mensaje"
        }
        else {
            $transaccion.Commit()
        }
    }
    catch {
        $detalle = MensajeInterno $_.Exception
        # XACT_ABORT pudo haberla revertido ya en el servidor.
        try { $transaccion.Rollback() } catch { }
        Escribir "No se aplicó ningún cambio de permisos.`r`n$detalle"
        exit 1
    }

    Escribir $mensaje
    exit 0
}
catch {
    Escribir (MensajeInterno $_.Exception)
    exit 1
}
